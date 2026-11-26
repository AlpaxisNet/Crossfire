// Not ported from a Crossfire file: tests of lane 6A of phase 3 (wave 6), containers, sacks and keys: the ported <c>put_object_in_sack</c>,
// <c>esrv_move_object</c>, <c>esrv_apply_container</c> and <c>find_key</c>, the function <c>mudmoveinvobject</c>, the hook of a drop into an open container,
// the apply of the types 121 and 122, the string <c>mud_container</c> and the account's round trip with nested sacks.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Text;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>An invented world for the container tests: sacks, a locked box, keys, a door, and a level with a sack on the ground.</summary>
internal static class ContainerWorld
{
    public const string Arc =
        PickupWorld.Arc
        + "Object lockbox\nname lockbox\nface lockbox.1\ntype 122\nweight 200\ncontainer 1000\nslaying vault\nend\n"
        + "Object bigsack\nname bigsack\nface bigsack.1\ntype 122\nweight 200\ncontainer 100000\nend\n"
        + "Object lightsack\nname lightsack\nface lightsack.1\ntype 122\nweight 200\ncontainer 100000\nStr 50\nend\n"
        + "Object closer\nname closer\nface closer.1\ntype 121\nweight 1\nend\n"
        + "Object otherkey\nname other key\nface key.1\ntype 21\nweight 10\nslaying other\nend\n"
        + "Object keyring\nname keyring\nface keyring.1\ntype 122\nweight 20\nrace keys\ncontainer 1000\nend\n"
        + "Object door\nname door\nface door.1\ntype 23\nweight 0\nend\n";

    /// <summary>A hall with a sack of three bananas on the square (3, 4) and an empty lockbox on another.</summary>
    public const string HallSeed =
        "arch map\nname hall\nwidth 16\nheight 16\nend\n"
        + "arch sack\nx 3\ny 4\narch banana\nnrof 3\nend\nend\n"
        + "arch lockbox\nx 6\ny 6\nend\n"
        + "arch sack\nx 9\ny 9\narch bigsack\napplied 1\nend\narch banana\nnrof 2\nend\nend\n";

    public const int OpTest = 8900;

    public static void Register(ManifestBuilder b)
    {
        PickupWorld.Register(b);
        b.AddFunction(Lane.Wave6A, new FunctionDef(OpTest, "t6a", new[] { "string", "string", "any", "any", "any" }, "any", Handler: Do));
    }

    private static string Describe(CfObject ob)
    {
        var sb = new StringBuilder(ob.Name);
        if (ob.Nrof > 1)
        {
            sb.Append('x').Append(ob.Nrof);
        }
        if (ob.Inv != null)
        {
            sb.Append('[');
            bool first = true;
            for (CfObject? c = ob.Inv; c != null; c = c.Below)
            {
                sb.Append(first ? "" : ",").Append(Describe(c));
                first = false;
            }
            sb.Append(']');
        }
        return sb.ToString();
    }

