// Not ported from a Crossfire file: tests of gmaps (docs/protocol.md section 7.1, protocol 1.1): the operation gmap.layout, the resolving
// of a gmap position to a segment, the objects made on a gmap and filed under their segment, and the queries (level.near fanned out and
// merged, npc.pos and player.pos translated, level.blocked, level.loaded) over an invented gmap and invented levels.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>The invented gmap "world": three columns by two rows of 64 tiles, one cell without a level.</summary>
internal static class Atlas
{
    //            column 0   column 1   column 2
    // row 0:     ga.nw      GB.nw      (none)
    // row 1:     gc.nw      gd.nw      ge.nw
    public static readonly string[] Cells = { "ga.nw", "GB.nw", "", "gc.nw", "gd.nw", "ge.nw" };

    public static Val[] Layout(string name = "world", long width = 3, long height = 2, IEnumerable<string>? segments = null, long size = 64) =>
        new[] { S(name), Val.Int(width), Val.Int(height), Val.List((segments ?? Cells).Select(S)), Val.Int(size) };

    public static ExtensionOptions Options(TimeSpan? queryTimeout = null) => new()
    {
        Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        QueryTimeout = queryTimeout ?? TimeSpan.FromSeconds(10),
    };

    public static DirectExt Start(long protoMinor = 1) => new(ObjectWorld.Make(), Options(), 1, true, 16_777_216, protoMinor);

    public static DirectExt WithLayout(long protoMinor = 1)
    {
        DirectExt ext = Start(protoMinor);
        Assert.Equal(0UL, ext.Call(17, Layout()).Status);
        return ext;
    }
}

/// <summary>A stand-in for the host's answers to the extension's queries: records them, answers by a function.</summary>
internal sealed class FakeHost
{
    public List<(string Name, IReadOnlyList<Val> Args)> Asked { get; } = new();

    public Func<string, IReadOnlyList<Val>, QueryAnswer> Answer { get; set; } = (_, _) => new QueryAnswer(Status.Ok, Val.List());

    public QueryAnswer Ask(string name, IReadOnlyList<Val> args)
    {
        Asked.Add((name, args));
        return Answer(name, args);
    }

    public static Val Row(ulong handle, double x, double y) => Val.List(Val.Handle(handle), Val.Float(x), Val.Float(y));
}

public sealed class GmapLayoutTests
{
    private static Val[] Layout(string name = "world", long width = 3, long height = 2, IEnumerable<string>? segments = null, long size = 64) =>
        Atlas.Layout(name, width, height, segments, size);

    [Fact]
    public void Layout_Given_a_1_1_link_When_the_layout_is_sent_Then_it_answers_nil_without_effects_or_events()
    {
        // Arrange
        using DirectExt ext = Atlas.Start();

        // Act
        ReplyFrame r = ext.Call(17, Layout());

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        Assert.Empty(r.Events);
    }

    [Fact]
    public void Layout_Given_a_1_0_link_When_the_layout_is_sent_Then_the_operation_is_unknown_and_a_gmap_name_stays_a_level_name()
    {
        // Arrange
        using DirectExt ext = Atlas.Start(protoMinor: 0);

        // Act
        ReplyFrame layout = ext.Call(17, Layout());
        ReplyFrame made = ext.Call(Manifest.OpMudAddObject, new[] { N(70), N(10), S("banana") }, level: "world");

        // Assert
        Assert.Equal((ulong)Status.UnknownOp, layout.Status);
        Val place = Assert.Single(Effects(made, "npc.place"));
        Assert.Equal("world", place.Get("level")!.AsString);
        Assert.Equal(70.0, place.Get("x")!.AsFloat);
    }

    [Fact]
    public void Layout_Given_requests_that_break_a_rule_When_sent_Then_each_is_bad_args()
    {
        // Arrange
        using DirectExt ext = Atlas.Start();
        string[] twoByTwo = { "a", "b", "c", "d" };
        var cases = new (string Why, Val[] Args)[]
        {
            ("no arguments", Array.Empty<Val>()),
            ("four arguments", Layout().Take(4).ToArray()),
            ("a nil name", new[] { Val.Nil }.Concat(Layout().Skip(1)).ToArray()),
            ("an empty name", Layout("")),
            ("one segment too few", Layout(segments: Atlas.Cells.Take(5))),
            ("one segment too many", Layout(segments: Atlas.Cells.Append("x"))),
            ("width 0", Layout(width: 0, height: 2, segments: Array.Empty<string>())),
            ("height 0", Layout(width: 3, height: 0, segments: Array.Empty<string>())),
            ("negative width", Layout(width: -1, height: 2, segments: Array.Empty<string>())),
            ("width over 4096", Layout(width: 4097, height: 1, segments: Enumerable.Range(0, 4097).Select(i => "s" + i))),
            ("height over 4096", Layout(width: 1, height: 4097, segments: Enumerable.Range(0, 4097).Select(i => "s" + i))),
            ("65 by 64 segments", Layout(width: 65, height: 64, segments: Enumerable.Range(0, 65 * 64).Select(i => "s" + i))),
            ("size 0", Layout(size: 0)),
            ("a nil size", Layout().Take(4).Append(Val.Nil).ToArray()),
            ("a segment that is not a string", new[] { S("world"), Val.Int(2), Val.Int(2), Val.List(S("a"), S("b"), S("c"), Val.Int(4)), Val.Int(64) }),
            ("segments that are not a list", new[] { S("world"), Val.Int(2), Val.Int(2), S("abcd"), Val.Int(64) }),
            ("a repeated segment", Layout(width: 2, height: 2, segments: new[] { "a", "b", "c", "a" })),
            ("a repeated segment in another case", Layout(width: 2, height: 2, segments: new[] { "a", "b", "c", "A" })),
        };
        Assert.Equal(0UL, ext.Call(17, Layout(width: 2, height: 2, segments: twoByTwo)).Status); // so that a failure also shows it keeps none

        foreach ((string why, Val[] args) in cases)
        {
            // Act
            ReplyFrame r = ext.Call(17, args);

            // Assert
            Assert.True(r.Status == (ulong)Status.BadArgs, why + ": status " + r.Status);
            Assert.Empty(r.Effects);
        }
    }

