// Not ported from a Crossfire file: the operations of the Crossfire extension (docs/protocol.md section 7). Everything here runs on
// the worker thread W only: the CfWorld has no locks. The operations are in the partial files Dispatcher.*.cs: this one holds the
// session, the handles and the members, Dispatcher.Levels.cs the levels and their objects, Dispatcher.Accounts.cs the players.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>The outcome of an operation: a status, and the result (a string that says what went wrong when the status is not 0).</summary>
internal readonly record struct Outcome(int Status, Val Result)
{
    public static Outcome Ok(Val result) => new(Protocol.Status.Ok, result);

    public static Outcome Fail(int status, string why) => new(status, Val.Str(why));
}

/// <summary>Hooks of the tests, none of them used in production.</summary>
internal sealed class ExtensionOptions
{
    /// <summary>Called on the worker thread before it handles a request (a test makes the worker slow here).</summary>
    public Action<ulong>? BeforeOperation { get; init; }

    /// <summary>Called on the worker thread right after it took a request from the queue (a test lets a request wait here).</summary>
    public Action<ulong>? AfterDequeue { get; init; }

    /// <summary>The largest tag the extension gives out (a test lowers it to see the answer when the tags are used up).</summary>
    public uint TagLimit { get; init; } = uint.MaxValue;

