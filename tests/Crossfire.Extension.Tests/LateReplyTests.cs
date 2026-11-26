// Not ported from a Crossfire file: the extension's half of "no second copy when a late success follows the fallback" (docs/mud-phase3.md
// sections 3 rule 10 and 9.1): a slow function of items answers busy before it changes anything while the worker is busy, and the same
// call then succeeds once (one copy, never two); a call whose reply the host gave up on has changed the tree once, and nothing in the
// extension runs it again.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed partial class ContainerTests
{
    private const string LateStart = "arch sack\napplied 1\nend\narch banana\nnrof 4\nend\n";

    /// <summary>The call of a slow function on the player's items, (the apply is its fallback: defaultApply).</summary>
    private static ReplyFrame LateCall(Game g, string function) => function switch
    {
        "mudaddinvobject" => g.Ext.Call(Manifest.OpMudAddInvObject, new[] { Val.Int(1), S("banana") }, level: "hall", player: "bob"),
        "muddropinvobject" => g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(g.Tag("banana")), Val.Float(3), Val.Float(4), Val.Int(1) }, level: "hall", player: "bob"),
        "mudmoveinvobject" => g.Move(g.Tag("banana"), g.Tag("sack"), 1),
        // the fallback of the onApply that mudapplyinvobject sent: a method, which asks the extension for the busy answer itself
        "defaultApply" => g.Ext.Call(13, new[] { H(Game.ItemHandle(g.Tag("sack"))), Val.Int(TMudObjectMembers.MethodDefaultApply), Val.List(H(g.PlayerHandle())) }, level: "hall", player: "bob"),
        _ => throw new ArgumentOutOfRangeException(nameof(function)),
    };

    [Theory]
    [InlineData("mudaddinvobject", "banana,sack,bananax4", false)]
    [InlineData("muddropinvobject", "sack,bananax3", false)]
    [InlineData("mudmoveinvobject", "sack[banana],bananax3", false)]
    [InlineData("defaultApply", "sack,bananax4", true)]
    public void SlowFunction_Given_a_busy_worker_and_then_a_late_success_When_the_script_falls_back_Then_there_is_one_copy_not_two(string function, string once, bool opens)
    {
        // Arrange
        using var g = new Game(LateStart);
        string before = g.Tree();
        g.Ext.Dispatcher.Busy = "saving";

        // Act: the call that passes the deadline of the host is answered busy ...
        ReplyFrame busy = LateCall(g, function);
        string whileBusy = g.Tree();
        long openWhileBusy = OpenTag(g);
        // ... the script falls back (the host's side: it makes the object on the ground), and the worker is free again: the same call is
        // run once more by whatever retries it, here the test
        g.Ext.Dispatcher.Busy = null;
        ReplyFrame late = LateCall(g, function);
        // the host gave up on the reply of this call: nothing in the extension runs it again, at the next request or at the flush
        g.T("weight");
        g.T("weight");

        // Assert
        Assert.Equal((ulong)Status.Busy, busy.Status);
        Assert.Empty(busy.Effects);
        Assert.Empty(busy.Events);
        Assert.Equal(before, whileBusy);
        Assert.Equal(0L, openWhileBusy);
        Assert.Equal(0UL, late.Status);
        Assert.True(late.Result.Kind == ValKind.Int ? late.Result.AsInt == 1 : late.Result.AsBool);
        Assert.Equal(once, g.Tree());
        Assert.Equal(opens, OpenTag(g) != 0);
    }
}