    [Fact]
    public void Layout_Given_the_widest_grids_allowed_When_sent_Then_they_are_taken()
    {
        // Arrange
        using DirectExt ext = Atlas.Start();

        // Act
        ReplyFrame wide = ext.Call(17, Layout("wide", 4096, 1, Enumerable.Range(0, 4096).Select(i => "w" + i)));
        ReplyFrame tall = ext.Call(17, Layout("tall", 1, 4096, Enumerable.Range(0, 4096).Select(i => "t" + i)));
        ReplyFrame square = ext.Call(17, Layout("square", 64, 64, Enumerable.Range(0, 4096).Select(i => "q" + i)));
        ReplyFrame empties = ext.Call(17, Layout("empties", 2, 1, new[] { "", "" }, 1));

        // Assert
        Assert.All(new[] { wide, tall, square, empties }, r => Assert.Equal(0UL, r.Status));
    }

    [Fact]
    public void Layout_Given_a_good_layout_and_then_a_bad_one_When_a_maker_runs_Then_the_name_is_a_level_name_again()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();
        Assert.Equal((ulong)Status.BadArgs, ext.Call(17, Layout(segments: Atlas.Cells.Take(5))).Status);

        // Act
        ReplyFrame made = ext.Call(Manifest.OpMudAddObject, new[] { N(70), N(10), S("banana") }, level: "world");

        // Assert
        Val place = Assert.Single(Effects(made, "npc.place"));
        Assert.Equal("world", place.Get("level")!.AsString);
    }

    [Fact]
    public void Layout_Given_a_layout_sent_again_When_a_maker_runs_Then_the_new_layout_resolves_and_nothing_moves()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();
        ulong before = Ok(ext.Call(Manifest.OpMudAddObject, new[] { N(70), N(10), S("banana") }, level: "world"));
        Assert.Equal(0UL, ext.Call(17, Layout("WORLD", 2, 1, new[] { "left.nw", "right.nw" }, 32)).Status);

        // Act
        ReplyFrame made = ext.Call(Manifest.OpMudAddObject, new[] { N(40), N(10), S("banana") }, level: "world");

        // Assert
        Val place = Assert.Single(Effects(made, "npc.place"));
        Assert.Equal("right.nw", place.Get("level")!.AsString);
        Assert.Equal(8.0, place.Get("x")!.AsFloat);
        Assert.Equal(10.0, place.Get("y")!.AsFloat);
        Assert.Equal(0UL, ext.Call(9, new[] { H(before), Val.Int(Obj("mudid")) }).Status);
    }

    [Fact]
    public void Layout_Given_a_new_connection_When_a_maker_runs_Then_the_layout_of_the_old_one_is_forgotten()
    {
        // Arrange
        using (DirectExt first = Atlas.WithLayout())
        {
            Assert.Single(Effects(first.Call(Manifest.OpMudAddObject, new[] { N(70), N(10), S("banana") }, level: "world"), "npc.place"));
        }
        using DirectExt second = Atlas.Start();

        // Act
        ReplyFrame made = second.Call(Manifest.OpMudAddObject, new[] { N(70), N(10), S("banana") }, level: "world");

        // Assert
        Assert.Equal("world", Assert.Single(Effects(made, "npc.place")).Get("level")!.AsString);
    }
}

public sealed class GmapPlacingTests
{
    private static ReplyFrame Add(DirectExt ext, double x, double y, string level = "world") =>
        ext.Call(Manifest.OpMudAddObject, new[] { N(x), N(y), S("banana") }, level: level);

