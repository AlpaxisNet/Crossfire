// Not ported from a Crossfire file: gmaps (docs/protocol.md section 7.1, protocol 1.1; docs/mud-phase3.md P9). The operation
// <c>gmap.layout</c> (op 17), the resolving of a gmap position to a segment and a position in it, and the queries of the extension that
// name a gmap: <c>level.near</c> fanned out over the segments and merged, <c>npc.pos</c> and <c>player.pos</c> translated into the
// gmap's tiles, <c>level.blocked</c> and <c>level.loaded</c> asked of the segment of a position. Every level name the extension sends
// or keeps is a segment's; a name without a layout is a level name, and below 1.1 nothing here applies (no layout is ever kept).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>A place of a request: the level that holds it (a segment's key on a gmap) and the position in that level's tiles.</summary>
internal readonly record struct Placement(string Level, double X, double Y, GmapLayout? Gmap);

internal sealed partial class Dispatcher
{
    /// <summary>The most rows a <c>level.near</c> answer has (protocol section 12).</summary>
    private const int NearLimit = 1000;

    // the layouts the host told, by the gmap's level key; a new connection (a new dispatcher) starts with none
    private readonly Dictionary<string, GmapLayout> gmaps = new(StringComparer.Ordinal);

    /// <summary>Asks the host a query (the runner sets it; a test sets a stand-in). Null: every query is <c>unavailable</c>. Worker only.</summary>
    internal HostQuery? QuerySource { get; set; }

    private QueryAnswer Ask(string name, params Val[] args) =>
        QuerySource?.Invoke(name, args) ?? new QueryAnswer(Status.Unavailable, Val.Str("the host cannot be asked"));

    private static Outcome Failed(QueryAnswer answer) => Outcome.Fail(answer.Status, answer.Result.Kind == ValKind.String ? answer.Result.AsString : "the query failed");

    /// <summary>The layout of a gmap (a level name, compared without case), or null when the name is a level's.</summary>
    internal GmapLayout? GmapOf(string? name) => name != null && gmaps.TryGetValue(LevelKey(name), out GmapLayout? layout) ? layout : null;

    // ------------------------------------------------------------------ gmap.layout

    private Outcome GmapLayoutOp(IReadOnlyList<Val> args)
    {
        if (args.Count != 5 || args[0].Kind != ValKind.String || args[0].AsString.Length == 0)
        {
            return Outcome.Fail(Status.BadArgs, "gmap.layout takes a gmap's name, width, height, a list of segments and size");
        }
        string name = LevelKey(args[0].AsString);
        GmapLayout? layout = ReadLayout(name, args, out string why);
        if (layout == null)
        {
            // a layout that is refused leaves none: the name is a level name again (1.0 behaviour)
            gmaps.Remove(name);
            return Outcome.Fail(Status.BadArgs, why);
        }
        gmaps[name] = layout; // replaces the one the gmap had; nothing already placed moves
        return Outcome.Ok(Val.Nil);
    }

    private static GmapLayout? ReadLayout(string name, IReadOnlyList<Val> args, out string why)
    {
        why = "";
        long? width = args[1].AsNumberToInt();
        long? height = args[2].AsNumberToInt();
        long? size = args[4].AsNumberToInt();
        if (width is not { } w || height is not { } h || size is not { } s || args[3].Kind != ValKind.List)
        {
            why = "gmap.layout: width, height and size are numbers and the segments a list";
            return null;
        }
        if (w < 1 || h < 1 || w > GmapLayout.Limit || h > GmapLayout.Limit || w * h > GmapLayout.Limit || s < 1 || s > int.MaxValue)
        {
            why = "gmap.layout: width and height are 1 to 4096 with at most 4096 segments, size at least 1";
            return null;
        }
        IReadOnlyList<Val> items = args[3].Items;
        if (items.Count != w * h)
        {
            why = "gmap.layout: the list has " + items.Count + " segments, width * height is " + w * h;
            return null;
        }
        var keys = new List<string>(items.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Val item in items)
        {
            if (item.Kind != ValKind.String)
            {
                why = "gmap.layout: a segment is not a string";
                return null;
            }
            string key = LevelKey(item.AsString);
            if (key.Length > 0 && !seen.Add(key))
            {
                why = "gmap.layout: the segment " + item.AsString + " is in the grid twice";
                return null;
            }
            keys.Add(key);
        }
        return new GmapLayout(name, (int)w, (int)h, keys, (int)s);
    }

    // ------------------------------------------------------------------ resolving

