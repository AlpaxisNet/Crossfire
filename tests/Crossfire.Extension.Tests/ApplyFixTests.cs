// Not ported from a Crossfire file: the tests of the fix lane of wave 6, for the apply of what lies on the ground (docs/mud-phase3.md
// section 4.4: the face's apply calls mudapplyobject).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed partial class ApplyTests
{
    private static string DataFile(DirectExt ext, string rel)
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

    private static bool WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 500 && !condition(); i++)
        {
            Thread.Sleep(10);
        }
        return condition();
    }

    [Fact]
    public void MudApplyObject_Given_bananas_on_the_ground_When_eaten_one_by_one_Then_each_apply_uses_one_up_and_the_face_goes_with_the_last()
    {
        // Arrange: the hall's banana is a stack of five
        using var g = new Game();
        ulong ground = g.Hall()[PickupWorld.Banana];

        // Act
        var results = new List<ReplyFrame>();
        var counts = new List<long>();
        for (int i = 0; i < 5; i++)
        {
            results.Add(g.Ext.Call(13, new[] { H(ground), Val.Int(TMudObjectMembers.MethodMudApplyObject), Val.List() }, level: "hall", player: "bob"));
            counts.Add(i < 4 ? g.Read(ground, "mudnrof").AsInt : -1);
        }
        ReplyFrame sixth = g.Ext.Call(13, new[] { H(ground), Val.Int(TMudObjectMembers.MethodMudApplyObject), Val.List() }, level: "hall", player: "bob");

        // Assert: one is used up each time, the last takes the face with it, and the level is saved without it
        Assert.Equal(new long[] { 4, 3, 2, 1, -1 }, counts);
        Assert.All(results, r => Assert.True(Result(r)));
        Assert.All(results, r => Assert.Contains("The banana tasted good.", Lines(r)));
        Assert.Equal(1, Effects(results[4], "npc.remove").Count(e => e.Get("handle")!.AsHandle == ground));
        Assert.Empty(Effects(results[0], "npc.remove"));
        Assert.Contains(results[0].Events, e => e.Get("name")!.AsString == "onUpdateObject" && e.Get("handle")!.AsHandle == ground);
        Assert.NotEqual(0UL, sixth.Status); // the handle is dead
        Assert.True(WaitUntil(() => !DataFile(g.Ext, "levels/hall.map").Contains("arch banana")));
    }

    [Fact]
    public void PlayerApply_Given_a_flying_player_and_an_object_of_his_inventory_When_applied_Then_he_is_not_floating_above_it()
    {
        // Arrange
        using var g = new Game("arch sword\nend\n");
        long id = g.Id(g.Item("sword"));

        // Act
        ReplyFrame r = g.Ext.Call(ApplyWorld.OpDo, new[] { S("flyinv"), S("bob"), Val.Int(id), Val.Nil }, player: "bob");

        // Assert
        Assert.DoesNotContain("But you are floating high above the ground!", Lines(r));
        Assert.Contains(r.Events, e => e.Get("name")!.AsString == "onApply"); // the apply went on: the script is asked
    }

    [Fact]
    public void DefaultApply_Given_an_unpaid_item_that_is_worn_When_applied_Then_it_is_taken_off_without_paying()
    {
        // Arrange
        using var g = new Game("arch sword\nunpaid 1\napplied 1\nend\n");
        ulong sword = g.Item("sword");

        // Act
        ReplyFrame r = g.DefaultApply(sword);

        // Assert
        Assert.DoesNotContain("You should pay for it first.", Lines(r));
        Assert.False(g.Applied(sword));
    }

    [Fact]
    public void MudApplyInvObject_Given_an_item_inside_a_sack_of_the_pack_When_applied_by_id_Then_it_is_found_and_the_script_is_told()
    {
        // Arrange
        using var g = new Game("arch sack\napplied 1\narch sword\nend\nend\n");
        ulong sack = g.Item("sack");
        ReplyFrame rows = g.Ext.Call(12, new[] { H(sack), Val.Int(Obj("muditems")), Val.List(Val.Int(Obj("mudrealname"))) });
        ulong sword = rows.Result.Items.First(x => x.Items[1].AsString == "sword").Items[0].AsHandle;

        // Act
        ReplyFrame r = g.ApplyId(sword);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Contains("onApplyItem", EventNames(r));
        Assert.Contains("onApply", r.Events.Select(e => e.Get("name")!.AsString));
    }
}