    [Theory]
    [InlineData(10.0, 10.0, "ga.nw", 10.0, 10.0)]
    [InlineData(70.5, 10.25, "gb.nw", 6.5, 10.25)]
    [InlineData(64.0, 0.0, "gb.nw", 0.0, 0.0)]
    [InlineData(63.9921875, 63.9921875, "ga.nw", 63.9921875, 63.9921875)]
    [InlineData(0.0, 64.0, "gc.nw", 0.0, 0.0)]
    [InlineData(100.0, 100.0, "gd.nw", 36.0, 36.0)]
    [InlineData(191.5, 127.5, "ge.nw", 63.5, 63.5)]
    public void Maker_Given_a_script_on_a_gmap_When_it_adds_an_object_Then_the_object_is_placed_in_the_segment_with_the_local_position(
        double x, double y, string segment, double localX, double localY)
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();

        // Act
        ReplyFrame r = Add(ext, x, y);

        // Assert
        Val place = Assert.Single(Effects(r, "npc.place"));
        Assert.Equal(segment, place.Get("level")!.AsString);
        Assert.Equal(localX, place.Get("x")!.AsFloat);
        Assert.Equal(localY, place.Get("y")!.AsFloat);
        Assert.Equal(place.Get("handle")!.AsHandle, r.Result.AsHandle);
    }

    [Fact]
    public void Maker_Given_the_gmap_name_in_another_case_When_it_adds_an_object_Then_it_is_resolved()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();

        // Act
        ReplyFrame r = Add(ext, 70, 10, "WoRlD");

        // Assert
        Assert.Equal("gb.nw", Assert.Single(Effects(r, "npc.place")).Get("level")!.AsString);
    }

    [Theory]
    [InlineData(-0.5, 10.0)]
    [InlineData(10.0, -0.001)]
    [InlineData(192.0, 10.0)]
    [InlineData(10.0, 128.0)]
    [InlineData(130.0, 10.0)] // the cell without a level
    [InlineData(double.NaN, 10.0)]
    [InlineData(10.0, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, 10.0)]
    public void Maker_Given_a_position_in_no_segment_When_it_adds_an_object_Then_nothing_is_made_or_sent(double x, double y)
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();

        // Act
        ReplyFrame r = Add(ext, x, y);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        Assert.Equal(0L, ext.Call(15).Result.Get("levels")!.AsInt);
        Assert.Contains(ext.Logs, l => l.Text.Contains("in no segment"));
    }

    [Fact]
    public void Maker_Given_a_special_object_on_a_gmap_When_it_is_added_Then_it_is_placed_in_the_segment()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddSpecialObject, new[] { N(70), N(80), N(5), S("banana some words"), S("a message") }, level: "world");
        ReplyFrame none = ext.Call(Manifest.OpMudAddSpecialObject, new[] { N(130), N(10), N(5), S("banana"), S("") }, level: "world");

        // Assert
        Val place = Assert.Single(Effects(r, "npc.place"));
        Assert.Equal("gd.nw", place.Get("level")!.AsString);
        Assert.Equal(6.0, place.Get("x")!.AsFloat);
        Assert.Equal(16.0, place.Get("y")!.AsFloat);
        Assert.True(none.Result.IsNil);
        Assert.Empty(none.Effects);
    }

    [Fact]
    public void Maker_Given_a_script_on_a_segment_or_a_level_When_it_adds_an_object_Then_the_position_is_not_translated()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();

        // Act
        ReplyFrame onSegment = Add(ext, 70, 10, "ga.nw");
        ReplyFrame onLevel = Add(ext, 500, 10, "hall.nw");

        // Assert
        Val a = Assert.Single(Effects(onSegment, "npc.place"));
        Assert.Equal("ga.nw", a.Get("level")!.AsString);
        Assert.Equal(70.0, a.Get("x")!.AsFloat);
        Val b = Assert.Single(Effects(onLevel, "npc.place"));
        Assert.Equal("hall.nw", b.Get("level")!.AsString);
        Assert.Equal(500.0, b.Get("x")!.AsFloat);
    }

    [Fact]
    public void Clone_Given_a_script_on_a_gmap_When_an_object_is_cloned_to_a_position_Then_the_clone_is_in_that_positions_segment()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();
        ulong original = Ok(Add(ext, 10, 10));
        int clone = TMudObjectMembers.MethodCloneMudObject;

        // Act
        ReplyFrame r = ext.Call(13, new[] { H(original), Val.Int(clone), Val.List(N(70), N(100)) }, level: "world");
        ReplyFrame none = ext.Call(13, new[] { H(original), Val.Int(clone), Val.List(N(130), N(10)) }, level: "world");
        ReplyFrame onSegment = ext.Call(13, new[] { H(original), Val.Int(clone), Val.List(N(3), N(4)) }, level: "ga.nw");

        // Assert
        Val place = Assert.Single(Effects(r, "npc.place"));
        Assert.Equal("gd.nw", place.Get("level")!.AsString);
        Assert.Equal(6.0, place.Get("x")!.AsFloat);
        Assert.Equal(36.0, place.Get("y")!.AsFloat);
        Assert.True(none.Result.IsNil);
        Assert.Empty(none.Effects);
        Val stays = Assert.Single(Effects(onSegment, "npc.place"));
        Assert.Equal("ga.nw", stays.Get("level")!.AsString);
        Assert.Equal(3.0, stays.Get("x")!.AsFloat);
    }

    [Fact]
    public void Clone_Given_a_full_target_segment_When_an_object_is_cloned_to_it_Then_the_count_of_that_segment_decides()
    {
        // Arrange
        var options = new ExtensionOptions
        {
            Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
            Gk = new GkSettings { MaxObjectsPerLevel = 1 },
        };
        using var ext = new DirectExt(ObjectWorld.Make(), options, protoMinor: 1);
        Assert.Equal(0UL, ext.Call(17, Atlas.Layout()).Status);
        ulong original = Ok(Add(ext, 10, 10)); // ga.nw is full now
        int clone = TMudObjectMembers.MethodCloneMudObject;

        // Act
        ReplyFrame other = ext.Call(13, new[] { H(original), Val.Int(clone), Val.List(N(70), N(10)) }, level: "world");
        ReplyFrame full = ext.Call(13, new[] { H(original), Val.Int(clone), Val.List(N(11), N(10)) }, level: "world");

        // Assert
        Assert.Equal("gb.nw", Assert.Single(Effects(other, "npc.place")).Get("level")!.AsString);
        Assert.True(full.Result.IsNil);
        Assert.Empty(full.Effects);
    }

    [Fact]
    public void Segment_Given_an_object_made_on_a_gmap_When_the_data_is_flushed_Then_the_file_is_the_segments_and_not_the_gmaps()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();
        Add(ext, 70.5, 10.25);

        // Act
        string text = Flushed(ext, "levels/gb.nw.map", t => t.Contains("arch banana"));

        // Assert
        Assert.Contains("x 6.5", text);
        Assert.Contains("y 10.25", text);
        Assert.False(File.Exists(ObjectWorld.Data(ext.World, "levels/world.map")));
        Assert.False(File.Exists(ObjectWorld.Data(ext.World, "levels/ga.nw.map")));
    }

    [Fact]
    public void Segment_Given_an_object_made_on_a_gmap_When_its_segment_is_unloaded_and_loaded_Then_it_goes_and_comes_back_with_the_segment_only()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();
        ulong inB = Ok(Add(ext, 70.5, 10.25));
        ulong inA = Ok(Add(ext, 5, 5));

        // Act
        Assert.Equal(0UL, ext.Call(8, new[] { S("gb.nw") }).Status);
        ReplyFrame deadRead = ext.Call(9, new[] { H(inB), Val.Int(Obj("mudid")) });
        ReplyFrame liveRead = ext.Call(9, new[] { H(inA), Val.Int(Obj("mudid")) });
        (List<Val> places, _) = LoadAll(ext, "gb.nw");
        (List<Val> none, _) = LoadAll(ext, "world");

        // Assert
        Assert.Equal((ulong)Status.BadHandle, deadRead.Status);
        Assert.Equal(0UL, liveRead.Status);
        Val place = Assert.Single(places);
        Assert.Equal("gb.nw", place.Get("level")!.AsString);
        Assert.Equal(6.5, place.Get("x")!.AsFloat);
        Assert.Equal(10.25, place.Get("y")!.AsFloat);
        Assert.Empty(none);
    }

    [Fact]
    public void Segment_Given_an_npc_that_walked_out_of_its_segment_When_the_blobs_arrive_Then_the_position_is_kept_as_sent()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();
        ulong h = Ok(Add(ext, 70.5, 10.25));

        // Act
        Assert.Equal(0UL, ext.Call(16, new[] { Val.List(Val.List(H(h), N(80), N(-3), S("state"))) }).Status);
        Assert.Equal(0UL, ext.Call(8, new[] { S("gb.nw") }).Status);
        (List<Val> places, _) = LoadAll(ext, "gb.nw");

        // Assert
        Val place = Assert.Single(places);
        Assert.Equal("gb.nw", place.Get("level")!.AsString);
        Assert.Equal(80.0, place.Get("x")!.AsFloat);
        Assert.Equal(-3.0, place.Get("y")!.AsFloat);
        Assert.Equal("state", place.Get("blob")!.AsString);
    }

    [Fact]
    public void ResolvePlace_Given_a_gmap_a_level_and_a_position_outside_When_resolved_Then_each_answers_as_section_7_1_says()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();
        Dispatcher d = ext.Dispatcher;

        // Act
        Placement? onGmap = d.ResolvePlace("World", 130, 70);
        Placement? onLevel = d.ResolvePlace("Hall.NW", 130, 70);
        Placement? outside = d.ResolvePlace("world", 130, 10);

        // Assert
        Assert.Equal(new Placement("ge.nw", 2, 6, d.GmapOf("world")), onGmap);
        Assert.Equal(new Placement("hall.nw", 130, 70, null), onLevel);
        Assert.Null(outside);
    }
}