    /// <summary>
    /// <c>t6a(action, account, a, b, c)</c>, the test lane's calls of the ported code on the tree of a player, in one request: apply (the
    /// type's apply as manual_apply asks for it), esrv (esrv_apply_container), put (put_object_in_sack), findkey, tree (the player's
    /// tree as text), weight (what the player carries), open (the tag of the open container), flag (a flag of an object), usekeys.
    /// </summary>
    private static Outcome Do(Dispatcher d, IReadOnlyList<Val> a)
    {
        string action = a[0].AsString;
        Player p = d.ControllerOf(a[1].AsString)!;
        CfObject pl = p.State.Ob;
        CfObject? Item(Val tag) => tag.IsNil ? null : tag.Kind == ValKind.Handle ? ((d as IMemberEnv).Resolve(tag.AsHandle) as LevelObject)?.Ob : d.FindItemById(p.State, tag.AsInt, deep: true);
        d.Pickups.Begin();
        switch (action)
        {
            case "apply":
            {
                int? r = d.ApplyByLane(pl, Item(a[2])!, (int)a[3].AsInt);
                return Outcome.Ok(r == null ? Val.Nil : Val.Int(r.Value));
            }
            case "esrv":
                return Outcome.Ok(Val.Int(d.Pickups.EsrvApplyContainer(pl, Item(a[2]))));
            case "applyas": // manual_apply by an object that is no player
            {
                CfWorld w = d.WorldOrNull!;
                CfObject other = w.ArchToObject(w.FindArchetype("pet"))!;
                int? r = d.ApplyByLane(other, Item(a[2])!, 0);
                return Outcome.Ok(r == null ? Val.Nil : Val.Int(r.Value));
            }
            case "esrvas": // another object applies: nothing happens
            {
                CfObject other = d.WorldOrNull!.ArchToObject(d.WorldOrNull.FindArchetype("pet"))!;
                return Outcome.Ok(Val.Int(d.Pickups.EsrvApplyContainer(other, Item(a[2]))));
            }
            case "put":
            {
                bool r = d.Pickups.PutObjectInSack(pl, Item(a[2])!, Item(a[3])!, a[4].IsNil ? 0 : a[4].AsInt);
                return Outcome.Ok(Val.Bool(r));
            }
            case "findkey":
            {
                CfObject? key = d.Pickups.FindKey(pl, a[3].IsNil ? pl : Item(a[3])!, Item(a[2])!);
                return Outcome.Ok(key == null ? Val.Nil : Val.Str(key.Name!));
            }
            case "tree":
                return Outcome.Ok(Val.Str(string.Join(",", Enumerate(pl.Inv).Select(Describe))));
            case "weight":
                return Outcome.Ok(Val.Int(pl.Carrying));
            case "open":
                return Outcome.Ok(Val.Int(pl.Container == null ? 0 : ((IMemberEnv)d).TagOf(d.ItemProxy(pl.Container))));
            case "flag":
                return Outcome.Ok(Val.Bool(Item(a[2])!.QueryFlag((int)a[3].AsInt)));
            case "findkeyas": // a key is looked for by an object that is no player, in a container
            {
                CfWorld w = d.WorldOrNull!;
                CfObject other = w.ArchToObject(w.FindArchetype("pet"))!;
                CfObject? key = d.Pickups.FindKey(other, Item(a[3])!, Item(a[2])!);
                return Outcome.Ok(key == null ? Val.Nil : Val.Str(key.Name!));
            }
            case "shown":
                return Outcome.Ok(p.Shown.Sent.TryGetValue(a[2].AsString, out string? shown) ? Val.Str(shown) : Val.Nil);
            case "usekeys":
                d.Pickups.UseKeys = (UseKeys)a[2].AsInt;
                return Outcome.Ok(Val.Nil);
            case "ground":
                return Outcome.Ok(Val.Str(Describe(Item(a[2])!)));
            case "place": // an object is made from an archetype and put into the container of the tag: no client is told
            {
                CfWorld w = d.WorldOrNull!;
                CfObject ob = d.NewItem(w.FindArchetype(a[3].AsString)!);
                ob.Nrof = a[4].IsNil ? ob.Nrof : (uint)a[4].AsInt;
                w.InsertObInOb(ob, Item(a[2]));
                return Outcome.Ok(Val.Nil);
            }
            default:
                return Outcome.Fail(Status.BadArgs, "unknown action");
        }
    }

    private static IEnumerable<CfObject> Enumerate(CfObject? first)
    {
        for (CfObject? c = first; c != null; c = c.Below)
        {
            yield return c;
        }
    }

    public static TempFolder MakeWorld(string account, string inventory)
    {
        TempFolder world = new();
        string dir = Path.Combine(world.Path, "Arch", "Test");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "test.arc"), Arc);
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts"));
        foreach (string c in ObjectWorld.Classes)
        {
            File.WriteAllText(Path.Combine(world.Path, "scripts", c + ".txt"), "// class\n");
        }
        Directory.CreateDirectory(Path.Combine(world.Path, "mudlevels"));
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall"), HallSeed);
        ItemWorld.Account(world, account, "", inventory);
        ItemWorld.Account(world, "amy", "", "arch sack\napplied 1\nend\narch banana\nend\n");
        return world;
    }

    public static ExtensionOptions Options(FileWriterOptions? writer = null) => new()
    {
        Writer = writer ?? new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = Register,
        Gk = new GkSettings(),
    };
}

