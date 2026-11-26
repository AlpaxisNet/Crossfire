// Not ported from a Crossfire file: tests of lane 5A of phase 3 (wave 5), objects between a level and a player's inventory: the rows of the
// table of docs/mud-phase3.md section 3 (pick up whole, merging, in part; drop whole, in part), its rules (the owner, nopick and unpaid,
// the weight, a level the extension does not hold, the write order, busy) and the ported functions of server/c_object.c.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>An invented world for the pick up and drop tests: a hall with objects on one square, and what the lane's commands need.</summary>
internal static class PickupWorld
{
    public const string Arc =
        ItemWorld.Arc
        + "Object anvil\nname anvil\nface anvil.1\nweight 500000\nend\n"
        + "Object statue\nname statue\nface statue.1\nweight 100\nno_pick 1\nend\n"
        + "Object pet\nname pet\nface pet.1\nweight 10\nalive 1\nend\n"
        + "Object relic\nname relic\nface relic.1\nweight 100\nno_drop 1\nend\n"
        + "Object loaner\nname loaner\nface loaner.1\nweight 100\nstartequip 1\nend\n"
        + "Object feather\nname feather\nface feather.1\nweight 0\nend\n"
        + "Object fruit\nname fruit\nname_pl fruits\nface fruit.1\ntype 6\nweight 50\nrace fruit\nend\n"
        + "Object sack\nname sack\nface sack.1\ntype 122\nweight 200\ncontainer 1000\nend\n"
        + "Object fruitbasket\nname fruitbasket\nface basket.1\ntype 122\nweight 200\nrace fruit\ncontainer 100000\nend\n"
        + "Object skeleton\nname skeleton key\nface key.1\ntype 21\nweight 10\nslaying vault\nend\n"
        + "Object ring\nname ring\nname_pl rings\nface ring.1\ntype 70\nweight 10\nend\n"
        + "Object potion\nname potion\nname_pl potions\nface potion.1\ntype 5\nweight 10\nend\n"
        + "Object helmet\nname helmet\nface helmet.1\ntype 34\nweight 500\nend\n"
        + "Object arrow\nname arrow\nname_pl arrows\nface arrow.1\ntype 13\nweight 5\nend\n"
        + "Object gem\nname gem\nname_pl gems\nface gem.1\ntype 60\nweight 5\nend\n"
        + "Object key\nname key\nface key.2\ntype 24\nweight 5\nend\n"
        + "Object mist\nname mist\nface mist.1\nweight 10\ninvisible 1\nend\n"
        + "Object shaman\nname shaman\nface shaman.1\ntype 122\nweight 1\nrace fruit\nend\n";

    /// <summary>The hall: eight objects on the square (3, 4) and a sword on another one. The order is the order of <see cref="Hall"/>.</summary>
    public const string HallSeed =
        "arch map\nname hall\nwidth 16\nheight 16\nend\n"
        + "arch banana\nx 3\ny 4\nnrof 5\nend\n"
        + "arch sword\nx 3\ny 4\nend\n"
        + "arch anvil\nx 3\ny 4\nend\n"
        + "arch statue\nx 3\ny 4\nend\n"
        + "arch sword\nx 3\ny 4\nunpaid 1\nend\n"
        + "arch pet\nx 3\ny 4\nend\n"
        + "arch relic\nx 3\ny 4\nend\n"
        + "arch feather\nx 3\ny 4\nend\n"
        + "arch sword\nx 9\ny 9\nend\n";

    /// <summary>A level with a sack that holds an anvil (read when a call names it).</summary>
    public const string VaultSeed = "arch map\nname vault\nwidth 8\nheight 8\nend\narch sack\nx 1\ny 1\narch anvil\nend\nend\n";

    /// <summary>A level the host has not been shown yet: its sword is no NPC.</summary>
    public const string PendingSeed = "arch map\nname pending\nwidth 8\nheight 8\nend\narch sword\nx 2\ny 2\nend\n";

    public const int Banana = 0;
    public const int Sword = 1;
    public const int Anvil = 2;
    public const int Statue = 3;
    public const int UnpaidSword = 4;
    public const int Pet = 5;
    public const int Relic = 6;
    public const int Feather = 7;
    public const int FarSword = 8;

    public const int OpCommand = 4900;
    public const int OpDrop = 4901;

    /// <summary>The test lane's function <c>t5acommand(action, account, text)</c>: runs one of the commands in a request, as wave 7's table will.</summary>
    public static void Register(ManifestBuilder b)
    {
        ItemWorld.Register(b);
        b.AddFunction(Lane.Wave5A, new FunctionDef(OpCommand, "t5acommand", new[] { "string", "string", "any" }, "any", Handler: Command));
        b.AddFunction(Lane.Wave5A, new FunctionDef(OpDrop, "t5adrop", new[] { "string", "string", "any" }, "any", Handler: DropDirect));
    }

    /// <summary>
    /// <c>t5adrop(action, account, id)</c>: calls the ported <c>drop</c> of c_object.c itself (the commands skip the objects it handles
    /// itself): "top" for the item of that tag, "head" for the first object of the pack, "sack" for the first object inside the first
    /// container of the pack, "lastused" for the item of that tag after it was made the player's last used object (the answer is the tag
    /// that is the last used one afterwards).
    /// </summary>
    private static Outcome DropDirect(Dispatcher d, IReadOnlyList<Val> a)
    {
        Player p = d.ControllerOf(a[1].AsString)!;
        CfObject pl = p.State.Ob;
        DropSite here = new(d.DropPlaceAt("hall", 5, 5, Val.Int(5), Val.Int(5)));
        switch (a[0].AsString)
        {
            case "top":
                return Outcome.Ok(Val.Bool(d.Pickups.Drop(pl, d.FindItemById(p.State, a[2].AsInt, deep: true), here)));
            case "head":
                return Outcome.Ok(Val.Bool(d.Pickups.Drop(pl, pl.Inv, here)));
            case "sack":
            {
                CfObject sack = pl.Inv!;
                while (sack.Type != Define.CONTAINER)
                {
                    sack = sack.Below!;
                }
                return Outcome.Ok(Val.Bool(d.Pickups.Drop(pl, sack.Inv, here)));
            }
            case "lastused":
            {
                CfObject item = d.FindItemById(p.State, a[2].AsInt, deep: true)!;
                p.LastUsed = item;
                p.LastUsedId = item.Count;
                d.Pickups.Drop(pl, item, here);
                return Outcome.Ok(Val.Int(p.LastUsedId));
            }
            default:
                return Outcome.Fail(Status.BadArgs, "unknown action");
        }
    }

    private static Outcome Command(Dispatcher d, IReadOnlyList<Val> a)
    {
        PlayerState ps = d.ControllerOf(a[1].AsString)!.State;
        string? text = a[2].IsNil ? null : a[2].AsString;
        return a[0].AsString switch
        {
            "take" => d.CommandTake(ps, text),
            "drop" => d.CommandDrop(ps, text),
            "dropall" => d.CommandDropAll(ps, text),
            _ => Outcome.Fail(Status.BadArgs, "unknown command"),
        };
    }

    public static TempFolder MakeWorld(string account = "bob", string inventory = "", string header = "")
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
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "vault"), VaultSeed);
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "pending"), PendingSeed);
        ItemWorld.Account(world, account, header, inventory);
        return world;
    }

    public static ExtensionOptions Options(FileWriterOptions? writer = null, GkSettings? gk = null) => new()
    {
        Writer = writer ?? new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = Register,
        Gk = gk ?? new GkSettings(),
    };
}

public sealed class PickupTests
{
    private const string Bob = "arch banana\nnrof 4\nend\narch bag\nnrof 1\narch banana\nnrof 2\nend\nend\n";

    private sealed class Game : IDisposable
    {
        public Game(string inventory = Bob, long protoMinor = 1, ExtensionOptions? options = null, string header = "")
        {
            Ext = new DirectExt(PickupWorld.MakeWorld("bob", inventory, header), options ?? PickupWorld.Options(), 1, true, 16_777_216, protoMinor);
            Player = Login("bob");
            Hall = LoadHall();
        }

        public DirectExt Ext { get; }

        public ulong Player { get; }

        /// <summary>The handles of the objects of the hall, in the order of <see cref="PickupWorld.HallSeed"/>.</summary>
        public List<ulong> Hall { get; }

        public ulong Login(string account)
        {
            Assert.Equal(0UL, Ext.Call(5, new[] { S(account) }, player: account).Status);
            return Ext.Call(5, new[] { S(account) }, player: account).Result.AsHandle;
        }

        private List<ulong> LoadHall() => Say.LoadAll(Ext, "hall").Places.Select(p => p.Get("handle")!.AsHandle).ToList();

        public ReplyFrame Pick(ulong handle, long n, string account = "bob", string level = "hall", int method = TMudObjectMembers.MethodMudPickUpObject) =>
            Ext.Call(13, new[] { H(handle), Val.Int(method), Val.List(Val.Int(n)) }, level: level, player: account);