public sealed class GmapQueryTests
{
    private static (DirectExt Ext, FakeHost Host) Start(long protoMinor = 1)
    {
        DirectExt ext = Atlas.WithLayout(protoMinor);
        var host = new FakeHost();
        ext.Dispatcher.QuerySource = host.Ask;
        return (ext, host);
    }

    private static List<(ulong Handle, double X, double Y)> Rows(Outcome o) =>
        o.Result.Items.Select(r => (r.Items[0].AsHandle, r.Items[1].AsFloat, r.Items[2].AsFloat)).ToList();

    [Fact]
    public void Near_Given_a_circle_that_touches_two_segments_When_asked_Then_each_is_asked_with_its_own_centre_and_the_rows_merge_nearest_first_in_gmap_tiles()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        host.Answer = (_, a) => new QueryAnswer(Status.Ok, a[0].AsString == "ga.nw"
            ? Val.List(FakeHost.Row(1, 50, 10), FakeHost.Row(2, 58, 10))
            : Val.List(FakeHost.Row(3, 0.5, 10), FakeHost.Row(4, 8, 10)));

        // Act
        Outcome o = ext.Dispatcher.NearObjects("world", 60, 10, 10);

        // Assert
        Assert.Equal(new[] { "ga.nw", "gb.nw" }, host.Asked.Select(q => q.Args[0].AsString).ToArray());
        Assert.All(host.Asked, q => Assert.Equal("level.near", q.Name));
        Assert.Equal(new[] { 60.0, -4.0 }, host.Asked.Select(q => q.Args[1].AsFloat).ToArray());
        Assert.All(host.Asked, q => Assert.Equal(10.0, q.Args[2].AsFloat));
        Assert.All(host.Asked, q => Assert.Equal(10.0, q.Args[3].AsFloat));
        Assert.Equal(0, o.Status);
        Assert.Equal(new[] { 2UL, 3UL, 1UL, 4UL }, Rows(o).Select(r => r.Handle).ToArray());
        Assert.Equal(new[] { 58.0, 64.5, 50.0, 72.0 }, Rows(o).Select(r => r.X).ToArray());
    }

    [Fact]
    public void Near_Given_a_circle_inside_one_segment_When_asked_Then_only_that_segment_is_asked()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;

        // Act
        Outcome o = ext.Dispatcher.NearObjects("world", 100, 100, 20);

        // Assert
        Assert.Equal(new[] { "gd.nw" }, host.Asked.Select(q => q.Args[0].AsString).ToArray());
        Assert.Equal(36.0, host.Asked[0].Args[1].AsFloat);
        Assert.Equal(36.0, host.Asked[0].Args[2].AsFloat);
        Assert.Empty(o.Result.Items);
    }

    [Theory]
    [InlineData(4.0, true)] // the circle's edge reaches x = 64 exactly: it touches
    [InlineData(3.9, false)]
    public void Near_Given_a_circle_whose_edge_reaches_the_next_segment_When_asked_Then_touching_counts(double radius, bool touchesNext)
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;

        // Act
        ext.Dispatcher.NearObjects("world", 60, 30, radius);

        // Assert
        Assert.Equal(touchesNext, host.Asked.Any(q => q.Args[0].AsString == "gb.nw"));
    }

    [Fact]
    public void Near_Given_a_circle_that_reaches_the_cell_without_a_level_and_past_the_grid_When_asked_Then_only_cells_with_levels_inside_the_grid_are_asked()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;

        // Act
        ext.Dispatcher.NearObjects("world", 120, 10, 1000);
        List<string> big = host.Asked.Select(q => q.Args[0].AsString).ToList();
        host.Asked.Clear();
        ext.Dispatcher.NearObjects("world", -30, -30, 10);

        // Assert
        Assert.Equal(new[] { "ga.nw", "gb.nw", "gc.nw", "gd.nw", "ge.nw" }, big);
        Assert.Empty(host.Asked);
    }

    [Fact]
    public void Near_Given_another_loaded_segment_that_holds_an_object_When_asked_Then_it_is_asked_too_and_an_empty_one_is_not()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        ext.Call(Manifest.OpMudAddObject, new[] { N(100), N(100), S("banana") }, level: "world"); // gd.nw holds an object
        host.Answer = (_, a) => new QueryAnswer(Status.Ok, a[0].AsString == "gd.nw" ? Val.List(FakeHost.Row(9, 70, -50)) : Val.List());

        // Act
        Outcome o = ext.Dispatcher.NearObjects("world", 10, 10, 5);

        // Assert
        Assert.Equal(new[] { "ga.nw", "gd.nw" }, host.Asked.Select(q => q.Args[0].AsString).ToArray());
        Assert.Equal(-54.0, host.Asked[1].Args[1].AsFloat);
        Assert.Equal(-54.0, host.Asked[1].Args[2].AsFloat);
        Assert.Equal(new[] { (9UL, 134.0, 14.0) }, Rows(o));
    }

    [Fact]
    public void Near_Given_more_than_a_thousand_rows_and_a_handle_twice_When_merged_Then_the_nearest_thousand_come_back_once_each()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        host.Answer = (_, a) => new QueryAnswer(Status.Ok, Val.List(Enumerable.Range(0, 600).Select(i =>
            a[0].AsString == "ga.nw" ? FakeHost.Row((ulong)(i + 1), 62, 10 + i * 0.01) : FakeHost.Row((ulong)(i + 1) + (i < 100 ? 0UL : 1000UL), 2, 10 + i * 0.01))));

        // Act
        Outcome o = ext.Dispatcher.NearObjects("world", 63, 10, 40);

        // Assert
        List<(ulong Handle, double X, double Y)> rows = Rows(o);
        Assert.Equal(1000, rows.Count);
        Assert.Equal(rows.Count, rows.Select(r => r.Handle).Distinct().Count());
        for (int i = 1; i < rows.Count; i++)
        {
            double a = Math.Pow(rows[i - 1].X - 63, 2) + Math.Pow(rows[i - 1].Y - 10, 2);
            double b = Math.Pow(rows[i].X - 63, 2) + Math.Pow(rows[i].Y - 10, 2);
            Assert.True(a <= b, "row " + i + " is nearer than the one before");
        }
    }

    [Fact]
    public void Near_Given_equal_distances_When_merged_Then_the_order_of_the_cells_decides()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        host.Answer = (_, a) => new QueryAnswer(Status.Ok, a[0].AsString == "ga.nw" ? Val.List(FakeHost.Row(1, 62, 10)) : Val.List(FakeHost.Row(2, 2, 10)));

        // Act
        Outcome o = ext.Dispatcher.NearObjects("world", 64, 10, 10);

        // Assert
        Assert.Equal(new[] { 1UL, 2UL }, Rows(o).Select(r => r.Handle).ToArray());
    }

    [Fact]
    public void Near_Given_a_failing_segment_a_nonsense_row_or_odd_numbers_When_asked_Then_the_status_is_the_hosts_or_bad_args_or_empty()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        host.Answer = (_, a) => a[0].AsString == "gb.nw" ? new QueryAnswer(Status.Unavailable, Val.Str("not now")) : new QueryAnswer(Status.Ok, Val.List());

        // Act
        Outcome failing = ext.Dispatcher.NearObjects("world", 60, 10, 10);
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, Val.List(Val.List(Val.Int(1))));
        Outcome nonsense = ext.Dispatcher.NearObjects("world", 10, 10, 1);
        host.Asked.Clear();
        Outcome negative = ext.Dispatcher.NearObjects("world", 10, 10, -1);
        Outcome nan = ext.Dispatcher.NearObjects("world", double.NaN, 10, 1);
        Outcome infinite = ext.Dispatcher.NearObjects("world", 10, double.PositiveInfinity, 1);

        // Assert
        Assert.Equal(Status.Unavailable, failing.Status);
        Assert.Equal("not now", failing.Result.AsString);
        Assert.Equal(Status.Internal, nonsense.Status);
        Assert.Equal(0, negative.Status);
        Assert.Empty(negative.Result.Items);
        Assert.Empty(host.Asked);
        Assert.Equal(Status.BadArgs, nan.Status);
        Assert.Equal(Status.BadArgs, infinite.Status);
    }

    [Fact]
    public void Near_Given_a_level_that_is_not_a_gmap_When_asked_Then_the_host_is_asked_once_with_the_arguments_and_its_rows_pass_on()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, Val.List(FakeHost.Row(7, 100, 200)));

        // Act
        Outcome onSegment = ext.Dispatcher.NearObjects("ga.nw", 100, 200, 3);
        Outcome onLevel = ext.Dispatcher.NearObjects("hall.nw", 5, 6, 3);

        // Assert
        Assert.Equal(new[] { "ga.nw", "hall.nw" }, host.Asked.Select(q => q.Args[0].AsString).ToArray());
        Assert.Equal(new[] { 100.0, 5.0 }, host.Asked.Select(q => q.Args[1].AsFloat).ToArray());
        Assert.Equal(new[] { (7UL, 100.0, 200.0) }, Rows(onSegment));
        Assert.Equal(2, host.Asked.Count);
        Assert.Equal(0, onLevel.Status);
    }

    [Fact]
    public void Query_Given_no_source_When_asked_Then_the_status_is_unavailable()
    {
        // Arrange
        using DirectExt ext = Atlas.WithLayout();

        // Act
        Outcome o = ext.Dispatcher.NearObjects("world", 10, 10, 5);

        // Assert
        Assert.Equal(Status.Unavailable, o.Status);
    }

    [Fact]
    public void NpcPos_Given_a_script_on_a_gmap_When_asked_Then_rows_of_its_segments_are_in_gmap_tiles_and_others_are_left()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, Val.List(
            Val.List(N(6.5), N(10), S("gb.nw")),
            Val.Nil,
            Val.List(N(1), N(2), S("GE.nw")),
            Val.List(N(1), N(2), S("hall.nw")),
            Val.List(N(-3), N(70), S("ga.nw"))));
        Val[] handles = { H(1), H(2), H(3), H(4), H(5) };

        // Act
        Outcome onGmap = ext.Dispatcher.NpcPositions("World", handles);
        Outcome onSegment = ext.Dispatcher.NpcPositions("ga.nw", handles);

        // Assert
        Assert.Equal("npc.pos", host.Asked[0].Name);
        Assert.Equal(5, host.Asked[0].Args[0].Items.Count);
        List<Val> rows = onGmap.Result.Items.ToList();
        Assert.Equal(new[] { 70.5, 10.0 }, new[] { rows[0].Items[0].AsFloat, rows[0].Items[1].AsFloat });
        Assert.Equal("world", rows[0].Items[2].AsString);
        Assert.True(rows[1].IsNil);
        Assert.Equal(new[] { 129.0, 66.0 }, new[] { rows[2].Items[0].AsFloat, rows[2].Items[1].AsFloat });
        Assert.Equal("world", rows[2].Items[2].AsString);
        Assert.Equal("hall.nw", rows[3].Items[2].AsString);
        Assert.Equal(1.0, rows[3].Items[0].AsFloat);
        Assert.Equal(new[] { -3.0, 70.0 }, new[] { rows[4].Items[0].AsFloat, rows[4].Items[1].AsFloat });
        Assert.Equal("gb.nw", onSegment.Result.Items[0].Items[2].AsString);
        Assert.Equal(6.5, onSegment.Result.Items[0].Items[0].AsFloat);
    }

    [Fact]
    public void PlayerPos_Given_a_script_on_a_gmap_When_asked_Then_the_position_is_in_gmap_tiles_else_as_the_host_said()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        Val answer = Val.List(S("gd.nw"), N(36), N(2.5));
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, answer);

        // Act
        Outcome onGmap = ext.Dispatcher.PlayerPosition("world", "someone");
        Outcome onSegment = ext.Dispatcher.PlayerPosition("gd.nw", "someone");
        answer = Val.List(S("hall.nw"), N(36), N(2.5));
        Outcome foreign = ext.Dispatcher.PlayerPosition("world", "someone");
        answer = Val.Nil;
        Outcome nobody = ext.Dispatcher.PlayerPosition("world", "someone");
        host.Answer = (_, _) => new QueryAnswer(Status.UnknownOp, Val.Str("1.0 host"));
        Outcome old = ext.Dispatcher.PlayerPosition("world", "someone");

        // Assert
        Assert.Equal("player.pos", host.Asked[0].Name);
        Assert.Equal("someone", host.Asked[0].Args[0].AsString);
        Assert.Equal(new[] { 100.0, 66.5 }, new[] { onGmap.Result.Items[1].AsFloat, onGmap.Result.Items[2].AsFloat });
        Assert.Equal("world", onGmap.Result.Items[0].AsString);
        Assert.Equal("gd.nw", onSegment.Result.Items[0].AsString);
        Assert.Equal(36.0, onSegment.Result.Items[1].AsFloat);
        Assert.Equal("hall.nw", foreign.Result.Items[0].AsString);
        Assert.Equal(36.0, foreign.Result.Items[1].AsFloat);
        Assert.True(nobody.Result.IsNil);
        Assert.Equal(Status.UnknownOp, old.Status);
    }

    [Fact]
    public void Blocked_Given_a_position_on_a_gmap_When_asked_Then_the_segment_of_the_position_is_asked_and_no_segment_is_blocked()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, Val.False);

        // Act
        Outcome inSegment = ext.Dispatcher.LevelBlocked("world", 70, 100);
        int asked = host.Asked.Count;
        Outcome nowhere = ext.Dispatcher.LevelBlocked("world", 130, 10);
        Outcome plain = ext.Dispatcher.LevelBlocked("hall.nw", 130, 10);

        // Assert
        Assert.False(inSegment.Result.AsBool);
        Assert.Equal("level.blocked", host.Asked[0].Name);
        Assert.Equal(new[] { "gd.nw" }, host.Asked.Take(1).Select(q => q.Args[0].AsString).ToArray());
        Assert.Equal(new[] { 6.0, 36.0 }, new[] { host.Asked[0].Args[1].AsFloat, host.Asked[0].Args[2].AsFloat });
        Assert.Equal(1, asked);
        Assert.True(nowhere.Result.AsBool);
        Assert.Equal(2, host.Asked.Count);
        Assert.Equal("hall.nw", host.Asked[1].Args[0].AsString);
        Assert.False(plain.Result.AsBool);
    }

    [Fact]
    public void Loaded_Given_a_position_or_a_segment_name_When_asked_Then_the_segment_is_asked_and_no_segment_is_not_loaded()
    {
        // Arrange
        (DirectExt ext, FakeHost host) = Start();
        using DirectExt _ = ext;
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, Val.True);

        // Act
        Outcome byPosition = ext.Dispatcher.LevelLoadedAt("world", 70, 10);
        Outcome nowhere = ext.Dispatcher.LevelLoadedAt("WORLD", 130, 10);
        Outcome byName = ext.Dispatcher.LevelLoaded("GB.nw");

        // Assert
        Assert.True(byPosition.Result.AsBool);
        Assert.False(nowhere.Result.AsBool);
        Assert.True(byName.Result.AsBool);
        Assert.Equal(new[] { "gb.nw", "gb.nw" }, host.Asked.Select(q => q.Args[0].AsString).ToArray());
        Assert.All(host.Asked, q => Assert.Equal("level.loaded", q.Name));
    }
}

