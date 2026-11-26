// Not ported from a Crossfire file: the operations on levels and the objects in them (docs/protocol.md section 7: ops 7, 8, 14, 16 and
// the functions mudaddobject and mudaddspecialobject; the effects of section 10 and the events of section 11).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    private readonly Dictionary<string, Level> levels = new(StringComparer.Ordinal);

    // names (as "level:<name>" and "account:<name>") whose file exists and could not be read: they are never created or written over
    private readonly HashSet<string> refused = new(StringComparer.Ordinal);

    // the files handed to the writer that may not be on disk yet: a level or an account read again meanwhile must see them
    private readonly Dictionary<string, byte[]> recent = new(StringComparer.Ordinal);

    private const int RecentLimit = 32;

    /// <summary>Hands a file to the writer (and remembers it until the writer has surely written it).</summary>
    private void PutFile(string relativePath, byte[] content)
    {
        recent[relativePath] = content;
        writer!.Submit(relativePath, content);
        if (recent.Count > RecentLimit && writer.IsIdle())
        {
            recent.Clear();
        }
    }

    /// <summary>The bytes of a file of the data folder, or null: what was handed to the writer last, else the file.</summary>
    private byte[]? ReadDataFile(string relativePath)
    {
        if (recent.TryGetValue(relativePath, out byte[]? bytes))
        {
            return bytes;
        }
        string path = Path.Combine(host.DataDir, relativePath);
        if (Directory.Exists(path))
        {
            throw new IOException(relativePath + " is a folder");
        }
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    // ------------------------------------------------------------------ levels

    /// <summary>The effect that shows an object as an NPC (protocol section 10), made from the object as it is now.</summary>
    private Val PlaceEffect(LevelObject lo) => Val.Map(
        ("k", Val.Str("npc.place")),
        ("handle", Val.Handle(handles.HandleOf(lo))),
        ("type", Val.Str(TMudObjectMembers.TypeName)),
        ("level", Val.Str(lo.Level.Name)),
        ("x", Val.Float(lo.X)),
        ("y", Val.Float(lo.Y)),
        ("image", Val.Str(GkLook.Image(lo.Ob))),
        ("classes", Val.List(classes.For(lo.Ob).Select(Val.Str))),
        ("props", GkLook.Props(lo)),
        ("blob", lo.Blob == null ? Val.Nil : Val.Str(lo.Blob)));

    /// <summary>
    /// The level of that name: the one the extension holds, else the one read from its saved file (<c>levels/</c> of the data folder)
    /// or, when there is none, from the seed (<c>mudlevels/</c> of the world folder); null when there is neither, or the file could
    /// not be read (the level is then never made or written over).
    /// </summary>
    private Level? GetOrReadLevel(string name)
    {
        name = LevelKey(name);
        if (levels.TryGetValue(name, out Level? held))
        {
            return held;
        }
        if (refused.Contains("level:" + name) || name.Length == 0)
        {
            return null;
        }
        try
        {
            byte[]? bytes = null;
            string source = "saved";
            string? rel = FileNames.LevelFile(name);
            if (rel != null)
            {
                bytes = ReadDataFile(rel);
            }
            if (bytes == null && IsPlainFileName(name))
            {
                string? seed = SeedFile(name);
                if (seed != null)
                {
                    bytes = File.ReadAllBytes(seed);
                    source = "seed";
                }
            }
            if (bytes == null)
            {
                return null;
            }
            forwardWorldLog = true;
            LevelRead read;
            try
            {
                read = LevelFiles.Read(world!, gk!, name, FileText.FromBytes(bytes));
            }
            finally
            {
                forwardWorldLog = false;
            }
            Level level = read.Level;
            level.Source = source;
            level.Dirty = source == "seed" && read.Objects > 0;
            foreach (LevelObject lo in level.Objects)
            {
                lo.Ob.Map = level;
            }
            levels[name] = level;
            log(2, "level " + name + ": " + read.Objects + " objects read from the " + (source == "seed" ? "seed" : "saved file")
                + (read.Errors > 0 ? "; " + read.Errors + " errors in the file" : ""));
            return level;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or CrossfireFatalException)
        {
            refused.Add("level:" + name);
            log(0, "level " + name + " cannot be read, it is left as it is: " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// The key and the file name of a level: its name with the ASCII letters folded to lower case. The host compares level names
    /// without regard to case, so one level must not be two here (two files, two sets of objects).
    /// </summary>
    internal static string LevelKey(string name) => string.Create(name.Length, name, (span, source) =>
    {
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            span[i] = c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
        }
    });

    /// <summary>The seed file of a level (the seed's own spelling of the name may differ in case), or null.</summary>
    private string? SeedFile(string key)
    {
        string folder = Path.Combine(host.WorldDir, "mudlevels");
        string exact = Path.Combine(folder, key);
        if (File.Exists(exact))
        {
            return exact;
        }
        if (!Directory.Exists(folder))
        {
            return null;
        }
        foreach (string file in Directory.EnumerateFiles(folder))
        {
            if (LevelKey(Path.GetFileName(file)) == key)
            {
                return file;
            }
        }
        return null;
    }

    private static bool IsPlainFileName(string name) =>
        name.Length > 0 && name != "." && name != ".." && name.IndexOfAny(new[] { '/', '\\', '\0' }) < 0;

    private Level NewLevel(string name)
    {
        var level = new Level(name) { Source = "new" };
        level.Header.Name = FileText.EscapeLine(FileText.ToBytesText(name));
        levels[name] = level;
        return level;
    }

    private Outcome LevelLoad(IReadOnlyList<Val> args)
    {
        if (args.Count != 2 || !(args[0].IsNil || args[0].Kind == ValKind.String))
        {
            return Outcome.Fail(Status.BadArgs, "level.load takes a level name and a cursor");
        }
        string name = LevelKey(args[0].AsString);
        Val cursor = args[1];
        Outcome none = Outcome.Ok(new Page<LevelObject>(Array.Empty<LevelObject>(), false, Val.Nil).Result());
        long budget = Paging.ReplyBudget(maxFrame, new Page<LevelObject>(Array.Empty<LevelObject>(), true, Val.Int(int.MaxValue)).Result());

        Level? level;
        if (cursor.IsNil)
        {
            level = GetOrReadLevel(name);
            if (level == null)
            {
                return none;
            }
            // the objects not shown yet; one that cannot be shown in a frame is given up
            var pending = new List<LevelObject>();
            foreach (LevelObject lo in level.Objects.Where(o => !o.Placed))
            {
                if (Paging.EncodedSize(PlaceEffect(lo)) > budget)
                {
                    lo.Placed = true;
                    log(1, "an object of level " + name + " is too large to show: " + lo.Ob.Arch?.Name);
                    continue;
                }
                pending.Add(lo);
            }
            if (pending.Count == 0)
            {
                level.Snapshot = null;
                return none;
            }
            level.Snapshot = pending;
        }
        else
        {
            if (!levels.TryGetValue(name, out level) || level.Snapshot == null)
            {
                return none;
            }
        }

        Page<LevelObject> page;
        try
        {
            page = Paging.LevelPage(level.Snapshot, cursor, Paging.MaxPerLoadCall, lo => Paging.EncodedSize(PlaceEffect(lo)), budget);
        }
        catch (BadCursorException e)
        {
            return Outcome.Fail(Status.BadArgs, e.Message);
        }
        catch (ItemTooLargeException e)
        {
            return Outcome.Fail(Status.TooLarge, e.Message);
        }
        List<LevelObject> live = page.Items.Where(o => !o.Dead).ToList();
        List<Val> places = live.Select(PlaceEffect).ToList();
        foreach (LevelObject lo in live)
        {
            lo.Placed = true;
        }
        effects.AddRange(places);
        if (!page.More)
        {
            level.Snapshot = null;
        }
        return Outcome.Ok(page.Result());
    }

    private Outcome LevelUnload(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || !(args[0].IsNil || args[0].Kind == ValKind.String))
        {
            return Outcome.Fail(Status.BadArgs, "level.unload takes a level name");
        }
        if (!levels.TryGetValue(LevelKey(args[0].AsString), out Level? level))
        {
            return Outcome.Ok(Val.Nil);
        }
        if (level.Dirty)
        {
            SubmitLevel(level);
        }
        foreach (LevelObject lo in level.Objects)
        {
            lo.Dead = true;
            handles.Kill(lo);
            FreeCfObject(lo.Ob);
        }
        level.Objects.Clear();
        level.Snapshot = null;
        levels.Remove(level.Name);
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>Writes the level's file (through the writer) and clears its dirty flag.</summary>
    private void SubmitLevel(Level level)
    {
        string? rel = FileNames.LevelFile(level.Name);
        if (rel == null || refused.Contains("level:" + level.Name))
        {
            log(1, "level " + level.Name + " has no usable file name and is not saved");
            level.Dirty = false;
            return;
        }
        PutFile(rel, FileText.ToBytes(LevelFiles.Write(world!, level)));
        level.Dirty = false;
    }

    private void FreeCfObject(CfObject ob)
    {
        ob.SetFlag(Define.FLAG_REMOVED);
        world!.FreeObject(ob);
    }

    private static string Text(Val v) => v.Kind switch
    {
        ValKind.Int => v.AsInt.ToString(CultureInfo.InvariantCulture),
        ValKind.Float => v.AsFloat.ToString("R", CultureInfo.InvariantCulture),
        ValKind.True => "1",
        ValKind.False => "0",
        _ => v.AsString,
    };

    // ------------------------------------------------------------------ objects

    /// <summary>The effect of a write on a target: the event of a level object (objects.md 3.2) or the client string of a player.</summary>
    private void AfterSet(object target, Bound member, Val stored, bool changed)
    {
        if (target is LevelObject lo)
        {
            if (!changed)
            {
                return;
            }
            lo.Level.Dirty = true;
            events.Add(Val.Map(
                ("name", Val.Str(Manifest.EventOnUpdateObject)),
                ("target", Val.Str("object")),
                ("handle", Val.Handle(handles.HandleOf(lo))),
                ("args", Val.List())));
        }
        else if (target is PlayerState ps)
        {
            ps.Dirty = true;
            if (member.ClientName is { } name)
            {
                SetClientString(ps, name, Text(stored));
            }
        }
    }

    private Outcome ObjectDestroyed(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 1))
        {
            return Outcome.Fail(Status.BadArgs, "object.destroyed takes a handle");
        }
        return WithTarget(args[0], t =>
        {
            if (t is not LevelObject lo)
            {
                return Outcome.Fail(Status.BadHandle, "the handle is not an object in a level");
            }
            // the host's NPC is gone already: the object leaves the level without an effect (and without onMudDestroy: objects.md F7)
            DestroyObject(lo, removeNpc: false);
            return Outcome.Ok(Val.Nil);
        });
    }

    /// <summary>
    /// Destroys an object of a level: it leaves the level (so it is not saved again), its handle is dead for good, its inventory
    /// goes with it. <paramref name="removeNpc"/> adds the <c>npc.remove</c> effect, for a script that destroys the object.
    /// </summary>
    private void DestroyObject(LevelObject lo, bool removeNpc)
    {
        if (removeNpc)
        {
            effects.Add(Val.Map(("k", Val.Str("npc.remove")), ("handle", Val.Handle(handles.HandleOf(lo)))));
        }
        lo.Dead = true;
        lo.Level.Objects.Remove(lo);
        lo.Level.Dirty = true;
        handles.Kill(lo);
        FreeCfObject(lo.Ob);
    }

    private Outcome ObjectsBlobs(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || !IsListOrNil(args[0]))
        {
            return Outcome.Fail(Status.BadArgs, "objects.blobs takes a list of rows");
        }
        // every row is checked before one is applied: a request that fails changes nothing
        var rows = new List<(ulong Handle, double X, double Y, string Blob)>();
        foreach (Val row in args[0].Items)
        {
            if (row.Kind != ValKind.List || row.Items.Count != 4 || row.Items[0].Kind != ValKind.Handle
                || row.Items[1].Kind is not (ValKind.Int or ValKind.Float) || row.Items[2].Kind is not (ValKind.Int or ValKind.Float)
                || !(row.Items[3].IsNil || row.Items[3].Kind == ValKind.String))
            {
                return Outcome.Fail(Status.BadArgs, "a row is [handle, x, y, blob]");
            }
            rows.Add((row.Items[0].AsHandle, AsNumber(row.Items[1])!.Value, AsNumber(row.Items[2])!.Value, row.Items[3].AsString));
        }
        foreach ((ulong handle, double x, double y, string blob) in rows)
        {
            if (handles.Resolve(handle) is not LevelObject lo)
            {
                continue; // dead, unknown, or not an object in a level
            }
            string? stored = blob.Length == 0 ? null : blob;
            if (lo.X != x || lo.Y != y || lo.Blob != stored || lo.SeedFlags.Count > 0)
            {
                lo.Level.Dirty = true;
            }
            lo.X = x;
            lo.Y = y;
            lo.Blob = stored;
            lo.SeedFlags.Clear(); // the host's state is the truth from now on
        }
        return Outcome.Ok(Val.Nil);
    }

    // ------------------------------------------------------------------ making objects

    /// <summary>
    /// The level that holds a place of the running request, or null with a log line. The script's level is <c>ctx.level</c>; when it is
    /// a gmap that has a layout (protocol section 7.1) the level is the segment that holds (<paramref name="x"/>, <paramref name="y"/>)
    /// and the position becomes the position in that segment; a position in no segment gives null and nothing is made.
    /// </summary>
    private Level? RequestLevel(string function, ref double x, ref double y, Val rawX, Val rawY)
    {
        if (string.IsNullOrEmpty(ctxLevel))
        {
            log(1, function + ": the call has no level");
            return null;
        }
        // the position as the call gave it: a NaN or an infinity is no position of a gmap (a level's script reads it as 0, as AsNumber does)
        if (ResolvePlace(ctxLevel, RawNumber(rawX) ?? x, RawNumber(rawY) ?? y) is not { } place)
        {
            log(1, function + ": the position is in no segment of the gmap " + ctxLevel);
            return null;
        }
        if (place.Gmap != null)
        {
            x = place.X;
            y = place.Y;
        }
        return LevelForPlacing(function, place.Level);
    }

    /// <summary>The level of that key for an object to be placed in it: the one held or read, else a new one; null with a log line when it cannot be used.</summary>
    private Level? LevelForPlacing(string function, string key)
    {
        Level? level = GetOrReadLevel(key);
        if (level == null)
        {
            if (refused.Contains("level:" + key) || FileNames.LevelFile(key) == null)
            {
                log(1, function + ": level " + key + " cannot be used");
                return null;
            }
            level = NewLevel(key);
        }
        return level;
    }

    /// <summary>A new object of an archetype in a level, not shown yet; null (with a log line) when the archetype is unknown, a treasure list or the level is full.</summary>
    private LevelObject? CreateObject(string function, Level level, double x, double y, string archName)
    {
        if (archName.StartsWith("treasure", StringComparison.OrdinalIgnoreCase) && archName.Length > 8 && char.IsWhiteSpace(archName[8]))
        {
            log(1, function + ": the treasure list \"" + archName.Substring(8).Trim() + "\" is not known (treasure lists come with the rules of phase 4)");
            return null;
        }
        bool warn = world!.WarnArchetypes;
        world.WarnArchetypes = false; // the call says so itself, once
        Archetype? at = world.FindArchetype(archName);
        world.WarnArchetypes = warn;
        if (at == null)
        {
            log(1, function + ": archetype " + archName + " not found");
            return null;
        }
        if (level.Objects.Count >= options.Gk.MaxObjectsPerLevel)
        {
            log(1, function + ": level " + level.Name + " holds " + level.Objects.Count + " objects, the most it may");
            return null;
        }
        CfObject ob = world.ArchToObject(at)!;
        return Attach(level, ob, x, y);
    }

    /// <summary>
    /// Adds the <c>npc.place</c> of a made object to the reply and returns its handle. A maker is atomic: when this fails (the tags
    /// of the epoch are used up) the object leaves the level again, so that no object is saved that the host never shows.
    /// </summary>
    private Val Place(LevelObject lo)
    {
        try
        {
            effects.Add(PlaceEffect(lo));
            return Val.Handle(handles.HandleOf(lo));
        }
        catch
        {
            DestroyObject(lo, removeNpc: false);
            throw;
        }
    }

    private LevelObject Attach(Level level, CfObject ob, double x, double y)
    {
        ob.Map = level;
        var lo = new LevelObject(level, ob) { X = x, Y = y, Placed = true };
        level.Objects.Add(lo);
        level.Dirty = true;
        return lo;
    }

    private Outcome MudAddObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 3 || AsNumber(args[0]) is not { } x || AsNumber(args[1]) is not { } y || !(args[2].IsNil || args[2].Kind == ValKind.String))
        {
            return Outcome.Fail(Status.BadArgs, "mudaddobject takes x, y and an archetype name");
        }
        Level? level = RequestLevel("mudaddobject", ref x, ref y, args[0], args[1]);
        if (level == null)
        {
            return Outcome.Ok(Val.Nil);
        }
        LevelObject? lo = CreateObject("mudaddobject", level, x, y, FileText.ToBytesText(args[2].AsString));
        if (lo == null)
        {
            return Outcome.Ok(Val.Nil);
        }
        return Outcome.Ok(Place(lo));
    }

    private Outcome MudAddSpecialObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 5 || AsNumber(args[0]) is not { } x || AsNumber(args[1]) is not { } y || AsNumber(args[2]) is not { } value
            || !(args[3].IsNil || args[3].Kind == ValKind.String) || !(args[4].IsNil || args[4].Kind == ValKind.String))
        {
            return Outcome.Fail(Status.BadArgs, "mudaddspecialobject takes x, y, a value, \"<archetype> <rest>\" and a message");
        }
        Level? level = RequestLevel("mudaddspecialobject", ref x, ref y, args[0], args[1]);
        if (level == null)
        {
            return Outcome.Ok(Val.Nil);
        }
        // the first word is the archetype, the rest of the text is the object's slaying (objects.md F2)
        string spec = FileText.ToBytesText(args[3].AsString).Trim();
        int cut = 0;
        while (cut < spec.Length && !char.IsWhiteSpace(spec[cut]))
        {
            cut++;
        }
        string archName = spec.Substring(0, cut);
        string rest = spec.Substring(cut).Trim();
        LevelObject? lo = CreateObject("mudaddspecialobject", level, x, y, archName);
        if (lo == null)
        {
            return Outcome.Ok(Val.Nil);
        }
        if (rest.Length > 0)
        {
            lo.Ob.Slaying = rest;
        }
        string message = FileText.ToBytesText(args[4].AsString);
        if (message.Length > 0)
        {
            lo.Ob.Msg = message;
        }
        lo.Ob.Value = (int)Math.Clamp(Math.Truncate(value), int.MinValue, int.MaxValue);
        return Outcome.Ok(Place(lo));
    }

    private Outcome InvokeOnObject(LevelObject lo, long method, IReadOnlyList<Val> args)
    {
        switch (method)
        {
            case TMudObjectMembers.MethodCloneMudObject:
            {
                if (args.Count != 2 || AsNumber(args[0]) is not { } x || AsNumber(args[1]) is not { } y)
                {
                    return Outcome.Fail(Status.BadArgs, "cloneMudObject takes x and y");
                }
                // a script on a gmap with a layout places the clone in the segment of its position (protocol section 7.1); else it stays in the object's level
                Level target = lo.Level;
                if (!string.IsNullOrEmpty(ctxLevel) && GmapOf(ctxLevel) != null)
                {
                    if (RequestLevel("cloneMudObject", ref x, ref y, args[0], args[1]) is not { } segment)
                    {
                        return Outcome.Ok(Val.Nil);
                    }
                    target = segment;
                }
                if (target.Objects.Count >= options.Gk.MaxObjectsPerLevel)
                {
                    log(1, "cloneMudObject: level " + target.Name + " holds " + target.Objects.Count + " objects, the most it may");
                    return Outcome.Ok(Val.Nil);
                }
                // the mud state is copied (fields, the extra lines, the inventory); the script's variables and the host's state are not
                CfObject? copy = world!.ObjectCreateClone(lo.Ob);
                if (copy == null)
                {
                    return Outcome.Fail(Status.Internal, "the object cannot be cloned");
                }
                LevelObject clone = Attach(target, copy, x, y);
                return Outcome.Ok(Place(clone));
            }
            case TMudObjectMembers.MethodDestroyMudObject:
                if (args.Count != 0)
                {
                    return Outcome.Fail(Status.BadArgs, "destroymudobject takes no arguments");
                }
                DestroyObject(lo, removeNpc: true);
                return Outcome.Ok(Val.Nil);
            default:
                return Outcome.Fail(Status.BadArgs, "unknown method " + method);
        }
    }
}
