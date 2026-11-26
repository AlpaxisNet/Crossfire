// Not ported from a Crossfire file: tests of lane 6A of phase 3 (wave 6), the moves into and out of containers: <c>put_object_in_sack</c>,
// <c>esrv_move_object</c>, the function <c>mudmoveinvobject</c>, the hook of a drop into an open container, the files of a sack on the ground and the
// account's round trip. The part of the class that holds the world and the open and close is in ContainerTests.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Text;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed partial class ContainerTests
{
    // ------------------------------------------------------------------ put_object_in_sack and esrv_move_object

    [Fact]
    public void Move_Given_a_whole_stack_and_a_readied_sack_When_moved_Then_it_goes_in_the_text_is_the_one_of_C_and_the_weights_follow()
    {
        // Arrange
        using var g = new Game(SackAndBananas);
        long banana = g.Tag("banana");
        long sack = g.Tag("sack");

        // Act
        ReplyFrame r = g.Move(banana, sack, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(new[] { "You put the bananas in sack (active)." }, Lines(r));
        Assert.Equal("sack[bananax4]", g.Tree());
        Assert.Equal(600L, g.T("weight").Result.AsInt);
        Assert.Equal("600", ItemField(g, sack, 4));
        Assert.Null(Shown(g, Key(banana))); // the sack is not open: no string for what is inside
    }

    [Fact]
    public void Move_Given_a_part_of_a_stack_When_moved_Then_the_stack_is_split_and_the_rest_keeps_its_string()
    {
        // Arrange
        using var g = new Game(SackAndBananas);
        long banana = g.Tag("banana");

        // Act
        ReplyFrame r = g.Move(banana, g.Tag("sack"), 1);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(new[] { "You put the banana in sack (active)." }, Lines(r));
        Assert.Equal("sack[banana],bananax3", g.Tree());
        Assert.Equal("3", ItemField(g, banana, 3));
    }

    [Fact]
    public void Move_Given_a_stack_that_merges_with_one_in_the_sack_When_moved_Then_the_stack_in_the_sack_grows_and_the_old_string_is_deleted()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\narch banana\nnrof 2\nend\nend\narch banana\nnrof 4\nend\n");
        long outside = g.Tag("banana");
        Apply(g, g.Tag("sack")); // open: its items have strings

        // Act
        ReplyFrame r = g.Move(outside, g.Tag("sack"), 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal("sack[bananax6]", g.Tree());
        Assert.Contains(Effects(r, "client.del"), e => e.Get("name")!.AsString == Key(outside));
        long merged = g.Inner("sack", "banana");
        Assert.Equal("6", ItemField(g, merged, 3));
        Assert.EndsWith(",1", Shown(g, Key(merged))!);
    }

    [Fact]
    public void Move_Given_more_than_the_stack_When_moved_Then_get_split_ob_refuses_with_its_text_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(SackAndBananas);

        // Act
        ReplyFrame r = g.Move(g.Tag("banana"), g.Tag("sack"), 9);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "There are only 4 bananas." }, Lines(r));
        Assert.Equal("sack,bananax4", g.Tree());
    }

    [Fact]
    public void Move_Given_a_sack_that_is_not_active_When_moved_into_Then_it_is_refused_by_sack_can_hold()
    {
        // Arrange
        using var g = new Game("arch sack\nend\narch banana\nend\n");

        // Act
        ReplyFrame r = g.Move(g.Tag("banana"), g.Tag("sack"), 0);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "The sack is not active." }, Lines(r));
    }

    [Fact]
    public void Move_Given_places_that_are_no_sack_or_unknown_When_moved_Then_nothing_changes_and_nothing_is_said()
    {
        // Arrange
        using var g = new Game(SackAndBananas + "arch sword\nend\narch feather\nend\n");
        long banana = g.Tag("banana");

        // Act
        ReplyFrame unknown = g.Move(banana, 424242, 0);
        ReplyFrame notSack = g.Move(banana, g.Tag("sword"), 0);
        ReplyFrame noItem = g.Move(424242, g.Tag("sack"), 0);
        ReplyFrame zero = g.Move(0, g.Tag("sack"), 0);
        ReplyFrame weightless = g.Move(g.Tag("feather"), g.Tag("sack"), 0);
        ReplyFrame negative = g.Move(banana, g.Tag("sack"), -1);

        // Assert
        foreach (ReplyFrame r in new[] { unknown, notSack, noItem, zero, weightless, negative })
        {
            Assert.False(Result(r));
            Assert.Empty(Lines(r));
        }
        Assert.Equal("sack,bananax4,sword,feather", g.Tree());
    }

    [Fact]
    public void Move_Given_another_players_item_or_sack_When_moved_Then_it_is_an_unknown_id_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(SackAndBananas);
        g.Ext.Call(5, new[] { S("amy") }, player: "amy");
        long amysBanana = g.Tag("banana", "amy");
        long amysSack = g.Tag("sack", "amy");

        // Act
        ReplyFrame take = g.Move(amysBanana, g.Tag("sack"), 0);
        ReplyFrame give = g.Move(g.Tag("banana"), amysSack, 0);

        // Assert
        Assert.False(Result(take));
        Assert.False(Result(give));
        Assert.Equal("sack,bananax4", g.Tree());
        Assert.Equal("sack,banana", g.T("tree", account: "amy").Result.AsString);
    }

    [Fact]
    public void Move_Given_a_startequip_item_When_moved_into_a_sack_Then_C_says_it_cannot_be_put_in_the_container()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\nend\narch loaner\nend\n");

        // Act
        ReplyFrame r = g.Move(g.Tag("loaner"), g.Tag("sack"), 0);

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "You cannot put the loaner in the container." }, Lines(r));
        Assert.Equal("sack,loaner", g.Tree());
    }

    [Fact]
    public void Put_Given_the_sack_itself_or_an_object_that_is_no_container_When_put_Then_C_returns_silently_or_says_so()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\nend\narch sword\nend\narch banana\nend\n");
        long sack = g.Tag("sack");

        // Act
        ReplyFrame itself = g.T("put", Val.Int(sack), Val.Int(sack));
        ReplyFrame plain = g.T("put", Val.Int(g.Tag("sword")), Val.Int(g.Tag("banana")));

        // Assert
        Assert.False(itself.Result.AsBool);
        Assert.Empty(Lines(itself));
        Assert.False(plain.Result.AsBool);
        Assert.Equal(new[] { "The swords is not a container." }, Lines(plain));
    }

    [Fact]
    public void Move_Given_a_container_that_holds_things_When_moved_onto_the_open_sack_Then_its_contents_move_until_the_sack_is_full()
    {
        // Arrange: the ring fits, the sword (3,000 g) does not fit a 1,000 g sack
        using var g = new Game("arch sack\napplied 1\nend\narch bigsack\narch ring\nend\narch sword\nend\nend\n");
        long sack = g.Tag("sack");
        Apply(g, sack);

        // Act
        ReplyFrame r = g.Move(g.Tag("bigsack"), sack, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(
            new[]
            {
                "You move the items from bigsack into sack (open) (active).",
                "You put the rings in sack (open) (active).",
                "That won't fit in the sack (open) (active)!",
                "Your sack (open) (active) fills up.",
            },
            Lines(r));
        Assert.Equal("sack[ring],bigsack[sword]", g.Tree());
    }

    [Fact]
    public void Move_Given_a_container_with_contents_and_no_open_container_When_moved_onto_a_sack_Then_the_target_is_where_the_contents_go()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\nend\narch bigsack\narch ring\nend\nend\n");

        // Act
        ReplyFrame r = g.Move(g.Tag("bigsack"), g.Tag("sack"), 0);

        // Assert: C reads op->container, which is null here; the target stands in for it (differences.md)
        Assert.True(Result(r));
        Assert.Equal("You move the items from bigsack into sack (active).", Lines(r)[0]);
        Assert.Equal("sack[ring],bigsack", g.Tree());
    }

    [Fact]
    public void Move_Given_a_container_that_holds_things_When_the_sack_cannot_take_any_Then_nothing_moved_and_C_says_it_is_full()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\nend\narch bigsack\narch sword\nend\nend\n");

        // Act
        ReplyFrame r = g.Move(g.Tag("bigsack"), g.Tag("sack"), 0);

        // Assert
        Assert.False(Result(r));
        Assert.Equal("Your sack (active) fills up.", Lines(r).Last());
    }

    [Fact]
    public void Move_Given_an_applied_object_When_the_unapply_hook_is_missing_or_refuses_or_succeeds_Then_it_goes_in_only_when_it_succeeds()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\nend\narch ring\napplied 1\nend\n");
        long ring = g.Tag("ring");
        long sack = g.Tag("sack");

        // Act
        ReplyFrame missing = g.Move(ring, sack, 0);
        g.Ext.Dispatcher.ApplySpecialUnapply = (op, tmp) => 1;
        ReplyFrame refused = g.Move(ring, sack, 0);
        g.Ext.Dispatcher.ApplySpecialUnapply = (op, tmp) =>
        {
            tmp.ClearFlag(Define.FLAG_APPLIED);
            return 0;
        };
        ReplyFrame done = g.Move(ring, sack, 0);

        // Assert
        Assert.False(Result(missing));
        Assert.False(Result(refused));
        Assert.True(Result(done));
        Assert.Equal("sack[ring]", g.Tree());
    }

    [Fact]
    public void Move_Given_a_busy_extension_When_moved_Then_it_answers_busy_before_anything_changes()
    {
        // Arrange
        using var g = new Game(SackAndBananas);
        g.Ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame r = g.Move(g.Tag("banana"), g.Tag("sack"), 0);
        g.Ext.Dispatcher.Busy = null;

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.Empty(r.Effects);
        Assert.Equal("sack,bananax4", g.Tree());
    }

    [Fact]
    public void Move_Given_arguments_of_the_wrong_kind_or_no_player_When_called_Then_bad_args_or_false()
    {
        // Arrange
        using var g = new Game(SackAndBananas);

        // Act
        ReplyFrame wrong = g.Ext.Call(Manifest.OpMudMoveInvObject, new[] { S("a"), Val.Int(1), Val.Int(1) }, player: "bob");
        ReplyFrame few = g.Ext.Call(Manifest.OpMudMoveInvObject, new[] { Val.Int(1) }, player: "bob");
        ReplyFrame nobody = g.Ext.Call(Manifest.OpMudMoveInvObject, new[] { Val.Int(1), Val.Int(2), Val.Int(0) });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, wrong.Status);
        Assert.Equal((ulong)Status.BadArgs, few.Status);
        Assert.False(Result(nobody));
        Assert.Contains(g.Ext.Logs, l => l.Text.Contains("mudmoveinvobject: the call has no logged-in player"));
    }

    [Fact]
    public void Move_Given_a_1_0_link_When_an_item_is_put_in_a_sack_Then_the_line_is_the_client_text_effect_and_there_are_no_events()
    {
        // Arrange
        using var g = new Game(SackAndBananas, protoMinor: 0);

        // Act
        ReplyFrame r = g.Move(g.Tag("banana"), g.Tag("sack"), 1);

        // Assert
        Assert.True(Result(r));
        Assert.Empty(r.Events);
        Assert.Equal(new[] { "You put the banana in sack (active)." }, Effects(r, "client.text").Select(e => e.Get("text")!.AsString));
        Assert.Equal("sack[banana],bananax3", g.Tree());
    }

    // ------------------------------------------------------------------ out of a sack, and the hook of the drop

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Move_Given_an_item_in_the_open_sack_When_moved_to_the_window_Then_it_comes_out_into_the_pack(long to)
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\narch banana\nnrof 3\nend\nend\n");
        Apply(g, g.Tag("sack"));
        long banana = g.Inner("sack", "banana");

        // Act
        ReplyFrame r = g.Move(banana, to, 2);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(new[] { "You pick up the bananas." }, Lines(r));
        Assert.Equal("bananax2,sack[banana]", g.Tree());
    }

    [Fact]
    public void Move_Given_the_id_C_calls_the_players_When_moved_there_Then_it_comes_out_too_and_an_item_already_in_the_pack_stays()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\narch ring\nend\nend\narch banana\nend\n");
        Apply(g, g.Tag("sack"));
        long self = g.PlayerTag();

        // Act
        ReplyFrame out1 = g.Move(g.Inner("sack", "ring"), self, 0);
        ReplyFrame already = g.Move(g.Tag("banana"), 0, 0);

        // Assert
        Assert.True(Result(out1));
        Assert.False(Result(already)); // C returns when the player has already picked it up
        Assert.Equal("ring,sack,banana", g.Tree());
    }

    [Fact]
    public void Move_Given_an_item_in_a_closed_sack_When_moved_to_the_window_Then_it_comes_out_by_its_deep_id()
    {
        // Arrange
        using var g = new Game("arch sack\narch ring\nend\nend\n");
        long ring = g.Inner("sack", "ring");

        // Act
        ReplyFrame r = g.Move(ring, 0, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal("ring,sack", g.Tree());
    }

    [Fact]
    public void Move_Given_a_drop_while_a_sack_is_open_When_the_command_drop_runs_Then_the_hook_puts_the_object_into_the_sack_and_nothing_lies_on_the_ground()
    {
        // Arrange
        using var g = new Game(SackAndBananas);
        Apply(g, g.Tag("sack"));

        // Act
        ReplyFrame r = g.Command("drop", "banana");

        // Assert
        Assert.Equal(new[] { "You put the bananas in sack (open) (active)." }, Lines(r));
        Assert.Empty(Effects(r, "npc.place"));
        Assert.Equal("sack[bananax4]", g.Tree());
    }

    [Fact]
    public void Move_Given_a_weight_reducing_sack_When_filled_Then_the_player_carries_only_the_reduced_weight()
    {
        // Arrange: a lightsack holds things at half their weight; the bigsack inside it came from the account file
        using var g = new Game("arch lightsack\napplied 1\narch bigsack\narch banana\nnrof 4\nend\nend\nend\narch banana\nnrof 10\nend\n");
        long before = g.T("weight").Result.AsInt;

        // Act
        ReplyFrame r = g.Move(g.Tag("banana"), g.Tag("lightsack"), 0);

        // Assert
        Assert.Equal(200L + (200 + 400) * 50 / 100 + 1000L, before); // the sack, what is in it at half, and the ten bananas outside
        Assert.True(Result(r));
        Assert.Equal(before - 1000 + 1000 * 50 / 100, g.T("weight").Result.AsInt);
    }

    // ------------------------------------------------------------------ a sack on the ground: the files

    private sealed class Writing
    {
        public Writing()
        {
            Order = new List<string>();
            Options = ContainerWorld.Options(new FileWriterOptions
            {
                Quiet = TimeSpan.FromHours(1),
                MaxDelay = TimeSpan.FromHours(1),
                BetweenMove = (source, destination) => Order.Add(source + ">" + destination),
            });
        }

        public List<string> Order { get; }

        public ExtensionOptions Options { get; }
    }

    private static bool WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 500 && !condition(); i++)
        {
            Thread.Sleep(10);
        }
        return condition();
    }

    private static string ReadData(DirectExt ext, string rel)
    {
        string path = ObjectWorld.Data(ext.World, rel);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path, Encoding.Latin1) : "";
        }
        catch (IOException)
        {
            return "";
        }
    }

    private static long ContainerId(ReplyFrame opened) => long.Parse(Sets(opened)["clientr.mud_container"].Split(',')[0]);

    [Fact]
    public void Drop_Given_a_sack_on_the_ground_that_is_open_When_an_item_is_dropped_into_it_Then_the_account_is_written_first_and_the_level_second_at_once()
    {
        // Arrange
        var writing = new Writing();
        using var g = new Game("arch banana\nnrof 2\nend\n", options: writing.Options);
        g.T("apply", H(g.Hall[0]), Val.Int(0));

        // Act
        ReplyFrame r = g.Command("drop", "banana");
        bool written = WaitFor(() => writing.Order.Count > 0 && ReadData(g.Ext, "levels/hall.map").Contains("nrof 5"));

        // Assert
        Assert.Equal(new[] { "You put the bananas in sack (open) (active)." }, Lines(r));
        Assert.True(written);
        Assert.Equal(new[] { "accounts/bob.txt>levels/hall.map" }, writing.Order);
        Assert.Equal("", g.Tree()); // the player has nothing left
    }

    [Fact]
    public void Move_Given_an_item_in_a_sack_on_the_ground_When_moved_to_the_window_Then_the_level_is_written_first_and_the_account_second_at_once()
    {
        // Arrange
        var writing = new Writing();
        using var g = new Game(options: writing.Options);
        ReplyFrame opened = g.T("apply", H(g.Hall[0]), Val.Int(0));
        long inside = Sets(opened).Single(kv => kv.Key.StartsWith("clientr.mud_item", StringComparison.Ordinal)).Key.Substring("clientr.mud_item".Length) is { } t ? long.Parse(t) : 0;

        // Act
        ReplyFrame r = g.Move(inside, 0, 2);
        bool written = WaitFor(() => writing.Order.Count > 0 && ReadData(g.Ext, "accounts/bob.txt").Contains("arch banana"));

        // Assert
        Assert.True(Result(r));
        Assert.True(written);
        Assert.Equal(new[] { "levels/hall.map>accounts/bob.txt" }, writing.Order);
        Assert.Equal("bananax2", g.Tree());
    }

    [Fact]
    public void Move_Given_the_open_sack_on_the_ground_When_an_item_is_moved_to_its_id_Then_it_goes_in_and_the_move_is_an_account_first_move()
    {
        // Arrange
        var writing = new Writing();
        using var g = new Game("arch banana\nnrof 2\nend\n", options: writing.Options);
        long ground = ContainerId(g.T("apply", H(g.Hall[0]), Val.Int(0)));
        Assert.Equal(ground, OpenTag(g)); // the id the string names is the one of the open container's proxy, and stays

        // Act
        ReplyFrame r = g.Move(g.Tag("banana"), ground, 0);
        bool written = WaitFor(() => writing.Order.Count > 0);

        // Assert
        Assert.True(Result(r));
        Assert.True(written);
        Assert.Equal(new[] { "accounts/bob.txt>levels/hall.map" }, writing.Order);
        Assert.Equal("", g.Tree());
    }

    [Fact]
    public void Move_Given_a_move_between_two_sacks_of_the_player_When_recorded_Then_only_his_account_is_written_and_no_move_is_made()
    {
        // Arrange
        var writing = new Writing();
        var g = new Game("arch sack\napplied 1\nend\narch bigsack\napplied 1\nend\narch banana\nend\n", options: writing.Options);
        string before = ReadData(g.Ext, "accounts/bob.txt");

        // Act
        Assert.True(Result(g.Move(g.Tag("banana"), g.Tag("sack"), 0)));
        Assert.True(Result(g.Move(g.Inner("sack", "banana"), g.Tag("bigsack"), 0)));
        g.Ext.End(); // the writer saves what is dirty when the extension ends
        string after = ReadData(g.Ext, "accounts/bob.txt");
        g.Dispose();

        // Assert: the quiet time is an hour, and a move across files never happened
        Assert.Empty(writing.Order);
        Assert.NotEqual(before, after);
        Assert.Matches("arch bigsack[\\s\\S]*arch banana", after);
    }

    [Fact]
    public void Apply_Given_a_sack_on_the_ground_When_opened_and_closed_Then_the_walk_off_flags_are_set_and_cleared()
    {
        // Arrange
        using var g = new Game();
        Val ground = H(g.Hall[0]);

        // Act
        g.T("apply", ground, Val.Int(0));
        bool walkOn = g.T("flag", ground, Val.Int(Define.FLAG_WALK_OFF)).Result.AsBool;
        bool flyOn = g.T("flag", ground, Val.Int(Define.FLAG_FLY_OFF)).Result.AsBool;
        g.T("apply", ground, Val.Int(0));
        bool walkOff = g.T("flag", ground, Val.Int(Define.FLAG_WALK_OFF)).Result.AsBool;
        bool flyOff = g.T("flag", ground, Val.Int(Define.FLAG_FLY_OFF)).Result.AsBool;

        // Assert
        Assert.True(walkOn);
        Assert.True(flyOn);
        Assert.False(walkOff);
        Assert.False(flyOff);
    }

    [Fact]
    public void Apply_Given_a_locked_box_on_the_ground_When_the_player_has_the_key_Then_it_opens_and_without_it_the_apply_answers_1_and_it_stays_shut()
    {
        // Arrange
        using var g = new Game("arch skeleton\nend\n");

        // Act
        ReplyFrame opened = g.T("apply", H(g.Hall[1]), Val.Int(0));
        using var h = new Game();
        ReplyFrame shut = h.T("apply", H(h.Hall[1]), Val.Int(0));

        // Assert
        Assert.Equal(new[] { "You unlock lockbox with skeleton key.", "You open lockbox." }, Lines(opened));
        Assert.Equal(1L, shut.Result.AsInt); // manual_apply answers 1 whatever esrv_apply_container did
        Assert.Equal(new[] { "You don't have the key to unlock lockbox." }, Lines(shut));
        Assert.Equal(0L, OpenTag(h));
    }

    [Fact]
    public void Apply_Given_one_sack_open_on_the_ground_When_a_readied_sack_of_the_pack_is_applied_Then_the_ground_sack_is_closed_with_its_flags_cleared()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\nend\n");
        Val ground = H(g.Hall[0]);
        g.T("apply", ground, Val.Int(0));

        // Act
        ReplyFrame r = Apply(g, g.Tag("sack"));

        // Assert
        Assert.Equal(new[] { "You close sack (open) (active).", "You open sack." }, Lines(r));
        Assert.False(g.T("flag", ground, Val.Int(Define.FLAG_WALK_OFF)).Result.AsBool);
        Assert.False(g.T("flag", ground, Val.Int(Define.FLAG_APPLIED)).Result.AsBool);
    }

    [Fact]
    public void Move_Given_a_move_inside_a_sack_on_the_ground_When_recorded_Then_only_its_level_is_written_and_no_move_is_made()
    {
        // Arrange: the third sack of the hall holds an active bigsack and two bananas
        var writing = new Writing();
        var g = new Game(options: writing.Options);
        ReplyFrame opened = g.T("apply", H(g.Hall[2]), Val.Int(0));
        long container = ContainerId(opened);
        long banana = Sets(opened).Where(kv => kv.Value.StartsWith("banana", StringComparison.Ordinal) || kv.Value.StartsWith("bananas", StringComparison.Ordinal))
            .Select(kv => long.Parse(kv.Key.Substring("clientr.mud_item".Length))).Single();
        long bigsack = Sets(opened).Where(kv => kv.Value.StartsWith("bigsack", StringComparison.Ordinal))
            .Select(kv => long.Parse(kv.Key.Substring("clientr.mud_item".Length))).Single();
        Assert.NotEqual(container, bigsack);

        // Act
        ReplyFrame r = g.Move(banana, bigsack, 0);
        g.Ext.End();
        string level = ReadData(g.Ext, "levels/hall.map");
        g.Dispose();

        // Assert
        Assert.True(Result(r));
        Assert.Empty(writing.Order);
        Assert.Matches("arch bigsack[\\s\\S]*?arch banana", level);
    }

    [Fact]
    public void Move_Given_an_item_in_the_pack_sack_and_a_sack_open_on_the_ground_When_moved_to_the_window_Then_pick_up_puts_it_into_the_open_sack_and_the_account_is_written_first()
    {
        // Arrange: C's pick_up uses the open container whatever it is
        var writing = new Writing();
        using var g = new Game("arch sack\napplied 1\narch ring\nend\nend\n", options: writing.Options);
        g.T("apply", H(g.Hall[0]), Val.Int(0));
        long ring = g.Inner("sack", "ring");

        // Act
        ReplyFrame r = g.Move(ring, 0, 0);
        bool written = WaitFor(() => writing.Order.Count > 0);

        // Assert
        Assert.True(Result(r));
        Assert.True(written);
        Assert.Equal(new[] { "accounts/bob.txt>levels/hall.map" }, writing.Order);
        Assert.Equal("sack", g.Tree());
        Assert.True(WaitFor(() => ReadData(g.Ext, "levels/hall.map").Contains("arch ring")));
    }

    [Fact]
    public void Apply_Given_an_object_that_is_no_player_When_manual_apply_asks_Then_the_lane_answers_1_and_changes_nothing()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\narch closer\nend\nend\n");
        long sack = g.Tag("sack");
        Apply(g, sack);

        // Act
        ReplyFrame onSack = g.T("applyas", Val.Int(sack));
        ReplyFrame onCloser = g.T("applyas", Val.Int(g.Inner("sack", "closer")));

        // Assert
        Assert.Equal(1L, onSack.Result.AsInt);
        Assert.Equal(1L, onCloser.Result.AsInt);
        Assert.Equal(sack, OpenTag(g));
    }

    [Fact]
    public void Move_Given_a_container_that_holds_things_When_another_sack_is_open_Then_C_asks_the_open_one_and_puts_the_contents_into_the_target()
    {
        // Arrange: the open one is the bigsack; the ring goes into the sack that was named
        using var g = new Game("arch sack\napplied 1\nend\narch lightsack\napplied 1\narch ring\nend\nend\narch bigsack\napplied 1\nend\n");
        Apply(g, g.Tag("bigsack"));

        // Act
        ReplyFrame r = g.Move(g.Tag("lightsack"), g.Tag("sack"), 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal("You move the items from lightsack (active) into bigsack (open) (active).", Lines(r)[0]);
        Assert.Equal("sack[ring],lightsack,bigsack", g.Tree());
    }

    [Fact]
    public void Move_Given_the_whole_count_of_a_stack_When_moved_Then_it_is_no_part_of_it_and_the_item_keeps_its_tag()
    {
        // Arrange
        using var g = new Game(SackAndBananas);
        long banana = g.Tag("banana");
        Apply(g, g.Tag("sack"));

        // Act
        ReplyFrame r = g.Move(banana, g.Tag("sack"), 4);

        // Assert
        Assert.True(Result(r));
        Assert.Equal(banana, g.Inner("sack", "banana"));
    }

    [Fact]
    public void Put_Given_a_sack_that_is_not_active_When_put_directly_Then_sack_can_hold_refuses_in_put_object_in_sack_too()
    {
        // Arrange
        using var g = new Game("arch sack\nend\narch banana\nend\n");

        // Act
        ReplyFrame r = g.T("put", Val.Int(g.Tag("sack")), Val.Int(g.Tag("banana")));

        // Assert
        Assert.False(r.Result.AsBool);
        Assert.Equal(new[] { "The sack is not active." }, Lines(r));
        Assert.Equal("sack,banana", g.Tree());
    }

    [Fact]
    public void Move_Given_an_unloaded_account_When_moved_Then_nothing_changes()
    {
        // Arrange
        using var g = new Game(SackAndBananas);
        ulong player = g.Ext.Call(5, new[] { S("bob") }, player: "bob").Result.AsHandle;
        long banana = g.Tag("banana");
        long sack = g.Tag("sack");
        Assert.Equal(0UL, g.Ext.Call(13, new[] { H(player), Val.Int(TMudPlayerMembers.Table.Methods.Single(m => m.Name == "unloadmudaccount").Id), Val.List() }, player: "bob").Status);

        // Act
        ReplyFrame r = g.Move(banana, sack, 0);

        // Assert
        Assert.False(Result(r));
    }

    [Fact]
    public void Apply_Given_a_sack_of_the_pack_and_one_on_the_ground_When_opened_Then_the_account_and_the_level_are_written_with_the_state_they_have()
    {
        // Arrange
        var g = new Game("arch sack\nend\n");
        Apply(g, g.Tag("sack")); // readied
        g.T("apply", H(g.Hall[0]), Val.Int(0)); // opens the one on the ground

        // Act
        g.Ext.End();
        string account = ReadData(g.Ext, "accounts/bob.txt");
        string level = ReadData(g.Ext, "levels/hall.map");
        g.Dispose();

        // Assert
        Assert.Matches("arch sack[\\s\\S]*applied 1", account);
        Assert.Matches("arch sack[\\s\\S]*walk_off 1", level);
    }

    // ------------------------------------------------------------------ the account

    [Fact]
    public void Account_Given_nested_sacks_and_an_open_container_When_the_player_logs_out_and_in_Then_the_tree_and_the_weight_are_restored_and_the_sack_is_readied()
    {
        // Arrange
        const string inventory = "arch lightsack\napplied 1\narch bigsack\narch banana\nnrof 3\nend\narch ring\nend\nend\nend\narch sword\nend\narch sack\narch banana\nend\nend\n";
        using var g = new Game(inventory);
        string tree = g.Tree();
        long weight = g.T("weight").Result.AsInt;
        Apply(g, g.Tag("lightsack"));
        Assert.NotEqual(0L, OpenTag(g));

        // Act
        Assert.Equal(0UL, g.Ext.Call(6, new[] { S("bob") }, player: "bob").Status);
        Assert.Equal(0UL, g.Ext.Call(5, new[] { S("bob") }, player: "bob").Status);
        ReplyFrame relogin = g.T("tree");

        // Assert
        Assert.Equal("lightsack[bigsack[bananax3,ring]],sword,sack[banana]", tree);
        Assert.Equal(tree, relogin.Result.AsString);
        Assert.Equal(weight, g.T("weight").Result.AsInt);
        Assert.Equal(0L, OpenTag(g)); // an open container is not part of what is saved: the sack comes back readied
        Assert.True(g.T("flag", Val.Int(g.Tag("lightsack")), Val.Int(Define.FLAG_APPLIED)).Result.AsBool);
        Assert.False(g.T("flag", Val.Int(g.Tag("sack")), Val.Int(Define.FLAG_APPLIED)).Result.AsBool);
    }
}
