// Not ported from a Crossfire file: tests of the skeleton that wave 4 of phase 3 builds the extension on: the 1.1 handshake, the manifest
// that each lane fills through one registration point, the player controller with its flush at the end of a request, the busy
// answer of the slow functions and the events on items (docs/protocol.md 4.1, 6, 11 and 11.1; docs/extension.md, "The skeleton of wave 4").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>What a test's lane registers in the manifest, and what its functions do (they run in the dispatcher's worker, like a lane's).</summary>
internal static class TestLane
{
    public const int OpMark = 1000;
    public const int OpMarkFail = 1001;
    public const int OpSlow = 1002;
    public const int OpFast = 1003;
    public const int OpItemEvent = 1004;
    public const int OpPlayerEvent = 1005;
    public const int OpObjectEvent = 1006;
    public const int OpUndeclared = 1007;
    public const int OpBadType = 1008;
    public const int OpWrongTarget = 1009;

    public static readonly string[] ItemClasses = { "specialitem", "mudobjects" };

    private static Player Controller(Dispatcher d, IReadOnlyList<Val> args) => d.ControllerOf(args[0].AsString)!;

    public static void Register(ManifestBuilder b)
    {
        b.AddFunction(Lane.Items, new FunctionDef(OpMark, "t4c0mark", new[] { "string", "int" }, "void", Handler: (d, a) =>
        {
            Controller(d, a).Shown.MarkDirty((uint)a[1].AsInt);
            return Outcome.Ok(Val.Nil);
        }));
        b.AddFunction(Lane.Items, new FunctionDef(OpMarkFail, "t4c0markfail", new[] { "string" }, "void", Handler: (d, a) =>
        {
            Controller(d, a).Shown.MarkDirty(9);
            return Outcome.Fail(Status.BadArgs, "a failure after a mark");
        }));
        b.AddFunction(Lane.Items, new FunctionDef(OpSlow, "t4c0slow", new[] { "string" }, "int", Slow: true, Handler: (d, a) => Outcome.Ok(Val.Int(++Controller(d, a).Count))));
        b.AddFunction(Lane.Items, new FunctionDef(OpFast, "t4c0fast", new[] { "string" }, "int", Handler: (d, a) => Outcome.Ok(Val.Int(++Controller(d, a).Count))));
        b.AddFunction(Lane.Items, new FunctionDef(OpItemEvent, "t4c0itemevent", new[] { "string", "bool" }, "int", Handler: (d, a) =>
        {
            Player p = Controller(d, a);
            Val? fallback = a[1].AsBool ? Dispatcher.TextFallback(p.Account, "You cannot use that.") : null;
            EmitResult r = d.EmitItemEvent("onT4c0Item", p.State, "TMudObject", ItemClasses, new[] { Val.Handle(d.HandleForTest(p.State)), Val.Int(3) }, fallback);
            return Outcome.Ok(Val.Int((int)r));
        }));
        b.AddFunction(Lane.Items, new FunctionDef(OpPlayerEvent, "t4c0playerevent", new[] { "string" }, "int", Handler: (d, a) =>
        {
            Player p = Controller(d, a);
            EmitResult r = d.EmitPlayerEvent("onT4c0Player", p.Account, new[] { Val.Int(1) }, d.InvokeFallback("defaultapply", p.State, Array.Empty<Val>()));
            return Outcome.Ok(Val.Int((int)r));
        }));
        b.AddFunction(Lane.Items, new FunctionDef(OpObjectEvent, "t4c0objectevent", new[] { "string" }, "int", Handler: (d, a) =>
        {
            Player p = Controller(d, a);
            EmitResult r = d.EmitObjectEvent("onT4c0Object", p.State, new[] { Val.Int(2) }, Dispatcher.TextFallback(p.Account, "Nothing happens."));
            return Outcome.Ok(Val.Int((int)r));
        }));
        b.AddFunction(Lane.Items, new FunctionDef(OpUndeclared, "t4c0undeclared", new[] { "string" }, "void", Handler: (d, a) =>
        {
            d.EmitItemEvent("onNobodyDeclaredThis", Controller(d, a).State, "TMudObject", Array.Empty<string>(), Array.Empty<Val>());
            return Outcome.Ok(Val.Nil);
        }));
        b.AddFunction(Lane.Items, new FunctionDef(OpBadType, "t4c0badtype", new[] { "string" }, "void", Handler: (d, a) =>
        {
            d.EmitItemEvent("onT4c0Item", Controller(d, a).State, "TNoSuchType", Array.Empty<string>(), new[] { Val.Nil, Val.Int(0) });
            return Outcome.Ok(Val.Nil);
        }));
        b.AddFunction(Lane.Items, new FunctionDef(OpWrongTarget, "t4c0wrongtarget", new[] { "string" }, "void", Handler: (d, a) =>
        {
            d.EmitPlayerEvent("onT4c0Item", Controller(d, a).Account, Array.Empty<Val>());
            return Outcome.Ok(Val.Nil);
        }));
        b.AddEvent(Lane.Items, new EventDef("onT4c0Item", "item", "sync", new[] { "handle:TMudPlayer", "int" }));
        b.AddEvent(Lane.Items, new EventDef("onT4c0Player", "player"));
        b.AddEvent(Lane.Items, new EventDef("onT4c0Object", "object"));
        b.AddEvent(Lane.Items, new EventDef("onT4c0Typed", "player", "deferred", new[] { "int" }));
        b.AddPlayerClass("mudplayers");
    }
}

