// Not ported from a Crossfire file: tests of the fixes that the review of wave 7 asked for (docs/mud-phase3.md, "Wave 7 as built"):
// what mudtransferobject refuses, the login of a player whose file carries no_fix_player, the cut title and the width of the name lists.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed class W7FixTests
{
    private static ReplyFrame Transfer(W7Game g, long id) =>
        g.Ext.Call(Manifest.OpMudTransferObject, new[] { Val.Int(id), Val.Int(0), Val.Int(1), Val.Int(0) }, level: "hall", player: "bob");

    [Fact]
    public void Transfer_Given_a_loan_of_the_gods_When_given_Then_it_is_refused_and_stays_with_the_giver()
    {
        // Arrange: C's drop_object makes a FLAG_STARTEQUIP item vanish; a gift would copy it into the other's file
        using var g = new W7Game(bob: ApplyWorld.Skills + "arch ring\nend\n");
        ulong bob = g.Handle("bob");
        ulong carol = g.Handle("carol");
        CfObject ring = g.Ob("bob").Inv!;
        while (ring.Arch?.Clone.Name != "ring")
        {
            ring = ring.Below!;
        }
        ring.SetFlag(Define.FLAG_STARTEQUIP);

        // Act
        ReplyFrame r = Transfer(g, g.IdOf(ring));

        // Assert
        Assert.False(r.Result.AsBool);
        Assert.Contains("ring", g.Names(bob));
        Assert.DoesNotContain("ring", g.Names(carol));
    }

    [Fact]
    public void Transfer_Given_an_invisible_object_of_the_giver_When_given_Then_it_is_refused()
    {
        // Arrange: a skill or an experience object has a tag once a handle was issued for it
        using var g = new W7Game(bob: ApplyWorld.Skills + "arch ring\nend\n");
        g.Handle("bob");
        g.Handle("carol");
        CfObject hidden = g.Ob("bob").Inv!;
        while (hidden.Invisible == 0)
        {
            hidden = hidden.Below!;
        }
        long id = g.IdOf(hidden);

        // Act
        ReplyFrame r = Transfer(g, id);

        // Assert
        Assert.False(r.Result.AsBool);
        Assert.Same(g.Ob("bob"), hidden.Env);
    }

    [Fact]
    public void Login_Given_a_file_with_no_fix_player_When_the_player_logs_in_Then_the_flag_is_cleared_and_the_player_is_fixed()
    {
        // Arrange: login.c clears FLAG_NO_FIX_PLAYER after load_object, or the player would never be fixed again
        using var g = new W7Game(header: ApplyWorld.Header + "no_fix_player 1\n");

        // Act
        ReplyFrame r = g.Login("bob");

        // Assert
        Assert.False(g.Ob("bob").QueryFlag(Define.FLAG_NO_FIX_PLAYER));
        Assert.Equal("14", W7Game.Sets(r)["clientr.mud_str"]);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("123456789012345", "123456789012345")]
    [InlineData("1234567890123456", "123456789012345")]
    [InlineData("12345678901234567890", "123456789012345")]
    public void CutTitle_Given_a_name_When_cut_Then_MAX_NAME_minus_one_characters_remain(string name, string expected)
    {
        // Assert
        Assert.Equal(expected, Commands.CutTitle(name));
    }

    [Theory]
    [InlineData(40, false)] // " " + 40 characters: the count is 41
    [InlineData(41, false)] // the count is exactly 42: still the same line ("more than 42" breaks it)
    [InlineData(42, true)] // 43: the name begins a line of its own
    public void WrapNames_Given_a_name_that_ends_at_the_limit_When_listed_Then_the_line_breaks_only_above_42_characters(int length, bool breaks)
    {
        // Arrange
        var said = new List<string>();
        string name = new('n', length);

        // Act
        HelpCommand.WrapNames(said.Add, new[] { name });

        // Assert: a name that does not fit says the line before it (empty here), then the last line
        Assert.Equal(breaks ? new[] { "", " " + name } : new[] { " " + name }, said);
    }
}