public sealed partial class ContainerTests
{
    private sealed class Game : IDisposable
    {
        public Game(string inventory = "", long protoMinor = 1, ExtensionOptions? options = null)
        {
            Ext = new DirectExt(ContainerWorld.MakeWorld("bob", inventory), options ?? ContainerWorld.Options(), 1, true, 16_777_216, protoMinor);
            Assert.Equal(0UL, Ext.Call(5, new[] { S("bob") }, player: "bob").Status);
            Hall = Say.LoadAll(Ext, "hall").Places.Select(p => p.Get("handle")!.AsHandle).ToList();
        }

        public DirectExt Ext { get; }

        /// <summary>The handles of the hall's objects: the sack on the ground, the lockbox.</summary>
        public List<ulong> Hall { get; }

        public ReplyFrame T(string action, Val? a = null, Val? b = null, Val? c = null, string account = "bob") =>
            Ext.Call(ContainerWorld.OpTest, new[] { S(action), S(account), a ?? Val.Nil, b ?? Val.Nil, c ?? Val.Nil }, level: "hall", player: account);

        public string Tree() => T("tree").Result.AsString;

        /// <summary>The tag of the first item of the player with that name (top level).</summary>
        public long Tag(string name, string account = "bob")
        {
            ulong player = Ext.Call(5, new[] { S(account) }, player: account).Result.AsHandle;
            ReplyFrame r = Ext.Call(12, new[] { H(player), Val.Int(Ply("muditems")), Val.List(Val.Int(Obj("mudrealname"))) });
            Val row = r.Result.Items.First(x => x.Items[1].AsString == name);
            return (uint)row.Items[0].AsHandle;
        }

        /// <summary>The tag of an item inside a container: found by the test function's tree walk of the container's tag.</summary>
        public long Inner(string container, string name)
        {
            ulong sack = Ext.Call(5, new[] { S("bob") }, player: "bob").Result.AsHandle;
            _ = sack;
            ReplyFrame r = Ext.Call(12, new[] { H(ItemHandle(Tag(container))), Val.Int(Obj("muditems")), Val.List(Val.Int(Obj("mudrealname"))) });
            Val row = r.Result.Items.First(x => x.Items[1].AsString == name);
            return (uint)row.Items[0].AsHandle;
        }

        /// <summary>The tag of the player's own handle: what a script knows as the id of the player.</summary>
        public long PlayerTag() => (uint)Ext.Call(5, new[] { S("bob") }, player: "bob").Result.AsHandle;

        public static ulong ItemHandle(long tag) => (1UL << 32) | (uint)tag;

        public ReplyFrame Move(long id, long to, long n, string account = "bob") =>
            Ext.Call(Manifest.OpMudMoveInvObject, new[] { Val.Int(id), Val.Int(to), Val.Int(n) }, level: "hall", player: account);

        public ReplyFrame Command(string action, string? text) =>
            Ext.Call(PickupWorld.OpCommand, new[] { S(action), S("bob"), text == null ? Val.Nil : S(text) }, level: "hall", player: "bob");

        public void Dispose() => Ext.Dispose();
    }

    private static List<string> Lines(ReplyFrame r) =>
        r.Events.Where(e => e.Get("name")!.AsString == "onRPGMessage").Select(e => e.Get("args")!.Items[0].AsString)
            .Concat(Effects(r, "client.text").Select(e => e.Get("text")!.AsString)).ToList();

    private static Dictionary<string, string> Sets(ReplyFrame r) =>
        Effects(r, "client.set").GroupBy(e => e.Get("name")!.AsString).ToDictionary(g => g.Key, g => g.Last().Get("value")!.AsString);