        public ReplyFrame Drop(ulong item, double x, double y, long n, string account = "bob", string? level = "hall") =>
            Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int((uint)item), N(x), N(y), Val.Int(n) }, level: level, player: account);

        public ReplyFrame Command(string action, string? text, string account = "bob", string? level = "hall") =>
            Ext.Call(PickupWorld.OpCommand, new[] { S(action), S(account), text == null ? Val.Nil : S(text) }, level: level, player: account);

        /// <summary>The items of the player as rows of (handle, name) from <c>get_list</c>.</summary>
        public List<(ulong Handle, string Name)> Items(ulong holder, bool player = true)
        {
            int member = player ? Ply("muditems") : Obj("muditems");
            ReplyFrame r = Ext.Call(12, new[] { H(holder), Val.Int(member), Val.List(Val.Int(Obj("mudrealname"))) });
            Assert.Equal(0UL, r.Status);
            return r.Result.Items.Select(row => (row.Items[0].AsHandle, row.Items[1].AsString)).ToList();
        }

        public ReplyFrame Direct(string action, long id = 0, string account = "bob") =>
            Ext.Call(PickupWorld.OpDrop, new[] { S(action), S(account), Val.Int(id) }, level: "hall", player: account);

        public Val Read(ulong handle, string member) => Say.Get(Ext, handle, Obj(member));

        public ReplyFrame ReadRaw(ulong handle, string member) => Ext.Call(9, new[] { H(handle), Val.Int(Obj(member)) });

        public void Dispose() => Ext.Dispose();
    }

    private static string Key(ulong handle) => "clientr.mud_item" + (uint)handle;

    private static List<string> Kinds(ReplyFrame r) => r.Effects.Select(e => e.Get("k")!.AsString).ToList();

    private static List<string> Lines(ReplyFrame r) =>
        r.Events.Where(e => e.Get("name")!.AsString == "onRPGMessage").Select(e => e.Get("args")!.Items[0].AsString)
            .Concat(Effects(r, "client.text").Select(e => e.Get("text")!.AsString)).ToList();

    private static Dictionary<string, string> Sets(ReplyFrame r) => Effects(r, "client.set").ToDictionary(e => e.Get("name")!.AsString, e => e.Get("value")!.AsString);

    private static bool Result(ReplyFrame r)
    {
        Assert.Equal(0UL, r.Status);
        return r.Result.AsBool;
    }

    // ------------------------------------------------------------------ pick up: the rows of the table

    [Fact]
    public void PickUp_Given_a_whole_object_on_the_ground_When_the_player_picks_it_up_Then_the_npc_goes_then_the_string_comes_and_the_handle_stays_the_items()
    {
        // Arrange
        using var g = new Game();
        ulong sword = g.Hall[PickupWorld.Sword];

        // Act
        ReplyFrame r = g.Pick(sword, 1);

        // Assert
        Assert.True(Result(r));
        List<string> kinds = Kinds(r);
        Assert.Equal("npc.remove", kinds[0]); // the NPC first, the strings of the flush after
        Assert.True(kinds.IndexOf("client.set") > 0);
        Assert.Equal(sword, Effects(r, "npc.remove")[0].Get("handle")!.AsHandle);
        Assert.Contains(Key(sword), Sets(r).Keys); // the string is of the same tag
        Assert.Equal(new[] { "bag", "banana", "sword" }, g.Items(g.Player).Select(i => i.Name).Order());
        Assert.Contains(g.Items(g.Player), i => i.Handle == sword); // the same handle names the item
        Assert.Equal("sword", g.Read(sword, "mudrealname").AsString);
        Assert.Equal(new[] { "You pick up the swords." }, Lines(r)); // C names an object whose nrof is 0 by its plural name
    }

    [Fact]
    public void PickUp_Given_a_stack_that_merges_into_the_players_stack_When_picked_up_Then_the_level_object_is_dead_and_the_stack_has_the_sum()
    {
        // Arrange
        using var g = new Game();
        ulong banana = g.Hall[PickupWorld.Banana];
        ulong stack = g.Items(g.Player).Single(i => i.Name == "banana").Handle;
        int tags = g.Ext.Dispatcher.LiveItemTags;

        // Act
        ReplyFrame r = g.Pick(banana, 5);

        // Assert
        Assert.True(Result(r));
        Assert.Equal("npc.remove", Kinds(r)[0]);
        Assert.Equal((ulong)Status.BadHandle, g.ReadRaw(banana, "mudrealname").Status); // the handle is dead
        Assert.Equal("bananas,mud_banana.png,6,9,900,0,0", Sets(r)[Key(stack)]); // 4 and 5
        Assert.Equal(9L, g.Read(stack, "mudnrof").AsInt);
        Assert.DoesNotContain(Key(banana), Sets(r).Keys);
        Assert.Equal(tags, g.Ext.Dispatcher.LiveItemTags); // the freed object keeps no tag
    }

    [Fact]
    public void PickUp_Given_a_part_of_a_stack_When_picked_up_Then_the_level_object_keeps_its_tag_and_a_smaller_count_and_gets_onUpdateObject()
    {
        // Arrange
        using var g = new Game(inventory: "");
        ulong banana = g.Hall[PickupWorld.Banana];

        // Act
        ReplyFrame r = g.Pick(banana, 2);

        // Assert
        Assert.True(Result(r));
        Assert.DoesNotContain("npc.remove", Kinds(r)); // the NPC stays
        Assert.Equal(3L, g.Read(banana, "mudnrof").AsInt);
        (ulong part, string name) = Assert.Single(g.Items(g.Player));
        Assert.Equal("banana", name);
        Assert.NotEqual(banana, part); // the new object has a new tag
        Assert.Equal(2L, g.Read(part, "mudnrof").AsInt);
        Val update = Assert.Single(r.Events, e => e.Get("name")!.AsString == "onUpdateObject");
        Assert.Equal("object", update.Get("target")!.AsString);
        Assert.Equal(banana, update.Get("handle")!.AsHandle);
        Assert.Equal("bananas,mud_banana.png,6,2,200,0,0", Sets(r)[Key(part)]);
    }

    [Fact]
    public void PickUp_Given_a_part_that_merges_into_the_players_stack_When_picked_up_Then_the_stack_grows_and_the_rest_stays_on_the_ground()
    {
        // Arrange
        using var g = new Game();
        ulong stack = g.Items(g.Player).Single(i => i.Name == "banana").Handle;

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Banana], 1);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(5L, g.Read(stack, "mudnrof").AsInt);
        Assert.Equal(4L, g.Read(g.Hall[PickupWorld.Banana], "mudnrof").AsInt);
        Assert.Equal("bananas,mud_banana.png,6,5,500,0,0", Sets(r)[Key(stack)]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(99)]
    public void PickUp_Given_zero_or_more_than_the_stack_When_picked_up_Then_the_whole_stack_goes(long n)
    {
        // Arrange
        using var g = new Game(inventory: "");
        ulong banana = g.Hall[PickupWorld.Banana];

        // Act
        ReplyFrame r = g.Pick(banana, n);

        // Assert
        Assert.True(Result(r));
        Assert.Equal("npc.remove", Kinds(r)[0]);
        Assert.Equal(5L, g.Read(banana, "mudnrof").AsInt); // the same object, in the inventory now
        Assert.Contains(g.Items(g.Player), i => i.Handle == banana);
    }

    [Fact]
    public void PickUp_Given_mudpickupobject2_When_called_Then_it_does_what_mudpickupobject_does()
    {
        // Arrange
        using var g = new Game(inventory: "");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Sword], 1, method: TMudObjectMembers.MethodMudPickUpObject2);

        // Assert
        Assert.True(Result(r));
        Assert.Equal("npc.remove", Kinds(r)[0]);
        Assert.Single(g.Items(g.Player));
    }

    [Fact]
    public void PickUp_Given_an_object_that_was_picked_up_When_dropped_and_picked_up_again_Then_the_tag_never_changes_and_the_live_tags_stay_flat()
    {
        // Arrange
        using var g = new Game(inventory: "");
        ulong sword = g.Hall[PickupWorld.Sword];
        g.Pick(sword, 1);
        int before = g.Ext.Dispatcher.LiveItemTags;

        // Act
        for (int i = 0; i < 20; i++)
        {
            Assert.True(Result(g.Drop(sword, 3, 4, 1)));
            Assert.True(Result(g.Pick(sword, 1)));
        }

        // Assert
        Assert.Equal(before, g.Ext.Dispatcher.LiveItemTags);
        Assert.Equal(sword, Assert.Single(g.Items(g.Player)).Handle);
    }

    // ------------------------------------------------------------------ pick up: the rules

    [Fact]
    public void PickUp_Given_nopick_When_picked_up_Then_false_with_no_text_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(inventory: "");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Statue], 1);

        // Assert
        Assert.False(Result(r));
        Assert.Empty(r.Effects);
        Assert.Empty(r.Events);
        Assert.Empty(g.Items(g.Player));
    }

    [Fact]
    public void PickUp_Given_an_unpaid_object_When_picked_up_Then_false_with_no_text_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(inventory: "");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.UnpaidSword], 1);

        // Assert
        Assert.False(Result(r));
        Assert.Empty(r.Effects);
        Assert.Empty(r.Events);
        Assert.Empty(g.Items(g.Player));
    }

    [Fact]
    public void PickUp_Given_an_object_that_is_too_heavy_When_picked_up_Then_false_with_the_text_and_the_object_stays()
    {
        // Arrange
        using var g = new Game(inventory: "");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Anvil], 1);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "That item is too heavy for you to put it in your inventory." }, Lines(r));
        Assert.DoesNotContain("npc.remove", Kinds(r));
        Assert.Empty(g.Items(g.Player));
        Assert.Equal("anvil", g.Read(g.Hall[PickupWorld.Anvil], "mudrealname").AsString);
    }

    [Fact]
    public void PickUp_Given_a_stack_whose_part_is_light_enough_When_picked_up_Then_the_weight_of_the_part_decides()
    {
        // Arrange: the player carries 70,000 g and may carry 300,000 g
        using var g = new Game(inventory: "arch anvil\nnrof 0\nend\n");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Banana], 5);

        // Assert
        Assert.False(Result(r)); // 70,000 + 500,000 carried already
        Assert.Contains("That item is too heavy for you to put it in your inventory.", Lines(r));
    }

    [Fact]
    public void PickUp_Given_an_object_that_cannot_be_picked_up_When_picked_up_Then_the_text_of_C_and_false()
    {
        // Arrange
        using var g = new Game(inventory: "");

        // Act
        ReplyFrame alive = g.Pick(g.Hall[PickupWorld.Pet], 1);
        ReplyFrame weightless = g.Pick(g.Hall[PickupWorld.Feather], 1);

        // Assert
        Assert.False(Result(alive));
        Assert.Equal(new[] { "You can't pick up the pet." }, Lines(alive));
        Assert.False(Result(weightless));
        Assert.Equal(new[] { "You can't pick up the feather." }, Lines(weightless));
    }

    [Fact]
    public void PickUp_Given_an_object_that_cannot_be_dropped_When_picked_up_Then_C_refuses_without_a_word()
    {
        // Arrange
        using var g = new Game(inventory: "");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Relic], 1);

        // Assert
        Assert.False(Result(r));
        Assert.Empty(r.Effects);
        Assert.Empty(Lines(r));
    }

    [Fact]
    public void PickUp_Given_a_flying_player_When_the_object_lies_on_the_ground_Then_he_cannot_reach_it()
    {
        // Arrange
        using var g = new Game(inventory: "", header: "flying 1\n");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Sword], 1);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "You are levitating, you can't reach the ground!" }, Lines(r));
        Assert.Empty(g.Items(g.Player));
    }

    [Fact]
    public void PickUp_Given_a_negative_number_When_called_Then_false_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(inventory: "");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Sword], -1);

        // Assert
        Assert.False(Result(r));
        Assert.Empty(r.Effects);
        Assert.Empty(g.Items(g.Player));
    }

    [Fact]
    public void PickUp_Given_the_wrong_arguments_When_called_Then_bad_args()
    {
        // Arrange
        using var g = new Game(inventory: "");

        // Act
        ReplyFrame none = g.Ext.Call(13, new[] { H(g.Hall[0]), Val.Int(TMudObjectMembers.MethodMudPickUpObject), Val.List() }, level: "hall", player: "bob");
        ReplyFrame text = g.Ext.Call(13, new[] { H(g.Hall[0]), Val.Int(TMudObjectMembers.MethodMudPickUpObject), Val.List(S("x")) }, level: "hall", player: "bob");

        // Assert
        Assert.Equal((ulong)Status.BadArgs, none.Status);
        Assert.Equal((ulong)Status.BadArgs, text.Status);
    }

    [Fact]
    public void PickUp_Given_no_player_in_the_context_When_called_Then_false_and_a_log_line()
    {
        // Arrange
        using var g = new Game(inventory: "");

        // Act
        ReplyFrame r = g.Ext.Call(13, new[] { H(g.Hall[PickupWorld.Sword]), Val.Int(TMudObjectMembers.MethodMudPickUpObject), Val.List(Val.Int(1)) }, level: "hall");

        // Assert
        Assert.False(Result(r));
        Assert.Contains(g.Ext.Logs, l => l.Text.Contains("mudpickupobject: the call has no logged-in player"));
    }

    [Fact]
    public void PickUp_Given_the_extension_is_busy_When_called_Then_busy_before_anything_changes()
    {
        // Arrange
        using var g = new Game(inventory: "");
        g.Ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Sword], 1);
        g.Ext.Dispatcher.Busy = null;

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.Empty(r.Effects);
        Assert.Empty(g.Items(g.Player));
        Assert.True(Result(g.Pick(g.Hall[PickupWorld.Sword], 1))); // not busy any more: the object was still there
    }

    [Fact]
    public void PickUp_Given_an_item_in_a_sack_of_the_players_tree_When_picked_up_Then_it_goes_into_the_pack()
    {
        // Arrange
        using var g = new Game();
        ulong bag = g.Items(g.Player).Single(i => i.Name == "bag").Handle;
        ulong inside = g.Items(bag, player: false).Single().Handle;

        // Act
        ReplyFrame r = g.Pick(inside, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Empty(g.Items(bag, player: false));
        Assert.Equal(6L, g.Read(g.Items(g.Player).Single(i => i.Name == "banana").Handle, "mudnrof").AsInt); // 4 and 2
        Assert.True(g.Ext.Dispatcher.ControllerOf("bob")!.State.Dirty); // the account alone changed: it is saved after the quiet time
    }

    [Fact]
    public void PickUp_Given_an_item_already_in_the_pack_or_in_another_players_tree_When_picked_up_Then_false()
    {
        // Arrange
        using var g = new Game();
        ulong stack = g.Items(g.Player).Single(i => i.Name == "banana").Handle;
        g.Ext.Call(5, new[] { S("amy") }, player: "amy");
        ulong amy = g.Ext.Call(5, new[] { S("amy") }, player: "amy").Result.AsHandle;
        Say.Get(g.Ext, amy, Ply("mudmoney"));
        ReplyFrame give = g.Ext.Call(ItemWorld.OpDo, new[] { S("add"), S("amy"), S("sword"), Val.Int(1) }, player: "amy");
        ulong amysSword = g.Items(amy).Single().Handle;

        // Act
        ReplyFrame top = g.Pick(stack, 0);
        ReplyFrame other = g.Pick(amysSword, 0);

        // Assert
        Assert.Equal(0UL, give.Status);
        Assert.False(Result(top));
        Assert.False(Result(other));
        Assert.Single(g.Items(amy));
    }

    // ------------------------------------------------------------------ drop: the rows of the table

    [Fact]
    public void Drop_Given_a_whole_stack_When_dropped_Then_the_string_is_deleted_then_a_new_face_is_placed_with_the_same_handle()
    {
        // Arrange
        using var g = new Game();
        ulong stack = g.Items(g.Player).Single(i => i.Name == "banana").Handle;
        int tags = g.Ext.Dispatcher.LiveItemTags;

        // Act
        ReplyFrame r = g.Drop(stack, 6.5, 7.25, 0);

        // Assert
        Assert.True(Result(r));
        List<string> kinds = Kinds(r);
        Assert.True(kinds.IndexOf("client.del") >= 0 && kinds.IndexOf("client.del") < kinds.IndexOf("npc.place"));
        Val place = Assert.Single(Effects(r, "npc.place"));
        Assert.Equal(stack, place.Get("handle")!.AsHandle);
        Assert.Equal("hall", place.Get("level")!.AsString);
        Assert.Equal(6.5, place.Get("x")!.AsFloat);
        Assert.Equal(7.25, place.Get("y")!.AsFloat);
        Assert.True(place.Get("blob")!.IsNil);
        Assert.Equal(Key(stack), Effects(r, "client.del")[0].Get("name")!.AsString);
        Assert.DoesNotContain(g.Items(g.Player), i => i.Handle == stack);
        Assert.Equal(4L, g.Read(stack, "mudnrof").AsInt); // a proxy held before the drop still reads the object
        Assert.Equal(new[] { "bag" }, g.Items(g.Player).Select(i => i.Name));
        Assert.Equal(tags - 1, g.Ext.Dispatcher.LiveItemTags); // a face on the ground is no item any more
    }

    [Fact]
    public void Drop_Given_a_whole_sack_with_contents_When_dropped_Then_the_tags_of_its_contents_die_with_it_and_the_sacks_own_stays()
    {
        // Arrange
        using var g = new Game();
        ulong bag = g.Items(g.Player).Single(i => i.Name == "bag").Handle;
        ulong inner = g.Items(bag, player: false).Single().Handle; // the banana in the sack gets a tag
        int tags = g.Ext.Dispatcher.LiveItemTags;

        // Act
        ReplyFrame r = g.Drop(bag, 2, 2, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(tags - 2, g.Ext.Dispatcher.LiveItemTags); // the sack leaves the item table (its level object keeps the tag) and the banana's tag is gone
        Assert.Equal(0UL, g.ReadRaw(bag, "mudnrof").Status); // the sack's handle still reads
        Assert.Equal((ulong)Status.BadHandle, g.ReadRaw(inner, "mudnrof").Status); // the banana's handle is dead
    }

    [Fact]
    public void Drop_Given_a_whole_object_When_dropped_Then_the_face_is_a_new_one_never_merged_with_another_object_of_the_square()
    {
        // Arrange
        using var g = new Game(inventory: "arch banana\nnrof 5\nend\n");
        ulong stack = g.Items(g.Player).Single().Handle;

        // Act: the hall has five bananas on (3, 4)
        ReplyFrame r = g.Drop(stack, 3, 4, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Single(Effects(r, "npc.place"));
        Assert.Equal(5L, g.Read(g.Hall[PickupWorld.Banana], "mudnrof").AsInt);
        Assert.Equal(5L, g.Read(stack, "mudnrof").AsInt);
        Assert.NotEqual(stack, g.Hall[PickupWorld.Banana]);
    }

    [Fact]
    public void Drop_Given_a_part_of_a_stack_When_dropped_Then_the_smaller_stack_is_set_then_a_new_face_with_a_new_tag_is_placed()
    {
        // Arrange
        using var g = new Game(inventory: "arch banana\nnrof 6\nend\n");
        ulong stack = g.Items(g.Player).Single().Handle;

        // Act
        ReplyFrame r = g.Drop(stack, 1, 2, 2);

        // Assert
        Assert.True(Result(r));
        List<string> kinds = Kinds(r);
        Assert.True(kinds.IndexOf("client.set") >= 0 && kinds.IndexOf("client.set") < kinds.IndexOf("npc.place"));
        Assert.Equal("bananas,mud_banana.png,6,4,400,0,0", Effects(r, "client.set")[0].Get("value")!.AsString);
        Val place = Assert.Single(Effects(r, "npc.place"));
        ulong part = place.Get("handle")!.AsHandle;
        Assert.NotEqual(stack, part);
        Assert.Equal(2L, g.Read(part, "mudnrof").AsInt);
        Assert.Equal(4L, g.Read(stack, "mudnrof").AsInt);
        Assert.Equal(stack, Assert.Single(g.Items(g.Player)).Handle);
        Assert.Empty(Effects(r, "client.del"));
    }

    [Fact]
    public void Drop_Given_a_number_equal_to_the_stack_or_one_for_a_single_object_When_dropped_Then_the_whole_object_goes()
    {
        // Arrange
        using var g = new Game(inventory: "arch banana\nnrof 3\nend\narch sword\nend\n");
        var items = g.Items(g.Player);
        ulong bananas = items.Single(i => i.Name == "banana").Handle;
        ulong sword = items.Single(i => i.Name == "sword").Handle;

        // Act
        ReplyFrame whole = g.Drop(bananas, 1, 1, 3);
        ReplyFrame single = g.Drop(sword, 1, 1, 1);

        // Assert
        Assert.True(Result(whole));
        Assert.True(Result(single));
        Assert.Equal(bananas, Effects(whole, "npc.place").Single().Get("handle")!.AsHandle);
        Assert.Equal(sword, Effects(single, "npc.place").Single().Get("handle")!.AsHandle);
        Assert.Empty(g.Items(g.Player));
    }

    [Fact]
    public void Drop_Given_more_than_the_stack_has_When_dropped_Then_false_with_the_text_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(inventory: "arch banana\nnrof 3\nend\narch sword\nend\n");
        var items = g.Items(g.Player);

        // Act
        ReplyFrame stack = g.Drop(items.Single(i => i.Name == "banana").Handle, 1, 1, 4);
        ReplyFrame single = g.Drop(items.Single(i => i.Name == "sword").Handle, 1, 1, 2);

        // Assert
        Assert.False(Result(stack));
        Assert.Equal(new[] { "There are only 3 banana" + "s." }, Lines(stack));
        Assert.False(Result(single));
        Assert.Equal(new[] { "There are only 1 swords." }, Lines(single));
        Assert.Empty(Effects(stack, "npc.place"));
        Assert.Equal(2, g.Items(g.Player).Count);
    }

    [Fact]
    public void Drop_Given_a_negative_number_When_dropped_Then_false_and_nothing_changes()
    {
        // Arrange
        using var g = new Game();
        ulong stack = g.Items(g.Player).First().Handle;

        // Act
        ReplyFrame r = g.Drop(stack, 1, 1, -1);

        // Assert
        Assert.False(Result(r));
        Assert.Empty(r.Effects);
        Assert.Equal(2, g.Items(g.Player).Count);
    }

    // ------------------------------------------------------------------ drop: the rules

    [Fact]
    public void Drop_Given_a_player_drops_When_the_face_is_placed_Then_mudowner_is_the_players_handle_while_online_and_the_account_text_in_the_file()
    {
        // Arrange
        using var g = new Game();
        ulong stack = g.Items(g.Player).Single(i => i.Name == "banana").Handle;

        // Act
        g.Drop(stack, 5, 5, 0);
        Val owner = g.Read(stack, "mudowner");
        string level = Say.Flushed(g.Ext, "levels/hall.map", t => t.Contains("mudowner"));

        // Assert
        Assert.Equal(g.Player, owner.AsHandle);
        Assert.Contains("mudowner bob", level);
    }

    [Fact]
    public void Drop_Given_an_object_with_no_drop_When_dropped_Then_false_with_no_text_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(inventory: "arch relic\nend\n");
        ulong relic = g.Items(g.Player).Single().Handle;

        // Act
        ReplyFrame r = g.Drop(relic, 1, 1, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Empty(r.Effects);
        Assert.Empty(Lines(r));
        Assert.Single(g.Items(g.Player));
    }

    [Fact]
    public void Drop_Given_a_level_the_extension_does_not_hold_When_dropped_Then_false_the_text_and_the_object_stays_in_the_inventory()
    {
        // Arrange
        using var g = new Game();
        ulong stack = g.Items(g.Player).Single(i => i.Name == "banana").Handle;

        // Act
        ReplyFrame r = g.Drop(stack, 1, 1, 0, level: "a group level");

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "You can't drop that here." }, Lines(r));
        Assert.Empty(Effects(r, "npc.place"));
        Assert.Empty(Effects(r, "client.del"));
        Assert.Equal(2, g.Items(g.Player).Count);
        Assert.False(File.Exists(ObjectWorld.Data(g.Ext.World, "levels/a%20group%20level.map")));
    }

    [Fact]
    public void Drop_Given_a_request_with_no_level_When_dropped_Then_false_and_the_text()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.Drop(g.Items(g.Player).First().Handle, 1, 1, 0, level: null);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "You can't drop that here." }, Lines(r));
    }

    [Fact]
    public void Drop_Given_a_level_that_is_full_When_dropped_Then_false_the_text_and_nothing_changes()
    {
        // Arrange: the hall already holds nine objects
        using var g = new Game(options: PickupWorld.Options(gk: new GkSettings { MaxObjectsPerLevel = 9 }));
        ulong stack = g.Items(g.Player).First().Handle;

        // Act
        ReplyFrame r = g.Drop(stack, 1, 1, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "You can't drop that here." }, Lines(r));
        Assert.Equal(2, g.Items(g.Player).Count);
    }

    [Fact]
    public void Drop_Given_a_starting_item_When_dropped_Then_the_gods_take_it_back_and_nothing_lies_on_the_ground()
    {
        // Arrange
        using var g = new Game(inventory: "arch loaner\nend\n");
        ulong loaner = g.Items(g.Player).Single().Handle;

        // Act
        ReplyFrame r = g.Drop(loaner, 1, 1, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(new[] { "You drop the loaner.", "The gods who lent it to you retrieves it." }, Lines(r));
        Assert.Empty(Effects(r, "npc.place"));
        Assert.Equal(Key(loaner), Effects(r, "client.del").Single().Get("name")!.AsString);
        Assert.Empty(g.Items(g.Player));
        Assert.Equal((ulong)Status.BadHandle, g.ReadRaw(loaner, "mudrealname").Status);
    }

    [Fact]
    public void Drop_Given_a_part_of_a_starting_stack_When_dropped_Then_the_gods_take_the_part_and_the_rest_stays()
    {
        // Arrange
        using var g = new Game(inventory: "arch loaner\nnrof 3\nend\n");
        ulong stack = g.Items(g.Player).Single().Handle;

        // Act
        ReplyFrame r = g.Drop(stack, 1, 1, 1);

        // Assert
        Assert.True(Result(r));
        Assert.Contains("The gods who lent it to you retrieves it.", Lines(r));
        Assert.Empty(Effects(r, "npc.place"));
        Assert.Equal(2L, g.Read(stack, "mudnrof").AsInt);
    }

    [Fact]
    public void Drop_Given_an_applied_object_When_no_apply_code_is_set_Then_it_stays_and_when_it_unapplies_Then_it_goes()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\napplied 1\nend\n");
        ulong sword = g.Items(g.Player).Single().Handle;
        var asked = new List<string>();

        // Act
        ReplyFrame without = g.Drop(sword, 1, 1, 0);
        g.Ext.Dispatcher.ApplySpecialUnapply = (op, tmp) =>
        {
            asked.Add(op.Name + ":" + tmp.Name);
            return 1;
        };
        ReplyFrame cannot = g.Drop(sword, 1, 1, 0);
        g.Ext.Dispatcher.ApplySpecialUnapply = (op, tmp) =>
        {
            tmp.ClearFlag(Define.FLAG_APPLIED);
            return 0;
        };
        ReplyFrame can = g.Drop(sword, 1, 1, 0);

        // Assert
        Assert.False(Result(without));
        Assert.False(Result(cannot));
        Assert.Equal(new[] { "bob:sword" }, asked.Select(a => a.Replace("human", "bob")));
        Assert.True(Result(can));
        Assert.Single(Effects(can, "npc.place"));
    }

    [Fact]
    public void Drop_Given_an_id_that_is_not_an_item_of_the_players_own_tree_When_dropped_Then_false_and_nothing_changes()
    {
        // Arrange
        using var g = new Game();
        g.Ext.Call(5, new[] { S("amy") }, player: "amy");
        ReplyFrame add = g.Ext.Call(ItemWorld.OpDo, new[] { S("add"), S("amy"), S("sword"), Val.Int(1) }, player: "amy");
        long amysTag = add.Result.AsInt;

        // Act
        ReplyFrame other = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(amysTag), N(1), N(1), Val.Int(0) }, level: "hall", player: "bob");
        ReplyFrame ground = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int((uint)g.Hall[PickupWorld.Sword]), N(1), N(1), Val.Int(0) }, level: "hall", player: "bob");
        ReplyFrame dead = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(987654), N(1), N(1), Val.Int(0) }, level: "hall", player: "bob");
        ReplyFrame zero = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(0), N(1), N(1), Val.Int(0) }, level: "hall", player: "bob");

        // Assert
        Assert.False(Result(other));
        Assert.False(Result(ground));
        Assert.False(Result(dead));
        Assert.False(Result(zero));
        Assert.Equal(1, Effects(add, "client.set").Count(e => e.Get("name")!.AsString.StartsWith("clientr.mud_item", StringComparison.Ordinal)));
    }

    [Fact]
    public void Drop_Given_the_wrong_arguments_or_no_player_When_called_Then_bad_args_or_false()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame few = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(1) }, level: "hall", player: "bob");
        ReplyFrame text = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { S("x"), N(1), N(1), Val.Int(0) }, level: "hall", player: "bob");
        ReplyFrame nobody = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(1), N(1), N(1), Val.Int(0) }, level: "hall");

        // Assert
        Assert.Equal((ulong)Status.BadArgs, few.Status);
        Assert.Equal((ulong)Status.BadArgs, text.Status);
        Assert.False(Result(nobody));
        Assert.Contains(g.Ext.Logs, l => l.Text.Contains("muddropinvobject: the call has no logged-in player"));
    }

    [Fact]
    public void Drop_Given_the_extension_is_busy_When_called_Then_busy_before_anything_changes()
    {
        // Arrange
        using var g = new Game();
        ulong stack = g.Items(g.Player).First().Handle;
        g.Ext.Dispatcher.Busy = "recovering";

        // Act
        ReplyFrame r = g.Drop(stack, 1, 1, 0);
        g.Ext.Dispatcher.Busy = null;

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.Empty(r.Effects);
        Assert.Equal(2, g.Items(g.Player).Count);
    }

    [Fact]
    public void Drop_Given_the_function_When_the_manifest_is_built_Then_it_is_slow_and_the_methods_are_declared_on_TMudObject()
    {
        // Arrange / Act
        ManifestBuilder manifest = Manifest.Collect(1);
        FunctionDef drop = manifest.FindFunction(Manifest.OpMudDropInvObject)!;

        // Assert
        Assert.Equal("muddropinvobject", drop.Name);
        Assert.True(drop.Slow);
        Assert.Equal(new[] { "int", "float", "float", "int" }, drop.Params);
        Assert.Equal("bool", drop.Ret);
        Assert.Equal(new[] { "mudpickupobject", "mudpickupobject2" }, TMudObjectMembers.Table.Methods.Where(m => m.Id is 4000 or 4001).Select(m => m.Name));
    }

    [Fact]
    public void Drop_Given_an_item_inside_a_sack_When_dropped_Then_it_leaves_the_sack_and_lies_on_the_ground()
    {
        // Arrange
        using var g = new Game();
        ulong bag = g.Items(g.Player).Single(i => i.Name == "bag").Handle;
        ulong inside = g.Items(bag, player: false).Single().Handle;

        // Act
        ReplyFrame r = g.Drop(inside, 2, 2, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(inside, Effects(r, "npc.place").Single().Get("handle")!.AsHandle);
        Assert.Empty(g.Items(bag, player: false));
    }

    // ------------------------------------------------------------------ drop: gmaps

    [Fact]
    public void Drop_Given_a_script_on_a_gmap_When_dropped_Then_the_face_is_placed_in_the_segment_with_the_position_in_it()
    {
        // Arrange
        using var g = new Game();
        Assert.Equal(0UL, g.Ext.Call(17, Atlas.Layout()).Status);
        g.Ext.Call(Manifest.OpMudAddObject, new[] { N(70), N(10), S("banana") }, level: "world", player: "bob"); // the segment GB.nw is held now
        ulong stack = g.Items(g.Player).First().Handle;

        // Act
        ReplyFrame r = g.Drop(stack, 70, 10, 0, level: "world");

        // Assert
        Assert.True(Result(r));
        Val place = Assert.Single(Effects(r, "npc.place"));
        Assert.Equal("gb.nw", place.Get("level")!.AsString);
        Assert.Equal(6.0, place.Get("x")!.AsFloat);
        Assert.Equal(10.0, place.Get("y")!.AsFloat);
    }

    [Fact]
    public void Drop_Given_a_position_in_no_segment_or_in_a_segment_that_is_not_held_When_dropped_Then_false_and_the_text()
    {
        // Arrange
        using var g = new Game();
        Assert.Equal(0UL, g.Ext.Call(17, Atlas.Layout()).Status);
        ulong stack = g.Items(g.Player).First().Handle;

        // Act
        ReplyFrame nowhere = g.Drop(stack, 140, 10, 0, level: "world"); // the empty cell
        ReplyFrame unheld = g.Drop(stack, 10, 10, 0, level: "world"); // ga.nw was never loaded

        // Assert
        Assert.False(Result(nowhere));
        Assert.False(Result(unheld));
        Assert.Equal(new[] { "You can't drop that here." }, Lines(nowhere));
        Assert.Equal(new[] { "You can't drop that here." }, Lines(unheld));
        Assert.Equal(2, g.Items(g.Player).Count);
    }

    // ------------------------------------------------------------------ the write order across files

    private static (DirectExt Ext, List<string> Order) WriteOrderGame(string inventory, out Game game)
    {
        var order = new List<string>();
        var writer = new FileWriterOptions
        {
            Quiet = TimeSpan.FromHours(1),
            MaxDelay = TimeSpan.FromHours(1),
            BetweenMove = (source, destination) => order.Add(source + ">" + destination),
        };
        game = new Game(inventory, options: PickupWorld.Options(writer));
        return (game.Ext, order);
    }

    private static string ReadData(DirectExt ext, string rel)
    {
        string path = ObjectWorld.Data(ext.World, rel);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path, System.Text.Encoding.Latin1) : "";
        }
        catch (IOException)
        {
            return "";
        }
    }

    private static bool WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 500 && !condition(); i++)
        {
            Thread.Sleep(10);
        }
        return condition();
    }

    [Fact]
    public void PickUp_Given_a_move_from_the_level_to_the_account_When_it_is_recorded_Then_the_level_is_written_first_and_the_account_second_at_once()
    {
        // Arrange
        (DirectExt ext, List<string> order) = WriteOrderGame("", out Game g);
        using Game game = g;

        // Act
        Assert.True(Result(game.Pick(game.Hall[PickupWorld.Sword], 1)));
        bool written = WaitFor(() => order.Count > 0 && ReadData(ext, "accounts/bob.txt").Contains("arch sword"));

        // Assert: a quiet time of an hour: the move was written at once, the source file in front
        Assert.True(written);
        Assert.Equal(new[] { "levels/hall.map>accounts/bob.txt" }, order);
        Assert.Contains("arch sword", File.ReadAllText(ObjectWorld.Data(ext.World, "accounts/bob.txt")));
        Assert.Equal(8, File.ReadAllText(ObjectWorld.Data(ext.World, "levels/hall.map")).Split('\n').Count(l => l.StartsWith("arch ", StringComparison.Ordinal)) - 1);
    }

    [Fact]
    public void Drop_Given_a_move_from_the_account_to_the_level_When_it_is_recorded_Then_the_account_is_written_first_and_the_level_second_at_once()
    {
        // Arrange
        (DirectExt ext, List<string> order) = WriteOrderGame("arch sword\nend\n", out Game g);
        using Game game = g;
        ulong sword = game.Items(game.Player).Single().Handle;

        // Act
        Assert.True(Result(game.Drop(sword, 8, 8, 0)));
        bool written = WaitFor(() => order.Count > 0 && File.Exists(ObjectWorld.Data(ext.World, "levels/hall.map")));

        // Assert
        Assert.True(written);
        Assert.Equal(new[] { "accounts/bob.txt>levels/hall.map" }, order);
        Assert.DoesNotContain("arch sword", File.ReadAllText(ObjectWorld.Data(ext.World, "accounts/bob.txt")));
        Assert.Equal(10, File.ReadAllText(ObjectWorld.Data(ext.World, "levels/hall.map")).Split('\n').Count(l => l.StartsWith("arch ", StringComparison.Ordinal)) - 1);
    }

    [Fact]
    public void PickUp_Given_an_object_made_and_picked_up_in_one_statement_When_it_is_recorded_Then_the_level_is_the_source_and_never_has_the_object()
    {
        // Arrange
        (DirectExt ext, List<string> order) = WriteOrderGame("", out Game g);
        using Game game = g;
        ulong made = Ok(ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("sword") }, level: "hall", player: "bob"));

        // Act
        ReplyFrame r = game.Pick(made, 1);
        bool written = WaitFor(() => order.Count > 0 && ReadData(ext, "accounts/bob.txt").Contains("arch sword"));

        // Assert
        Assert.True(Result(r));
        Assert.Equal("npc.remove", Kinds(r)[0]);
        Assert.True(written);
        Assert.Equal(new[] { "levels/hall.map>accounts/bob.txt" }, order);
        Assert.Equal(9, File.ReadAllText(ObjectWorld.Data(ext.World, "levels/hall.map")).Split('\n').Count(l => l.StartsWith("arch ", StringComparison.Ordinal)) - 1);
        Assert.Contains("arch sword", File.ReadAllText(ObjectWorld.Data(ext.World, "accounts/bob.txt")));
    }

    [Fact]
    public void PickUp_Given_a_part_of_a_stack_When_it_is_recorded_Then_both_files_go_in_one_move_with_the_smaller_stack_in_the_level()
    {
        // Arrange
        (DirectExt ext, List<string> order) = WriteOrderGame("", out Game g);
        using Game game = g;

        // Act
        Assert.True(Result(game.Pick(game.Hall[PickupWorld.Banana], 2)));
        bool written = WaitFor(() => order.Count > 0 && ReadData(ext, "accounts/bob.txt").Contains("nrof 2"));

        // Assert
        Assert.True(written);
        Assert.Equal(new[] { "levels/hall.map>accounts/bob.txt" }, order);
        Assert.Contains("nrof 3", File.ReadAllText(ObjectWorld.Data(ext.World, "levels/hall.map")));
        Assert.Contains("nrof 2", File.ReadAllText(ObjectWorld.Data(ext.World, "accounts/bob.txt")));
    }

    [Fact]
    public void PickUp_Given_a_refused_pick_up_or_drop_When_it_is_recorded_Then_nothing_is_written_at_all()
    {
        // Arrange
        (DirectExt ext, List<string> order) = WriteOrderGame("arch relic\nend\n", out Game g);
        using Game game = g;

        // Act
        game.Pick(game.Hall[PickupWorld.Anvil], 1);
        game.Pick(game.Hall[PickupWorld.Statue], 1);
        game.Drop(game.Items(game.Player).Single().Handle, 1, 1, 0);
        Thread.Sleep(200);

        // Assert
        Assert.Empty(order);
        Assert.False(File.Exists(ObjectWorld.Data(ext.World, "levels/hall.map")));
    }

    // ------------------------------------------------------------------ a 1.0 link

    [Fact]
    public void PickUp_Given_a_1_0_link_When_the_player_picks_up_and_drops_Then_the_text_is_a_client_text_effect_and_no_fallback_or_item_event_is_sent()
    {
        // Arrange
        using var g = new Game(inventory: "arch banana\nnrof 6\nend\n", protoMinor: 0);
        ulong sword = g.Hall[PickupWorld.Sword];

        // Act
        ReplyFrame pick = g.Pick(sword, 1);
        ReplyFrame part = g.Pick(g.Hall[PickupWorld.Banana], 2);
        ReplyFrame drop = g.Drop(sword, 1, 1, 0);
        ReplyFrame heavy = g.Pick(g.Hall[PickupWorld.Anvil], 1);

        // Assert
        Assert.True(Result(pick));
        Assert.Equal(new[] { "You pick up the swords." }, Effects(pick, "client.text").Select(e => e.Get("text")!.AsString));
        Assert.Empty(pick.Events);
        Assert.Equal(new[] { "onUpdateObject" }, part.Events.Select(e => e.Get("name")!.AsString)); // the 1.0 event, no params and no fallback
        Assert.DoesNotContain(part.Events, e => e.Get("fallback") != null);
        Assert.True(Result(drop));
        Assert.Single(Effects(drop, "npc.place"));
        Assert.Empty(drop.Events);
        Assert.Equal(new[] { "That item is too heavy for you to put it in your inventory." }, Effects(heavy, "client.text").Select(e => e.Get("text")!.AsString));
    }

    // ------------------------------------------------------------------ the commands

    private static FakeHost Feet(Game g, string level = "hall", double x = 3.5, double y = 4.5)
    {
        var host = new FakeHost();
        host.Answer = (name, _) => name == "player.pos" ? new QueryAnswer(Status.Ok, Val.List(S(level), N(x), N(y))) : new QueryAnswer(Status.Ok, Val.List());
        g.Ext.Dispatcher.QuerySource = host.Ask;
        return host;
    }

    [Fact]
    public void CommandDrop_Given_a_name_When_dropped_Then_the_player_position_is_asked_once_and_the_face_is_at_his_feet()
    {
        // Arrange
        using var g = new Game(inventory: "arch banana\nnrof 2\nend\narch sword\nend\n");
        FakeHost host = Feet(g);

        // Act
        ReplyFrame r = g.Command("drop", "sword,banana");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Single(host.Asked);
        Assert.Equal("player.pos", host.Asked[0].Name);
        Assert.Equal("bob", host.Asked[0].Args[0].AsString);
        Assert.Equal(2, Effects(r, "npc.place").Count);
        Assert.All(Effects(r, "npc.place"), p =>
        {
            Assert.Equal("hall", p.Get("level")!.AsString);
            Assert.Equal(3.5, p.Get("x")!.AsFloat);
            Assert.Equal(4.5, p.Get("y")!.AsFloat);
        });
        Assert.Empty(g.Items(g.Player));
    }

    [Fact]
    public void CommandDrop_Given_a_count_in_front_of_the_name_When_dropped_Then_only_that_many_go()
    {
        // Arrange
        using var g = new Game(inventory: "arch banana\nnrof 6\nend\n");
        Feet(g);

        // Act
        ReplyFrame r = g.Command("drop", "2 banana");

        // Assert
        Assert.Equal(0UL, r.Status);
        Val place = Assert.Single(Effects(r, "npc.place"));
        Assert.Equal(2L, g.Read(place.Get("handle")!.AsHandle, "mudnrof").AsInt);
        Assert.Equal(4L, g.Read(g.Items(g.Player).Single().Handle, "mudnrof").AsInt);
    }

    [Fact]
    public void CommandDrop_Given_no_name_or_no_match_or_only_a_locked_item_When_run_Then_the_texts_of_C_and_no_query()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\ninv_locked 1\nend\narch relic\nend\n");
        FakeHost host = Feet(g);

        // Act
        ReplyFrame nothing = g.Command("drop", null);
        ReplyFrame empty = g.Command("drop", "");
        ReplyFrame nomatch = g.Command("drop", "nothing like this");
        ReplyFrame locked = g.Command("drop", "sword");
        ReplyFrame relic = g.Command("drop", "relic");

        // Assert
        Assert.Equal(new[] { "Drop what?" }, Lines(nothing));
        Assert.Equal(new[] { "Drop what?" }, Lines(empty));
        Assert.Equal(new[] { "Nothing to drop." }, Lines(nomatch));
        Assert.Equal(new[] { "This item is locked" }, Lines(locked));
        Assert.Equal(new[] { "Nothing to drop." }, Lines(relic)); // an object with no drop is skipped
        Assert.DoesNotContain(host.Asked, a => a.Name == "player.pos");
        Assert.Equal(2, g.Items(g.Player).Count);
    }

    [Fact]
    public void CommandDrop_Given_a_1_1_link_and_the_player_on_a_gmap_When_dropped_Then_the_position_is_translated_and_the_face_is_in_the_segment()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\nend\n");
        Assert.Equal(0UL, g.Ext.Call(17, Atlas.Layout()).Status);
        g.Ext.Call(Manifest.OpMudAddObject, new[] { N(70), N(10), S("banana") }, level: "world", player: "bob"); // gb.nw is held
        FakeHost host = Feet(g, "GB.nw", 6, 10); // the host names the segment; the script is on the gmap

        // Act
        ReplyFrame r = g.Command("drop", "sword", level: "world");

        // Assert
        Assert.Equal(0UL, r.Status);
        Val place = Assert.Single(Effects(r, "npc.place"));
        Assert.Equal("gb.nw", place.Get("level")!.AsString);
        Assert.Equal(6.0, place.Get("x")!.AsFloat);
        Assert.Equal(10.0, place.Get("y")!.AsFloat);
        Assert.Single(host.Asked);
    }

    [Fact]
    public void CommandDrop_Given_a_host_that_has_no_position_or_a_level_not_held_When_dropped_Then_the_text_once_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\nend\narch banana\nend\n");
        FakeHost host = Feet(g, "a group level");

        // Act
        ReplyFrame unheld = g.Command("drop", "all");
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, Val.Nil);
        ReplyFrame nil = g.Command("drop", "all");

        // Assert
        Assert.Equal(0UL, unheld.Status);
        Assert.Equal(0UL, nil.Status);
        Assert.Equal(2, Lines(unheld).Count); // one text for each of the two items: C says it for each drop
        Assert.All(Lines(unheld).Concat(Lines(nil)), l => Assert.Equal("You can't drop that here.", l));
        Assert.Empty(Effects(unheld, "npc.place"));
        Assert.Equal(2, g.Items(g.Player).Count);
    }

    [Fact]
    public void CommandDrop_Given_a_query_that_fails_When_dropped_Then_the_request_answers_with_the_hosts_status_and_nothing_changed()
    {
        // Arrange: no query source: the host cannot be asked
        using var g = new Game(inventory: "arch sword\nend\n");

        // Act
        ReplyFrame r = g.Command("drop", "sword");

        // Assert
        Assert.Equal((ulong)Status.Unavailable, r.Status);
        Assert.Empty(r.Effects);
        Assert.Single(g.Items(g.Player));
    }

    [Fact]
    public void CommandDrop_Given_the_extension_is_busy_When_run_Then_busy_and_no_query()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\nend\n");
        FakeHost host = Feet(g);
        g.Ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame r = g.Command("drop", "sword");
        ReplyFrame take = g.Command("take", null);
        ReplyFrame all = g.Command("dropall", null);
        g.Ext.Dispatcher.Busy = null;

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.Equal((ulong)Status.Busy, take.Status);
        Assert.Equal((ulong)Status.Busy, all.Status);
        Assert.Empty(host.Asked);
        Assert.Single(g.Items(g.Player));
    }

    private const string Everything =
        "arch banana\nend\narch sword\nend\narch mail\nend\narch goldcoin\nnrof 3\nend\narch skeleton\nend\narch bow\nend\narch relic\nend\n"
        + "arch sword\ninv_locked 1\nend\narch tool\nend\n";

    [Fact]
    public void CommandDropAll_Given_no_parameters_When_run_Then_everything_but_money_food_keys_gems_and_locked_goes()
    {
        // Arrange
        using var g = new Game(inventory: Everything);
        Feet(g);

        // Act
        ReplyFrame r = g.Command("dropall", null);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(new[] { "sword", "mail", "bow", "tool" }.Order(), Effects(r, "npc.place").Select(p => g.Read(p.Get("handle")!.AsHandle, "mudrealname").AsString).Order());
        Assert.Equal(new[] { "banana", "gold coin", "skeleton key", "relic", "sword" }.Order(), g.Items(g.Player).Select(i => i.Name).Order());
    }

    [Theory]
    [InlineData("weapons", new[] { "sword", "bow" })]
    [InlineData("armor", new[] { "mail" })]
    [InlineData("armour", new[] { "mail" })]
    [InlineData("nothing", new string[0])]
    public void CommandDropAll_Given_a_kind_When_run_Then_only_that_kind_goes(string kind, string[] dropped)
    {
        // Arrange
        using var g = new Game(inventory: Everything);
        Feet(g);

        // Act
        ReplyFrame r = g.Command("dropall", kind);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(dropped.Order(), Effects(r, "npc.place").Select(p => g.Read(p.Get("handle")!.AsHandle, "mudrealname").AsString).Order());
    }

    [Fact]
    public void CommandDropAll_Given_misc_When_run_Then_the_listed_types_that_are_not_applied_go_and_nothing_else()
    {
        // Arrange: C's list has rings and potions, not a sword, a banana (food), a helmet (armour) or a tool; the applied ring stays
        using var g = new Game(inventory: "arch ring\nend\narch potion\nend\narch ring\napplied 1\nend\narch sword\nend\narch banana\nend\narch helmet\nend\narch tool\nend\n");
        Feet(g);

        // Act
        ReplyFrame r = g.Command("dropall", "misc");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(new[] { "potion", "ring" }, Effects(r, "npc.place").Select(p => g.Read(p.Get("handle")!.AsHandle, "mudrealname").AsString).Order());
        Assert.Equal(new[] { "banana", "helmet", "ring", "sword", "tool" }, g.Items(g.Player).Select(i => i.Name).Order());
    }

    [Fact]
    public void CommandDropAll_Given_armor_and_weapons_When_run_Then_helmets_shields_and_arrows_are_on_the_lists_too()
    {
        // Arrange
        using var g = new Game(inventory: "arch helmet\nend\narch arrow\nnrof 3\nend\narch ring\nend\n");
        Feet(g);

        // Act
        ReplyFrame armor = g.Command("dropall", "armor");
        ReplyFrame weapons = g.Command("dropall", "weapons");

        // Assert
        Assert.Equal(new[] { "helmet" }, Effects(armor, "npc.place").Select(p => g.Read(p.Get("handle")!.AsHandle, "mudrealname").AsString));
        Assert.Equal(new[] { "arrow" }, Effects(weapons, "npc.place").Select(p => g.Read(p.Get("handle")!.AsHandle, "mudrealname").AsString));
        Assert.Equal(new[] { "ring" }, g.Items(g.Player).Select(i => i.Name));
    }

    [Fact]
    public void CommandDropAll_Given_an_empty_inventory_When_run_Then_nothing_to_drop()
    {
        // Arrange
        using var g = new Game(inventory: "");
        FakeHost host = Feet(g);

        // Act
        ReplyFrame r = g.Command("dropall", null);

        // Assert
        Assert.Equal(new[] { "Nothing to drop!" }, Lines(r));
        Assert.Empty(host.Asked);
    }

    [Fact]
    public void CommandDropAll_Given_an_invisible_object_in_a_sack_that_is_not_the_players_When_dropped_Then_it_is_thrown_away()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\nend\n");
        Feet(g);

        // Act
        ReplyFrame r = g.Command("dropall", null);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Single(Effects(r, "npc.place"));
    }

    private static ulong Made(Game g, string arch, double x, double y, string level = "hall") =>
        Ok(g.Ext.Call(Manifest.OpMudAddObject, new[] { N(x), N(y), S(arch) }, level: level, player: "bob"));

    [Fact]
    public void CommandTake_Given_objects_on_the_square_When_taken_without_parameters_Then_the_last_placed_that_can_be_picked_up_is_taken_and_the_rest_stay()
    {
        // Arrange
        using var g = new Game(inventory: "");
        ulong banana = Made(g, "banana", 11.2, 11.7);
        ulong sword = Made(g, "sword", 11.9, 11.1);
        Feet(g, x: 11.5, y: 11.5);

        // Act
        ReplyFrame r = g.Command("take", null);

        // Assert: the sword was placed last, so it is the one on top; one is taken, and the loop stops
        Assert.Equal(0UL, r.Status);
        Assert.Equal(sword, Assert.Single(Effects(r, "npc.remove")).Get("handle")!.AsHandle);
        Assert.Equal("banana", g.Read(banana, "mudrealname").AsString);
        Assert.Equal(new[] { "sword" }, g.Items(g.Player).Select(i => i.Name));
    }

    [Fact]
    public void CommandTake_Given_the_object_on_top_cannot_be_dropped_When_taken_without_parameters_Then_nothing_is_taken_and_the_loop_stops_there_as_in_C()
    {
        // Arrange: on (3, 4) the relic is the last object that can_pick, and C stops after the first pick_up whatever came of it
        using var g = new Game(inventory: "");
        Feet(g, x: 3.5, y: 4.5);

        // Act
        ReplyFrame r = g.Command("take", null);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Empty(Effects(r, "npc.remove"));
        Assert.Empty(Lines(r));
        Assert.Empty(g.Items(g.Player));
    }

    [Fact]
    public void CommandTake_Given_a_name_When_taken_Then_every_object_of_the_square_that_matches_is_taken_and_C_lets_an_unpaid_one_go_with_its_price()
    {
        // Arrange
        using var g = new Game(inventory: "");
        Feet(g);

        // Act
        ReplyFrame r = g.Command("take", "sword,banana");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(
            new[] { g.Hall[PickupWorld.Banana], g.Hall[PickupWorld.Sword], g.Hall[PickupWorld.UnpaidSword] }.Order(),
            Effects(r, "npc.remove").Select(e => e.Get("handle")!.AsHandle).Order());
        Assert.Contains(Lines(r), l => l.Contains("will cost you"));
        Assert.Equal("sword", g.Read(g.Hall[PickupWorld.FarSword], "mudrealname").AsString); // the sword of another square stays
        Assert.Equal(3, g.Items(g.Player).Count);
    }

    [Fact]
    public void CommandTake_Given_an_empty_square_or_nothing_that_can_be_taken_When_run_Then_the_texts_of_C()
    {
        // Arrange
        using var g = new Game(inventory: "");
        Made(g, "statue", 13.5, 13.5);
        Made(g, "mist", 14.5, 14.5);
        FakeHost host = Feet(g, x: 12.5, y: 12.5);

        // Act
        ReplyFrame empty = g.Command("take", null);
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, Val.List(S("hall"), N(13.5), N(13.5)));
        ReplyFrame statue = g.Command("take", null);
        ReplyFrame named = g.Command("take", "statue");
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, Val.List(S("hall"), N(14.5), N(14.5)));
        ReplyFrame invisible = g.Command("take", null);

        // Assert
        Assert.Equal(new[] { "Nothing to take!" }, Lines(empty));
        Assert.Equal(new[] { "You can't pick up a statue." }, Lines(statue));
        Assert.Equal(new[] { "You can't pick up the statue." }, Lines(named));
        Assert.Equal(new[] { "There is nothing to pick up." }, Lines(invisible));
    }

    [Fact]
    public void CommandTake_Given_a_player_on_another_level_or_unknown_When_run_Then_nothing_to_take_and_a_failing_query_answers_with_its_status()
    {
        // Arrange
        using var g = new Game(inventory: "");
        FakeHost host = Feet(g, "a group level");

        // Act
        ReplyFrame other = g.Command("take", null);
        host.Answer = (_, _) => new QueryAnswer(Status.Unavailable, S("the player is gone"));
        ReplyFrame failed = g.Command("take", null);

        // Assert
        Assert.Equal(new[] { "Nothing to take!" }, Lines(other));
        Assert.Equal((ulong)Status.Unavailable, failed.Status);
    }

    [Fact]
    public void CommandTake_Given_an_open_container_When_taken_Then_the_contents_of_the_container_are_taken_not_the_ground()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\napplied 1\narch sword\nend\nend\n");
        ulong sack = g.Items(g.Player).Single().Handle;
        FakeHost host = Feet(g);
        Assert.Equal(0UL, g.Ext.Call(ItemWorld.OpDo, new[] { S("open"), S("bob"), Val.Int((uint)sack) }, player: "bob").Status);

        // Act
        ReplyFrame r = g.Command("take", "sword");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Empty(host.Asked); // no position needed
        Assert.Empty(Effects(r, "npc.remove"));
        Assert.Empty(Effects(r, "npc.place"));
        Assert.Contains(new[] { "sack", "sword" }, n => g.Items(g.Player).Any(i => i.Name == n));
    }

    // ------------------------------------------------------------------ the containers pick_up looks for

    [Fact]
    public void PickUp_Given_an_applied_container_of_the_objects_race_When_picked_up_Then_it_goes_into_that_container_first()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\napplied 1\nend\narch fruitbasket\napplied 1\nend\n");
        ulong basket = g.Items(g.Player).Single(i => i.Name == "fruitbasket").Handle;
        ulong made = Ok(g.Ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("fruit") }, level: "hall", player: "bob"));

        // Act
        ReplyFrame r = g.Pick(made, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(made, Assert.Single(g.Items(basket, player: false)).Handle);
    }

    [Fact]
    public void PickUp_Given_only_a_general_applied_container_that_holds_it_When_picked_up_Then_it_goes_into_that_container()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\napplied 1\nend\n");
        ulong sack = g.Items(g.Player).Single().Handle;

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Banana], 1);

        // Assert
        Assert.True(Result(r));
        Assert.Single(g.Items(sack, player: false));
        Assert.DoesNotContain(g.Items(g.Player), i => i.Name == "banana");
    }

    [Fact]
    public void PickUp_Given_a_container_that_is_not_applied_or_does_not_fit_When_picked_up_Then_it_goes_into_the_pack()
    {
        // Arrange: a sack that is not applied, and a basket for fruit only
        using var g = new Game(inventory: "arch sack\nend\narch fruitbasket\napplied 1\nend\n");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Sword], 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(new[] { "sack", "fruitbasket", "sword" }.Order(), g.Items(g.Player).Select(i => i.Name).Order());
    }

    [Fact]
    public void PickUp_Given_a_starting_item_and_a_container_that_holds_it_When_picked_up_Then_it_stays_out_of_the_container()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\napplied 1\nend\n");
        ulong made = Ok(g.Ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("loaner") }, level: "hall", player: "bob"));

        // Act
        ReplyFrame r = g.Pick(made, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "This object cannot be put into containers!" }, Lines(r));
        Assert.Empty(g.Items(g.Items(g.Player).Single().Handle, player: false));
    }

    [Fact]
    public void PickUp_Given_an_open_container_that_cannot_hold_the_object_When_picked_up_Then_false_with_the_reason()
    {
        // Arrange: the open sack is not applied
        using var g = new Game(inventory: "arch sack\nend\n");
        ulong sack = g.Items(g.Player).Single().Handle;
        g.Ext.Call(ItemWorld.OpDo, new[] { S("open"), S("bob"), Val.Int((uint)sack) }, player: "bob");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Banana], 1);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "The sack (open) is not active." }, Lines(r));
        Assert.Equal(5L, g.Read(g.Hall[PickupWorld.Banana], "mudnrof").AsInt);
    }

    [Fact]
    public void PickUp_Given_an_open_applied_container_that_holds_it_When_picked_up_Then_it_goes_into_the_open_container()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\napplied 1\nend\n");
        ulong sack = g.Items(g.Player).Single().Handle;
        g.Ext.Call(ItemWorld.OpDo, new[] { S("open"), S("bob"), Val.Int((uint)sack) }, player: "bob");

        // Act
        ReplyFrame r = g.Pick(g.Hall[PickupWorld.Banana], 1);

        // Assert
        Assert.True(Result(r));
        ulong inside = Assert.Single(g.Items(sack, player: false)).Handle;
        Assert.Equal("1", Sets(r)[Key(inside)].Split(',')[6]); // shown inside the open container
    }

    // ------------------------------------------------------------------ drop() itself

    [Fact]
    public void Drop_Given_an_invisible_object_at_the_head_of_the_pack_When_dropped_Then_the_next_visible_one_goes_and_with_none_the_text()
    {
        // Arrange: the pack is a sword and, in front of it, a mist that is invisible
        using var g = new Game(inventory: "arch sword\nend\n");
        g.Ext.Call(ItemWorld.OpDo, new[] { S("add"), S("bob"), S("mist"), Val.Int(1) }, player: "bob");

        // Act
        ReplyFrame first = g.Direct("head");
        ReplyFrame second = g.Direct("head");

        // Assert
        Assert.True(Result(first));
        Assert.Equal("sword", g.Read(Effects(first, "npc.place").Single().Get("handle")!.AsHandle, "mudrealname").AsString);
        Assert.False(Result(second)); // only the mist is left
        Assert.Equal(new[] { "You don't have anything to drop." }, Lines(second));
    }

    [Fact]
    public void Drop_Given_an_invisible_object_in_a_sack_When_dropped_Then_it_is_thrown_away_and_nothing_lies_on_the_ground()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\narch mist\nend\nend\n");
        ulong sack = g.Items(g.Player).Single().Handle;
        int tags = g.Ext.Dispatcher.LiveItemTags;

        // Act
        ReplyFrame r = g.Direct("sack");

        // Assert
        Assert.False(Result(r));
        Assert.Empty(Effects(r, "npc.place"));
        Assert.Empty(Lines(r));
        Assert.Equal(sack, Assert.Single(g.Items(g.Player)).Handle);
        Assert.True(g.Ext.Dispatcher.LiveItemTags <= tags);
    }

    [Fact]
    public void Drop_Given_the_last_used_object_When_it_is_dropped_Then_the_last_used_one_is_a_neighbour_or_none()
    {
        // Arrange: two items, then one
        using var g = new Game(inventory: "arch sword\nend\narch banana\nend\n");
        var items = g.Items(g.Player);

        // Act
        ReplyFrame first = g.Direct("lastused", (uint)items[0].Handle);
        ReplyFrame second = g.Direct("lastused", (uint)items[1].Handle);

        // Assert
        Assert.Equal(0UL, first.Status);
        Assert.NotEqual(0L, first.Result.AsInt); // the neighbour that was left
        Assert.Equal(0L, second.Result.AsInt); // nothing is left
    }

    [Fact]
    public void Drop_Given_an_open_container_When_the_player_drops_Then_put_object_in_sack_is_asked_and_nothing_goes_to_the_ground_without_it()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\napplied 1\nend\narch sword\nend\n");
        var items = g.Items(g.Player);
        ulong sack = items.Single(i => i.Name == "sack").Handle;
        ulong sword = items.Single(i => i.Name == "sword").Handle;
        g.Ext.Call(ItemWorld.OpDo, new[] { S("open"), S("bob"), Val.Int((uint)sack) }, player: "bob");
        var asked = new List<string>();

        // Act
        ReplyFrame without = g.Direct("top", (uint)sword);
        g.Ext.Dispatcher.PutObjectInSack = (op, container, tmp, nrof) =>
        {
            asked.Add(container.Name + "<-" + tmp.Name + ":" + nrof);
            return true;
        };
        ReplyFrame with = g.Direct("top", (uint)sword);

        // Assert
        Assert.False(Result(without));
        Assert.Empty(Effects(without, "npc.place"));
        Assert.True(Result(with));
        Assert.Equal(new[] { "sack<-sword:0" }, asked);
        Assert.Empty(Effects(with, "npc.place"));
    }

    [Fact]
    public void Drop_Given_a_locked_object_or_one_with_no_drop_When_dropped_by_drop_Then_the_text_or_nothing()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\ninv_locked 1\nend\narch relic\nend\n");
        var items = g.Items(g.Player);

        // Act
        ReplyFrame locked = g.Direct("top", (uint)items.Single(i => i.Name == "sword").Handle);
        ReplyFrame relic = g.Direct("top", (uint)items.Single(i => i.Name == "relic").Handle);

        // Assert
        Assert.False(Result(locked));
        Assert.Equal(new[] { "This item is locked" }, Lines(locked));
        Assert.False(Result(relic));
        Assert.Empty(Lines(relic));
        Assert.Equal(2, g.Items(g.Player).Count);
    }

    [Fact]
    public void PickUp_Given_a_level_object_with_script_state_When_it_is_picked_up_and_dropped_Then_the_new_face_has_no_blob_and_no_flags()
    {
        // Arrange: the host gave the object its script state
        using var g = new Game(inventory: "");
        ulong sword = g.Hall[PickupWorld.Sword];
        Assert.True(Result(g.Pick(sword, 1)));
        Assert.Equal(0UL, g.Ext.Call(16, new[] { Val.List(Val.List(H(sword), N(3), N(4), S("state of the npc"))) }, level: "hall").Status);

        // Act
        ReplyFrame r = g.Drop(sword, 2, 2, 0);

        // Assert
        Val place = Assert.Single(Effects(r, "npc.place"));
        Assert.True(place.Get("blob")!.IsNil);
    }

    [Fact]
    public void CommandTake_Given_an_empty_text_When_taken_Then_it_is_as_no_text_and_the_first_object_that_can_be_picked_up_goes()
    {
        // Arrange
        using var g = new Game(inventory: "");
        ulong sword = Made(g, "sword", 12.5, 12.5);
        Feet(g, x: 12.5, y: 12.5);

        // Act
        ReplyFrame r = g.Command("take", "");

        // Assert
        Assert.Equal(sword, Assert.Single(Effects(r, "npc.remove")).Get("handle")!.AsHandle);
    }

    [Fact]
    public void CommandDrop_Given_an_answer_that_is_no_position_When_dropped_Then_the_text_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\nend\n");
        FakeHost host = Feet(g);
        host.Answer = (_, _) => new QueryAnswer(Status.Ok, S("somewhere"));

        // Act
        ReplyFrame r = g.Command("drop", "sword");

        // Assert
        Assert.Equal(new[] { "You can't drop that here." }, Lines(r));
        Assert.Single(g.Items(g.Player));
    }

    [Fact]
    public void PickUp_Given_a_sack_with_a_heavy_content_When_picked_up_Then_the_weight_of_the_contents_counts()
    {
        // Arrange: the sack weighs 200 g and holds an anvil of 500,000 g
        using var g = new Game(inventory: "");
        ulong sack = Say.LoadAll(g.Ext, "vault").Places.Single().Get("handle")!.AsHandle;

        // Act
        ReplyFrame r = g.Ext.Call(13, new[] { H(sack), Val.Int(TMudObjectMembers.MethodMudPickUpObject), Val.List(Val.Int(1)) }, level: "vault", player: "bob");

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "That item is too heavy for you to put it in your inventory." }, Lines(r));
    }

    [Fact]
    public void PickUp_Given_an_item_in_an_open_container_When_picked_up_Then_it_goes_into_the_pack_not_back_into_the_container()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\napplied 1\narch banana\nend\nend\n");
        ulong sack = g.Items(g.Player).Single().Handle;
        ulong inside = g.Items(sack, player: false).Single().Handle;
        g.Ext.Call(ItemWorld.OpDo, new[] { S("open"), S("bob"), Val.Int((uint)sack) }, player: "bob");

        // Act
        ReplyFrame r = g.Pick(inside, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Empty(g.Items(sack, player: false));
        Assert.Contains(g.Items(g.Player), i => i.Handle == inside);
    }

    [Fact]
    public void CommandTake_Given_a_name_When_taken_Then_only_what_matches_goes_and_an_invisible_object_is_never_taken()
    {
        // Arrange
        using var g = new Game(inventory: "");
        ulong mist = Made(g, "mist", 3.5, 4.5);
        Feet(g);

        // Act
        ReplyFrame banana = g.Command("take", "banana");
        ReplyFrame invisible = g.Command("take", "mist");

        // Assert
        Assert.Equal(g.Hall[PickupWorld.Banana], Assert.Single(Effects(banana, "npc.remove")).Get("handle")!.AsHandle);
        Assert.Empty(Effects(invisible, "npc.remove"));
        Assert.Equal("mist", g.Read(mist, "mudrealname").AsString);
    }

    [Fact]
    public void CommandTake_Given_an_object_that_no_level_load_has_shown_When_taken_Then_it_is_not_on_the_square()
    {
        // Arrange: the level "pending" is read for a maker, its sword is not an NPC yet
        using var g = new Game(inventory: "");
        Made(g, "banana", 5, 5, "pending");
        FakeHost host = Feet(g, "pending", 2.5, 2.5);

        // Act
        ReplyFrame r = g.Command("take", null, level: "pending");

        // Assert
        Assert.Equal(new[] { "Nothing to take!" }, Lines(r));
        Assert.Empty(Effects(r, "npc.remove"));
        Assert.Single(host.Asked);
    }

    [Fact]
    public void CommandTake_Given_a_player_on_the_same_column_but_another_row_When_taken_Then_nothing_is_there()
    {
        // Arrange: the hall's objects stand on (3, 4)
        using var g = new Game(inventory: "");
        Feet(g, x: 3.5, y: 7.5);

        // Act
        ReplyFrame r = g.Command("take", null);

        // Assert
        Assert.Equal(new[] { "Nothing to take!" }, Lines(r));
    }

    [Fact]
    public void CommandDropAll_Given_an_object_of_a_kind_it_keeps_at_the_head_of_the_pack_When_run_Then_that_object_stays()
    {
        // Arrange: money, keys, gems, food, an invisible object, a locked sword and an open sack, each in front of a ring that goes (with a container open every drop goes to put_object_in_sack, which is wave 6's: no ring goes to the ground then)
        foreach (string kept in new[] { "goldcoin", "key", "gem", "banana", "mist", "sack", "sword" })
        {
            string extra = kept == "sword" ? "inv_locked 1\n" : kept == "sack" ? "applied 1\n" : "";
            using var g = new Game(inventory: "arch " + kept + "\n" + extra + "end\narch ring\nend\n");
            Feet(g);
            if (kept == "sack")
            {
                g.Ext.Call(ItemWorld.OpDo, new[] { S("open"), S("bob"), Val.Int((uint)g.Items(g.Player).Single(i => i.Name.StartsWith("sack", StringComparison.Ordinal)).Handle) }, player: "bob");
            }

            // Act
            ReplyFrame r = g.Command("dropall", null);

            // Assert
            Assert.Equal(0UL, r.Status);
            Assert.True((kept == "sack" ? Array.Empty<string>() : new[] { "ring" }).SequenceEqual(Effects(r, "npc.place").Select(p => g.Read(p.Get("handle")!.AsHandle, "mudrealname").AsString)), kept);
            Assert.True(Lines(r).Count == 0, kept + ": " + string.Join("|", Lines(r))); // nothing said about what stayed
        }
    }

    [Fact]
    public void CommandDropAll_Given_an_open_container_When_run_Then_the_container_itself_is_not_put_into_itself_but_the_rest_is_offered_to_it()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\napplied 1\nend\narch ring\nend\n");
        Feet(g);
        ulong sack = g.Items(g.Player).Single(i => i.Name.StartsWith("sack", StringComparison.Ordinal)).Handle;
        g.Ext.Call(ItemWorld.OpDo, new[] { S("open"), S("bob"), Val.Int((uint)sack) }, player: "bob");
        var offered = new List<string>();
        g.Ext.Dispatcher.PutObjectInSack = (op, container, tmp, nrof) =>
        {
            offered.Add(tmp.Name ?? "");
            return true;
        };

        // Act
        ReplyFrame r = g.Command("dropall", null);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(new[] { "ring" }, offered);
    }

    [Fact]
    public void CommandDropAll_Given_an_applied_ring_When_misc_is_dropped_and_apply_works_Then_the_applied_one_stays()
    {
        // Arrange
        using var g = new Game(inventory: "arch ring\napplied 1\nend\narch ring\nend\n");
        Feet(g);
        g.Ext.Dispatcher.ApplySpecialUnapply = (op, tmp) =>
        {
            tmp.ClearFlag(Define.FLAG_APPLIED);
            return 0;
        };

        // Act
        ReplyFrame r = g.Command("dropall", "misc");

        // Assert
        Assert.Single(Effects(r, "npc.place"));
        Assert.Single(g.Items(g.Player));
    }

    [Fact]
    public void Drop_Given_a_count_the_player_typed_When_drop_is_called_Then_that_many_go_and_the_count_is_forgotten()
    {
        // Arrange
        using var g = new Game(inventory: "arch banana\nnrof 5\nend\n");
        ulong stack = g.Items(g.Player).Single().Handle;
        Player contr = g.Ext.Dispatcher.ControllerOf("bob")!;
        contr.Count = 2;

        // Act
        ReplyFrame r = g.Direct("top", (uint)stack);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(3L, g.Read(stack, "mudnrof").AsInt);
        Assert.Equal(0u, contr.Count);
    }

    // ------------------------------------------------------------------ sack_can_hold

    private sealed class SackRig : IDisposable
    {
        public SackRig()
        {
            Ext = new DirectExt(PickupWorld.MakeWorld(), PickupWorld.Options(), 1, true, 16_777_216, 1);
            World = Ext.Dispatcher.WorldOrNull!;
        }

        public DirectExt Ext { get; }

        public CfWorld World { get; }

        public CfObject Make(string arch, bool applied = false)
        {
            CfObject ob = World.ArchToObject(World.FindArchetype(arch))!;
            if (applied)
            {
                ob.SetFlag(Define.FLAG_APPLIED);
            }
            return ob;
        }

        public bool Holds(CfObject sack, CfObject op, long nrof = 0) => Ext.Dispatcher.Pickups.SackCanHold(null, sack, op, nrof);

        public void Dispose() => Ext.Dispose();
    }

    [Fact]
    public void SackCanHold_Given_a_container_that_is_not_active_or_the_object_is_the_container_When_asked_Then_no()
    {
        // Arrange
        using var rig = new SackRig();
        CfObject idle = rig.Make("sack");
        CfObject sack = rig.Make("sack", applied: true);

        // Act / Assert
        Assert.False(rig.Holds(idle, rig.Make("banana")));
        Assert.False(rig.Holds(sack, sack));
        Assert.True(rig.Holds(sack, rig.Make("banana")));
    }

    [Fact]
    public void SackCanHold_Given_a_container_for_one_race_When_asked_Then_only_that_race_and_no_container_goes_in()
    {
        // Arrange
        using var rig = new SackRig();
        CfObject basket = rig.Make("fruitbasket", applied: true);

        // Act / Assert
        Assert.True(rig.Holds(basket, rig.Make("fruit")));
        Assert.False(rig.Holds(basket, rig.Make("sword")));
        Assert.False(rig.Holds(basket, rig.Make("shaman"))); // a container of the same race
    }

    [Fact]
    public void SackCanHold_Given_a_special_key_and_a_container_with_a_slaying_When_asked_Then_no()
    {
        // Arrange
        using var rig = new SackRig();
        CfObject vault = rig.Make("sack", applied: true);
        vault.Slaying = "vault";

        // Act / Assert
        Assert.False(rig.Holds(vault, rig.Make("skeleton")));
        Assert.True(rig.Holds(rig.Make("sack", applied: true), rig.Make("skeleton")));
    }

    [Fact]
    public void SackCanHold_Given_a_weight_limit_When_asked_Then_the_object_must_fit_and_a_number_of_objects_is_counted()
    {
        // Arrange: the sack holds 1,000 g, a sword weighs 3,000 g and a banana 100 g
        using var rig = new SackRig();
        CfObject sack = rig.Make("sack", applied: true);

        // Act / Assert
        Assert.False(rig.Holds(sack, rig.Make("sword")));
        Assert.True(rig.Holds(sack, rig.Make("banana"), 10));
        Assert.False(rig.Holds(sack, rig.Make("banana"), 11));
        sack.Carrying = 950;
        Assert.False(rig.Holds(sack, rig.Make("banana")));
        sack.Stats.Str = 50; // a container that makes things lighter
        Assert.True(rig.Holds(sack, rig.Make("banana")));
    }

    [Fact]
    public void SackCanHold_Given_a_player_When_the_object_does_not_fit_Then_he_is_told_why_in_the_words_of_C()
    {
        // Arrange
        using var g = new Game(inventory: "arch sack\napplied 1\nend\narch fruitbasket\napplied 1\nend\n");
        var sack = g.Items(g.Player).Single(i => i.Name == "sack").Handle;
        g.Ext.Call(ItemWorld.OpDo, new[] { S("open"), S("bob"), Val.Int((uint)sack) }, player: "bob");

        // Act
        ReplyFrame heavy = g.Pick(g.Hall[PickupWorld.Sword], 0);

        // Assert
        Assert.False(Result(heavy));
        Assert.Equal(new[] { "That won't fit in the sack (open) (active)!" }, Lines(heavy));
    }
}
