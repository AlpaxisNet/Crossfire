// Not ported from a Crossfire file: the tests of the fix lane of wave 6 for containers: a container on the ground that goes away while a
// player has it open, and a take out of an open container on the ground (the level's file is written first).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed partial class ContainerTests
{
    [Fact]
    public void LevelUnload_Given_a_player_with_a_sack_of_the_level_open_When_it_unloads_Then_the_sack_is_closed_and_his_drop_loses_nothing()
    {
        // Arrange
        using var g = new Game("arch banana\nnrof 2\nend\n");
        g.T("apply", H(g.Hall[0]), Val.Int(0));
        Assert.NotEqual(0L, OpenTag(g));

        // Act
        ReplyFrame unload = g.Ext.Call(8, new[] { S("hall") }, player: "bob");
        ReplyFrame drop = g.Command("drop", "banana");

        // Assert
        Assert.Equal(0UL, unload.Status);
        Assert.Equal(0L, OpenTag(g));
        Assert.Contains("You close sack (open) (active).", Lines(unload));
        Assert.DoesNotContain(Lines(drop), l => l.StartsWith("You put", StringComparison.Ordinal));
        Assert.Equal("bananax2", g.Tree());
    }

    [Fact]
    public void DestroyMudObject_Given_a_player_with_a_sack_on_the_ground_open_When_a_script_destroys_it_Then_the_sack_is_closed()
    {
        // Arrange
        using var g = new Game("arch banana\nnrof 2\nend\n");
        g.T("apply", H(g.Hall[0]), Val.Int(0));
        Assert.NotEqual(0L, OpenTag(g));

        // Act
        ReplyFrame destroy = g.Ext.Call(13, new[] { H(g.Hall[0]), Val.Int(TMudObjectMembers.MethodDestroyMudObject), Val.List() }, level: "hall", player: "bob");
        ReplyFrame drop = g.Command("drop", "banana");

        // Assert
        Assert.Equal(0UL, destroy.Status);
        Assert.Equal(0L, OpenTag(g));
        Assert.DoesNotContain(Lines(drop), l => l.StartsWith("You put", StringComparison.Ordinal));
        Assert.Equal("bananax2", g.Tree());
    }

    [Fact]
    public void Take_Given_an_open_sack_on_the_ground_When_its_contents_are_taken_Then_the_level_is_written_first_and_the_account_second_at_once()
    {
        // Arrange
        var writing = new Writing();
        using var g = new Game(options: writing.Options);
        g.T("apply", H(g.Hall[0]), Val.Int(0));

        // Act
        ReplyFrame r = g.Command("take", null);
        bool written = WaitFor(() => writing.Order.Count > 0 && ReadData(g.Ext, "accounts/bob.txt").Contains("arch banana"));

        // Assert
        Assert.Equal(new[] { "You pick up the bananas." }, Lines(r));
        Assert.True(written);
        Assert.Equal(new[] { "levels/hall.map>accounts/bob.txt" }, writing.Order);
        Assert.Equal("bananax3", g.Tree());
    }

    [Fact]
    public void Take_Given_a_part_of_a_stack_in_an_open_sack_on_the_ground_When_taken_Then_the_level_is_written_first_too()
    {
        // Arrange
        var writing = new Writing();
        using var g = new Game(options: writing.Options);
        g.T("apply", H(g.Hall[0]), Val.Int(0));

        // Act
        ReplyFrame r = g.Command("take", "2 banana");
        bool written = WaitFor(() => writing.Order.Count > 0 && ReadData(g.Ext, "accounts/bob.txt").Contains("arch banana"));

        // Assert
        Assert.True(written);
        Assert.Equal(new[] { "levels/hall.map>accounts/bob.txt" }, writing.Order);
        Assert.Equal("bananax2", g.Tree());
        Assert.Contains("nrof 1", ReadData(g.Ext, "levels/hall.map"));
    }

    [Fact]
    public void Move_Given_a_sack_inside_a_sack_of_the_pack_as_the_place_When_an_item_is_moved_into_it_Then_it_goes_in()
    {
        // Arrange
        using var g = new Game("arch bigsack\napplied 1\narch lightsack\napplied 1\nend\nend\narch banana\nend\n");
        long inner = g.Inner("bigsack", "lightsack");

        // Act
        ReplyFrame r = g.Move(g.Tag("banana"), inner, 0);

        // Assert
        Assert.True(Result(r));
        Assert.Equal("bigsack[lightsack[banana]]", g.Tree());
    }
}