    /// <summary>
    /// The level and the position in it for a place of a request. The hook of everything that names a level together with a position
    /// (the makers of objects, the drops of wave 5): <paramref name="level"/> is the <c>ctx.level</c> of the script. For a gmap that has
    /// a layout the result is the segment's key and the position in the segment (section 7.1), and null when the position is in no
    /// segment (the operation then fails as the rules of a place with no level say, and sends nothing). Any other name is a level's:
    /// its key and the position as given.
    /// </summary>
    internal Placement? ResolvePlace(string level, double x, double y)
    {
        string key = LevelKey(level);
        if (!gmaps.TryGetValue(key, out GmapLayout? layout))
        {
            return new Placement(key, x, y, null);
        }
        return layout.TryResolve(x, y, out SegmentPlace place) ? new Placement(place.Level, place.X, place.Y, layout) : null;
    }

    /// <summary>A position in the tiles of a segment of the layout, in the gmap's tiles; null when the segment is not in the layout.</summary>
    private static (double X, double Y)? ToGmapTiles(GmapLayout layout, string segmentKey, double x, double y) =>
        layout.TryFindSegment(LevelKey(segmentKey), out int column, out int row) ? layout.ToGmap(column, row, x, y) : null;

    // ------------------------------------------------------------------ queries

    /// <summary>
    /// <c>level.near</c> for a script on <paramref name="level"/>: the NPCs showing objects within <paramref name="r"/> tiles of (x, y),
    /// nearest first, at most 1,000, as rows <c>[handle, x, y]</c>. For a gmap with a layout the extension asks every segment the circle
    /// touches and every other loaded segment of the gmap that holds an object, with the centre in each segment's tiles, and merges the
    /// rows translated into the gmap's tiles; otherwise it asks the host for that level and passes the rows on.
    /// </summary>
    internal Outcome NearObjects(string level, double x, double y, double r)
    {
        if (GmapOf(level) is not { } layout)
        {
            return AskNear(level, x, y, r);
        }
        if (!double.IsFinite(x) || !double.IsFinite(y) || double.IsNaN(r))
        {
            return Outcome.Fail(Status.BadArgs, "level.near takes finite numbers");
        }
        if (r < 0)
        {
            return Outcome.Ok(Val.List());
        }
        var asked = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<(double Distance, ulong Handle, double X, double Y)>();
        var seen = new HashSet<ulong>();
        foreach ((string segment, int column, int row) in layout.Cells())
        {
            bool touched = CircleTouches(x, y, r, column * (double)layout.Size, row * (double)layout.Size, layout.Size);
            bool holds = !touched && levels.TryGetValue(segment, out Level? held) && held.Objects.Count > 0;
            if (!touched && !holds)
            {
                continue;
            }
            asked.Add(segment);
            (double cx, double cy) = (x - column * (double)layout.Size, y - row * (double)layout.Size);
            QueryAnswer answer = Ask("level.near", Val.Str(segment), Val.Float(cx), Val.Float(cy), Val.Float(r));
            if (!answer.IsOk)
            {
                return Failed(answer);
            }
            if (answer.Result.Kind != ValKind.List)
            {
                return Outcome.Fail(Status.Internal, "level.near did not answer a list");
            }
            foreach (Val row2 in answer.Result.Items)
            {
                if (!ReadNearRow(row2, out ulong handle, out double rx, out double ry))
                {
                    return Outcome.Fail(Status.Internal, "level.near answered a row that is not [handle, x, y]");
                }
                if (!seen.Add(handle))
                {
                    continue;
                }
                (double gx, double gy) = layout.ToGmap(column, row, rx, ry);
                rows.Add((Math.Sqrt((gx - x) * (gx - x) + (gy - y) * (gy - y)), handle, gx, gy));
            }
        }
        // nearest first; equal distances stay in the order of the cells (a stable sort)
        List<Val> merged = rows.OrderBy(t => t.Distance).Take(NearLimit)
            .Select(t => Val.List(Val.Handle(t.Handle), Val.Float(t.X), Val.Float(t.Y))).ToList();
        return Outcome.Ok(Val.List(merged));
    }

    private Outcome AskNear(string level, double x, double y, double r)
    {
        QueryAnswer answer = Ask("level.near", Val.Str(level), Val.Float(x), Val.Float(y), Val.Float(r));
        return answer.IsOk ? Outcome.Ok(answer.Result) : Failed(answer);
    }

    private static bool ReadNearRow(Val row, out ulong handle, out double x, out double y)
    {
        handle = 0;
        x = y = 0;
        if (row.Kind != ValKind.List || row.Items.Count != 3 || row.Items[0].Kind != ValKind.Handle
            || AsNumber(row.Items[1]) is not { } rx || AsNumber(row.Items[2]) is not { } ry)
        {
            return false;
        }
        handle = row.Items[0].AsHandle;
        x = rx;
        y = ry;
        return true;
    }