internal static class DispatcherTestExtensions
{
    /// <summary>The handle of a player, as <c>player.login</c> gave it.</summary>
    public static ulong HandleForTest(this Dispatcher d, PlayerState state) => ((IMemberEnv)d).HandleOf(state);
}

public sealed class SkeletonTests
{
    private static ExtensionOptions Options(Action<Dispatcher, PlayerState>? flush = null) => new()
    {
        Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = TestLane.Register,
        FlushHook = flush,
    };

    private static DirectExt Start(TempFolder world, long protoMinor = 1, Action<Dispatcher, PlayerState>? flush = null, long maxFrame = 16_777_216) =>
        new(world, Options(flush), 1, true, maxFrame, protoMinor);

    private static ulong Login(DirectExt ext, string account) => Ok(ext.Call(5, new[] { S(account) }, player: account));

    private static ReplyFrame CallOp(DirectExt ext, int op, string account, params Val[] more) =>
        ext.Call((ulong)op, new[] { S(account) }.Concat(more), player: account);

    // ------------------------------------------------------------------ the handshake

    [Theory]
    [InlineData(0L, 0)]
    [InlineData(1L, 1)]
    [InlineData(7L, 1)]
    public void LinkMinor_Given_the_hosts_proto_minor_When_parsed_Then_it_is_the_smaller_of_the_two_sides(long hostMinor, int expected)
    {
        // Arrange
        Val hello = ScriptedHost.HostHello("/w", protoMinor: hostMinor);

        // Act
        HostHello parsed = HostHello.Parse(hello);

        // Assert
        Assert.Equal(hostMinor, parsed.ProtoMinor);
        Assert.Equal(expected, parsed.LinkMinor);
    }