/// <summary>The query channel over a real socket: the extension's QUERY frames and the host's QRESULT frames.</summary>
public sealed class GmapLinkTests
{
    private const int OpNear = 3000;

    private static void Register(ManifestBuilder b) =>
        b.AddFunction(Lane.Gmaps, new FunctionDef(OpNear, "t4gnear", new[] { "string", "float", "float", "float" }, "any", Handler: (d, a) =>
            d.NearObjects(a[0].AsString, a[1].AsFloat, a[2].AsFloat, a[3].AsFloat)));

    private static ScriptedHost Start(TimeSpan? timeout = null)
    {
        TempFolder world = ObjectWorld.Make();
        ExtensionOptions options = Atlas.Options(timeout);
        ScriptedHost host = ScriptedHost.Start(ScriptedHost.HostHello(world.Path, protoMinor: 1), new ExtensionOptions
        {
            Writer = options.Writer,
            QueryTimeout = options.QueryTimeout,
            ExtraManifest = Register,
        });
        Assert.IsType<HelloFrame>(host.Read());
        Assert.Equal(0UL, host.Init().Status);
        Assert.Equal(0UL, host.Call(17, Atlas.Layout()).Status);
        return host;
    }

    [Fact]
    public void Query_Given_a_request_that_asks_the_host_When_the_answers_come_Then_the_queries_go_one_at_a_time_and_the_reply_follows()
    {
        // Arrange
        using ScriptedHost host = Start();
        ulong id = host.SendReq(OpNear, new[] { S("world"), N(60), N(10), N(10) }, level: "world");

        // Act
        var first = Assert.IsType<QueryFrame>(host.Read());
        host.Send(new QResultFrame(first.Qid, 0, Val.List(FakeHost.Row(1, 50, 10))));
        var second = Assert.IsType<QueryFrame>(host.Read());
        host.Send(new QResultFrame(second.Qid, 0, Val.List(FakeHost.Row(2, 1, 10))));
        ReplyFrame reply = host.ReadReply(id);

        // Assert
        Assert.Equal(1UL, first.Qid);
        Assert.Equal(2UL, second.Qid);
        Assert.Equal("level.near", first.Name);
        Assert.Equal("ga.nw", first.Args[0].AsString);
        Assert.Equal("gb.nw", second.Args[0].AsString);
        Assert.Equal(0UL, reply.Status);
        Assert.Equal(new[] { 2UL, 1UL }, reply.Result.Items.Select(r => r.Items[0].AsHandle).ToArray());
        Assert.Equal(65.0, reply.Result.Items[0].Items[1].AsFloat);
    }