    /// <summary>True when the circle of radius <paramref name="r"/> at (x, y) touches the square of <paramref name="size"/> tiles at (left, top), its edge included.</summary>
    private static bool CircleTouches(double x, double y, double r, double left, double top, double size)
    {
        double dx = Math.Max(Math.Max(left - x, 0), x - (left + size));
        double dy = Math.Max(Math.Max(top - y, 0), y - (top + size));
        return dx * dx + dy * dy <= r * r;
    }

    /// <summary>
    /// <c>npc.pos</c> for a script on <paramref name="scriptLevel"/>: the rows <c>[x, y, level]</c> of the host, in order. For a script
    /// on a gmap with a layout, a row that names a segment of it is given in the gmap's tiles and names the gmap (the level the position
    /// counts in); any other row is as the host sent it.
    /// </summary>
    internal Outcome NpcPositions(string? scriptLevel, IReadOnlyList<Val> targets)
    {
        QueryAnswer answer = Ask("npc.pos", Val.List(targets));
        if (!answer.IsOk)
        {
            return Failed(answer);
        }
        if (GmapOf(scriptLevel) is not { } layout || answer.Result.Kind != ValKind.List)
        {
            return Outcome.Ok(answer.Result);
        }
        var rows = new List<Val>(answer.Result.Items.Count);
        foreach (Val row in answer.Result.Items)
        {
            rows.Add(row.Kind == ValKind.List && row.Items.Count == 3 && row.Items[2].Kind == ValKind.String
                && AsNumber(row.Items[0]) is { } x && AsNumber(row.Items[1]) is { } y && ToGmapTiles(layout, row.Items[2].AsString, x, y) is { } g
                ? Val.List(Val.Float(g.X), Val.Float(g.Y), Val.Str(layout.Name))
                : row);
        }
        return Outcome.Ok(Val.List(rows));
    }

    /// <summary>
    /// <c>player.pos</c> (1.1) for a script on <paramref name="scriptLevel"/>: <c>[level, x, y]</c> or nil. For a script on a gmap with a
    /// layout whose segment the player is in, the gmap's name and the position in the gmap's tiles; otherwise as the host answered.
    /// </summary>
    internal Outcome PlayerPosition(string? scriptLevel, string account)
    {
        QueryAnswer answer = Ask("player.pos", Val.Str(account));
        if (!answer.IsOk)
        {
            return Failed(answer);
        }
        Val v = answer.Result;
        if (GmapOf(scriptLevel) is { } layout && v.Kind == ValKind.List && v.Items.Count == 3 && v.Items[0].Kind == ValKind.String
            && AsNumber(v.Items[1]) is { } x && AsNumber(v.Items[2]) is { } y && ToGmapTiles(layout, v.Items[0].AsString, x, y) is { } g)
        {
            return Outcome.Ok(Val.List(Val.Str(layout.Name), Val.Float(g.X), Val.Float(g.Y)));
        }
        return Outcome.Ok(v);
    }

    /// <summary>
    /// <c>level.blocked</c> of a position of <paramref name="level"/>: the one segment of the position is asked, in its own tiles. A
    /// position in no segment is blocked, as the host answers for a level that is not loaded.
    /// </summary>
    internal Outcome LevelBlocked(string level, double x, double y)
    {
        if (ResolvePlace(level, x, y) is not { } place)
        {
            return Outcome.Ok(Val.True);
        }
        QueryAnswer answer = Ask("level.blocked", Val.Str(place.Level), Val.Float(place.X), Val.Float(place.Y));
        return answer.IsOk ? Outcome.Ok(answer.Result) : Failed(answer);
    }

    /// <summary><c>level.loaded</c> of the segment of a position of <paramref name="level"/> (a gmap's name or a level's); false for a position in no segment.</summary>
    internal Outcome LevelLoadedAt(string level, double x, double y)
    {
        if (ResolvePlace(level, x, y) is not { } place)
        {
            return Outcome.Ok(Val.False);
        }
        return LevelLoaded(place.Level);
    }

    /// <summary><c>level.loaded</c> of a segment or level name.</summary>
    internal Outcome LevelLoaded(string level)
    {
        QueryAnswer answer = Ask("level.loaded", Val.Str(LevelKey(level)));
        return answer.IsOk ? Outcome.Ok(answer.Result) : Failed(answer);
    }
}