    [Theory]
    [InlineData(0L, 0, false)]
    [InlineData(1L, 1, true)]
    [InlineData(5L, 1, true)]
    public void Session_Given_a_host_hello_When_the_dispatcher_starts_Then_every_part_reads_the_link_minor_version(long hostMinor, int expected, bool speaks11)
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make(), hostMinor);

        // Act
        int minor = ext.Dispatcher.LinkMinor;

        // Assert
        Assert.Equal(expected, minor);
        Assert.Equal(speaks11, ext.Dispatcher.Speaks11);
    }

    private static Val HelloOf(long hostMinor, Action<ManifestBuilder>? extra, out ScriptedHost host)
    {
        TempFolder world = ObjectWorld.Make();
        host = ScriptedHost.Start(ScriptedHost.HostHello(world.Path, protoMinor: hostMinor), new ExtensionOptions { ExtraManifest = extra });
        return ((HelloFrame)host.Read()!).Hello;
    }

    [Fact]
    public void Handshake_Given_a_1_0_host_When_the_extension_answers_Then_it_sends_proto_minor_1_and_a_manifest_without_a_1_1_key()
    {
        // Arrange / Act
        Val hello = HelloOf(0, TestLane.Register, out ScriptedHost host);
        using ScriptedHost _ = host;
        Val manifest = hello.Get("manifest")!;
        string[] events = manifest.Get("events")!.Items.Select(e => e.Get("name")!.AsString).ToArray();

        // Assert
        Assert.Equal(1L, hello.Get("proto_minor")!.AsInt);
        Assert.Null(manifest.Get("player_classes"));
        Assert.DoesNotContain("onT4c0Item", events);
        Assert.Contains("onT4c0Typed", events);
        Assert.All(manifest.Get("events")!.Items, e => Assert.Null(e.Get("params")));
        Assert.All(manifest.Get("events")!.Items, e => Assert.NotEqual("item", e.Get("target")!.AsString));
        Assert.True(Val.Same(Manifest.Build(false, 0, TestLane.Register), manifest));
    }

    [Fact]
    public void Handshake_Given_a_1_1_host_When_the_extension_answers_Then_the_manifest_has_the_1_1_keys_the_lanes_registered()
    {
        // Arrange / Act
        Val hello = HelloOf(1, TestLane.Register, out ScriptedHost host);
        using ScriptedHost _ = host;
        Val manifest = hello.Get("manifest")!;
        Val item = manifest.Get("events")!.Items.Single(e => e.Get("name")!.AsString == "onT4c0Item");
        Val typed = manifest.Get("events")!.Items.Single(e => e.Get("name")!.AsString == "onT4c0Typed");

        // Assert
        Assert.Equal(1L, hello.Get("proto_minor")!.AsInt);
        Assert.Equal(new[] { "mudplayers" }, manifest.Get("player_classes")!.Items.Select(c => c.AsString));
        Assert.Equal("item", item.Get("target")!.AsString);
        Assert.Equal(new[] { "handle:TMudPlayer", "int" }, item.Get("params")!.Items.Select(p => p.AsString));
        Assert.Equal(new[] { "int" }, typed.Get("params")!.Items.Select(p => p.AsString));
        Assert.True(Val.Same(Manifest.Build(false, 1, TestLane.Register), manifest));
        Assert.Equal(new[] { "functions", "types", "events", "wants_tick", "limits", "player_classes" }, manifest.Pairs.Select(p => p.Key));
    }

    [Fact]
    public void Handshake_Given_a_1_1_host_and_no_lane_registration_When_the_extension_answers_Then_nothing_1_1_is_declared()
    {
        // Arrange / Act
        Val hello = HelloOf(1, null, out ScriptedHost host);
        using ScriptedHost _ = host;

        // Assert
        Assert.Equal(1L, hello.Get("proto_minor")!.AsInt);
        Assert.True(Val.Same(Manifest.Build(), hello.Get("manifest")!));
    }

    [Fact]
    public void Dispatch_Given_a_function_of_a_lane_on_a_1_0_link_that_needs_1_1_When_called_Then_it_is_not_in_the_manifest_and_unknown_op()
    {
        // Arrange
        Action<ManifestBuilder> register = b => b.AddFunction(Lane.Items, new FunctionDef(1100, "t4c0new", new[] { "ref" }, "void", MinMinor: 1, Handler: (_, _) => Outcome.Ok(Val.Nil)));
        var options = new ExtensionOptions { ExtraManifest = register, Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) } };
        using var old = new DirectExt(ObjectWorld.Make(), options, protoMinor: 0);
        using var current = new DirectExt(ObjectWorld.Make(), options, protoMinor: 1);

        // Act
        ReplyFrame onOld = old.Call(1100, new[] { Val.Nil });
        ReplyFrame onCurrent = current.Call(1100, new[] { Val.Nil });

        // Assert
        Assert.Equal((ulong)Status.UnknownOp, onOld.Status);
        Assert.Equal(0UL, onCurrent.Status);
        Assert.DoesNotContain(Manifest.Build(false, 0, register).Get("functions")!.Items, f => f.Get("name")!.AsString == "t4c0new");
        Assert.Contains(Manifest.Build(false, 1, register).Get("functions")!.Items, f => f.Get("name")!.AsString == "t4c0new");
    }

    // ------------------------------------------------------------------ the registration point

    [Fact]
    public void Lane_Given_every_lane_When_the_ranges_are_compared_Then_none_overlaps_and_the_core_keeps_what_it_has()
    {
        // Arrange
        IReadOnlyList<Lane> lanes = Lane.All;

        // Act / Assert
        for (int i = 0; i < lanes.Count; i++)
        {
            for (int j = i + 1; j < lanes.Count; j++)
            {
                Assert.True(lanes[i].LastFunction < lanes[j].FirstFunction || lanes[j].LastFunction < lanes[i].FirstFunction, lanes[i].Name + " and " + lanes[j].Name + " share functions");
                Assert.True(lanes[i].LastType < lanes[j].FirstType || lanes[j].LastType < lanes[i].FirstType, lanes[i].Name + " and " + lanes[j].Name + " share types");
                Assert.True(lanes[i].LastMember < lanes[j].FirstMember || lanes[j].LastMember < lanes[i].FirstMember, lanes[i].Name + " and " + lanes[j].Name + " share members");
            }
        }
        Assert.All(lanes, l => Assert.True(l.FirstFunction >= 64));
        Assert.All(lanes.Skip(1), l => Assert.Equal(1000, l.LastMember - l.FirstMember + 1));
        Assert.All(lanes.Skip(1), l => Assert.True(l.OwnTypesMembersFirst + 249 == l.LastMember && l.PlayerMembersLast + 1 == l.OwnTypesMembersFirst && l.ObjectMembersLast + 1 == l.PlayerMembersFirst));
    }

    [Fact]
    public void Build_Given_the_shipped_manifest_When_every_id_is_checked_Then_each_is_in_the_range_of_the_core()
    {
        // Arrange
        Val manifest = Manifest.Build(false, 1);

        // Act
        var functions = manifest.Get("functions")!.Items.Select(f => f.Get("id")!.AsInt).ToList();
        var types = manifest.Get("types")!.Items.Select(t => t.Get("id")!.AsInt).ToList();
        var members = manifest.Get("types")!.Items.SelectMany(t => t.Get("members")!.Items.Concat(t.Get("methods")!.Items)).Select(m => m.Get("id")!.AsInt).ToList();

        // Assert
        Assert.All(functions, id => Assert.True(Lane.Core.OwnsFunction(id)));
        Assert.All(types, id => Assert.True(Lane.Core.OwnsType(id)));
        Assert.All(members, id => Assert.True(Lane.Core.OwnsMember(id)));
    }

    [Fact]
    public void Docs_Given_the_lanes_When_compared_with_the_text_Then_each_range_is_written_in_the_table_of_extension_md()
    {
        // Arrange
        string doc = File.ReadAllText(RepoFile(Path.Combine("docs", "extension.md")));

        // Act / Assert
        foreach (Lane l in Lane.All)
        {
            string row = "| " + l.Name + " | " + l.FirstFunction + " to " + l.LastFunction + " | " + l.FirstType + " to " + l.LastType + " | " + l.FirstMember + " to " + l.LastMember + " |";
            Assert.Contains(row, doc);
        }
    }

    private static string RepoFile(string relative)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Crossfire.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(dir ?? throw new FileNotFoundException("Crossfire.sln"), relative);
    }

    private static readonly (string Why, Action<ManifestBuilder> Register)[] RefusalCases =
    {
        ("a function id outside the lane", (Action<ManifestBuilder>)(b => b.AddFunction(Lane.Items, new FunctionDef(2000, "a", Array.Empty<string>(), "void")))),
        ("a function id below the core's range", (Action<ManifestBuilder>)(b => b.AddFunction(Lane.Core, new FunctionDef(63, "a", Array.Empty<string>(), "void")))),
        ("a function id used twice", (Action<ManifestBuilder>)(b => { b.AddFunction(Lane.Items, new FunctionDef(1500, "a", Array.Empty<string>(), "void")); b.AddFunction(Lane.Items, new FunctionDef(1500, "b", Array.Empty<string>(), "void")); })),
        ("a function name used twice, in another case", (Action<ManifestBuilder>)(b => { b.AddFunction(Lane.Items, new FunctionDef(1500, "Aa", Array.Empty<string>(), "void")); b.AddFunction(Lane.Items, new FunctionDef(1501, "aA", Array.Empty<string>(), "void")); })),
        ("a function name that is not a name", (Action<ManifestBuilder>)(b => b.AddFunction(Lane.Items, new FunctionDef(1500, "1a", Array.Empty<string>(), "void")))),
        ("an unknown type name", (Action<ManifestBuilder>)(b => b.AddFunction(Lane.Items, new FunctionDef(1500, "a", new[] { "integer" }, "void")))),
        ("list of void", (Action<ManifestBuilder>)(b => b.AddFunction(Lane.Items, new FunctionDef(1500, "a", new[] { "list:void" }, "void")))),
        ("ref without MinMinor 1", (Action<ManifestBuilder>)(b => b.AddFunction(Lane.Items, new FunctionDef(1500, "a", new[] { "ref" }, "void")))),
        ("ref:Type without MinMinor 1", (Action<ManifestBuilder>)(b => b.AddFunction(Lane.Items, new FunctionDef(1500, "a", Array.Empty<string>(), "ref:TMudObject")))),
        ("a type id outside the lane", (Action<ManifestBuilder>)(b => b.AddType(Lane.Items, new TypeDef(20, "TX", TypeMap(20, 1000, "TX"))))),
        ("a type id used twice", (Action<ManifestBuilder>)(b => { b.AddType(Lane.Items, new TypeDef(10, "TX", TypeMap(10, 1000, "TX"))); b.AddType(Lane.Items, new TypeDef(10, "TY", TypeMap(10, 1001, "TY"))); })),
        ("a type name used twice", (Action<ManifestBuilder>)(b => { b.AddType(Lane.Items, new TypeDef(10, "TX", TypeMap(10, 1000, "TX"))); b.AddType(Lane.Items, new TypeDef(11, "TX", TypeMap(11, 1001, "TX"))); })),
        ("a member id outside the lane", (Action<ManifestBuilder>)(b => b.AddType(Lane.Items, new TypeDef(10, "TX", TypeMap(10, 2000, "TX"))))),
        ("a member id of the core's", (Action<ManifestBuilder>)(b => b.AddType(Lane.Items, new TypeDef(10, "TX", TypeMap(10, 545, "TX"))))),
        ("a member id used in two types", (Action<ManifestBuilder>)(b => { b.AddType(Lane.Items, new TypeDef(10, "TX", TypeMap(10, 1000, "TX"))); b.AddType(Lane.Items, new TypeDef(11, "TY", TypeMap(11, 1000, "TY"))); })),
        ("an event name used twice", (Action<ManifestBuilder>)(b => { b.AddEvent(Lane.Items, new EventDef("onA", "player")); b.AddEvent(Lane.Items, new EventDef("onA", "object")); })),
        ("an event target that is none", (Action<ManifestBuilder>)(b => b.AddEvent(Lane.Items, new EventDef("onA", "level")))),
        ("an event mode that is none", (Action<ManifestBuilder>)(b => b.AddEvent(Lane.Items, new EventDef("onA", "player", "later")))),
        ("a type whose map has another name than the registered one", (Action<ManifestBuilder>)(b => b.AddType(Lane.Items, new TypeDef(10, "TX", TypeMap(10, 1000, "TZ"))))),
        ("a type whose map has another id than the registered one", (Action<ManifestBuilder>)(b => b.AddType(Lane.Items, new TypeDef(10, "TX", TypeMap(11, 1000, "TX"))))),
        ("a handle type that is not in the manifest", (Action<ManifestBuilder>)(b => { b.AddFunction(Lane.Items, new FunctionDef(1500, "a", Array.Empty<string>(), "handle:TNobody")); b.ToVal(); })),
    };

    public static IEnumerable<object[]> Refusals() => Enumerable.Range(0, RefusalCases.Length).Select(i => new object[] { i });

    private static Val TypeMap(int id, int member, string? name = null) => Val.Map(
        ("id", Val.Int(id)), ("name", Val.Str(name ?? "T" + id)), ("host", Val.Str("object")),
        ("members", Val.List(Val.Map(("id", Val.Int(member)), ("name", Val.Str("m")), ("type", Val.Str("int")), ("access", Val.Str("r")), ("stable", Val.False)))),
        ("methods", Val.List()));

    [Theory]
    [MemberData(nameof(Refusals))]
    public void ManifestBuilder_Given_a_registration_that_breaks_the_rules_When_added_Then_the_builder_refuses_it(int index)
    {
        // Arrange
        var b = new ManifestBuilder(1);
        (string why, Action<ManifestBuilder> register) = RefusalCases[index];

        // Act
        Exception? e = Record.Exception(() => register(b));

        // Assert
        Assert.True(e is InvalidOperationException, why + ": " + (e?.ToString() ?? "no exception"));
    }

    [Fact]
    public void ManifestBuilder_Given_valid_registrations_When_added_Then_a_1_1_name_is_taken_with_MinMinor_1_and_the_two_ranges_of_a_type_hold()
    {
        // Arrange
        var b = new ManifestBuilder(1);

        // Act
        b.AddFunction(Lane.Items, new FunctionDef(1500, "a", new[] { "ref" }, "ref:TX", MinMinor: 1));
        b.AddType(Lane.Items, new TypeDef(10, "TX", TypeMap(10, 1999, "TX")));
        Val manifest = b.ToVal();

        // Assert
        Assert.Equal("a", manifest.Get("functions")!.Items.Single().Get("name")!.AsString);
        Assert.Equal("TX", manifest.Get("types")!.Items.Single().Get("name")!.AsString);
    }

    [Fact]
    public void FieldBuilder_Given_a_range_When_a_lane_adds_more_members_than_it_holds_Then_the_builder_refuses()
    {
        // Arrange
        var b = new FieldBuilder<LevelObject>(Lane.Items.ObjectMembersFirst, o => o.Ob);
        b.UseRange(Lane.Items.ObjectMembersFirst, Lane.Items.ObjectMembersFirst + 1);

        // Act
        b.Add("a", "int", false, true, (_, _) => Val.Int(1), null);
        b.Add("b", "int", false, true, (_, _) => Val.Int(2), null);
        Exception? method = Record.Exception(() => b.NextMethodId());
        Exception? member = Record.Exception(() => b.Add("c", "int", false, true, (_, _) => Val.Int(3), null));

        // Assert
        Assert.Equal(new[] { 1000, 1001 }, b.Members.Select(m => m.Id));
        Assert.IsType<InvalidOperationException>(method);
        Assert.IsType<InvalidOperationException>(member);
    }

    // ------------------------------------------------------------------ the player controller

    [Fact]
    public void PlayerLogin_Given_a_new_player_When_logged_in_Then_the_object_has_a_controller_with_empty_fields_and_a_second_login_keeps_it()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());

        // Act
        Login(ext, "bob");
        Player? first = ext.Dispatcher.ControllerOf("bob");
        Login(ext, "bob");

        // Assert
        Assert.NotNull(first);
        Assert.Same(first, first!.State.Ob.Contr);
        Assert.Same(first, ext.Dispatcher.ControllerOf("bob"));
        Assert.Equal("bob", first.Account);
        Assert.Equal(0u, first.Count);
        Assert.Null(first.Mark);
        Assert.Equal(0u, first.MarkCount);
        Assert.Empty(first.Shown.Sent);
        Assert.False(first.Shown.HasMarks);
        Assert.False(first.NeedsFlush);
    }

    [Fact]
    public void PlayerLogout_Given_a_logged_in_player_When_logged_out_Then_the_controller_is_dropped_from_the_object_and_the_dispatcher()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        Login(ext, "bob");
        CfObject ob = ext.Dispatcher.ControllerOf("bob")!.State.Ob;

        // Act
        Ok(ext.Call(6, new[] { S("bob") }, player: "bob"));

        // Assert
        Assert.Null(ob.Contr);
        Assert.Null(ext.Dispatcher.ControllerOf("bob"));
    }

    [Fact]
    public void Shown_Given_marks_When_an_item_is_marked_dirty_and_removed_Then_each_is_in_one_list_in_order_and_the_last_mark_wins()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        Login(ext, "bob");
        ShownTable shown = ext.Dispatcher.ControllerOf("bob")!.Shown;

        // Act
        shown.MarkDirty(5);
        shown.MarkDirty(6);
        shown.MarkDirty(5);
        shown.MarkRemoved(6);
        shown.MarkRemoved(7);
        shown.MarkDirty(7);

        // Assert
        Assert.Equal(new uint[] { 5, 7 }, shown.Dirty);
        Assert.Equal(new uint[] { 6 }, shown.Removed);
        shown.ClearMarks();
        Assert.False(shown.HasMarks);
    }

    // ------------------------------------------------------------------ the flush at the end of a request

    [Fact]
    public void Flush_Given_marks_in_a_request_When_it_ends_Then_the_hook_runs_once_and_its_effect_follows_the_requests_own_in_the_reply()
    {
        // Arrange
        var calls = new List<string>();
        using DirectExt ext = Start(ObjectWorld.Make(), 1, (d, ps) =>
        {
            calls.Add(ps.Account + ":" + string.Join(",", ps.Contr!.Shown.Dirty));
            d.SetClientString(ps, "clientr.mud_item5", "flushed");
            ps.Contr.Shown.ClearMarks();
        });
        Login(ext, "bob");
        calls.Clear();

        // Act
        ReplyFrame r = CallOp(ext, TestLane.OpMark, "bob", Val.Int(5));
        CallOp(ext, TestLane.OpMark, "bob", Val.Int(5));
        CallOp(ext, TestLane.OpFast, "bob");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(new[] { "bob:5", "bob:5" }, calls);
        Assert.Equal("client.set", r.Effects.Single().Get("k")!.AsString);
        Assert.Equal("clientr.mud_item5", r.Effects.Single().Get("name")!.AsString);
    }

    [Fact]
    public void Flush_Given_five_marks_of_one_item_in_one_request_When_the_request_ends_Then_the_hook_runs_once()
    {
        // Arrange
        int calls = 0;
        using DirectExt ext = Start(ObjectWorld.Make(), 1, (_, ps) =>
        {
            calls++;
            ps.Contr!.Shown.ClearMarks();
        });
        Login(ext, "bob");
        ShownTable shown = ext.Dispatcher.ControllerOf("bob")!.Shown;
        calls = 0;

        // Act
        for (int i = 0; i < 5; i++)
        {
            shown.MarkDirty(5);
        }
        CallOp(ext, TestLane.OpFast, "bob");

        // Assert
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Flush_Given_a_request_without_marks_When_it_ends_Then_the_hook_does_not_run()
    {
        // Arrange
        int calls = 0;
        using DirectExt ext = Start(ObjectWorld.Make(), 1, (_, _) => calls++);
        Login(ext, "bob");

        // Act
        CallOp(ext, TestLane.OpFast, "bob");

        // Assert
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Flush_Given_a_request_that_fails_after_a_mark_When_it_ends_Then_nothing_is_flushed_and_the_next_request_flushes_it()
    {
        // Arrange
        var calls = new List<string>();
        using DirectExt ext = Start(ObjectWorld.Make(), 1, (_, ps) =>
        {
            calls.Add(string.Join(",", ps.Contr!.Shown.Dirty));
            ps.Contr.Shown.ClearMarks();
        });
        Login(ext, "bob");

        // Act
        ReplyFrame failed = CallOp(ext, TestLane.OpMarkFail, "bob");
        int afterFailure = calls.Count;
        ReplyFrame next = CallOp(ext, TestLane.OpFast, "bob");

        // Assert
        Assert.Equal((ulong)Status.BadArgs, failed.Status);
        Assert.Equal(0, afterFailure);
        Assert.Equal(0UL, next.Status);
        Assert.Equal(new[] { "9" }, calls);
    }

    [Fact]
    public void Flush_Given_a_marked_player_who_logs_out_before_the_flush_When_the_next_request_ends_Then_the_departed_is_not_flushed()
    {
        // Arrange
        var calls = new List<string>();
        using DirectExt ext = Start(ObjectWorld.Make(), 1, (_, ps) => calls.Add(ps.Account));
        Login(ext, "bob");
        CallOp(ext, TestLane.OpMarkFail, "bob");

        // Act
        Ok(ext.Call(6, new[] { S("bob") }, player: "bob"));

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public void Flush_Given_a_reply_too_large_for_a_frame_When_the_error_replaces_it_Then_the_shown_table_forgets_and_the_next_flush_knows_to_resend()
    {
        // Arrange
        var resend = new List<bool>();
        bool first = true;
        using DirectExt ext = Start(ObjectWorld.Make(), 1, (d, ps) =>
        {
            resend.Add(ps.Contr!.NeedsResend);
            ps.Contr.Shown.Sent["clientr.mud_item5"] = "held";
            if (first)
            {
                first = false;
                d.SetClientString(ps, "clientr.big", new string('x', 70_000));
            }
        }, maxFrame: 65_536);
        Login(ext, "bob");
        Player contr = ext.Dispatcher.ControllerOf("bob")!;
        contr.Shown.MarkDirty(5);

        // Act
        ReplyFrame big = CallOp(ext, TestLane.OpFast, "bob");
        bool forgot = contr.Shown.Sent.Count == 0 && contr.NeedsResend && contr.NeedsFlush;
        ReplyFrame next = CallOp(ext, TestLane.OpFast, "bob");

        // Assert
        Assert.Equal((ulong)Status.TooLarge, big.Status);
        Assert.True(forgot);
        Assert.Equal(0UL, next.Status);
        Assert.Equal(new[] { false, true }, resend);
        Assert.False(contr.NeedsResend);
    }

    // ------------------------------------------------------------------ slow functions and the busy answer

    [Fact]
    public void Manifest_Given_a_slow_function_When_built_Then_the_manifest_says_slow_true_and_only_for_it()
    {
        // Arrange / Act
        var functions = Manifest.Build(false, 1, TestLane.Register).Get("functions")!.Items;

        // Assert
        Assert.True(functions.Single(f => f.Get("name")!.AsString == "t4c0slow").Get("slow")!.AsBool);
        Assert.False(functions.Single(f => f.Get("name")!.AsString == "t4c0fast").Get("slow")!.AsBool);
        Assert.Equal(1, functions.Count(f => f.Get("slow")!.AsBool));
    }

    [Fact]
    public void Busy_Given_the_extension_is_busy_When_a_slow_function_is_called_Then_error_7_comes_before_anything_changes()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        Login(ext, "bob");
        Player contr = ext.Dispatcher.ControllerOf("bob")!;
        ext.Dispatcher.Busy = "the extension is saving";

        // Act
        ReplyFrame r = CallOp(ext, TestLane.OpSlow, "bob");

        // Assert
        Assert.Equal(7UL, r.Status);
        Assert.Equal("the extension is saving", r.Result.AsString);
        Assert.Equal(0u, contr.Count);
        Assert.Empty(r.Effects);
    }

    [Fact]
    public void Busy_Given_the_extension_is_busy_When_a_function_that_is_not_slow_is_called_Then_it_runs()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        Login(ext, "bob");
        ext.Dispatcher.Busy = "the extension is saving";

        // Act
        ReplyFrame r = CallOp(ext, TestLane.OpFast, "bob");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(1L, r.Result.AsInt);
    }

    [Fact]
    public void Busy_Given_the_busy_state_has_ended_When_a_slow_function_is_called_Then_it_runs()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        Login(ext, "bob");
        ext.Dispatcher.Busy = "recovering";
        CallOp(ext, TestLane.OpSlow, "bob");
        ext.Dispatcher.Busy = null;

        // Act
        ReplyFrame r = CallOp(ext, TestLane.OpSlow, "bob");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(1L, r.Result.AsInt);
        Assert.Null(ext.Dispatcher.CheckBusy());
    }

    [Fact]
    public void Busy_Given_the_busy_state_When_a_lane_asks_CheckBusy_Then_it_gets_the_error_7_outcome()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        ext.Dispatcher.Busy = "recovering";

        // Act
        Outcome? o = ext.Dispatcher.CheckBusy();

        // Assert
        Assert.Equal(Status.Busy, o!.Value.Status);
    }

    // ------------------------------------------------------------------ events

    [Fact]
    public void EmitItemEvent_Given_a_1_1_link_When_a_part_emits_an_item_event_Then_the_reply_has_the_event_with_the_keys_of_section_11_1_in_order()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        ulong player = Login(ext, "bob");

        // Act
        ReplyFrame r = CallOp(ext, TestLane.OpItemEvent, "bob", Val.True);
        Val e = r.Events.Single();

        // Assert
        Assert.Equal((long)EmitResult.Sent, r.Result.AsInt);
        Assert.Equal(new[] { "name", "target", "handle", "type", "classes", "args", "fallback" }, e.Pairs.Select(p => p.Key));
        Assert.Equal("onT4c0Item", e.Get("name")!.AsString);
        Assert.Equal("item", e.Get("target")!.AsString);
        Assert.Equal(player, e.Get("handle")!.AsHandle);
        Assert.Equal("TMudObject", e.Get("type")!.AsString);
        Assert.Equal(TestLane.ItemClasses, e.Get("classes")!.Items.Select(c => c.AsString));
        Assert.Equal(player, e.Get("args")!.Items[0].AsHandle);
        Assert.Equal(3L, e.Get("args")!.Items[1].AsInt);
        Assert.Equal(new[] { "k", "player", "text" }, e.Get("fallback")!.Pairs.Select(p => p.Key));
        Assert.Equal("client.text", e.Get("fallback")!.Get("k")!.AsString);
        Assert.Equal("bob", e.Get("fallback")!.Get("player")!.AsString);
        Assert.Equal("You cannot use that.", e.Get("fallback")!.Get("text")!.AsString);
    }

    [Fact]
    public void EmitItemEvent_Given_no_fallback_When_emitted_Then_the_event_has_no_fallback_key()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        Login(ext, "bob");

        // Act
        ReplyFrame r = CallOp(ext, TestLane.OpItemEvent, "bob", Val.False);

        // Assert
        Assert.Equal(new[] { "name", "target", "handle", "type", "classes", "args" }, r.Events.Single().Pairs.Select(p => p.Key));
    }

    [Fact]
    public void EmitItemEvent_Given_a_1_0_link_When_a_part_emits_an_item_event_Then_the_reply_has_no_event()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make(), 0);
        Login(ext, "bob");

        // Act
        ReplyFrame r = CallOp(ext, TestLane.OpItemEvent, "bob", Val.True);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal((long)EmitResult.NotSent, r.Result.AsInt);
        Assert.Empty(r.Events);
    }

    [Fact]
    public void EmitPlayerEvent_Given_a_1_1_link_When_emitted_with_an_invoke_fallback_Then_the_event_carries_the_fallback_map()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        ulong player = Login(ext, "bob");

        // Act
        ReplyFrame r = CallOp(ext, TestLane.OpPlayerEvent, "bob");
        Val e = r.Events.Single();

        // Assert
        Assert.Equal((long)EmitResult.Sent, r.Result.AsInt);
        Assert.Equal(new[] { "name", "target", "player", "args", "fallback" }, e.Pairs.Select(p => p.Key));
        Assert.Equal("bob", e.Get("player")!.AsString);
        Assert.Equal(new[] { "invoke", "handle", "args" }, e.Get("fallback")!.Pairs.Select(p => p.Key));
        Assert.Equal("defaultapply", e.Get("fallback")!.Get("invoke")!.AsString);
        Assert.Equal(player, e.Get("fallback")!.Get("handle")!.AsHandle);
        Assert.Empty(e.Get("fallback")!.Get("args")!.Items);
    }

    [Fact]
    public void EmitPlayerEvent_Given_a_1_0_link_When_emitted_with_a_fallback_Then_the_event_is_sent_without_it_and_the_result_says_so()
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make(), 0);
        Login(ext, "bob");

        // Act
        ReplyFrame r = CallOp(ext, TestLane.OpPlayerEvent, "bob");

        // Assert
        Assert.Equal((long)EmitResult.SentWithoutFallback, r.Result.AsInt);
        Assert.Equal(new[] { "name", "target", "player", "args" }, r.Events.Single().Pairs.Select(p => p.Key));
    }

    [Fact]
    public void EmitObjectEvent_Given_a_fallback_When_the_link_is_1_1_and_1_0_Then_only_the_1_1_link_carries_it()
    {
        // Arrange
        using DirectExt current = Start(ObjectWorld.Make(), 1);
        using DirectExt old = Start(ObjectWorld.Make(), 0);
        Login(current, "bob");
        Login(old, "bob");

        // Act
        ReplyFrame onCurrent = CallOp(current, TestLane.OpObjectEvent, "bob");
        ReplyFrame onOld = CallOp(old, TestLane.OpObjectEvent, "bob");

        // Assert
        Assert.Equal(new[] { "name", "target", "handle", "args", "fallback" }, onCurrent.Events.Single().Pairs.Select(p => p.Key));
        Assert.Equal(new[] { "name", "target", "handle", "args" }, onOld.Events.Single().Pairs.Select(p => p.Key));
        Assert.Equal((long)EmitResult.SentWithoutFallback, onOld.Result.AsInt);
    }

    [Theory]
    [InlineData(TestLane.OpUndeclared)]
    [InlineData(TestLane.OpBadType)]
    [InlineData(TestLane.OpWrongTarget)]
    public void Emit_Given_an_event_nobody_declared_or_a_type_not_in_the_manifest_When_emitted_Then_the_request_is_an_internal_error_without_events(int op)
    {
        // Arrange
        using DirectExt ext = Start(ObjectWorld.Make());
        Login(ext, "bob");

        // Act
        ReplyFrame r = CallOp(ext, op, "bob");

        // Assert
        Assert.Equal((ulong)Status.Internal, r.Status);
        Assert.Empty(r.Events);
    }
}