    /// <summary>How long the process waits for the worker to finish when the connection ends.</summary>
    public TimeSpan WorkerGrace { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How long <c>session.init</c> waits for an older process's lock on the data folder (protocol section 1: 20 s).</summary>
    public TimeSpan LockWait { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>How long <c>session.shutdown</c> with save true waits for the files (the host waits 10 s for its REPLY).</summary>
    public TimeSpan ShutdownSaveTimeout { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>How long the exit waits for the files when the connection ends (the process must be gone within 5 s: the grace and this fit in it).</summary>
    public TimeSpan ExitSaveTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>When the file writer writes.</summary>
    public FileWriterOptions Writer { get; init; } = new();

    /// <summary>The manifest's <c>wants_tick</c>: false in the shipped manifest, a test sets it to send ticks.</summary>
    public bool WantsTick { get; init; }

    /// <summary>How long the worker spins on its queue after a REPLY before it blocks (design section 7).</summary>
    public TimeSpan Spin { get; init; } = TimeSpan.FromTicks(500); // 50 microseconds: a tick of TimeSpan is 100 ns

    /// <summary>The source of the wall-clock times written to the state file.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>The settings of the Graal Kingdoms adaptation (class prefixes, the default class, the cap of objects per level).</summary>
    public GkSettings Gk { get; init; } = new();

    /// <summary>How long the worker waits for the host's answer to a query (protocol section 4.3) before the query fails with <c>internal</c>.</summary>
    public TimeSpan QueryTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>A test's registrations in the manifest, after every lane's (functions, types and events that only a test needs).</summary>
    public Action<ManifestBuilder>? ExtraManifest { get; init; }

    /// <summary>Called at the end of a request for each player controller that asked for a flush, after <c>FlushShown</c> (a test's stand-in for lane 4C's flush).</summary>
    public Action<Dispatcher, PlayerState>? FlushHook { get; init; }
}

/// <summary>The worker side: one request at a time, in the order they arrive.</summary>
internal sealed partial class Dispatcher : IMemberEnv
{
    private readonly HostHello host;
    private readonly Action<int, string> log;
    private readonly ExtensionOptions options;
    private readonly Counters counters;
    private readonly int maxFrame;
    private readonly CancellationToken stopping;
    private readonly SessionState session;
    private readonly HostClock clock = new();
    private readonly HandleTable handles;
    private DataLock? dataLock;
    private FileWriter? writer;
    private bool saveOnExit = true;
    private bool savedAtShutdown;

    private CfWorld? world;
    private GkHost? gk;
    private GkClasses classes;
    private int archetypeCount;

    // what a request collects for its REPLY (protocol sections 10 and 11) and the context it came with (section 4.2)
    private readonly List<Val> effects = new();
    private readonly List<Val> events = new();
    private string? ctxLevel;
    private string? ctxPlayer;

    // the world's error lines forwarded to the host's log (the first few of each request, and only for level and account work)
    private bool forwardWorldLog;
    private int worldErrorLines;

    /// <summary>The manifest of this link (what the HELLO carried): its functions are the operations above 63 that the dispatcher knows.</summary>
    private readonly ManifestBuilder manifest;

    // manifest: the one the HELLO carried; made from the host's minor version when it is null
    public Dispatcher(HostHello host, int maxFrame, Action<int, string> log, ExtensionOptions options, Counters counters, CancellationToken stopping = default, ManifestBuilder? manifest = null)
    {
        this.stopping = stopping;
        session = new SessionState(options.Time, host.LinkMinor);
        this.manifest = manifest ?? Manifest.Collect(host.LinkMinor, options.ExtraManifest);
        this.host = host;
        this.maxFrame = maxFrame;
        this.log = log;
        this.options = options;
        this.counters = counters;
        handles = new HandleTable(host.Epoch, options.TagLimit);
        classes = GkClasses.Of(Array.Empty<string>(), options.Gk);
    }

    /// <summary>True after the REPLY to <c>session.init</c> was made with status 0.</summary>
    public bool Initialized { get; private set; }

    /// <summary>The link's minor version (protocol section 4.1); every part of the extension reads it here.</summary>
    public int LinkMinor => session.LinkMinor;

    /// <summary>True when the link speaks 1.1: the 1.1 effects, event targets and keys are for such a link only.</summary>
    public bool Speaks11 => session.Speaks11;

    /// <summary>
    /// While not null, the extension is busy (saving or recovering: lane 4F sets it): a function the manifest declares <c>slow</c>
    /// answers error 7 <c>busy</c> with this text, before it changes anything (docs/mud-phase3.md section 3, rule 10). A method or
    /// an operation that changes items or money and is not such a function calls <see cref="CheckBusy"/> first. Worker only.
    /// </summary>
    public string? Busy { get; set; }

    /// <summary>The <c>busy</c> answer of an operation that changes items or money, or null when the extension is not busy.</summary>
    public Outcome? CheckBusy() => Busy is { } why ? Outcome.Fail(Status.Busy, why) : null;

    CfWorld IMemberEnv.World => world!;

    internal CfWorld? WorldOrNull => world;

    ulong IMemberEnv.HandleOf(object target) => handles.HandleOf(target);

    uint IMemberEnv.TagOf(object target) => handles.TagOf(target);

    /// <summary>The REPLY to a REQ. A reply that would not fit in the link's maximum becomes <c>too_large</c> (section 4.2).</summary>
    public ReplyFrame Handle(ReqFrame req)
    {
        counters.CountRequest();
        // The releases (req.Releases) are handles of objects the host no longer refers to. Every handle this extension gives out names an
        // archetype (permanent), an object in a level (never released: protocol section 5) or a logged-in player: there is nothing to
        // forget, and dead or unknown handles are ignored.
        effects.Clear();
        events.Clear();
        flushed.Clear();
        worldErrorLines = 0;
        ctxLevel = req.Ctx.Get("level") is { Kind: ValKind.String } l ? LevelKey(l.AsString) : null;
        ctxPlayer = req.Ctx.Get("player") is { Kind: ValKind.String } p ? p.AsString : null;
        Outcome outcome;
        try
        {
            options.BeforeOperation?.Invoke(req.Op);
            outcome = Dispatch(req);
            if (outcome.Status == Status.Ok)
            {
                // the one flush of the request, before the REPLY is built (docs/mud-phase3.md section 5); after an error nothing is sent
                FlushControllers();
            }
        }
        catch (TagsUsedUpException)
        {
            outcome = Outcome.Fail(Status.Internal, "the tags of this epoch are used up");
        }
        catch (Exception e)
        {
            log(0, "request " + req.ReqId + " (op " + req.Op + ") failed: " + e.GetType().Name + ": " + e.Message);
            outcome = Outcome.Fail(Status.Internal, "internal error");
        }
        ReplyFrame reply = outcome.Status == Status.Ok
            ? new ReplyFrame(req.ReqId, (ulong)outcome.Status, outcome.Result, effects.ToArray(), events.ToArray())
            : new ReplyFrame(req.ReqId, (ulong)outcome.Status, outcome.Result, Array.Empty<Val>(), Array.Empty<Val>());
        if (outcome.Status != Status.Ok)
        {
            counters.CountError();
        }
        else if (FrameCodec.Encode(reply).Length - 4 > maxFrame)
        {
            counters.CountError();
            reply = new ReplyFrame(req.ReqId, (ulong)Status.TooLarge, Val.Str("the answer does not fit in a frame"), Array.Empty<Val>(), Array.Empty<Val>());
            ForgetFlushed(); // the strings of this flush never left: the next flush sends them again
        }
        return reply;
    }

    private Outcome Dispatch(ReqFrame req)
    {
        if (req.Op == 1)
        {
            return Init(req.Args);
        }
        if (!Initialized)
        {
            return Outcome.Fail(Status.Unavailable, "session.init has not been answered");
        }
        switch (req.Op)
        {
            case 2 when options.WantsTick:
                return Tick(req.Args);
            case 3:
                return Flush(req.Args);
            case 4:
                return Shutdown(req.Args);
            case 5:
                return PlayerLogin(req.Args);
            case 6:
                return PlayerLogout(req.Args);
            case 7:
                return LevelLoad(req.Args);
            case 8:
                return LevelUnload(req.Args);
            case 9:
                return Get(req.Args);
            case 10:
                return Set(req.Args);
            case 11:
                return GetMany(req.Args);
            case 12:
                return GetList(req.Args);
            case 13:
                return Invoke(req.Args);
            case 14:
                return ObjectDestroyed(req.Args);
            case 15:
                return req.Args.Count == 0 ? Outcome.Ok(Stats()) : Outcome.Fail(Status.BadArgs, "stats takes no arguments");
            case 16:
                return ObjectsBlobs(req.Args);
            case 17 when Speaks11:
                return GmapLayoutOp(req.Args);
            case Manifest.OpFindArchetype:
                return FindArchetype(req.Args, byRealName: false);
            case Manifest.OpFindArchetypeByRealName:
                return FindArchetype(req.Args, byRealName: true);
            case Manifest.OpMudAddObject:
                return MudAddObject(req.Args);
            case Manifest.OpMudAddSpecialObject:
                return MudAddSpecialObject(req.Args);
            default:
                if (manifest.FindFunction((long)req.Op) is { Handler: { } handler } function)
                {
                    // a lane's function: a slow one answers busy before it can change anything (rule 10 of docs/mud-phase3.md section 3)
                    return function.Slow && CheckBusy() is { } busy ? busy : handler(this, req.Args);
                }
                // 2 without wants_tick (the host must not send it), 17 to 63 (reserved) and the manifest's other ids
                return Outcome.Fail(Status.UnknownOp, "unknown operation " + req.Op);
        }
    }

    // ------------------------------------------------------------------ session

    private void WorldLog(LogLevel level, string message)
    {
        if (forwardWorldLog && level == LogLevel.Error && worldErrorLines++ < 5)
        {
            log(1, "crossfire: " + message.TrimEnd('\n'));
        }
    }

    private Outcome Init(IReadOnlyList<Val> args)
    {
        if (args.Count != 0 || Initialized)
        {
            return Outcome.Fail(Status.BadArgs, "session.init takes no arguments and comes once");
        }
        // the lock comes before the world is loaded: an older process may still be saving into this folder (protocol section 1)
        try
        {
            Directory.CreateDirectory(host.DataDir);
            // a session.init that failed after the lock was taken keeps it: a second try must not wait for itself
            dataLock ??= DataLock.Acquire(Path.Combine(host.DataDir, ".lock"), options.LockWait, stopping);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Outcome.Fail(Status.Internal, "the data folder cannot be locked: " + e.Message);
        }
        if (dataLock == null)
        {
            log(1, "the data folder " + host.DataDir + " is locked by another process");
            return Outcome.Fail(Status.Busy, "the data folder is locked by another process");
        }
        writer ??= new FileWriter(host.DataDir, options.Writer, counters, log);
        string? arch = new[] { "Arch", "arch" }.Select(n => Path.Combine(host.WorldDir, n)).FirstOrDefault(Directory.Exists);
        if (arch == null)
        {
            return Outcome.Fail(Status.Internal, "the world has no Arch folder");
        }
        gk = new GkHost { Items = this };
        var w = new CfWorld(gk, WorldLog);
        PrepareStartPath(w);
        ArchCollection collection = ArchCollector.Collect(arch);
        w.InitArchetypes(collection.Archetypes);
        InitSkills(w);
        InitLanes(w);

        int count = 0;
        int parts = 0;
        for (Archetype? at = w.FirstArchetype; at != null; at = at.More == null ? at.Next : at.More)
        {
            count++;
            if (at.Head != null)
            {
                parts++;
            }
        }
        world = w;
        archetypeCount = count;
        session.Archetypes = count;
        classes = GkClasses.Read(host.WorldDir, options.Gk);
        session.MarkInitialized();
        Initialized = true;
        SubmitSnapshot("init");
        log(2, "loaded " + count + " archetypes (" + parts + " of them parts) from " + collection.Files.Count + " files; "
            + w.LogCounts[(int)LogLevel.Error] + " errors in the data, " + collection.Warnings.Count + " warnings of the collector; "
            + classes.Count + " script classes");
        return Outcome.Ok(Val.Map(("archetypes", Val.Int(count))));
    }

    private Outcome Tick(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || args[0].AsNumberToInt() is not { } nowMs)
        {
            return Outcome.Fail(Status.BadArgs, "session.tick takes now_ms, an int");
        }
        if (!clock.Tick(nowMs))
        {
            return Outcome.Fail(Status.BadArgs, "now_ms is negative or before the last tick's");
        }
        session.Ticks++;
        return Outcome.Ok(Val.Nil);
    }

    private Outcome Flush(IReadOnlyList<Val> args)
    {
        if (args.Count != 0)
        {
            return Outcome.Fail(Status.BadArgs, "session.flush takes no arguments");
        }
        session.Flushes++;
        SubmitSnapshot("flush");
        writer!.Kick(); // the REPLY does not wait for the files
        return Outcome.Ok(Val.Nil);
    }

    private Outcome Shutdown(IReadOnlyList<Val> args)
    {
        // the one argument is `save`, a bool (nil reads as false: section 6.3)
        if (args.Count != 1 || !(args[0].IsNil || args[0].IsBool))
        {
            return Outcome.Fail(Status.BadArgs, "session.shutdown takes one bool");
        }
        if (!args[0].AsBool)
        {
            saveOnExit = false;
            writer!.Discard();
            return Outcome.Ok(Val.Nil);
        }
        SubmitSnapshot("shutdown");
        if (!writer!.WaitWritten(options.ShutdownSaveTimeout))
        {
            return Outcome.Fail(Status.Internal, "the files could not be written");
        }
        savedAtShutdown = true;
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>
    /// Gives the file writer a snapshot of everything this process holds (worker only; the writer gets bytes it alone owns): the
    /// state file, every level that changed since it was read or saved, and every logged-in account.
    /// </summary>
    private void SubmitSnapshot(string reason)
    {
        writer!.Submit(SessionState.FileName, session.Snapshot(reason, host.Epoch, host.ServerName, counters, clock));
        if (world == null)
        {
            return;
        }
        foreach (Level level in levels.Values.Where(l => l.Dirty).ToList())
        {
            SubmitLevel(level);
        }
        foreach (PlayerState player in players.Values.ToList())
        {
            SubmitAccount(player);
        }
    }

    /// <summary>
    /// The end of the connection (protocol section 1, "Stopping"): saves unless <c>session.shutdown</c> with false came before, and
    /// stops the writer. <paramref name="workerDone"/> is false when the worker did not finish: its state is not read then, and only
    /// what was already handed to the writer is written.
    /// </summary>
    public void Finish(bool workerDone)
    {
        if (writer == null)
        {
            return; // session.init never got as far as the lock: nothing is held, nothing to save
        }
        if (!saveOnExit)
        {
            writer.Discard();
        }
        else if (!savedAtShutdown)
        {
            if (workerDone && Initialized)
            {
                SubmitSnapshot("exit");
            }
            if (!writer.WaitWritten(options.ExitSaveTimeout))
            {
                log(0, "the files were not all written before the exit");
            }
        }
        writer.Stop(TimeSpan.FromMilliseconds(500));
    }

    /// <summary>Gives up the lock on the data folder.</summary>
    public void Release() => dataLock?.Dispose();

    private Val Stats()
    {
        List<(string, Val)> pairs = counters.ToPairs();
        pairs.Add(("archetypes", Val.Int(archetypeCount)));
        pairs.Add(("tags", Val.Int(handles.Issued)));
        pairs.Add(("ticks", Val.Int(session.Ticks)));
        pairs.Add(("last_tick_ms", Val.Int(clock.LastTickMs)));
        pairs.Add(("flushes", Val.Int(session.Flushes)));
        pairs.Add(("levels", Val.Int(levels.Count)));
        pairs.Add(("level_objects", Val.Int(levels.Values.Sum(l => l.Objects.Count))));
        pairs.Add(("players", Val.Int(players.Count)));
        pairs.Add(("examine_lines", Val.Int(ExamineLines)));
        return Val.Map(pairs.ToArray());
    }

    // ------------------------------------------------------------------ members

    /// <summary>A list argument: nil reads as the empty list (section 6.3), any other kind but a list is not one.</summary>
    private static bool IsListOrNil(Val v) => v.Kind is ValKind.List or ValKind.Nil;

    private static bool ArgsShapeOk(IReadOnlyList<Val> args, int count, bool handleFirst = true) =>
        args.Count == count && (!handleFirst || args[0].Kind is ValKind.Handle or ValKind.Nil);

    /// <summary>A member of a live target, ready to read and write.</summary>
    private abstract class Bound
    {
        public abstract Val Read();

        public abstract bool Writable { get; }

        public abstract SetResult Write(Val v);

        public virtual string? ClientName => null;

        /// <summary>For a member that is a list of handles: the objects of the list; null for any other member.</summary>
        public virtual IReadOnlyList<CfObject>? Items() => null;
    }

    private sealed class BoundArch : Bound
    {
        private readonly ArchMember member;
        private readonly Archetype arch;
        private readonly CfWorld world;

        public BoundArch(ArchMember member, Archetype arch, CfWorld world)
        {
            this.member = member;
            this.arch = arch;
            this.world = world;
        }

        public override Val Read() => member.Read(world, arch);

        public override bool Writable => false;

        public override SetResult Write(Val v) => SetResult.Fail("read only");
    }

    private sealed class BoundMember<T> : Bound
    {
        private readonly IMemberEnv env;
        private readonly T target;
        private readonly ObjMember<T> member;

        public BoundMember(IMemberEnv env, T target, ObjMember<T> member)
        {
            this.env = env;
            this.target = target;
            this.member = member;
        }

        public override Val Read() => member.Read(env, target);

        public override bool Writable => member.Writable && member.Write != null;

        public override SetResult Write(Val v) => member.Write!(env, target, v);

        public override string? ClientName => member.ClientName;

        public override IReadOnlyList<CfObject>? Items() => member.Items?.Invoke(env, target);
    }

    /// <summary>The member of that id on a target (an archetype, a level object or a player), or null when the target's type has none.</summary>
    private Bound? BindMember(object target, long id) => target switch
    {
        Archetype at => TMudArchMembers.Find(id) is { } m ? new BoundArch(m, at, world!) : null,
        LevelObject lo => TMudObjectMembers.Table.Find(id) is { } m ? new BoundMember<LevelObject>(this, lo, m) : null,
        PlayerState ps => TMudPlayerMembers.Table.Find(id) is { } m ? new BoundMember<PlayerState>(this, ps, m) : null,
        _ => null,
    };

    private Outcome WithTarget(Val handleArg, Func<object, Outcome> then)
    {
        // nil (and any other value that ArgsShapeOk let through) is a nil handle: bad_handle
        object? target = handleArg.Kind == ValKind.Handle ? handles.Resolve(handleArg.AsHandle) : null;
        return target == null ? Outcome.Fail(Status.BadHandle, "dead handle") : then(target);
    }

    private Outcome Get(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 2) || args[1].AsNumberToInt() is not { } id)
        {
            return Outcome.Fail(Status.BadArgs, "get takes a handle and a member id");
        }
        return WithTarget(args[0], t =>
            BindMember(t, id) is { } m ? Outcome.Ok(m.Read()) : Outcome.Fail(Status.BadArgs, "unknown member " + id));
    }

    private Outcome GetMany(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 2) || !IsListOrNil(args[1]))
        {
            return Outcome.Fail(Status.BadArgs, "get_many takes a handle and a list of member ids");
        }
        var ids = new List<long>();
        foreach (Val v in args[1].Items)
        {
            if (v.AsNumberToInt() is not { } id)
            {
                return Outcome.Fail(Status.BadArgs, "a member id is not a number");
            }
            ids.Add(id);
        }
        return WithTarget(args[0], t =>
        {
            var values = new List<Val>(ids.Count);
            foreach (long id in ids)
            {
                if (BindMember(t, id) is not { } m)
                {
                    return Outcome.Fail(Status.BadArgs, "unknown member " + id);
                }
                values.Add(m.Read());
            }
            return Outcome.Ok(Val.List(values));
        });
    }

    private Outcome Set(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 3) || args[1].AsNumberToInt() is not { } id)
        {
            return Outcome.Fail(Status.BadArgs, "set takes a handle, a member id and a value");
        }
        return WithTarget(args[0], t =>
        {
            if (BindMember(t, id) is not { } m)
            {
                return Outcome.Fail(Status.BadArgs, "unknown member " + id);
            }
            if (!m.Writable)
            {
                return Outcome.Fail(Status.BadArgs, "member " + id + " is read-only");
            }
            Val before = t is LevelObject ? m.Read() : Val.Nil;
            SetResult r = m.Write(args[2]);
            if (r.Stored is not { } stored)
            {
                return Outcome.Fail(Status.BadArgs, r.Error ?? "the value does not fit the member");
            }
            // a write that leaves the value as it was changes nothing: no event (a handler that writes what it read would loop), no save
            bool changed = t is not LevelObject || !Val.Same(before, m.Read());
            AfterSet(t, m, stored, changed);
            return Outcome.Ok(stored);
        });
    }

    private Outcome GetList(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 3) || args[1].AsNumberToInt() is not { } id || !IsListOrNil(args[2]))
        {
            return Outcome.Fail(Status.BadArgs, "get_list takes a handle, a member id and a list of field ids");
        }
        var fields = new List<long>();
        foreach (Val v in args[2].Items)
        {
            if (v.AsNumberToInt() is not { } field || TMudObjectMembers.Table.Find(field) is null)
            {
                return Outcome.Fail(Status.BadArgs, "a field id is not a member of TMudObject");
            }
            fields.Add(field);
        }
        return WithTarget(args[0], t =>
        {
            if (BindMember(t, id) is not { } m)
            {
                return Outcome.Fail(Status.BadArgs, "unknown member " + id);
            }
            if (m.Items() is not { } items)
            {
                return Outcome.Fail(Status.BadArgs, "member " + id + " is not a list of handles");
            }
            if (items.Count > manifest.MaxList)
            {
                return Outcome.Fail(Status.TooLarge, "the list has " + items.Count + " elements, the most get_list gives is " + manifest.MaxList);
            }
            var rows = new List<Val>(items.Count);
            foreach (CfObject item in items)
            {
                LevelObject proxy = HolderOf(item) is { } holder ? TagItem(holder, item) : ItemProxy(item);
                var row = new List<Val>(fields.Count + 1) { Val.Handle(handles.HandleOf(proxy)) };
                foreach (long field in fields)
                {
                    row.Add(BindMember(proxy, field)!.Read());
                }
                rows.Add(Val.List(row));
            }
            return Outcome.Ok(Val.List(rows));
        });
    }

    private Outcome Invoke(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 3) || args[1].AsNumberToInt() is not { } method || !IsListOrNil(args[2]))
        {
            return Outcome.Fail(Status.BadArgs, "invoke takes a handle, a method id and a list");
        }
        return WithTarget(args[0], t => t switch
        {
            // a lane's method first (Dispatcher.Lanes.cs): the core's InvokeOnObject answers every method it does not know with bad_args
            LevelObject lo => InvokeLaneMethod(lo, method, args[2].Items) ?? InvokeOnObject(lo, method, args[2].Items),
            PlayerState ps => InvokeStartMethod(ps, method, args[2].Items)
                ?? InvokeLaneMethod(ps, method, args[2].Items)
                ?? Outcome.Fail(Status.BadArgs, "unknown method"),
            _ => InvokeLaneMethod(t, method, args[2].Items) ?? Outcome.Fail(Status.BadArgs, "unknown method"),
        });
    }

    // ------------------------------------------------------------------ the manifest's functions

    private Outcome FindArchetype(IReadOnlyList<Val> args, bool byRealName)
    {
        if (args.Count != 1 || !(args[0].IsNil || args[0].Kind == ValKind.String))
        {
            return Outcome.Fail(Status.BadArgs, "the function takes one string");
        }
        string name = args[0].AsString;
        Archetype? at = byRealName ? world!.FindArchetypeByObjectName(name) : world!.FindArchetype(name);
        if (at == null)
        {
            return Outcome.Ok(Val.Nil);
        }
        return Outcome.Ok(Val.Handle(handles.HandleOf(at)));
    }

    /// <summary>A number as the call sent it: a float as it is (NaN and the infinities too); null for any other kind but an int and nil, which is 0.</summary>
    private static double? RawNumber(Val v) => v.Kind == ValKind.Float ? v.AsFloat : AsNumber(v);

    /// <summary>The number a value stands for as a script number: an int or a float, nil as 0; null for any other kind.</summary>
    private static double? AsNumber(Val v) => v.Kind switch
    {
        ValKind.Nil => 0,
        ValKind.Int => v.AsInt,
        ValKind.Float => double.IsFinite(v.AsFloat) ? v.AsFloat : 0,
        _ => null,
    };
}