    private static bool Result(ReplyFrame r)
    {
        Assert.Equal(0UL, r.Status);
        return r.Result.AsBool;
    }

    private static string Key(long tag) => "clientr.mud_item" + (uint)tag;

    private const string SackAndBananas = "arch sack\napplied 1\nend\narch banana\nnrof 4\nend\n";

    private static ReplyFrame Apply(Game g, long tag) => g.T("apply", Val.Int(tag), Val.Int(0));

    private static long OpenTag(Game g) => g.T("open").Result.AsInt;

    /// <summary>The string the client holds (the flush's record), or null.</summary>
    private static string? Shown(Game g, string name)
    {
        Val v = g.T("shown", S(name)).Result;
        return v.IsNil ? null : v.AsString;
    }

    /// <summary>A field of the item string the client holds for an item.</summary>
    private static string ItemField(Game g, long tag, int field) => Shown(g, Key(tag))!.Split(',')[field];

    private static uint Flags(Game g, long tag) => uint.Parse(ItemField(g, tag, 5));

    // ------------------------------------------------------------------ the manifest and the hooks

    [Fact]
    public void Manifest_Given_the_lane_When_read_Then_mudmoveinvobject_is_a_slow_function_of_op_8000_and_the_hook_of_the_drop_is_set()
    {
        // Arrange
        using var g = new Game();

        // Act
        ManifestBuilder manifest = Manifest.Collect(1);
        FunctionDef f = manifest.FindFunction(Manifest.OpMudMoveInvObject)!;

        // Assert
        Assert.Equal("mudmoveinvobject", f.Name);
        Assert.Equal(8000, f.Id);
        Assert.True(f.Slow);
        Assert.Equal(new[] { "int", "int", "int" }, f.Params);
        Assert.Equal("bool", f.Ret);
        Assert.NotNull(g.Ext.Dispatcher.PutObjectInSack);
    }

    // ------------------------------------------------------------------ esrv_apply_container: the three states

    [Fact]
    public void Apply_Given_a_closed_sack_When_applied_Then_it_is_readied_and_a_second_apply_opens_it_and_a_third_closes_it()
    {
        // Arrange
        using var g = new Game("arch sack\nend\n");
        long sack = g.Tag("sack");

        // Act
        ReplyFrame readied = Apply(g, sack);
        ReplyFrame opened = Apply(g, sack);
        long open = OpenTag(g);
        ReplyFrame closed = Apply(g, sack);

        // Assert
        Assert.Equal(1L, readied.Result.AsInt);
        Assert.Equal(new[] { "You readied sack." }, Lines(readied));
        Assert.Equal(new[] { "You open sack." }, Lines(opened));
        Assert.Equal(sack, open);
        Assert.Equal(new[] { "You close sack (open) (active)." }, Lines(closed));
        Assert.Equal(0L, OpenTag(g));
        Assert.Equal(0u, Flags(g, sack)); // closing clears the applied flag: closed
    }

    [Fact]
    public void Apply_Given_the_states_When_the_flags_are_sent_Then_a_readied_sack_is_active_and_an_open_one_has_the_open_bit()
    {
        // Arrange
        using var g = new Game("arch sack\nend\n");
        long sack = g.Tag("sack");

        // Act
        Apply(g, sack);
        uint readied = Flags(g, sack);
        Apply(g, sack);
        uint open = Flags(g, sack);

        // Assert
        Assert.Equal(ItemStrings.AActive, readied);
        Assert.Equal(ItemStrings.AActive | ItemStrings.FOpen, open);
    }

    [Fact]
    public void Apply_Given_a_sack_that_is_open_When_another_applied_sack_is_applied_Then_the_first_is_closed_and_the_second_opens()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\nend\narch bigsack\napplied 1\nend\n");
        long first = g.Tag("sack");
        long second = g.Tag("bigsack");
        Apply(g, first);
        Assert.Equal(first, OpenTag(g));

        // Act
        ReplyFrame r = Apply(g, second);

