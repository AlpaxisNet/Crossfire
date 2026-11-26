// Not ported from a Crossfire file: tests of lane 5B of phase 3 wave 5, the inventory functions (docs/mud-phase3.md section 4.2): the
// global functions mudaddinvobject, mudremoveinvobject, mudcountinvobject, mudfindinvobject, mudresetinvobject, mudorderinventory,
// findMudObject, mudhaveskill, mudskilllevel and the four lists of the quick menu; the inventory methods of TMudPlayer and TMudObject
// and muddecreasequantity. Each test names what it pins; the mutants that were tried against them are in docs/extension.md.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

internal static class InventoryWorld
{
    public const string Arc =
        ItemWorld.Arc
        + "Object skill_missile\nname missile weapons\ntype 43\ninvisible 1\nno_drop 1\nsp 11\nend\n"
        + "Object skill_crafting\nname crafting\ntype 43\ninvisible 1\nno_drop 1\nsp 12\nend\n"
        + "Object experience_agility\nname agility\ntype 44\ninvisible 1\nlevel 1\nend\n"
        + "Object relic\nname relic\nname_pl relics\ntitle of doom\ntype 6\nweight 10\nvalue 5\nend\n"
        + "Object evil\nname Evil Master, Bonehead\nface evil.111\ntype 15\nweight 100\nend\n"
        + "Object wand\nname wand\ntype 109\nweight 100\nend\n";

    public static TempFolder Make(params (string Account, string Inventory)[] accounts)
    {
        TempFolder world = TestWorld.Make(arc: Arc);
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts"));
        foreach (string c in ObjectWorld.Classes)
        {
            File.WriteAllText(Path.Combine(world.Path, "scripts", c + ".txt"), "// class\n");
        }
        Directory.CreateDirectory(Path.Combine(world.Path, "mudlevels"));
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall.nw"), ObjectWorld.SeedHall);
        foreach ((string account, string inventory) in accounts)
        {
            ItemWorld.Account(world, account, "", inventory);
        }
        return world;
    }

    /// <summary>What bob has: bananas, a sword, a bag with two bananas, a skill, two things with names, and an applied bow.</summary>
    public const string Bob =
        "arch banana\nnrof 4\nidentified 1\nend\narch sword\nnrof 1\nend\narch bag\nnrof 1\narch banana\nnrof 2\nend\nend\n"
        + "arch skill_missile\nend\narch relic\nend\narch evil\nend\narch bow\napplied 1\nend\n";

    public const string Alice = "arch banana\nnrof 7\nidentified 1\nend\narch sword\nnrof 1\nend\narch bag\nnrof 1\narch banana\nnrof 5\nend\nend\n";
}

public sealed class InventoryTests : IDisposable
{
    private readonly TempFolder world = InventoryWorld.Make(("bob", InventoryWorld.Bob), ("alice", InventoryWorld.Alice));

    public void Dispose() => world.Dispose();

    private static ExtensionOptions Options() => new()
    {
        Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
    };

    private DirectExt Start(long protoMinor = 1) => new(world, Options(), 1, false, 16_777_216, protoMinor);

    private static ReplyFrame Login(IExt ext, string account) => ext.Call(5, new[] { S(account) }, player: account);

    private static ReplyFrame Fn(IExt ext, int op, string account, params Val[] args) => ext.Call((ulong)op, args, player: account);

    private static ReplyFrame Method(IExt ext, ulong target, int method, string account, params Val[] args) =>
        ext.Call(13, new[] { H(target), Val.Int(method), Val.List(args) }, player: account);

    private static CfObject Pl(DirectExt ext, string account) => ext.Dispatcher.ControllerOf(account)!.State.Ob;

    private static CfObject? First(CfObject holder, string arch)
    {
        for (CfObject? c = holder.Inv; c != null; c = c.Below)
        {
            if (c.Arch?.Name == arch)
            {
                return c;
            }
        }
        return null;
    }

    private static long Tag(DirectExt ext, CfObject item) => ((IMemberEnv)ext.Dispatcher).TagOf(ext.Dispatcher.ItemProxy(item));

    private static long Count(CfObject holder, string arch)
    {
        long sum = 0;
        for (CfObject? c = holder.Inv; c != null; c = c.Below)
        {
            if (c.Arch?.Name == arch)
            {
                sum += Math.Max(1u, c.Nrof);
            }
        }
        return sum;
    }

    private static string Snapshot(CfObject holder)
    {
        var sb = new System.Text.StringBuilder();
        for (CfObject? c = holder.Inv; c != null; c = c.Below)
        {
            sb.Append(c.Arch?.Name).Append(':').Append(c.Nrof).Append('/').Append(c.Count).Append('[').Append(Snapshot(c)).Append("];");
        }
        return sb.ToString();
    }

    private (DirectExt Ext, ulong Bob, ulong Alice) TwoPlayers()
    {
        DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        ulong alice = Login(ext, "alice").Result.AsHandle;
        return (ext, bob, alice);
    }

    private static bool NothingToSay(ReplyFrame r) => r.Effects.Count == 0 && r.Events.Count == 0;

    // ------------------------------------------------------------------ the manifest

    [Fact]
    public void Manifest_Given_the_lane_When_asked_Then_its_functions_and_methods_are_in_its_ranges_and_the_slow_ones_are_marked()
    {
        // Arrange
        using DirectExt ext = Start();
        ManifestBuilder b = Manifest.Collect(1);

        // Act
        var functions = b.Functions.Where(f => Lane.Wave5B.OwnsFunction(f.Id)).ToDictionary(f => f.Name);

        // Assert
        Assert.Equal(13, functions.Count);
        Assert.All(functions.Values, f => Assert.NotNull(f.Handler));
        Assert.Equal(new[] { "mudaddinvobject", "mudremoveinvobject", "mudresetinvobject" }, functions.Values.Where(f => f.Slow).Select(f => f.Name).OrderBy(n => n));
        Assert.All(new[] { "mudlistweapons", "mudlistskills", "mudlistspells", "mudlistequipment" }, n => Assert.Equal(1, functions[n].MinMinor));
        Assert.Equal(new[] { 5000, 5001, 5002, 5003, 5004 }, TMudObjectMembers.Table.Methods.Where(m => Lane.Wave5B.OwnsMember(m.Id)).Select(m => m.Id));
        Assert.Equal(Enumerable.Range(5500, 9), TMudPlayerMembers.Table.Methods.Where(m => Lane.Wave5B.OwnsMember(m.Id)).Select(m => m.Id));
        Assert.Contains(TMudObjectMembers.Table.Methods, m => m.Name == "muddecreasequantity");
    }

    [Fact]
    public void Manifest_Given_a_1_0_link_When_the_lists_are_called_Then_they_are_unknown_operations_and_the_rest_works()
    {
        // Arrange
        using DirectExt ext = Start(protoMinor: 0);
        Login(ext, "bob");

        // Act
        ReplyFrame list = Fn(ext, Manifest.OpMudListWeapons, "bob");
        ReplyFrame count = Fn(ext, Manifest.OpMudCountInvObject, "bob", S("banana"), Val.True);

        // Assert
        Assert.Equal((ulong)Status.UnknownOp, list.Status);
        Assert.Equal(6, count.Result.AsInt);
    }

    // ------------------------------------------------------------------ add

