// Not ported from a Crossfire file: tests of lane 7B of phase 3 (wave 7), the items of another player (docs/mud-phase3.md section 4.5):
// mudexamineinvobject2, the method examineinvobject and mudtransferobject, with the query player.at answered by a stand-in host.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed class TradeTests
{
    private const string TooHeavy = "That item is too heavy for you to put it in your inventory.";

    // bob: a stack of five bananas, a ring, a relic that cannot be dropped, an anvil, a sack with two bananas in it, and a sword he wields
    private const string TradeBob =
        ApplyWorld.Skills + "arch banana\nnrof 5\nend\narch ring\nend\narch relic\nend\narch anvil\nnrof 3\nend\narch sack\narch banana\nnrof 2\nend\nend\narch sword\napplied 1\nend\n";

    private static ReplyFrame Transfer(W7Game g, long id, long index, long n, string account = "bob", long mode = 0) =>
        g.Ext.Call(Manifest.OpMudTransferObject, new[] { Val.Int(id), Val.Int(mode), Val.Int(index), Val.Int(n) }, level: "hall", player: account);

    private static ReplyFrame Examine2(W7Game g, long index, long id, string account = "bob") =>
        g.Ext.Call(Manifest.OpMudExamineInvObject2, new[] { Val.Int(index), Val.Int(id) }, level: "hall", player: account);

    private static bool Result(ReplyFrame r)
    {
        Assert.Equal(0UL, r.Status);
        return r.Result.AsBool;
    }

    private static long Quantity(W7Game g, ulong item) => Get(g.Ext, item, Obj("mudquantity")).AsInt;

    // bob and carol logged in: (bob's handle, carol's handle)
    private static (ulong Bob, ulong Carol) Both(W7Game g) => (g.Handle("bob"), g.Handle("carol"));

    // ------------------------------------------------------------------ mudexamineinvobject2

    [Fact]
    public void ExamineInvObject2_Given_the_index_of_another_player_and_his_item_When_called_Then_the_asker_reads_the_text_of_the_examine_and_the_owner_reads_nothing()
    {
        // Arrange
        using var g = new W7Game();
        (_, ulong carol) = Both(g);
        long sword = g.Id(g.Item(carol, "sword"));
        ReplyFrame own = g.Ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(sword) }, level: "hall", player: "carol");

        // Act
        ReplyFrame r = Examine2(g, 1, sword);

        // Assert: the text that carol reads when she examines her own sword
        Assert.True(Result(r));
        List<string> expected = W7Game.Lines(own, "carol");
        Assert.NotEmpty(expected);
        Assert.Equal(expected, W7Game.Lines(r, "bob"));
        Assert.Empty(W7Game.Lines(r, "carol"));
        Assert.Equal("player.at", g.Host.Asked.Last().Name);
        Assert.Equal(new[] { "bob", "1" }, g.Host.Asked.Last().Args.Select(a => a.Kind == ValKind.String ? a.AsString : a.AsInt.ToString()).ToArray());
    }

    [Fact]
    public void ExamineInvObject2_Given_an_index_with_no_player_When_called_Then_false_and_nothing_is_said()
    {
        // Arrange
        using var g = new W7Game();
        (_, ulong carol) = Both(g);

        // Act
        ReplyFrame r = Examine2(g, 7, g.Id(g.Item(carol, "sword")));

        // Assert
        Assert.False(Result(r));
        Assert.Empty(W7Game.Lines(r, "bob"));
    }

    [Fact]
    public void ExamineInvObject2_Given_a_negative_index_When_called_Then_false_and_the_host_is_not_asked()
    {
        // Arrange
        using var g = new W7Game();
        (_, ulong carol) = Both(g);

        // Act
        ReplyFrame r = Examine2(g, -1, g.Id(g.Item(carol, "sword")));

        // Assert: GK's getplayerindex gives -1 for a player that is not there
        Assert.False(Result(r));
        Assert.Empty(g.Host.Asked);
    }

    [Fact]
    public void ExamineInvObject2_Given_a_player_the_host_names_who_is_not_logged_in_here_When_called_Then_false()
    {
        // Arrange
        using var g = new W7Game();
        (_, ulong carol) = Both(g);
        g.Players[2] = "dave"; // on the level but without a session in this extension

        // Act
        ReplyFrame r = Examine2(g, 2, g.Id(g.Item(carol, "sword")));

        // Assert
        Assert.False(Result(r));
        Assert.Empty(W7Game.Lines(r, "bob"));
    }

    [Fact]
    public void ExamineInvObject2_Given_a_player_whose_account_is_unloaded_When_called_Then_false()
    {
        // Arrange
        using var g = new W7Game();
        (_, ulong carol) = Both(g);
        long id = g.Id(g.Item(carol, "sword"));
        g.Method(carol, TMudPlayerMembers.MethodUnloadMudAccount, "carol");

        // Act
        ReplyFrame r = Examine2(g, 1, id);

        // Assert
        Assert.False(Result(r));
    }

    [Fact]
    public void ExamineInvObject2_Given_an_id_of_the_askers_own_tree_When_called_with_the_index_of_the_other_player_Then_false_the_id_is_looked_for_in_his_tree_only()
    {
        // Arrange
        using var g = new W7Game();
        (ulong bob, _) = Both(g);

        // Act
        ReplyFrame r = Examine2(g, 1, g.Id(g.Item(bob, "plate")));

        // Assert
        Assert.False(Result(r));
        Assert.Empty(W7Game.Lines(r, "bob"));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    [InlineData(99999L)]
    public void ExamineInvObject2_Given_an_id_that_is_no_item_of_the_other_player_When_called_Then_false(long id)
    {
        // Arrange
        using var g = new W7Game();
        Both(g);

        // Act
        ReplyFrame r = Examine2(g, 1, id);

        // Assert
        Assert.False(Result(r));
    }

    [Fact]
    public void ExamineInvObject2_Given_a_host_that_fails_the_query_When_called_Then_the_call_fails_with_the_status_of_the_host()
    {
        // Arrange
        using var g = new W7Game();
        (_, ulong carol) = Both(g);
        long id = g.Id(g.Item(carol, "sword"));
        g.Host.Answer = (_, _) => new QueryAnswer(Status.Unavailable, S("no"));

        // Act
        ReplyFrame r = Examine2(g, 1, id);

        // Assert
        Assert.Equal((ulong)Status.Unavailable, r.Status);
        Assert.Empty(W7Game.Lines(r, "bob"));
    }

    [Fact]
    public void ExamineInvObject2_Given_a_call_without_a_player_or_with_wrong_arguments_When_called_Then_false_or_bad_args()
    {
        // Arrange
        using var g = new W7Game();
        Both(g);

        // Act
        ReplyFrame noPlayer = g.Ext.Call(Manifest.OpMudExamineInvObject2, new[] { Val.Int(1), Val.Int(3) }, level: "hall");
        ReplyFrame one = g.Ext.Call(Manifest.OpMudExamineInvObject2, new[] { Val.Int(1) }, level: "hall", player: "bob");
        ReplyFrame text = g.Ext.Call(Manifest.OpMudExamineInvObject2, new[] { S("x"), Val.Int(3) }, level: "hall", player: "bob");

        // Assert
        Assert.False(Result(noPlayer));
        Assert.Equal((ulong)Status.BadArgs, one.Status);
        Assert.Equal((ulong)Status.BadArgs, text.Status);
    }

    [Fact]
    public void ExamineInvObject2_Given_a_1_0_link_When_called_Then_the_operation_is_unknown()
    {
        // Arrange
        using var g = new W7Game(protoMinor: 0);
        g.Handle("bob");

        // Act
        ReplyFrame r = Examine2(g, 1, 3);

        // Assert: 1.0 has no player.at, so it has no such function
        Assert.Equal((ulong)Status.UnknownOp, r.Status);
    }

    // ------------------------------------------------------------------ player.examineinvobject

    [Fact]
    public void ExamineInvObject_Given_an_item_of_the_players_own_tree_When_the_method_is_called_Then_he_reads_the_text_of_mudexamineinvobject()
    {
        // Arrange
        using var g = new W7Game();
        (ulong bob, _) = Both(g);
        long id = g.Id(g.Item(bob, "sword"));
        ReplyFrame expected = g.Ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(id) }, level: "hall", player: "bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodExamineInvObject, "bob", Val.Int(id));

        // Assert
        Assert.True(Result(r));
        Assert.NotEmpty(W7Game.Lines(expected, "bob"));
        Assert.Equal(W7Game.Lines(expected, "bob"), W7Game.Lines(r, "bob"));
    }

    [Fact]
    public void ExamineInvObject_Given_an_id_of_another_players_item_When_the_method_is_called_Then_false_and_nothing_is_said()
    {
        // Arrange
        using var g = new W7Game();
        (ulong bob, ulong carol) = Both(g);

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodExamineInvObject, "bob", Val.Int(g.Id(g.Item(carol, "sword"))));

        // Assert
        Assert.False(Result(r));
        Assert.Empty(W7Game.Lines(r, "bob"));
        Assert.Empty(W7Game.Lines(r, "carol"));
    }

    [Fact]
    public void ExamineInvObject_Given_the_handle_of_another_player_When_a_script_calls_it_for_the_context_player_Then_false_and_he_reads_nothing()
    {
        // Arrange
        using var g = new W7Game();
        (_, ulong carol) = Both(g);

        // Act: the request is bob's; carol's handle, carol's own id
        ReplyFrame r = g.Method(carol, TMudPlayerMembers.MethodExamineInvObject, "bob", Val.Int(g.Id(g.Item(carol, "sword"))));

        // Assert
        Assert.False(Result(r));
        Assert.Empty(W7Game.Lines(r, "carol"));
        Assert.Empty(W7Game.Lines(r, "bob"));
    }

    [Fact]
    public void ExamineInvObject_Given_a_text_instead_of_an_id_When_called_Then_bad_args()
    {
        // Arrange
        using var g = new W7Game();
        (ulong bob, _) = Both(g);

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodExamineInvObject, "bob", S("x"));

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
    }

    [Fact]
    public void ExamineInvObject_Given_a_1_0_link_When_the_method_is_called_Then_it_examines_the_players_own_item()
    {
        // Arrange: the method asks nothing of the host
        using var g = new W7Game(protoMinor: 0);
        ulong bob = g.Handle("bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodExamineInvObject, "bob", Val.Int(g.Id(g.Item(bob, "sword"))));

        // Assert
        Assert.True(Result(r));
        Assert.Empty(g.Host.Asked);
    }

    // ------------------------------------------------------------------ mudtransferobject

    [Fact]
    public void Transfer_Given_a_whole_item_When_given_to_the_player_at_the_index_Then_it_leaves_his_tree_and_enters_the_other_and_the_strings_of_both_windows_follow()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        ulong ring = g.Item(bob, "ring");
        long id = g.Id(ring);
        int carolBefore = g.Items(carol).Count;

        // Act
        ReplyFrame r = Transfer(g, id, 1, 0);

        // Assert
        Assert.True(Result(r));
        Assert.DoesNotContain("ring", g.Names(bob));
        Assert.Contains("ring", g.Names(carol));
        Assert.Equal(carolBefore + 1, g.Items(carol).Count);
        Assert.Contains(Effects(r, "client.del"), e => e.Get("player")!.AsString == "bob" && e.Get("name")!.AsString == "clientr.mud_item" + id);
        Assert.Contains(Effects(r, "client.set"), e => e.Get("player")!.AsString == "carol" && e.Get("name")!.AsString.StartsWith("clientr.mud_item", StringComparison.Ordinal)
            && e.Get("value")!.AsString.StartsWith("ring", StringComparison.Ordinal));
        Assert.False(Result(Transfer(g, id, 1, 0))); // the old id is dead for bob
        Assert.NotEqual(0UL, g.Ext.Call(9, new[] { H(ring), Val.Int(Obj("mudrealname")) }).Status); // and so is the handle a script held
        Assert.NotEqual(id, g.Id(g.Item(carol, "ring"))); // carol's window knows it by an id of her own
    }

    [Fact]
    public void Transfer_Given_part_of_a_stack_When_given_Then_the_rest_stays_and_the_part_merges_into_the_stack_of_the_receiver()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        ulong bananas = g.Items(bob).First(i => i.Name == "banana" && Quantity(g, i.Handle) == 5).Handle;
        ulong carolStack = g.Item(carol, "banana");

        // Act
        ReplyFrame r = Transfer(g, g.Id(bananas), 1, 2);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(3, Quantity(g, bananas));
        Assert.Equal(7, Quantity(g, carolStack));
        Assert.Contains(Effects(r, "client.set"), e => e.Get("player")!.AsString == "bob" && e.Get("name")!.AsString == "clientr.mud_item" + g.Id(bananas));
    }

    [Fact]
    public void Transfer_Given_a_count_equal_to_the_stack_When_given_Then_the_whole_stack_moves()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        ulong bananas = g.Items(bob).First(i => i.Name == "banana" && Quantity(g, i.Handle) == 5).Handle;
        ulong carolStack = g.Item(carol, "banana");

        // Act
        ReplyFrame r = Transfer(g, g.Id(bananas), 1, 5);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(10, Quantity(g, carolStack));
        Assert.DoesNotContain(g.Items(bob), i => i.Name == "banana"); // the two in the sack are not on his top level
    }

    [Theory]
    [InlineData(6L)]
    [InlineData(-1L)]
    public void Transfer_Given_a_count_he_cannot_give_When_called_Then_false_and_nothing_changed(long n)
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        ulong bananas = g.Items(bob).First(i => i.Name == "banana" && Quantity(g, i.Handle) == 5).Handle;
        List<string> bobBefore = g.Names(bob);
        List<string> carolBefore = g.Names(carol);

        // Act
        ReplyFrame r = Transfer(g, g.Id(bananas), 1, n);

        // Assert
        Assert.False(Result(r));
        Assert.Empty(W7Game.Lines(r, "bob")); // refused before anything is tried
        Assert.Equal(5, Quantity(g, bananas));
        Assert.Equal(bobBefore, g.Names(bob));
        Assert.Equal(carolBefore, g.Names(carol));
    }

    [Fact]
    public void Transfer_Given_a_second_argument_that_is_not_0_When_called_Then_false_and_nothing_changed()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, _) = Both(g);

        // Act (GK's fishing rod passes 1 with the index 0)
        ReplyFrame r = Transfer(g, g.Id(g.Item(bob, "ring")), 1, 0, mode: 1);

        // Assert
        Assert.False(Result(r));
        Assert.Contains("ring", g.Names(bob));
    }

    [Theory]
    [InlineData(7L)]
    [InlineData(-1L)]
    [InlineData(0L)] // himself
    public void Transfer_Given_no_other_player_at_the_index_When_called_Then_false_and_nothing_changed(long index)
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        List<string> carolBefore = g.Names(carol);

        // Act
        ReplyFrame r = Transfer(g, g.Id(g.Item(bob, "ring")), index, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Contains("ring", g.Names(bob));
        Assert.Equal(carolBefore, g.Names(carol));
    }

    [Fact]
    public void Transfer_Given_a_player_the_host_names_who_is_not_logged_in_here_When_called_Then_false_and_nothing_changed()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, _) = Both(g);
        g.Players[2] = "dave";

        // Act
        ReplyFrame r = Transfer(g, g.Id(g.Item(bob, "ring")), 2, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Contains("ring", g.Names(bob));
    }

    [Fact]
    public void Transfer_Given_a_receiver_whose_account_is_unloaded_When_called_Then_false_and_nothing_changed()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        g.Method(carol, TMudPlayerMembers.MethodUnloadMudAccount, "carol");

        // Act
        ReplyFrame r = Transfer(g, g.Id(g.Item(bob, "ring")), 1, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Contains("ring", g.Names(bob));
        Assert.Empty(g.Names(carol));
    }

    [Fact]
    public void Transfer_Given_an_item_of_the_receivers_tree_When_the_giver_names_its_id_Then_false_and_nothing_changed()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        long id = g.Id(g.Item(carol, "sword"));
        List<string> carolBefore = g.Names(carol);

        // Act
        ReplyFrame r = Transfer(g, id, 1, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(carolBefore, g.Names(carol));
        Assert.Single(g.Items(bob), i => i.Name == "sword");
    }

    [Fact]
    public void Transfer_Given_an_item_in_a_sack_of_the_giver_When_given_Then_it_moves_out_of_the_sack()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        CfObject sack = g.Ob("bob").Inv!;
        while (sack.Type != Define.CONTAINER)
        {
            sack = sack.Below!;
        }
        long id = g.IdOf(sack.Inv!);

        // Act
        ReplyFrame r = Transfer(g, id, 1, 1);

        // Assert: one of the two bananas in the sack went to carol's stack
        Assert.True(Result(r));
        Assert.Equal(1u, sack.Inv!.Nrof);
        Assert.Equal(6, Quantity(g, g.Item(carol, "banana")));
    }

    [Fact]
    public void Transfer_Given_a_sack_with_contents_When_given_Then_the_contents_go_with_it_and_the_open_sack_is_closed()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        CfObject sack = g.Ob("bob").Inv!;
        while (sack.Type != Define.CONTAINER)
        {
            sack = sack.Below!;
        }
        g.Ob("bob").Container = sack;
        sack.SetFlag(Define.FLAG_APPLIED);
        g.IdOf(sack.Inv!); // the banana in it has a tag
        int tags = g.Dispatcher.LiveItemTags;

        // Act
        ReplyFrame r = Transfer(g, g.Id(g.Item(bob, "sack")), 1, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(tags - 1, g.Dispatcher.LiveItemTags); // the tag of the banana died with the giver's window; the sack has one again in carol's
        Assert.Null(g.Ob("bob").Container);
        Assert.False(sack.QueryFlag(Define.FLAG_APPLIED));
        Assert.DoesNotContain("sack", g.Names(bob));
        Assert.Contains("sack", g.Names(carol));
        Assert.Equal(2u, sack.Inv!.Nrof);
        Assert.Same(g.Ob("carol"), sack.Env);
    }

    [Fact]
    public void Transfer_Given_the_marked_item_When_given_Then_the_mark_is_gone()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, _) = Both(g);
        ulong ring = g.Item(bob, "ring");
        CfObject item = g.Dispatcher.FindItemById(g.Ext.Dispatcher.ControllerOf("bob")!.State, g.Id(ring), deep: true)!;
        var contr = (Player)g.Ob("bob").Contr!;
        contr.Mark = item;
        contr.MarkCount = item.Count;
        g.Ob("bob").CurrentWeapon = item;

        // Act
        Assert.True(Result(Transfer(g, g.Id(ring), 1, 0)));

        // Assert
        Assert.Null(contr.Mark);
        Assert.Equal(0u, contr.MarkCount);
        Assert.NotSame(item, g.Ob("bob").CurrentWeapon); // fix_player chooses again: his wielded sword
    }

    [Fact]
    public void Transfer_Given_an_applied_weapon_When_given_Then_it_is_taken_off_first_the_giver_gets_the_hook_and_the_receiver_gets_it_unapplied()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        ulong sword = g.Item(bob, "sword");
        long id = g.Id(sword);

        // Act
        ReplyFrame r = Transfer(g, id, 1, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(new[] { "onWeaponUnWielded" }, W7Game.Hooks(r).Select(h => h.Name));
        Assert.DoesNotContain("sword", g.Names(bob));
        Assert.Equal(2, g.Items(carol).Count(i => i.Name == "sword"));
        Assert.All(g.Items(carol).Where(i => i.Name == "sword"), i => Assert.False(Get(g.Ext, i.Handle, Obj("applied")).AsBool));
        Assert.Null(g.Ob("bob").CurrentWeapon);
    }

    [Fact]
    public void Transfer_Given_an_item_that_cannot_be_dropped_When_given_Then_false_and_nothing_changed()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);

        // Act
        ReplyFrame r = Transfer(g, g.Id(g.Item(bob, "relic")), 1, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Contains("relic", g.Names(bob));
        Assert.DoesNotContain("relic", g.Names(carol));
    }

    [Fact]
    public void Transfer_Given_an_item_the_receiver_cannot_carry_When_given_Then_false_he_reads_the_text_of_a_pick_up_and_nothing_changed()
    {
        // Arrange: three anvils of 500 kg are more than the 1,400 kg of Str 14
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        ulong anvils = g.Item(bob, "anvil");

        // Act
        ReplyFrame r = Transfer(g, g.Id(anvils), 1, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { TooHeavy }, W7Game.Lines(r, "carol"));
        Assert.Empty(W7Game.Lines(r, "bob"));
        Assert.Equal(3, Quantity(g, anvils));
        Assert.DoesNotContain("anvil", g.Names(carol));
    }

    [Fact]
    public void Transfer_Given_a_part_of_a_stack_that_the_receiver_can_carry_When_given_Then_only_the_part_is_weighed()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        ulong anvils = g.Item(bob, "anvil");

        // Act
        ReplyFrame r = Transfer(g, g.Id(anvils), 1, 2);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(1, Quantity(g, anvils));
        Assert.Equal(2, Quantity(g, g.Item(carol, "anvil")));
        Assert.Empty(W7Game.Lines(r, "carol"));
    }

    [Fact]
    public void Transfer_Given_a_receiver_who_carries_anvils_already_When_given_one_or_two_more_Then_what_he_carries_counts_and_both_are_refused()
    {
        // Arrange: carol has a stack of anvils already
        using var g = new W7Game(bob: TradeBob, carol: W7Game.CarolItems + "arch anvil\nnrof 2\nend\n");
        (ulong bob, ulong carol) = Both(g);
        ulong anvils = g.Item(bob, "anvil");

        // Act
        ReplyFrame two = Transfer(g, g.Id(anvils), 1, 2);
        ReplyFrame one = Transfer(g, g.Id(anvils), 1, 1);

        // Assert: 1,000 kg and a pack of her own, and one more anvil is over 1,400 kg: what she carries counts, not the stack alone
        Assert.False(Result(two));
        Assert.Equal(new[] { TooHeavy }, W7Game.Lines(two, "carol"));
        Assert.False(Result(one));
        Assert.Equal(3, Quantity(g, anvils));
    }

    [Fact]
    public void TooHeavyFor_Given_a_container_with_contents_When_asked_Then_the_contents_count_after_the_containers_reduction()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills);
        g.Login("bob");
        CfWorld w = g.Ext.Dispatcher.WorldOfTests!;
        CfObject receiver = g.Ob("bob");
        receiver.Weight = 0;
        receiver.Carrying = 0;
        receiver.Stats.Str = 10; // 1,000,000
        CfObject sack = w.GetObject();
        sack.Weight = 100;
        sack.Carrying = 1_000_000;
        w.InsertObInOb(w.GetObject(), sack); // it holds something

        // Act / Assert: 100 + 1,000,000 * (100 - 0) / 100 is over the limit, with a reduction of 50 it is 500,100
        sack.Stats.Str = 0;
        Assert.True(Trade.TooHeavyFor(receiver, sack, 1));
        sack.Stats.Str = 50;
        Assert.False(Trade.TooHeavyFor(receiver, sack, 1));
        // without contents only its own weight counts
        CfObject plain = w.GetObject();
        plain.Weight = 600_000;
        Assert.False(Trade.TooHeavyFor(receiver, plain, 1));
        Assert.True(Trade.TooHeavyFor(receiver, plain, 2));
        plain.Weight = 1_000_000; // exactly the limit still fits
        Assert.False(Trade.TooHeavyFor(receiver, plain, 1));
        plain.Weight = 600_000;
        receiver.Carrying = 400_000;
        Assert.False(Trade.TooHeavyFor(receiver, plain, 1));
        receiver.Carrying = 400_001;
        Assert.True(Trade.TooHeavyFor(receiver, plain, 1));
        receiver.Carrying = 0;
        receiver.Weight = 400_001;
        Assert.True(Trade.TooHeavyFor(receiver, plain, 1));
    }

    [Fact]
    public void Transfer_Given_the_function_busy_When_called_Then_busy_before_anything_changes()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        long id = g.Id(g.Item(bob, "ring"));
        g.Ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame r = Transfer(g, id, 1, 0);
        g.Ext.Dispatcher.Busy = null;

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.Contains("ring", g.Names(bob));
        Assert.DoesNotContain("ring", g.Names(carol));
        Assert.DoesNotContain(g.Host.Asked, a => a.Name == "player.at");
    }

    [Fact]
    public void Transfer_Given_a_host_that_fails_the_query_When_called_Then_the_call_fails_and_nothing_changed()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        (ulong bob, ulong carol) = Both(g);
        long id = g.Id(g.Item(bob, "ring"));
        g.Host.Answer = (_, _) => new QueryAnswer(Status.Unavailable, S("no"));

        // Act
        ReplyFrame r = Transfer(g, id, 1, 0);

        // Assert
        Assert.Equal((ulong)Status.Unavailable, r.Status);
        Assert.Contains("ring", g.Names(bob));
        Assert.DoesNotContain("ring", g.Names(carol));
    }

    [Fact]
    public void Transfer_Given_wrong_arguments_or_no_player_When_called_Then_bad_args_or_false()
    {
        // Arrange
        using var g = new W7Game(bob: TradeBob);
        Both(g);

        // Act
        ReplyFrame three = g.Ext.Call(Manifest.OpMudTransferObject, new[] { Val.Int(1), Val.Int(0), Val.Int(1) }, level: "hall", player: "bob");
        ReplyFrame text = g.Ext.Call(Manifest.OpMudTransferObject, new[] { S("a"), Val.Int(0), Val.Int(1), Val.Int(1) }, level: "hall", player: "bob");
        ReplyFrame noPlayer = g.Ext.Call(Manifest.OpMudTransferObject, new[] { Val.Int(1), Val.Int(0), Val.Int(1), Val.Int(1) }, level: "hall");

        // Assert
        Assert.Equal((ulong)Status.BadArgs, three.Status);
        Assert.Equal((ulong)Status.BadArgs, text.Status);
        Assert.False(Result(noPlayer));
    }

    [Fact]
    public void Transfer_Given_a_1_0_link_When_called_Then_the_operation_is_unknown()
    {
        // Arrange
        using var g = new W7Game(protoMinor: 0);
        g.Handle("bob");

        // Act
        ReplyFrame r = Transfer(g, 1, 1, 0);

        // Assert
        Assert.Equal((ulong)Status.UnknownOp, r.Status);
    }

    [Fact]
    public void Transfer_Given_a_cursed_applied_weapon_When_given_Then_false_he_keeps_it_and_nothing_changed()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills + "arch sword\napplied 1\ncursed 1\nend\n");
        (ulong bob, ulong carol) = Both(g);

        // Act
        ReplyFrame r = Transfer(g, g.Id(g.Item(bob, "sword")), 1, 0);

        // Assert: apply_special cannot unapply it
        Assert.False(Result(r));
        Assert.Contains("sword", g.Names(bob));
        Assert.Equal(1, g.Items(carol).Count(i => i.Name == "sword"));
        Assert.True(Get(g.Ext, g.Item(bob, "sword"), Obj("applied")).AsBool);
    }

    [Fact]
    public void Transfer_Given_a_sack_that_holds_the_sack_he_has_open_When_given_Then_the_open_one_is_closed()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills + "arch sack\narch sack\narch banana\nend\nend\nend\n");
        (ulong bob, _) = Both(g);
        CfObject outer = g.Ob("bob").Inv!;
        while (outer.Type != Define.CONTAINER)
        {
            outer = outer.Below!;
        }
        CfObject inner = outer.Inv!;
        g.Ob("bob").Container = inner;
        inner.SetFlag(Define.FLAG_APPLIED);

        // Act
        ReplyFrame r = Transfer(g, g.Id(g.Item(bob, "sack")), 1, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Null(g.Ob("bob").Container);
        Assert.False(inner.QueryFlag(Define.FLAG_APPLIED));
    }

    [Fact]
    public void Transfer_Given_the_index_of_the_giver_himself_When_called_Then_false_and_the_tree_is_untouched()
    {
        // Arrange: a light pack, so that the weight is no reason to refuse
        using var g = new W7Game(bob: ApplyWorld.Skills + "arch ring\nend\n");
        (ulong bob, _) = Both(g);
        ulong ring = g.Item(bob, "ring");

        // Act
        ReplyFrame r = Transfer(g, g.Id(ring), 0, 0);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(ring, g.Item(bob, "ring"));
        Assert.Empty(g.WrittenMoves);
    }

    // ------------------------------------------------------------------ the write order

    private static string WaitFor(W7Game g, string relative, Func<string, bool> ready)
    {
        string text = "";
        for (int i = 0; i < 500; i++)
        {
            string path = g.File(relative);
            try
            {
                text = File.Exists(path) ? File.ReadAllText(path, System.Text.Encoding.Latin1) : "";
            }
            catch (IOException)
            {
                text = "";
            }
            if (ready(text))
            {
                break;
            }
            Thread.Sleep(10);
        }
        return text;
    }

    [Fact]
    public void Transfer_Given_a_gift_When_it_is_recorded_Then_the_givers_file_is_written_first_and_both_at_once_with_the_item_in_the_receivers_only()
    {
        // Arrange: a quiet time of an hour: only a move is written now
        using var g = new W7Game(bob: TradeBob, slowWriter: true);
        (ulong bob, _) = Both(g);

        // Act
        Assert.True(Result(Transfer(g, g.Id(g.Item(bob, "ring")), 1, 0)));
        string carolFile = WaitFor(g, "accounts/carol.txt", t => t.Contains("arch ring"));
        string bobFile = WaitFor(g, "accounts/bob.txt", t => t.Length > 0 && !t.Contains("arch ring") && t.Contains("arch banana"));

        // Assert
        Assert.Equal(new[] { "accounts/bob.txt>accounts/carol.txt" }, g.WrittenMoves);
        Assert.Contains("arch ring", carolFile);
        Assert.DoesNotContain("arch ring", bobFile);
    }
}
