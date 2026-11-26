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
}