    [Fact]
    public void AddInvObject_Given_an_archetype_and_a_count_When_added_Then_the_stack_holds_them_identified_and_the_player_is_told()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame r = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(5), S("goldcoin"));

        // Assert
        Assert.Equal(0UL, r.Status);
        CfObject coins = First(Pl(ext, "bob"), "goldcoin")!;
        Assert.Equal(5u, coins.Nrof);
        Assert.True(coins.QueryFlag(Define.FLAG_IDENTIFIED));
        Assert.Equal(ValKind.Handle, r.Result.Kind);
        Assert.Equal((uint)Tag(ext, coins), (uint)r.Result.AsHandle);
        Assert.Equal(5, Get(ext, r.Result.AsHandle, Obj("mudquantity")).AsInt);
        Assert.Contains(Effects(r, "client.set"), e => e.Get("value")!.AsString.StartsWith("gold coins,", StringComparison.Ordinal) && e.Get("value")!.AsString.Contains(",5,100,"));
    }

    [Fact]
    public void AddInvObject_Given_a_stack_of_the_same_kind_When_added_Then_they_merge_and_the_result_is_the_old_stack_with_its_tag()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject bananas = First(Pl(ext, "bob"), "banana")!;
        long tag = Tag(ext, bananas);

        // Act
        ReplyFrame r = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(3), S("banana"));

        // Assert
        Assert.Equal(7u, bananas.Nrof);
        Assert.Equal(tag, (long)(uint)r.Result.AsHandle);
        Assert.Equal(7, Count(Pl(ext, "bob"), "banana")); // the 2 in the bag are not at the top level
    }

    [Theory]
    [InlineData("nosuchthing", 1)]
    [InlineData("banana", 0)]
    [InlineData("banana", -3)]
    [InlineData("banana", 100_000_001)]
    public void AddInvObject_Given_an_unknown_archetype_or_a_count_that_is_not_one_to_the_most_When_added_Then_nil_and_nothing_changes(string arch, long count)
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        string before = Snapshot(Pl(ext, "bob"));

        // Act
        ReplyFrame method = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(count), S(arch));
        ReplyFrame function = Fn(ext, Manifest.OpMudAddInvObject, "bob", Val.Int(count), S(arch));

        // Assert
        Assert.True(method.Result.IsNil);
        Assert.Equal(Val.False.Kind, function.Result.Kind);
        Assert.Equal(before, Snapshot(Pl(ext, "bob")));
        Assert.Empty(method.Effects);
    }

    [Fact]
    public void MudAddInvObject_Given_a_count_and_an_archetype_When_called_Then_true_and_the_objects_are_in_the_window()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudAddInvObject, "bob", Val.Int(2), S("silvercoin"));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Equal(2u, First(Pl(ext, "bob"), "silvercoin")!.Nrof);
        Assert.Single(Effects(r, "client.set"), e => e.Get("value")!.AsString.StartsWith("silver coins,", StringComparison.Ordinal));
    }

    [Fact]
    public void AddInvObject_Given_more_than_the_player_can_carry_When_added_Then_nil_the_text_of_the_limit_and_nothing_changes()
    {
        // Arrange: Str 2 carries 300,000 g, the player weighs 70,000 g and carries a little; 200 swords weigh 600,000 g
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        string before = Snapshot(Pl(ext, "bob"));

        // Act
        ReplyFrame method = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(200), S("sword"));
        ReplyFrame function = Fn(ext, Manifest.OpMudAddInvObject, "bob", Val.Int(200), S("sword"));

        // Assert
        Assert.True(method.Result.IsNil);
        Assert.False(function.Result.AsBool);
        Assert.Equal(before, Snapshot(Pl(ext, "bob")));
        Val line = Assert.Single(method.Events);
        Assert.Equal("onRPGMessage", line.Get("name")!.AsString);
        Assert.Equal("That item is too heavy for you to put it in your inventory.", line.Get("args")!.Items[0].AsString);
    }

    [Fact]
    public void AddInvObject_Given_a_weight_just_under_and_just_over_the_limit_When_added_Then_the_limit_is_weight_limit_of_strength()
    {
        // Arrange: 70,000 own + what bob carries + n swords of 3,000 g against 300,000 g
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject pl = Pl(ext, "bob");
        long room = ItemMatching.WeightLimitOf(pl.Stats.Str) - pl.Weight - pl.Carrying;
        long fits = room / 3000;

        // Act
        ReplyFrame over = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(fits + 1), S("sword"));
        ReplyFrame under = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(fits), S("sword"));

        // Assert
        Assert.True(over.Result.IsNil);
        Assert.Equal(ValKind.Handle, under.Result.Kind);
        Assert.Equal(1 + fits, Count(pl, "sword"));
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(9, false)]
    [InlineData(11, true)]
    public void AddInvObject_Given_exactly_the_room_for_a_relic_When_added_Then_the_limit_itself_is_allowed_and_one_gram_more_is_not(int room, bool added)
    {
        // Arrange: a relic weighs 10 g; the player has `room` grams left under weight_limit[Str]
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject pl = Pl(ext, "bob");
        pl.Carrying = (int)(ItemMatching.WeightLimitOf(pl.Stats.Str) - pl.Weight - room);
        long relics = Count(pl, "relic");

        // Act
        ReplyFrame r = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(1), S("relic"));

        // Assert
        Assert.Equal(added, r.Result.Kind == ValKind.Handle);
        Assert.Equal(added ? relics + 1 : relics, Count(pl, "relic"));
    }

    [Fact]
    public void AddInvObject2_Given_more_than_the_player_can_carry_When_added_Then_the_limit_is_ignored_and_no_text_is_sent()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame r = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject2, "bob", Val.Int(200), S("sword"));

        // Assert
        Assert.Equal(ValKind.Handle, r.Result.Kind);
        Assert.Equal(201, Count(Pl(ext, "bob"), "sword"));
        Assert.Empty(r.Events);
    }

    [Fact]
    public void Add_Given_the_writer_is_busy_When_a_function_or_a_method_adds_Then_busy_and_nothing_changed()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        string before = Snapshot(Pl(ext, "bob"));
        ext.Dispatcher.Busy = "the writer is saving";

        // Act
        ReplyFrame function = Fn(ext, Manifest.OpMudAddInvObject, "bob", Val.Int(1), S("goldcoin"));
        ReplyFrame method = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(1), S("goldcoin"));
        ReplyFrame method2 = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject2, "bob", Val.Int(1), S("goldcoin"));
        ReplyFrame remove = Fn(ext, Manifest.OpMudRemoveInvObject, "bob", Val.Int(1), S("banana"));
        ReplyFrame removeMethod = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObject, "bob", Val.Int(1), S("banana"));
        ReplyFrame byId = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(1), Val.Int(Tag(ext, First(Pl(ext, "bob"), "banana")!)));
        ReplyFrame all = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjects, "bob", S("banana"));
        ReplyFrame reset = Fn(ext, Manifest.OpMudResetInvObject, "bob", Val.Int(Tag(ext, First(Pl(ext, "bob"), "sword")!)));
        ReplyFrame decrease = Method(ext, ItemHandle(ext, First(Pl(ext, "bob"), "banana")!), TMudObjectMembers.MethodMudDecreaseQuantity, "bob", Val.Int(1));

        // Assert
        foreach (ReplyFrame r in new[] { function, method, method2, remove, removeMethod, byId, all, reset, decrease })
        {
            Assert.Equal((ulong)Status.Busy, r.Status);
            Assert.True(NothingToSay(r));
        }
        Assert.Equal(before, Snapshot(Pl(ext, "bob")));
    }

    [Fact]
    public void Add_Given_an_item_added_When_the_account_is_saved_Then_the_file_holds_it()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(4), S("silvercoin"));

        // Act
        string file = Flushed(ext, "accounts/bob.txt", t => t.Contains("arch silvercoin", StringComparison.Ordinal));

        // Assert
        Assert.Contains("nrof 4", file);
    }

    // ------------------------------------------------------------------ remove

    [Fact]
    public void RemoveInvObject_Given_part_of_a_stack_When_removed_Then_true_the_stack_is_smaller_and_its_string_is_written_again()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject bananas = First(Pl(ext, "bob"), "banana")!;

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudRemoveInvObject, "bob", Val.Int(3), S("banana"));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Equal(1u, bananas.Nrof);
        Assert.Contains(Effects(r, "client.set"), e => e.Get("name")!.AsString == "clientr.mud_item" + (uint)Tag(ext, bananas) && e.Get("value")!.AsString.StartsWith("banana,mud_banana.png,6,1,100,", StringComparison.Ordinal));
        Assert.Empty(Effects(r, "client.del"));
    }

    [Fact]
    public void RemoveInvObject_Given_the_whole_stack_When_removed_Then_it_is_gone_its_handle_is_dead_and_its_string_is_deleted()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject bananas = First(Pl(ext, "bob"), "banana")!;
        long tag = Tag(ext, bananas);

        // Act
        ReplyFrame r = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObject, "bob", Val.Int(4), S("banana"));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Null(First(Pl(ext, "bob"), "banana"));
        Assert.Null(ext.Dispatcher.FindItemById(ext.Dispatcher.ControllerOf("bob")!.State, tag, deep: true));
        Assert.Contains(Effects(r, "client.del"), e => e.Get("name")!.AsString == "clientr.mud_item" + (uint)tag);
        Assert.Equal((ulong)Status.BadHandle, ext.Call(9, new[] { H(((ulong)1 << 32) | (uint)tag), Val.Int(Obj("mudquantity")) }, player: "bob").Status);
    }

    [Fact]
    public void RemoveInvObject_Given_fewer_than_asked_When_removed_Then_false_nothing_changes_and_nothing_is_shown()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        string before = Snapshot(Pl(ext, "bob"));

        // Act
        ReplyFrame function = Fn(ext, Manifest.OpMudRemoveInvObject, "bob", Val.Int(5), S("banana"));
        ReplyFrame method = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObject, "bob", Val.Int(5), S("banana"));

        // Assert
        Assert.False(function.Result.AsBool);
        Assert.False(method.Result.AsBool);
        Assert.Equal(before, Snapshot(Pl(ext, "bob")));
        Assert.True(NothingToSay(function));
        Assert.True(NothingToSay(method));
    }

    [Theory]
    [InlineData("ctfredflag", 1)]
    [InlineData("goldcoin", 1)]
    [InlineData("banana", 0)]
    [InlineData("banana", -1)]
    public void RemoveInvObject_Given_an_absent_item_or_a_count_below_one_When_removed_Then_false_and_silent(string arch, long count)
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        string before = Snapshot(Pl(ext, "bob"));

        // Act
        ReplyFrame r = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObject, "bob", Val.Int(count), S(arch));

        // Assert
        Assert.False(r.Result.AsBool);
        Assert.True(NothingToSay(r));
        Assert.Equal(before, Snapshot(Pl(ext, "bob")));
    }

    [Fact]
    public void RemoveInvObject_Given_two_stacks_of_one_archetype_When_more_than_the_first_is_asked_Then_it_takes_from_both_and_is_all_or_nothing()
    {
        // Arrange: a stack of unpaid bananas does not merge with the paid ones
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        Pl(ext, "bob");
        CfObject first = First(Pl(ext, "bob"), "banana")!;
        CfObject second = ext.Dispatcher.NewItem(ext.Dispatcher.WorldOfTests!.FindArchetype("banana")!);
        second.Nrof = 3;
        second.SetFlag(Define.FLAG_UNPAID);
        ext.Dispatcher.WorldOfTests!.InsertObInOb(second, Pl(ext, "bob"));
        Assert.NotSame(first, second);

        // Act
        ReplyFrame tooMany = Fn(ext, Manifest.OpMudRemoveInvObject, "bob", Val.Int(8), S("banana"));
        ReplyFrame r = Fn(ext, Manifest.OpMudRemoveInvObject, "bob", Val.Int(6), S("banana"));

        // Assert
        Assert.False(tooMany.Result.AsBool);
        Assert.True(r.Result.AsBool);
        Assert.Equal(1, Count(Pl(ext, "bob"), "banana"));
    }

    [Fact]
    public void RemoveInvObject_Given_an_applied_weapon_that_is_removed_When_it_is_the_current_one_Then_the_player_has_none_and_the_mark_is_clear()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject bow = First(Pl(ext, "bob"), "bow")!;
        Pl(ext, "bob").CurrentWeapon = bow;
        ext.Dispatcher.ControllerOf("bob")!.Mark = bow;

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudRemoveInvObject, "bob", Val.Int(1), S("bow"));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Null(Pl(ext, "bob").CurrentWeapon);
        Assert.Null(ext.Dispatcher.ControllerOf("bob")!.Mark);
    }

    [Fact]
    public void RemoveInvObjectById_Given_an_id_and_a_count_When_removed_Then_that_item_loses_them_or_nothing_when_it_has_fewer()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject bananas = First(Pl(ext, "bob"), "banana")!;
        long id = Tag(ext, bananas);

        // Act
        ReplyFrame tooMany = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(5), Val.Int(id));
        ReplyFrame none = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(0), Val.Int(id));
        ReplyFrame part = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(3), Val.Int(id));
        ReplyFrame rest = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(1), Val.Int(id));
        ReplyFrame gone = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(1), Val.Int(id));

        // Assert
        Assert.False(tooMany.Result.AsBool);
        Assert.False(none.Result.AsBool);
        Assert.True(part.Result.AsBool);
        Assert.True(rest.Result.AsBool);
        Assert.False(gone.Result.AsBool);
        Assert.Null(First(Pl(ext, "bob"), "banana"));
    }

    [Fact]
    public void RemoveInvObjectById_Given_the_id_of_an_item_in_a_sack_When_removed_Then_it_leaves_the_sack_and_the_sacks_weight_is_right()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject bag = First(Pl(ext, "bob"), "bag")!;
        CfObject inside = bag.Inv!;
        int carried = bag.Carrying;

        // Act
        ReplyFrame r = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(1), Val.Int(Tag(ext, inside)));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Equal(1u, inside.Nrof);
        Assert.Equal(carried - 100, bag.Carrying);
    }

    [Fact]
    public void RemoveInvObjects_Given_an_archetype_When_removed_Then_every_stack_of_it_goes_and_the_count_is_returned()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject extra = ext.Dispatcher.NewItem(ext.Dispatcher.WorldOfTests!.FindArchetype("banana")!);
        extra.Nrof = 3;
        extra.SetFlag(Define.FLAG_UNPAID);
        ext.Dispatcher.WorldOfTests!.InsertObInOb(extra, Pl(ext, "bob"));

        // Act
        ReplyFrame r = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjects, "bob", S("banana"));
        ReplyFrame again = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjects, "bob", S("banana"));
        ReplyFrame unknown = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjects, "bob", S("nosuchthing"));

        // Assert
        Assert.Equal(7, r.Result.AsInt);
        Assert.Equal(0, again.Result.AsInt);
        Assert.Equal(0, unknown.Result.AsInt);
        Assert.Null(First(Pl(ext, "bob"), "banana"));
        Assert.NotNull(First(Pl(ext, "bob"), "sword"));
        Assert.NotNull(First(Pl(ext, "bob"), "bag")!.Inv); // the bananas in the bag are not at the top level
    }

    // ------------------------------------------------------------------ count and find

    [Fact]
    public void CountInvObject_Given_stacks_and_single_objects_When_counted_Then_the_sum_of_nrof_with_one_for_an_object_without_it()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        First(Pl(ext, "bob"), "sword")!.Nrof = 0; // a sword that is not a stack counts one

        // Act / Assert
        Assert.Equal(4, Method(ext, bob, TMudPlayerMembers.MethodCountInvObject, "bob", S("banana"), Val.False).Result.AsInt);
        Assert.Equal(6, Method(ext, bob, TMudPlayerMembers.MethodCountInvObject, "bob", S("banana"), Val.True).Result.AsInt);
        Assert.Equal(1, Method(ext, bob, TMudPlayerMembers.MethodCountInvObject, "bob", S("sword"), Val.False).Result.AsInt);
        Assert.Equal(0, Method(ext, bob, TMudPlayerMembers.MethodCountInvObject, "bob", S("nosuchthing"), Val.True).Result.AsInt);
        Assert.Equal(0, Method(ext, bob, TMudPlayerMembers.MethodCountInvObject, "bob", S("goldcoin"), Val.True).Result.AsInt);
    }

    [Fact]
    public void MudCountInvObject_Given_an_archetype_and_a_flag_When_called_Then_the_same_count_for_the_requests_player()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        Login(ext, "alice");

        // Act / Assert
        Assert.Equal(4, Fn(ext, Manifest.OpMudCountInvObject, "bob", S("banana"), Val.False).Result.AsInt);
        Assert.Equal(6, Fn(ext, Manifest.OpMudCountInvObject, "bob", S("banana"), Val.True).Result.AsInt);
        Assert.Equal(7, Fn(ext, Manifest.OpMudCountInvObject, "alice", S("banana"), Val.False).Result.AsInt);
        Assert.Equal(0, ext.Call((ulong)Manifest.OpMudCountInvObject, new[] { S("banana"), Val.True }).Result.AsInt); // no player in the context
    }

    [Fact]
    public void FindInvObject_Given_a_name_When_found_Then_the_best_match_of_the_top_level_and_the_sacks_only_when_deep()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject pl = Pl(ext, "bob");
        CfObject bag = First(pl, "bag")!;
        CfObject wandInBag = ext.Dispatcher.NewItem(ext.Dispatcher.WorldOfTests!.FindArchetype("wand")!);
        ext.Dispatcher.WorldOfTests!.InsertObInOb(wandInBag, bag);

        // Act
        ReplyFrame sword = Method(ext, bob, TMudPlayerMembers.MethodFindInvObject, "bob", S("sword"), Val.False);
        ReplyFrame shallow = Method(ext, bob, TMudPlayerMembers.MethodFindInvObject, "bob", S("wand"), Val.False);
        ReplyFrame deep = Method(ext, bob, TMudPlayerMembers.MethodFindInvObject, "bob", S("wand"), Val.True);
        ReplyFrame miss = Method(ext, bob, TMudPlayerMembers.MethodFindInvObject, "bob", S("nosuchthing"), Val.True);
        ReplyFrame empty = Method(ext, bob, TMudPlayerMembers.MethodFindInvObject, "bob", S(""), Val.True);

        // Assert
        Assert.Equal((uint)Tag(ext, First(pl, "sword")!), (uint)sword.Result.AsHandle);
        Assert.True(shallow.Result.IsNil);
        Assert.Equal((uint)Tag(ext, wandInBag), (uint)deep.Result.AsHandle);
        Assert.True(miss.Result.IsNil);
        Assert.True(empty.Result.IsNil);
        Assert.Same(ItemMatching.FindBestObjectMatch(ext.Dispatcher.WorldOfTests!, pl, "sword"), First(pl, "sword"));
    }

    [Fact]
    public void FindInvObject_Given_a_count_in_the_text_and_a_miss_When_found_Then_a_nil_whose_members_read_zero()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame r = Method(ext, bob, TMudPlayerMembers.MethodFindInvObject, "bob", S("goldkey"), Val.True);

        // Assert: nil is the answer; a script's .mudquantity on it reads 0 in the host (objects.md 8)
        Assert.True(r.Result.IsNil);
        Assert.Equal(0UL, r.Status);
    }

    [Fact]
    public void FindInvObjectById_Given_ids_When_found_Then_the_top_level_and_with_deep_the_sacks_and_nil_for_an_unknown_id()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject pl = Pl(ext, "bob");
        CfObject inBag = First(pl, "bag")!.Inv!;
        long bagId = Tag(ext, First(pl, "bag")!);
        long inBagId = Tag(ext, inBag);
        long swordId = Tag(ext, First(pl, "sword")!);

        // Act
        ReplyFrame sword = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", Val.Int(swordId), Val.False);
        ReplyFrame shallow = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", Val.Int(inBagId), Val.False);
        ReplyFrame deep = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", Val.Int(inBagId), Val.True);
        ReplyFrame unknown = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", Val.Int(999999), Val.True);
        ReplyFrame zero = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", Val.Int(0), Val.True);

        // Assert
        Assert.Equal((uint)swordId, (uint)sword.Result.AsHandle);
        Assert.True(shallow.Result.IsNil);
        Assert.Equal((uint)inBagId, (uint)deep.Result.AsHandle);
        Assert.True(unknown.Result.IsNil);
        Assert.True(zero.Result.IsNil);
        Assert.NotEqual(0, bagId);
    }

    [Fact]
    public void FindInvObjectByTitle_Given_a_title_or_name_and_title_When_found_Then_the_object_else_nil()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        CfObject relic = First(Pl(ext, "bob"), "relic")!;

        // Act
        ReplyFrame title = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectByTitle, "bob", S("OF DOOM"), Val.False);
        ReplyFrame both = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectByTitle, "bob", S("relic of doom"), Val.False);
        ReplyFrame miss = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectByTitle, "bob", S("of fortune"), Val.True);
        ReplyFrame empty = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectByTitle, "bob", S(""), Val.True);

        // Assert
        Assert.Equal((uint)Tag(ext, relic), (uint)title.Result.AsHandle);
        Assert.Equal((uint)Tag(ext, relic), (uint)both.Result.AsHandle);
        Assert.True(miss.Result.IsNil);
        Assert.True(empty.Result.IsNil);
    }

    [Fact]
    public void MudFindInvObject_Given_a_text_When_called_Then_the_id_of_the_best_match_or_minus_one()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject sword = First(Pl(ext, "bob"), "sword")!;

        // Act
        ReplyFrame found = Fn(ext, Manifest.OpMudFindInvObject, "bob", S("sword"));
        ReplyFrame miss = Fn(ext, Manifest.OpMudFindInvObject, "bob", S("nosuchthing"));
        ReplyFrame noPlayer = ext.Call((ulong)Manifest.OpMudFindInvObject, new[] { S("sword") });

        // Assert
        Assert.Equal(Tag(ext, sword), found.Result.AsInt);
        Assert.Equal(-1, miss.Result.AsInt);
        Assert.Equal(-1, noPlayer.Result.AsInt);
    }

    [Fact]
    public void FindMudObject_Given_an_id_When_called_Then_the_item_of_the_requests_player_even_in_a_sack_else_nil()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject inBag = First(Pl(ext, "bob"), "bag")!.Inv!;

        // Act
        ReplyFrame found = Fn(ext, Manifest.OpFindMudObject, "bob", Val.Int(Tag(ext, inBag)));
        ReplyFrame miss = Fn(ext, Manifest.OpFindMudObject, "bob", Val.Int(424242));

        // Assert
        Assert.Equal((uint)Tag(ext, inBag), (uint)found.Result.AsHandle);
        Assert.Equal(2, Get(ext, found.Result.AsHandle, Obj("mudquantity")).AsInt);
        Assert.True(miss.Result.IsNil);
    }

    // ------------------------------------------------------------------ the inventory of a container

    [Fact]
    public void TMudObject_Given_a_sack_When_its_inventory_methods_are_called_Then_they_look_inside_it_and_the_id_must_be_the_players_own()
    {
        // Arrange
        (DirectExt ext, _, _) = TwoPlayers();
        using DirectExt owner = ext;
        CfObject bag = First(Pl(ext, "bob"), "bag")!;
        CfObject inBag = bag.Inv!;
        CfObject aliceBag = First(Pl(ext, "alice"), "bag")!;
        ulong sack = ItemHandle(ext, bag);
        CfObject topBananas = First(Pl(ext, "bob"), "banana")!;

        // Act
        ReplyFrame count = Method(ext, sack, TMudObjectMembers.MethodCountInvObject, "bob", S("banana"), Val.False);
        ReplyFrame find = Method(ext, sack, TMudObjectMembers.MethodFindInvObject, "bob", S("banana"), Val.False);
        ReplyFrame byId = Method(ext, sack, TMudObjectMembers.MethodFindInvObjectById, "bob", Val.Int(Tag(ext, inBag)), Val.False);
        ReplyFrame outside = Method(ext, sack, TMudObjectMembers.MethodFindInvObjectById, "bob", Val.Int(Tag(ext, topBananas)), Val.True);
        ReplyFrame foreign = Method(ext, sack, TMudObjectMembers.MethodFindInvObjectById, "bob", Val.Int(Tag(ext, aliceBag.Inv!)), Val.True);
        ReplyFrame title = Method(ext, sack, TMudObjectMembers.MethodFindInvObjectByTitle, "bob", S("of doom"), Val.True);

        // Assert
        Assert.Equal(2, count.Result.AsInt);
        Assert.Equal((uint)Tag(ext, inBag), (uint)find.Result.AsHandle);
        Assert.Equal((uint)Tag(ext, inBag), (uint)byId.Result.AsHandle);
        Assert.True(outside.Result.IsNil);
        Assert.True(foreign.Result.IsNil);
        Assert.True(title.Result.IsNil);
    }

    // ------------------------------------------------------------------ muddecreasequantity

    private static ulong ItemHandle(DirectExt ext, CfObject item) => ext.Dispatcher.HandlesOfTests.HandleOf(ext.Dispatcher.ItemProxy(item));

    [Fact]
    public void MudDecreaseQuantity_Given_part_of_a_stack_When_decreased_Then_the_stack_is_smaller_the_weight_follows_and_the_string_changes()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject bananas = First(Pl(ext, "bob"), "banana")!;
        int carrying = Pl(ext, "bob").Carrying;

        // Act
        ReplyFrame r = Method(ext, ItemHandle(ext, bananas), TMudObjectMembers.MethodMudDecreaseQuantity, "bob", Val.Int(1));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(3u, bananas.Nrof);
        Assert.Equal(carrying - 100, Pl(ext, "bob").Carrying);
        Assert.Contains(Effects(r, "client.set"), e => e.Get("value")!.AsString.StartsWith("bananas,mud_banana.png,6,3,300,", StringComparison.Ordinal));
        Assert.Equal(3, Get(ext, ItemHandle(ext, bananas), Obj("mudquantity")).AsInt);
    }

    [Fact]
    public void MudDecreaseQuantity_Given_the_whole_stack_or_more_When_decreased_Then_the_object_is_gone_and_its_handle_dies()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject bananas = First(Pl(ext, "bob"), "banana")!;
        ulong handle = ItemHandle(ext, bananas);

        // Act
        ReplyFrame r = Method(ext, handle, TMudObjectMembers.MethodMudDecreaseQuantity, "bob", Val.Int(9));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Null(First(Pl(ext, "bob"), "banana"));
        Assert.Contains(Effects(r, "client.del"), e => e.Get("name")!.AsString == "clientr.mud_item" + (uint)handle);
        Assert.Equal((ulong)Status.BadHandle, ext.Call(9, new[] { H(handle), Val.Int(Obj("mudquantity")) }, player: "bob").Status);
    }

    [Fact]
    public void MudDecreaseQuantity_Given_a_negative_number_When_called_Then_the_stack_grows_with_its_weight_and_its_string()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject bananas = First(Pl(ext, "bob"), "banana")!;
        int carrying = Pl(ext, "bob").Carrying;

        // Act
        ReplyFrame r = Method(ext, ItemHandle(ext, bananas), TMudObjectMembers.MethodMudDecreaseQuantity, "bob", Val.Int(-5));
        ReplyFrame zero = Method(ext, ItemHandle(ext, bananas), TMudObjectMembers.MethodMudDecreaseQuantity, "bob", Val.Int(0));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(9u, bananas.Nrof);
        Assert.Equal(carrying + 500, Pl(ext, "bob").Carrying);
        Assert.Contains(Effects(r, "client.set"), e => e.Get("value")!.AsString.StartsWith("bananas,mud_banana.png,6,9,900,", StringComparison.Ordinal));
        Assert.Equal(9u, bananas.Nrof);
        Assert.True(NothingToSay(zero));
    }

    [Fact]
    public void MudDecreaseQuantity_Given_an_item_in_a_sack_When_decreased_Then_the_sack_and_the_file_follow()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject bag = First(Pl(ext, "bob"), "bag")!;
        CfObject inBag = bag.Inv!;

        // Act
        ReplyFrame r = Method(ext, ItemHandle(ext, inBag), TMudObjectMembers.MethodMudDecreaseQuantity, "bob", Val.Int(1));
        Method(ext, ItemHandle(ext, inBag), TMudObjectMembers.MethodMudDecreaseQuantity, "bob", Val.Int(1));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Null(bag.Inv);
        Assert.Equal(0, bag.Carrying);
        Flushed(ext, "accounts/bob.txt", t => !t.Contains("nrof 2", StringComparison.Ordinal));
    }

    [Fact]
    public void MudDecreaseQuantity_Given_an_object_of_a_level_When_decreased_Then_its_count_changes_with_an_update_event_and_at_zero_the_npc_is_removed()
    {
        // Arrange
        using DirectExt ext = Start();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong banana = places[0].Get("handle")!.AsHandle;
        Assert.Equal(3, Get(ext, banana, Obj("mudquantity")).AsInt);

        // Act
        ReplyFrame one = ext.Call(13, new[] { H(banana), Val.Int(TMudObjectMembers.MethodMudDecreaseQuantity), Val.List(Val.Int(1)) }, level: "hall.nw");
        ReplyFrame more = ext.Call(13, new[] { H(banana), Val.Int(TMudObjectMembers.MethodMudDecreaseQuantity), Val.List(Val.Int(-2)) }, level: "hall.nw");
        ReplyFrame all = ext.Call(13, new[] { H(banana), Val.Int(TMudObjectMembers.MethodMudDecreaseQuantity), Val.List(Val.Int(4)) }, level: "hall.nw");

        // Assert
        Assert.Equal(0UL, one.Status);
        Assert.Equal("onUpdateObject", Assert.Single(one.Events).Get("name")!.AsString);
        Assert.Equal("onUpdateObject", Assert.Single(more.Events).Get("name")!.AsString);
        Assert.Contains(all.Effects, e => e.Get("k")!.AsString == "npc.remove" && e.Get("handle")!.AsHandle == banana);
        Assert.Equal((ulong)Status.BadHandle, ext.Call(9, new[] { H(banana), Val.Int(Obj("mudquantity")) }).Status);
    }

    [Fact]
    public void MudDecreaseQuantity_Given_the_count_of_a_level_object_When_decreased_Then_the_new_count_is_read_back()
    {
        // Arrange
        using DirectExt ext = Start();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong banana = places[0].Get("handle")!.AsHandle;

        // Act
        ext.Call(13, new[] { H(banana), Val.Int(TMudObjectMembers.MethodMudDecreaseQuantity), Val.List(Val.Int(1)) }, level: "hall.nw");
        long after = Get(ext, banana, Obj("mudquantity")).AsInt;
        ext.Call(13, new[] { H(banana), Val.Int(TMudObjectMembers.MethodMudDecreaseQuantity), Val.List(Val.Int(-4)) }, level: "hall.nw");

        // Assert
        Assert.Equal(2, after);
        Assert.Equal(6, Get(ext, banana, Obj("mudquantity")).AsInt);
    }

    // ------------------------------------------------------------------ skills

    [Fact]
    public void MudHaveSkill_Given_a_skill_in_the_tree_When_asked_Then_true_in_any_case_and_false_for_one_the_player_lacks()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        Login(ext, "alice");

        // Act / Assert
        Assert.True(Fn(ext, Manifest.OpMudHaveSkill, "bob", S("missile weapons")).Result.AsBool);
        Assert.True(Fn(ext, Manifest.OpMudHaveSkill, "bob", S("Missile Weapons")).Result.AsBool);
        Assert.False(Fn(ext, Manifest.OpMudHaveSkill, "bob", S("crafting")).Result.AsBool);
        Assert.False(Fn(ext, Manifest.OpMudHaveSkill, "bob", S("")).Result.AsBool);
        Assert.False(Fn(ext, Manifest.OpMudHaveSkill, "alice", S("missile weapons")).Result.AsBool);
        Assert.False(ext.Call((ulong)Manifest.OpMudHaveSkill, new[] { S("missile weapons") }).Result.AsBool);
    }

    [Fact]
    public void MudSkillLevel_Given_a_skill_When_asked_Then_the_level_of_its_experience_object_else_its_own_else_zero()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        Login(ext, "alice");
        CfObject skill = First(Pl(ext, "bob"), "skill_missile")!;
        skill.Level = 3;

        // Act
        long own = Fn(ext, Manifest.OpMudSkillLevel, "bob", S("missile weapons")).Result.AsInt;
        skill.ExpObj = new CfObject { Type = Define.EXPERIENCE, Level = 17 };
        long linked = Fn(ext, Manifest.OpMudSkillLevel, "bob", S("missile weapons")).Result.AsInt;

        // Assert
        Assert.Equal(3, own);
        Assert.Equal(17, linked);
        Assert.Equal(0, Fn(ext, Manifest.OpMudSkillLevel, "bob", S("crafting")).Result.AsInt);
        Assert.Equal(0, Fn(ext, Manifest.OpMudSkillLevel, "alice", S("missile weapons")).Result.AsInt);
    }

    // ------------------------------------------------------------------ the lists of the quick menu

    private static List<(string Name, string Value)> Props(ReplyFrame r) =>
        Effects(r, "caller.setprop").Select(e => (e.Get("name")!.AsString, e.Get("value")!.AsString)).ToList();

    [Fact]
    public void MudListWeapons_Given_weapons_in_the_tree_When_called_Then_numbered_name_and_icon_into_the_caller_and_an_empty_end_marker()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudListWeapons, "bob");

        // Assert: the sword, the bow and the sword of the name with a comma; not the bananas, the bag, the relic or the skill
        Assert.Equal(0UL, r.Status);
        Assert.Equal(
            new[]
            {
                ("this.weapon0", "sword,mud_sword.png"),
                ("this.weapon1", "Evil Master; Bonehead,mud_evil.png"),
                ("this.weapon2", "bow,mud_bow.png"),
                ("this.weapon3", ""),
            },
            Props(r));
    }

    [Fact]
    public void MudListWeapons_Given_every_item_of_a_loaded_account_When_listed_Then_the_names_are_field_zero_of_the_item_strings_of_the_weapons()
    {
        // Arrange
        using DirectExt ext = Start();
        ReplyFrame login = Login(ext, "bob");
        var strings = Effects(login, "client.set").Where(e => e.Get("name")!.AsString.StartsWith("clientr.mud_item", StringComparison.Ordinal))
            .Select(e => e.Get("value")!.AsString.Split(',')).ToList();

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudListWeapons, "bob");

        // Assert: seven fields each (the comma of a name is replaced), and the listed names are exactly field [0] of the weapon-typed strings
        Assert.All(strings, f => Assert.Equal(7, f.Length));
        var listed = Props(r).Where(p => p.Value.Length > 0).Select(p => p.Value.Split(',')[0]).OrderBy(n => n).ToList();
        var fromStrings = strings.Where(f => f[2] is "15" or "14" or "162" or "163").Select(f => f[0]).OrderBy(n => n).ToList();
        Assert.Equal(fromStrings, listed);
        Assert.Contains("Evil Master; Bonehead", listed);
        Assert.Equal(3, listed.Count);
        Assert.All(Props(r).Where(p => p.Value.Length > 0), p => Assert.Equal("mud_" + p.Value.Split(',')[1].Substring(4), p.Value.Split(',')[1]));
    }

    [Fact]
    public void MudListWeapons_Given_the_names_a_script_got_When_it_looks_for_the_item_by_field_zero_Then_each_name_finds_its_item()
    {
        // Arrange: the Item-Menu's equipweapon compares field [0] of each string with the name it was given
        using DirectExt ext = Start();
        ReplyFrame login = Login(ext, "bob");
        var strings = Effects(login, "client.set").Where(e => e.Get("name")!.AsString.StartsWith("clientr.mud_item", StringComparison.Ordinal)).ToList();

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudListWeapons, "bob");

        // Assert
        foreach ((string _, string value) in Props(r).Where(p => p.Value.Length > 0))
        {
            string name = value.Split(',')[0];
            Assert.Single(strings, s => s.Get("value")!.AsString.Split(',')[0] == name);
        }
    }

    [Fact]
    public void MudListWeapons_Given_no_weapon_or_no_player_When_called_Then_only_the_end_marker()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "alice");
        CfObject sword = First(Pl(ext, "alice"), "sword")!;
        ext.Dispatcher.WorldOfTests!.RemoveOb(sword);
        ext.Dispatcher.WorldOfTests!.FreeObject(sword);

        // Act
        ReplyFrame none = Fn(ext, Manifest.OpMudListWeapons, "alice");
        ReplyFrame noPlayer = ext.Call((ulong)Manifest.OpMudListWeapons, Array.Empty<Val>());

        // Assert
        Assert.Equal(new[] { ("this.weapon0", "") }, Props(none));
        Assert.Equal(new[] { ("this.weapon0", "") }, Props(noPlayer));
    }

    [Fact]
    public void MudListSkills_Given_skills_in_the_tree_When_called_Then_numbered_name_and_icon_and_an_end_marker()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudListSkills, "bob");

        // Assert
        Assert.Equal(new[] { ("this.skill0", "missile weapons,"), ("this.skill1", "") }, Props(r).Select(p => (p.Name, p.Value.Replace("mud_skill_missile.png", ""))));
    }

    [Fact]
    public void MudListEquipment_Given_applied_items_When_called_Then_only_they_are_listed_in_the_order_of_the_inventory()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        First(Pl(ext, "bob"), "sword")!.SetFlag(Define.FLAG_APPLIED);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudListEquipment, "bob");

        // Assert
        Assert.Equal(
            new[] { ("this.equipment0", "sword,mud_sword.png"), ("this.equipment1", "bow,mud_bow.png"), ("this.equipment2", "") },
            Props(r));
    }

    [Fact]
    public void MudListSpells_Given_a_player_When_called_Then_the_spell_paths_and_an_empty_list_as_this_port_has_no_spell_table()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudListSpells, "bob");

        // Assert
        var props = Props(r);
        Assert.Equal("this.spellpaths", props[0].Name);
        Assert.StartsWith("Protection,Fire,Frost,Electricity,Missiles,Self,Summoning,", props[0].Value);
        Assert.Equal(20, props[0].Value.Split(',').Length);
        Assert.Equal(("this.spell0", ""), props[1]);
        Assert.Equal(2, props.Count);
    }

    [Fact]
    public void MudListFunctions_Given_arguments_When_called_Then_bad_args_and_nothing_written()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");

        // Act / Assert
        foreach (int op in new[] { Manifest.OpMudListWeapons, Manifest.OpMudListSkills, Manifest.OpMudListSpells, Manifest.OpMudListEquipment })
        {
            ReplyFrame r = Fn(ext, op, "bob", Val.Int(1));
            Assert.Equal((ulong)Status.BadArgs, r.Status);
            Assert.Empty(r.Effects);
        }
    }

    [Fact]
    public void DisplayName_Given_a_name_with_a_comma_When_the_item_string_is_made_Then_it_has_seven_fields_and_the_comma_is_a_semicolon()
    {
        // Arrange
        using DirectExt ext = Start();
        CfWorld w = ext.Dispatcher.WorldOfTests!;
        CfObject evil = w.ArchToObject(w.FindArchetype("evil"))!;

        // Act
        string text = ItemStrings.Text(w, evil, false);
        string name = ItemStrings.DisplayName(w, evil);

        // Assert
        Assert.Equal("Evil Master; Bonehead", name);
        Assert.Equal(7, text.Split(',').Length);
        Assert.StartsWith(name + ",", text);
    }

    // ------------------------------------------------------------------ reset and order

    [Fact]
    public void MudResetInvObject_Given_a_changed_item_When_reset_Then_it_is_what_its_archetype_is_with_its_count_and_identified()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject bananas = First(Pl(ext, "bob"), "banana")!;
        bananas.Value = 999;
        bananas.Name = "rotten banana";
        bananas.Extra = new ExtraKeys();
        bananas.Extra.Set("mudrealname", "Super Banana");
        bananas.ClearFlag(Define.FLAG_IDENTIFIED);
        long id = Tag(ext, bananas);
        int carried = Pl(ext, "bob").Carrying;

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudResetInvObject, "bob", Val.Int(id));

        // Assert
        Assert.True(r.Result.AsBool);
        CfObject now = First(Pl(ext, "bob"), "banana")!;
        Assert.Equal(5, now.Value);
        Assert.Equal("banana", now.Name);
        Assert.Null(now.Extra?.Get("mudrealname"));
        Assert.Equal(4u, now.Nrof);
        Assert.True(now.QueryFlag(Define.FLAG_IDENTIFIED));
        Assert.Equal(carried, Pl(ext, "bob").Carrying);
    }

    [Fact]
    public void MudResetInvObject_Given_a_sack_with_contents_When_reset_Then_its_carrying_is_still_the_weight_of_the_contents()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject bag = First(Pl(ext, "bob"), "bag")!;
        int bagCarrying = bag.Carrying;
        int carried = Pl(ext, "bob").Carrying;
        long id = Tag(ext, bag);
        Assert.True(bagCarrying > 0);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudResetInvObject, "bob", Val.Int(id));

        // Assert
        Assert.True(r.Result.AsBool);
        CfObject now = First(Pl(ext, "bob"), "bag")!;
        Assert.NotNull(now.Inv);
        Assert.Equal(bagCarrying, now.Carrying);
        Assert.Equal(carried, Pl(ext, "bob").Carrying);
    }

    [Fact]
    public void MudResetInvObject_Given_an_applied_item_When_reset_Then_it_is_still_applied()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject bow = First(Pl(ext, "bob"), "bow")!;
        bow.Value = 77;

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudResetInvObject, "bob", Val.Int(Tag(ext, bow)));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.True(First(Pl(ext, "bob"), "bow")!.QueryFlag(Define.FLAG_APPLIED));
        Assert.NotEqual(77, First(Pl(ext, "bob"), "bow")!.Value);
    }

    [Fact]
    public void MudResetInvObject_Given_an_unknown_id_or_no_player_When_called_Then_false()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");

        // Act / Assert
        Assert.False(Fn(ext, Manifest.OpMudResetInvObject, "bob", Val.Int(31337)).Result.AsBool);
        Assert.False(ext.Call((ulong)Manifest.OpMudResetInvObject, new[] { Val.Int(1) }).Result.AsBool);
    }

    [Fact]
    public void MudOrderInventory_Given_ids_When_ordered_Then_those_items_come_first_in_that_order_and_unknown_or_repeated_ids_are_skipped()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject pl = Pl(ext, "bob");
        CfObject sword = First(pl, "sword")!;
        CfObject bananas = First(pl, "banana")!;
        CfObject bag = First(pl, "bag")!;
        List<CfObject> before = new();
        for (CfObject? c = pl.Inv; c != null; c = c.Below)
        {
            before.Add(c);
        }

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudOrderInventory, "bob", Val.List(Val.Int(Tag(ext, bag)), Val.Int(7777), Val.Int(Tag(ext, sword)), Val.Int(Tag(ext, bag)), Val.Float(Tag(ext, bananas))));

        // Assert
        var after = new List<CfObject>();
        for (CfObject? c = pl.Inv; c != null; c = c.Below)
        {
            after.Add(c);
        }
        Assert.Equal(0UL, r.Status);
        Assert.Same(bag, after[0]);
        Assert.Same(sword, after[1]);
        Assert.Same(bananas, after[2]);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before.OrderBy(o => o.Count), after.OrderBy(o => o.Count));
        Assert.Null(after[0].Above);
        Assert.Same(after[0], after[1].Above);
        Assert.Same(after[^2], after[^1].Above);
        Assert.Null(after[^1].Below);
    }

    [Fact]
    public void MudOrderInventory_Given_the_id_of_an_item_in_the_open_sack_When_ordered_Then_it_stays_in_the_sack_and_the_pack_is_unchanged()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject pl = Pl(ext, "bob");
        CfObject bag = First(pl, "bag")!;
        CfObject inside = bag.Inv!;
        pl.Container = bag; // the sack is open: the id of what is in it resolves even without deep
        long insideId = Tag(ext, inside);
        string before = Snapshot(pl);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudOrderInventory, "bob", Val.List(Val.Int(insideId)));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Same(bag, inside.Env);
        Assert.Equal(before, Snapshot(pl));
    }

    [Fact]
    public void MudOrderInventory_Given_a_non_list_When_called_Then_bad_args_and_an_empty_or_nil_list_changes_nothing()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        string before = Snapshot(Pl(ext, "bob"));

        // Act
        ReplyFrame bad = Fn(ext, Manifest.OpMudOrderInventory, "bob", S("1,2"));
        ReplyFrame empty = Fn(ext, Manifest.OpMudOrderInventory, "bob", Val.List());
        ReplyFrame nil = Fn(ext, Manifest.OpMudOrderInventory, "bob", Val.Nil);

        // Assert
        Assert.Equal((ulong)Status.BadArgs, bad.Status);
        Assert.Equal(0UL, empty.Status);
        Assert.Equal(0UL, nil.Status);
        Assert.Equal(before, Snapshot(Pl(ext, "bob")));
    }

    // ------------------------------------------------------------------ ids are scoped to the player's own tree

    [Fact]
    public void Ids_Given_player_B_sends_player_As_tags_to_every_id_function_and_method_When_called_Then_nothing_of_A_changes_and_nothing_is_shown()
    {
        // Arrange
        (DirectExt ext, ulong bob, ulong alice) = TwoPlayers();
        using DirectExt _ = ext;
        CfObject aliceBananas = First(Pl(ext, "alice"), "banana")!;
        CfObject aliceSword = First(Pl(ext, "alice"), "sword")!;
        CfObject aliceBag = First(Pl(ext, "alice"), "bag")!;
        CfObject aliceInBag = aliceBag.Inv!;
        string before = Snapshot(Pl(ext, "alice"));
        string bobBefore = Snapshot(Pl(ext, "bob"));
        var foreignIds = new[] { aliceBananas, aliceSword, aliceBag, aliceInBag }.Select(o => Tag(ext, o)).ToList();

        // Act / Assert
        foreach (long id in foreignIds)
        {
            var replies = new List<ReplyFrame>
            {
                Fn(ext, Manifest.OpFindMudObject, "bob", Val.Int(id)),
                Fn(ext, Manifest.OpMudResetInvObject, "bob", Val.Int(id)),
                Fn(ext, Manifest.OpMudOrderInventory, "bob", Val.List(Val.Int(id))),
                Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", Val.Int(id), Val.True),
                Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", Val.Int(id), Val.False),
                Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(1), Val.Int(id)),
                Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(2), Val.Int(id)),
            };
            foreach (ReplyFrame r in replies)
            {
                Assert.Equal(0UL, r.Status);
                Assert.True(r.Result.IsNil || (r.Result.Kind == ValKind.False));
                Assert.True(NothingToSay(r));
            }
        }
        Assert.Equal(before, Snapshot(Pl(ext, "alice")));
        Assert.Equal(bobBefore, Snapshot(Pl(ext, "bob")));
        Assert.Equal(7u, aliceBananas.Nrof);
        Assert.NotEqual(0UL, alice);
    }

    [Fact]
    public void Ids_Given_a_dead_tag_and_the_tag_of_an_object_of_a_level_When_used_Then_they_are_unknown_ids()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        long levelTag = (uint)places[0].Get("handle")!.AsHandle;
        CfObject bananas = First(Pl(ext, "bob"), "banana")!;
        long dead = Tag(ext, bananas);
        Fn(ext, Manifest.OpMudRemoveInvObject, "bob", Val.Int(4), S("banana"));

        // Act
        ReplyFrame level = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", Val.Int(levelTag), Val.True);
        ReplyFrame removed = Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", Val.Int(dead), Val.True);
        ReplyFrame levelRemove = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(1), Val.Int(levelTag));

        // Assert
        Assert.True(level.Result.IsNil);
        Assert.True(removed.Result.IsNil);
        Assert.False(levelRemove.Result.AsBool);
        Assert.Equal(3, Get(ext, places[0].Get("handle")!.AsHandle, Obj("mudquantity")).AsInt);
    }

    [Fact]
    public void MudOrderInventory_Given_the_id_of_an_item_in_a_sack_When_ordered_Then_it_is_not_moved_out_of_the_sack()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        CfObject bag = First(Pl(ext, "bob"), "bag")!;
        CfObject inBag = bag.Inv!;
        string before = Snapshot(Pl(ext, "bob"));

        // Act
        Fn(ext, Manifest.OpMudOrderInventory, "bob", Val.List(Val.Int(Tag(ext, inBag))));

        // Assert
        Assert.Same(bag, inBag.Env);
        Assert.Equal(before, Snapshot(Pl(ext, "bob")));
    }

    // ------------------------------------------------------------------ the account is not loaded, bad shapes

    [Fact]
    public void Functions_Given_a_wrong_number_or_kind_of_arguments_When_called_Then_bad_args()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;

        // Act / Assert
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudAddInvObject, "bob", Val.Int(1)).Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudAddInvObject, "bob", S("x"), S("banana")).Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudRemoveInvObject, "bob", Val.Int(1), Val.Int(2)).Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudCountInvObject, "bob", Val.Int(1), Val.True).Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudFindInvObject, "bob", Val.Int(1)).Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudResetInvObject, "bob", S("a")).Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpFindMudObject, "bob").Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudHaveSkill, "bob", Val.Int(1)).Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudSkillLevel, "bob").Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(1)).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObject, "bob", S("x"), S("banana")).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjectById, "bob", Val.Int(1)).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjects, "bob").Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, bob, TMudPlayerMembers.MethodFindInvObject, "bob", Val.Int(1), Val.True).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectById, "bob", S("a"), Val.True).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, bob, TMudPlayerMembers.MethodFindInvObjectByTitle, "bob", Val.Int(1), Val.True).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, bob, TMudPlayerMembers.MethodCountInvObject, "bob", Val.Int(1), Val.True).Status);
    }

    [Fact]
    public void Methods_Given_a_wrong_shape_on_a_container_When_called_Then_bad_args()
    {
        // Arrange
        using DirectExt ext = Start();
        Login(ext, "bob");
        ulong sack = ItemHandle(ext, First(Pl(ext, "bob"), "bag")!);

        // Act / Assert
        Assert.Equal((ulong)Status.BadArgs, Method(ext, sack, TMudObjectMembers.MethodFindInvObject, "bob", Val.Int(1), Val.True).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, sack, TMudObjectMembers.MethodFindInvObjectById, "bob", S("a"), Val.True).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, sack, TMudObjectMembers.MethodFindInvObjectByTitle, "bob", Val.Int(1), Val.True).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, sack, TMudObjectMembers.MethodCountInvObject, "bob", Val.Int(1), Val.True).Status);
        Assert.Equal((ulong)Status.BadArgs, Method(ext, sack, TMudObjectMembers.MethodMudDecreaseQuantity, "bob", S("x")).Status);
    }

    [Fact]
    public void AddInvObject_Given_a_player_whose_account_is_not_loaded_When_added_Then_nil_and_nothing_changes()
    {
        // Arrange
        using DirectExt ext = Start();
        ulong bob = Login(ext, "bob").Result.AsHandle;
        ext.Dispatcher.ControllerOf("bob")!.State.Loaded = false;
        string before = Snapshot(Pl(ext, "bob"));

        // Act
        ReplyFrame add = Method(ext, bob, TMudPlayerMembers.MethodAddInvObject, "bob", Val.Int(1), S("goldcoin"));
        ReplyFrame remove = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObject, "bob", Val.Int(1), S("banana"));
        ReplyFrame all = Method(ext, bob, TMudPlayerMembers.MethodRemoveInvObjects, "bob", S("banana"));
        ReplyFrame order = Fn(ext, Manifest.OpMudOrderInventory, "bob", Val.List(Val.Int(1)));

        // Assert
        Assert.True(add.Result.IsNil);
        Assert.False(remove.Result.AsBool);
        Assert.Equal(0, all.Result.AsInt);
        Assert.Equal(0UL, order.Status);
        Assert.Equal(before, Snapshot(Pl(ext, "bob")));
    }
}
