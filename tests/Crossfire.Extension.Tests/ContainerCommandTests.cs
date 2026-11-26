// Not ported from a Crossfire file: the tests of lane 7A of wave 7 for the commands and the open container: <c>drop</c>, <c>dropall</c> and
// <c>take</c> through <c>mudcommand</c> into and out of an open container (in the pack and on the ground), the write order across files,
// the player who walked away from his open container, and the open container that is picked up, dropped or given away (c_object.c
// <c>pick_up</c>, <c>drop_object</c>, <c>put_object_in_sack</c> and common/object.c <c>remove_ob</c>).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed partial class ContainerTests
{
    private static ReplyFrame Mud(Game g, string command, string? parameters, string account = "bob") =>
        g.Ext.Call(Manifest.OpMudCommand, new[] { S(command), parameters == null ? Val.Nil : S(parameters) }, level: "hall", player: account);

    private static FakeHost AtFeet(Game g, string level = "hall", double x = 5.5, double y = 5.5)
    {
        var host = new FakeHost();
        host.Answer = (name, _) => name == "player.pos" ? new QueryAnswer(Status.Ok, Val.List(S(level), N(x), N(y))) : new QueryAnswer(Status.Ok, Val.List());
        g.Ext.Dispatcher.QuerySource = host.Ask;
        return host;
    }

    private static List<string> LinesOf(ReplyFrame r, string account) =>
        r.Events.Where(e => e.Get("name")!.AsString == "onRPGMessage" && e.Get("player")!.AsString == account).Select(e => e.Get("args")!.Items[0].AsString).ToList();

    private static bool Applied(Game g, Val item) => g.T("flag", item, Val.Int(Define.FLAG_APPLIED)).Result.AsBool;

    // ------------------------------------------------------------------ drop, dropall and take with a container open

    [Fact]
    public void Mud_drop_Given_a_sack_on_the_ground_open_and_the_player_far_from_it_When_dropping_Then_it_goes_into_the_sack_without_asking_for_the_position_and_the_account_is_written_first()
    {
        // Arrange: nothing closes a container when the player walks away (C's pick_up and drop use op->container wherever it is)
        var writing = new Writing();
        using var g = new Game("arch banana\nnrof 4\nend\n", options: writing.Options);
        g.T("apply", H(g.Hall[0]), Val.Int(0));
        FakeHost host = AtFeet(g, "hall", 14.5, 14.5);

        // Act
        ReplyFrame r = Mud(g, "drop", "banana");
        bool written = WaitFor(() => writing.Order.Count > 0 && ReadData(g.Ext, "levels/hall.map").Contains("nrof 7", StringComparison.Ordinal));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(new[] { "You put the bananas in sack (open) (active)." }, Lines(r));
        Assert.Empty(host.Asked);
        Assert.Empty(Effects(r, "npc.place"));
        Assert.Equal("", g.Tree());
        Assert.Equal("sack[bananax7]", g.T("ground", H(g.Hall[0])).Result.AsString);
        Assert.True(written);
        Assert.Equal(new[] { "accounts/bob.txt>levels/hall.map" }, writing.Order); // the source first, the destination second
    }

    [Fact]
    public void Mud_take_Given_a_sack_on_the_ground_open_When_taking_Then_the_level_is_written_first_and_the_account_second()
    {
        // Arrange
        var writing = new Writing();
        using var g = new Game(options: writing.Options);
        g.T("apply", H(g.Hall[0]), Val.Int(0));

        // Act
        ReplyFrame r = Mud(g, "take", "2 banana");
        ReplyFrame all = Mud(g, "get", null);
        bool written = WaitFor(() => writing.Order.Count > 1 && ReadData(g.Ext, "accounts/bob.txt").Contains("arch banana", StringComparison.Ordinal));

        // Assert
        Assert.Equal(new[] { "You pick up the bananas." }, Lines(r));
        Assert.Equal(new[] { "You pick up the banana." }, Lines(all));
        Assert.True(written);
        Assert.Equal(new[] { "levels/hall.map>accounts/bob.txt", "levels/hall.map>accounts/bob.txt" }, writing.Order);
        Assert.Equal("bananax3", g.Tree());
        Assert.Equal("sack", g.T("ground", H(g.Hall[0])).Result.AsString);
    }

    [Fact]
    public void Mud_take_Given_no_container_open_When_taking_Then_the_objects_of_his_square_are_asked_for_once()
    {
        // Arrange
        using var g = new Game();
        FakeHost host = AtFeet(g, "hall", 3.5, 4.5);

        // Act
        ReplyFrame r = Mud(g, "take", null);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Single(host.Asked, a => a.Name == "player.pos");
        Assert.Equal(new[] { "You pick up the sack." }, Lines(r)); // nothing is open: the sack on the square is picked up
        Assert.Equal("sack[bananax3]", g.Tree());
    }

    [Fact]
    public void Mud_drop_Given_a_readied_sack_of_the_pack_open_When_dropping_Then_it_goes_into_it_and_only_the_account_is_written()
    {
        // Arrange
        using var g = new Game("arch bigsack\napplied 1\nend\narch sword\nend\n");
        Apply(g, g.Tag("bigsack"));
        Assert.NotEqual(0L, OpenTag(g));
        FakeHost host = AtFeet(g);

        // Act
        ReplyFrame r = Mud(g, "drop", "sword");

        // Assert
        Assert.Equal(new[] { "You put the swords in bigsack (open) (active)." }, Lines(r));
        Assert.Empty(host.Asked);
        Assert.Empty(Effects(r, "npc.place"));
        Assert.Equal("bigsack[sword]", g.Tree());
    }

    [Fact]
    public void Mud_dropall_Given_a_sack_open_When_run_Then_everything_droppable_goes_into_it_but_not_the_sack_itself_or_the_food()
    {
        // Arrange
        using var g = new Game("arch bigsack\napplied 1\nend\narch sword\nend\narch helmet\nend\narch banana\nnrof 2\nend\n");
        Apply(g, g.Tag("bigsack"));
        AtFeet(g);

        // Act
        ReplyFrame r = Mud(g, "dropall", null);

        // Assert
        Assert.Equal(0UL, r.Status);
        string tree = g.Tree();
        Assert.Contains("bananax2", tree); // food stays
        Assert.Contains("bigsack[", tree);
        Assert.Contains("sword", tree.Split("bigsack[")[1]);
        Assert.Contains("helmet", tree.Split("bigsack[")[1]);
        Assert.Empty(Effects(r, "npc.place"));
    }

    [Fact]
    public void Mud_drop_Given_the_name_of_the_open_container_itself_When_run_Then_nothing_happens_as_in_C()
    {
        // Arrange: put_object_in_sack returns at once when the sack is the object
        using var g = new Game("arch bigsack\napplied 1\narch banana\nnrof 2\nend\nend\n");
        Apply(g, g.Tag("bigsack"));
        FakeHost host = AtFeet(g);

        // Act
        ReplyFrame r = Mud(g, "drop", "bigsack");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Empty(Lines(r));
        Assert.Empty(Effects(r, "npc.place"));
        Assert.Equal("bigsack[bananax2]", g.Tree());
        Assert.NotEqual(0L, OpenTag(g));
    }

    [Fact]
    public void Mud_mark_Given_the_id_of_an_item_inside_the_open_sack_When_run_Then_it_is_not_marked_for_C_marks_only_what_the_pack_holds()
    {
        // Arrange
        using var g = new Game("arch bigsack\napplied 1\narch banana\nnrof 2\nend\nend\n");
        Apply(g, g.Tag("bigsack"));
        long inner = g.Inner("bigsack", "banana");

        // Act
        ReplyFrame r = Mud(g, "mark", inner.ToString());
        ReplyFrame outer = Mud(g, "mark", g.Tag("bigsack").ToString());

        // Assert
        Assert.Equal(new[] { "Could not find an object that matches " + inner }, Lines(r));
        Assert.Equal(new[] { "Marked item bigsack (open) (active)" }, Lines(outer));
    }

    // ------------------------------------------------------------------ the open container itself

    [Fact]
    public void PickUp_Given_the_sack_on_the_ground_that_the_player_has_open_When_a_script_picks_it_up_Then_C_refuses_it_for_it_cannot_hold_itself_and_it_stays_open()
    {
        // Arrange: pick_up asks sack_can_hold(op, op->container, tmp) with the container the object itself
        using var g = new Game();
        g.T("apply", H(g.Hall[0]), Val.Int(0));
        Assert.NotEqual(0L, OpenTag(g));

        // Act
        ReplyFrame r = g.Ext.Call(13, new[] { H(g.Hall[0]), Val.Int(TMudObjectMembers.MethodMudPickUpObject), Val.List(Val.Int(0)) }, level: "hall", player: "bob");

        // Assert
        Assert.False(Result(r));
        Assert.Equal(new[] { "You can't put the sack (open) (active) into itself." }, Lines(r));
        Assert.Empty(Effects(r, "npc.remove"));
        Assert.NotEqual(0L, OpenTag(g));
        Assert.True(Applied(g, H(g.Hall[0])));
        Assert.Equal("", g.Tree());
    }

    [Fact]
    public void RemoveOb_Given_another_player_who_picks_up_the_sack_that_the_first_has_open_When_it_leaves_the_square_Then_his_container_is_NULL_and_the_sack_is_not_applied_without_a_word()
    {
        // Arrange: remove_ob (common/object.c): "if (tmp->container==op) { CLEAR_FLAG(op, FLAG_APPLIED); tmp->container=NULL; }"
        using var g = new Game("arch sword\nend\n");
        g.Ext.Call(5, new[] { S("amy") }, player: "amy");
        g.T("apply", H(g.Hall[0]), Val.Int(0));
        g.T("apply", Val.Int(g.Tag("sack", "amy")), Val.Int(0), account: "amy"); // amy has a sack of her own open: the pick up leaves it alone
        Assert.NotEqual(0L, OpenTag(g));
        Assert.NotEqual(0L, g.T("open", account: "amy").Result.AsInt);
        Assert.NotEqual("", Shown(g, "clientr.mud_container"));

        // Act
        ReplyFrame r = g.Ext.Call(13, new[] { H(g.Hall[0]), Val.Int(TMudObjectMembers.MethodMudPickUpObject), Val.List(Val.Int(0)) }, level: "hall", player: "amy");
        ReplyFrame again = Mud(g, "who", null); // a request of bob's: his flush has run in the request of amy's pick up already
        AtFeet(g);
        ReplyFrame drop = g.Ext.Call(Manifest.OpMudCommand, new[] { S("drop"), S("sword") }, level: "hall", player: "bob");

        // Assert
        Assert.True(Result(r));
        Assert.Equal(0L, OpenTag(g));
        Assert.False(Applied(g, H(g.Hall[0]))); // the sack is in amy's open sack, not applied
        Assert.NotEqual(0L, g.T("open", account: "amy").Result.AsInt);
        Assert.Empty(LinesOf(r, "bob"));
        Assert.Equal("", Effects(r, "client.set").Where(e => e.Get("name")!.AsString == "clientr.mud_container").Select(e => e.Get("value")!.AsString).Single());
        Assert.Equal("", Shown(g, "clientr.mud_container"));
        Assert.Equal(0UL, again.Status);
        Assert.Equal(0UL, drop.Status);
        Assert.DoesNotContain(Lines(drop), l => l.StartsWith("You put", StringComparison.Ordinal)); // nothing is open now: the sword goes to the ground
        Assert.Single(Effects(drop, "npc.place"));
        Assert.Equal("", g.Tree());
    }

    [Fact]
    public void DropObject_Given_the_sack_of_the_pack_that_the_player_has_open_When_a_script_drops_it_Then_it_is_unapplied_and_stays_his_container_as_in_C_and_the_string_follows()
    {
        // Arrange: drop_object unapplies it (apply_special) and remove_ob leaves op->container: it is open "on the ground", not applied
        using var g = new Game("arch bigsack\napplied 1\narch banana\nnrof 2\nend\nend\narch sword\nend\n");
        long sack = g.Tag("bigsack");
        Apply(g, sack);
        Assert.NotEqual(0L, OpenTag(g));

        // Act
        ReplyFrame r = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(sack), N(5.5), N(5.5), Val.Int(0) }, level: "hall", player: "bob");
        Val placed = Assert.Single(Effects(r, "npc.place"));
        long open = OpenTag(g);
        long openAgain = OpenTag(g);
        ReplyFrame refused = Mud(g, "drop", "sword");

        // Assert
        Assert.True(Result(r));
        Assert.Contains("You unapply bigsack (open).", Lines(r));
        Assert.False(Applied(g, H(placed.Get("handle")!.AsHandle)));
        Assert.NotEqual(0L, open); // C leaves op->container as it was
        Assert.Equal(open, openAgain); // and its tag is one tag, not a new one at each look
        Assert.Equal(open + ",bigsack,mud_bigsack.png", Shown(g, "clientr.mud_container"));
        Assert.Equal(new[] { "The bigsack is not active." }, Lines(refused));
        Assert.Equal("sword", g.Tree());
    }

    [Fact]
    public void Apply_Given_the_dropped_sack_that_is_still_his_container_When_he_applies_it_on_the_ground_Then_it_opens_again_and_a_drop_goes_into_it()
    {
        // Arrange
        using var g = new Game("arch bigsack\napplied 1\nend\narch sword\nend\n");
        long sack = g.Tag("bigsack");
        Apply(g, sack);
        ReplyFrame dropped = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(sack), N(5.5), N(5.5), Val.Int(0) }, level: "hall", player: "bob");
        ulong face = Effects(dropped, "npc.place").Single().Get("handle")!.AsHandle;
        AtFeet(g);

        // Act
        ReplyFrame opened = g.T("apply", H(face), Val.Int(0));
        ReplyFrame put = Mud(g, "drop", "sword");

        // Assert
        Assert.Equal(new[] { "You open bigsack." }, Lines(opened));
        Assert.True(Applied(g, H(face)));
        Assert.Equal(new[] { "You put the swords in bigsack (open) (active)." }, Lines(put));
        Assert.Equal("", g.Tree());
        Assert.Equal("bigsack[sword]", g.T("ground", H(face)).Result.AsString);
    }

    [Fact]
    public void Move_Given_the_empty_sack_he_has_open_When_it_is_put_into_another_sack_Then_it_is_unapplied_and_stays_his_container_as_in_C()
    {
        // Arrange: put_object_in_sack unapplies it and remove_ob (inventory branch) leaves op->container
        using var g = new Game("arch lightsack\napplied 1\nend\narch bigsack\napplied 1\nend\n");
        long inner = g.Tag("lightsack");
        long outer = g.Tag("bigsack");
        Apply(g, inner);
        Assert.Equal(inner, OpenTag(g));

        // Act
        ReplyFrame r = g.Move(inner, outer, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal("bigsack[lightsack]", g.Tree());
        Assert.False(g.T("flag", Val.Int(inner), Val.Int(Define.FLAG_APPLIED)).Result.AsBool);
        Assert.Equal(inner, OpenTag(g));
        Assert.Equal(inner + ",lightsack,mud_lightsack.png", Shown(g, "clientr.mud_container"));
    }

    [Fact]
    public void Move_Given_the_sack_he_has_open_with_things_in_it_When_it_is_given_to_another_sack_Then_its_things_go_there_and_it_stays_open_as_in_C()
    {
        // Arrange: put_object_in_sack moves the contents of a container that holds something (Eneq), never the container
        using var g = new Game("arch lightsack\napplied 1\narch banana\nnrof 2\nend\nend\narch bigsack\napplied 1\nend\n");
        long inner = g.Tag("lightsack");
        long outer = g.Tag("bigsack");
        Apply(g, inner);

        // Act
        ReplyFrame r = g.Move(inner, outer, 0);

        // Assert
        Assert.Equal(new[] { "You move the items from lightsack (open) (active) into lightsack (open) (active).", "You put the bananas in bigsack (active)." }, Lines(r));
        Assert.Equal("lightsack,bigsack[bananax2]", g.Tree());
        Assert.Equal(inner, OpenTag(g));
        Assert.True(g.T("flag", Val.Int(inner), Val.Int(Define.FLAG_APPLIED)).Result.AsBool);
    }

    [Fact]
    public void MudTransferObject_Given_an_item_in_the_sack_on_the_ground_that_the_player_has_open_When_given_Then_it_is_refused_and_the_level_keeps_it()
    {
        // Arrange: the sack belongs to the level, and a trade writes only the two accounts: the banana would be in two places after a restart
        using var g = new Game();
        g.Ext.Call(5, new[] { S("amy") }, player: "amy");
        g.T("apply", H(g.Hall[0]), Val.Int(0));
        ReplyFrame inner = g.Ext.Call(12, new[] { H(g.Hall[0]), Val.Int(Obj("muditems")), Val.List(Val.Int(Obj("mudrealname"))) });
        long banana = (uint)inner.Result.Items.First(x => x.Items[1].AsString == "banana").Items[0].AsHandle;
        var host = new FakeHost();
        host.Answer = (name, _) => name == "player.at" ? new QueryAnswer(Status.Ok, S("amy")) : new QueryAnswer(Status.Ok, Val.Nil);
        g.Ext.Dispatcher.QuerySource = host.Ask;
        string before = g.T("ground", H(g.Hall[0])).Result.AsString;

        // Act
        ReplyFrame r = g.Ext.Call(Manifest.OpMudTransferObject, new[] { Val.Int(banana), Val.Int(0), Val.Int(1), Val.Int(1) }, level: "hall", player: "bob");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.False(r.Result.AsBool);
        Assert.Equal(before, g.T("ground", H(g.Hall[0])).Result.AsString);
        Assert.Equal("", g.Tree());
    }
}