        // Assert
        Assert.Equal(new[] { "You close sack (open) (active).", "You open bigsack." }, Lines(r));
        Assert.Equal(second, OpenTag(g));
        Assert.Equal(0u, Flags(g, first)); // closed
    }

    [Fact]
    public void Apply_Given_a_sack_that_is_open_When_a_closed_sack_is_applied_Then_it_is_only_readied_and_the_open_one_stays_open()
    {
        // Arrange: C closes the open container only when the sack that is applied is itself applied
        using var g = new Game("arch sack\napplied 1\nend\narch bigsack\nend\n");
        long first = g.Tag("sack");
        Apply(g, first);

        // Act
        ReplyFrame r = Apply(g, g.Tag("bigsack"));

        // Assert
        Assert.Equal(new[] { "You readied bigsack." }, Lines(r));
        Assert.Equal(first, OpenTag(g));
    }

    [Fact]
    public void Apply_Given_a_sack_inside_a_sack_When_applied_Then_it_is_refused_with_the_text_of_C_and_nothing_changes()
    {
        // Arrange
        using var g = new Game("arch bigsack\narch sack\nend\nend\n");
        long inner = g.Inner("bigsack", "sack");

        // Act
        ReplyFrame r = g.T("esrv", Val.Int(inner));

        // Assert
        Assert.Equal(0L, r.Result.AsInt);
        Assert.Equal(new[] { "You can't open sack" }, Lines(r));
        Assert.Equal(0L, OpenTag(g));
    }

    [Fact]
    public void Apply_Given_an_object_that_is_no_container_or_no_player_When_esrv_apply_container_is_called_Then_it_answers_0_and_logs_the_error_of_C()
    {
        // Arrange
        using var g = new Game(SackAndBananas);
        CfWorld w = g.Ext.Dispatcher.WorldOrNull!;
        int errors = w.LogCounts[(int)LogLevel.Error];

        // Act
        ReplyFrame banana = g.T("esrv", Val.Int(g.Tag("banana")));
        ReplyFrame none = g.T("esrv", Val.Nil);
        ReplyFrame stranger = g.T("esrvas", Val.Int(g.Tag("sack")));

        // Assert
        Assert.Equal(0L, banana.Result.AsInt);
        Assert.Equal(0L, none.Result.AsInt);
        Assert.Equal(2, w.LogCounts[(int)LogLevel.Error] - errors);
        Assert.Equal(0L, stranger.Result.AsInt);
        Assert.Equal(0L, OpenTag(g));
    }

    [Fact]
    public void Apply_Given_the_types_of_the_lane_When_asked_by_manual_apply_Then_a_container_and_the_closer_are_answered_with_1_and_any_other_type_with_null()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\narch closer\nend\nend\narch banana\nend\narch closer\nend\n");
        long sack = g.Tag("sack");
        Apply(g, sack);
        long inside = g.Inner("sack", "closer");

        // Act
        ReplyFrame banana = Apply(g, g.Tag("banana"));
        ReplyFrame lonely = g.T("apply", Val.Int(g.Tag("closer")), Val.Int(0));
        ReplyFrame closes = Apply(g, inside);

        // Assert
        Assert.True(banana.Result.IsNil);
        Assert.Equal(1L, lonely.Result.AsInt); // no environment: esrv_apply_container logs it and C still answers 1
        Assert.Equal(1L, closes.Result.AsInt);
        Assert.Equal(new[] { "You close sack (open) (active)." }, Lines(closes));
        Assert.Equal(0L, OpenTag(g));
    }

    // ------------------------------------------------------------------ locks and keys

    [Fact]
    public void Apply_Given_a_locked_box_and_no_key_When_applied_Then_he_is_told_and_the_apply_answers_0_through_esrv()
    {
        // Arrange
        using var g = new Game("arch lockbox\nend\n");
        long box = g.Tag("lockbox");

        // Act
        ReplyFrame r = g.T("esrv", Val.Int(box));

        // Assert
        Assert.Equal(0L, r.Result.AsInt);
        Assert.Equal(new[] { "You don't have the key to unlock lockbox." }, Lines(r));
        Assert.Equal(0L, OpenTag(g));
        Assert.False(g.T("flag", Val.Int(box), Val.Int(Define.FLAG_APPLIED)).Result.AsBool);
    }

    [Fact]
    public void Apply_Given_a_locked_box_and_the_key_in_the_pack_When_applied_Then_it_is_unlocked_and_readied_then_open()
    {
        // Arrange
        using var g = new Game("arch lockbox\nend\narch skeleton\nend\n");
        long box = g.Tag("lockbox");

        // Act
        ReplyFrame readied = Apply(g, box);
        ReplyFrame opened = Apply(g, box);

        // Assert
        Assert.Equal(new[] { "You unlock lockbox with skeleton key.", "You readied lockbox." }, Lines(readied));
        Assert.Equal(new[] { "You unlock lockbox (active) with skeleton key.", "You open lockbox." }, Lines(opened));
        Assert.Equal(box, OpenTag(g));
    }

    [Fact]
    public void Apply_Given_a_key_that_does_not_fit_When_the_box_is_applied_Then_no_key_is_found()
    {
        // Arrange
        using var g = new Game("arch lockbox\nend\narch otherkey\nend\narch key\nend\n");

        // Act
        ReplyFrame r = g.T("esrv", Val.Int(g.Tag("lockbox")));

        // Assert: a type 21 key of another slaying and a plain key (type 24, only for doors) do not open it
        Assert.Equal(0L, r.Result.AsInt);
        Assert.Equal(new[] { "You don't have the key to unlock lockbox." }, Lines(r));
    }

    [Fact]
    public void FindKey_Given_a_door_When_the_pack_holds_a_type_24_key_Then_it_is_the_key_and_a_special_key_is_not()
    {
        // Arrange
        using var g = new Game("arch door\nend\narch skeleton\nend\n");
        long door = g.Tag("door");

        // Act
        Val special = g.T("findkey", Val.Int(door)).Result;
        using var h = new Game("arch door\nend\narch key\nend\n");
        Val plain = h.T("findkey", Val.Int(h.Tag("door"))).Result;

        // Assert
        Assert.True(special.IsNil); // door->type == DOOR takes KEY (24); the special key's slaying (vault) is not the door's (none)
        Assert.Equal("key", plain.AsString);
    }

    [Fact]
    public void FindKey_Given_a_container_without_objects_When_searched_Then_nothing_is_found()
    {
        // Arrange
        using var g = new Game("arch lockbox\nend\n");

        // Act
        Val r = g.T("findkey", Val.Int(g.Tag("lockbox"))).Result;

        // Assert
        Assert.True(r.IsNil);
    }

    [Fact]
    public void FindKey_Given_the_key_inside_a_sack_When_usekeys_is_the_default_Then_it_vibrates_and_is_not_used()
    {
        // Arrange
        using var g = new Game("arch lockbox\nend\narch sack\napplied 1\narch skeleton\nend\nend\narch bigsack\nend\n");
        long box = g.Tag("lockbox");

        // Act
        ReplyFrame r = g.T("esrv", Val.Int(box));

        // Assert: the empty bigsack is skipped, the sack's key is found and refused with the message of C
        Assert.Equal(0L, r.Result.AsInt);
        Assert.Equal(
            new[] { "The skeleton key in your sack (active) vibrates as you approach the door", "You don't have the key to unlock lockbox." },
            Lines(r));
    }

    [Theory]
    [InlineData((int)UseKeys.Containers, true, "sack", "skeleton key")]
    [InlineData((int)UseKeys.Containers, false, "sack", null)]
    [InlineData((int)UseKeys.Keyrings, true, "keyring", "skeleton key")]
    [InlineData((int)UseKeys.Keyrings, true, "sack", null)]
    [InlineData((int)UseKeys.Keyrings, false, "keyring", null)]
    [InlineData((int)UseKeys.KeyInventory, true, "keyring", null)]
    public void FindKey_Given_a_usekeys_setting_When_the_key_is_in_a_container_Then_only_the_allowed_containers_give_it(int mode, bool active, string holder, string? expected)
    {
        // Arrange
        string inventory = "arch lockbox\nend\narch " + holder + "\n" + (active ? "applied 1\n" : "") + "arch skeleton\nend\nend\n";
        using var g = new Game(inventory);
        g.T("usekeys", Val.Int(mode));

        // Act
        Val r = g.T("findkey", Val.Int(g.Tag("lockbox"))).Result;

        // Assert
        Assert.Equal(expected, r.IsNil ? null : r.AsString);
    }

    [Fact]
    public void FindKey_Given_a_searcher_that_has_no_controller_When_the_key_is_in_a_container_Then_it_is_not_used()
    {
        // Arrange
        using var g = new Game("arch lockbox\nend\narch sack\napplied 1\narch skeleton\nend\nend\n");

        // Act
        Val r = g.T("findkeyas", Val.Int(g.Tag("lockbox")), Val.Int(g.Tag("sack"))).Result;

        // Assert: only players may use keys in containers (and no one is told)
        Assert.True(r.IsNil);
    }

    // ------------------------------------------------------------------ mud_container and the strings of the window

    [Fact]
    public void Container_Given_an_open_sack_When_the_flush_runs_Then_mud_container_names_it_and_its_items_carry_the_1_and_only_changes_are_written()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\narch banana\nnrof 2\nend\nend\narch sword\nend\n");
        long sack = g.Tag("sack");
        long banana = g.Inner("sack", "banana");
        long sword = g.Tag("sword");

        // Act
        ReplyFrame r = Apply(g, sack);
        ReplyFrame again = g.T("tree");

        // Assert
        Dictionary<string, string> sets = Sets(r);
        Assert.StartsWith(sack + ",sack", sets["clientr.mud_container"]);
        Assert.EndsWith(",1", sets[Key(banana)]);
        Assert.EndsWith(",0", Shown(g, Key(sword))!);
        Assert.Empty(Effects(again, "client.set"));
        Assert.Empty(Effects(again, "client.del"));
    }

    [Fact]
    public void Container_Given_an_open_sack_When_it_is_closed_Then_mud_container_is_empty_and_the_strings_of_its_items_are_deleted()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\narch banana\nnrof 2\nend\nend\n");
        long sack = g.Tag("sack");
        long banana = g.Inner("sack", "banana");
        Apply(g, sack);

        // Act
        ReplyFrame r = Apply(g, sack);

        // Assert
        Assert.Equal("", Sets(r)["clientr.mud_container"]);
        Assert.Contains(Effects(r, "client.del"), e => e.Get("name")!.AsString == Key(banana));
        Assert.Null(Shown(g, Key(banana)));
        Assert.NotNull(Shown(g, Key(sack)));
    }

    // ------------------------------------------------------------------ a sack on the ground

    [Fact]
    public void Apply_Given_a_sack_on_the_ground_When_applied_Then_it_opens_with_the_walk_off_flags_and_its_items_are_shown_and_closing_clears_them()
    {
        // Arrange
        using var g = new Game();
        Val ground = H(g.Hall[0]);
        long sack = 0;

        // Act
        ReplyFrame opened = g.T("apply", ground, Val.Int(0));
        string container = Sets(opened)["clientr.mud_container"];
        sack = long.Parse(container.Split(',')[0]);
        ReplyFrame closed = g.T("apply", ground, Val.Int(0));

        // Assert
        Assert.Equal(new[] { "You open sack." }, Lines(opened));
        Assert.Contains(Sets(opened), kv => kv.Key.StartsWith("clientr.mud_item", StringComparison.Ordinal) && kv.Value.EndsWith(",1", StringComparison.Ordinal));
        Assert.Equal(new[] { "You close sack (open) (active)." }, Lines(closed));
        Assert.Equal("", Sets(closed)["clientr.mud_container"]);
        Assert.True(sack > 0);
        Assert.Equal(0L, OpenTag(g));
    }
}