    [Fact]
    public void Query_Given_the_host_answers_with_an_error_When_the_request_runs_Then_the_reply_has_that_status()
    {
        // Arrange
        using ScriptedHost host = Start();
        ulong id = host.SendReq(OpNear, new[] { S("hall.nw"), N(1), N(1), N(1) }, level: "hall.nw");

        // Act
        var query = Assert.IsType<QueryFrame>(host.Read());
        host.Send(new QResultFrame(query.Qid, (ulong)Status.Unavailable, S("not now")));
        ReplyFrame reply = host.ReadReply(id);

        // Assert
        Assert.Equal((ulong)Status.Unavailable, reply.Status);
        Assert.Equal("not now", reply.Result.AsString);
    }

    [Fact]
    public void Query_Given_a_host_that_never_answers_When_the_time_is_up_Then_the_reply_is_internal_and_a_late_answer_is_dropped()
    {
        // Arrange
        using ScriptedHost host = Start(TimeSpan.FromMilliseconds(150));
        ulong id = host.SendReq(OpNear, new[] { S("hall.nw"), N(1), N(1), N(1) }, level: "hall.nw");

        // Act
        var query = Assert.IsType<QueryFrame>(host.Read());
        ReplyFrame reply = host.ReadReply(id);
        host.Send(new QResultFrame(query.Qid, 0, Val.List()));
        ReplyFrame next = host.Call(15);

        // Assert
        Assert.Equal((ulong)Status.Internal, reply.Status);
        Assert.Equal(0UL, next.Status);
    }

    [Fact]
    public void Query_Given_an_answer_to_a_query_that_was_never_sent_When_it_arrives_Then_it_is_a_protocol_error()
    {
        // Arrange
        using ScriptedHost host = Start();

        // Act
        host.Send(new QResultFrame(99, 0, Val.Nil));

        // Assert
        Assert.Equal(ExtensionRunner.ExitProtocol, host.WaitExit());
    }
}
