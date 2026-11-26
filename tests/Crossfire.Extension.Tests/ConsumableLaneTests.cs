// Not ported from a Crossfire file: tests of lane 6C through the whole extension: Dispatcher.ApplyByLane asks ApplyWave6C, which applies
// food, drink, flesh, potions and poison and answers null for any other type, and the player event onEatFood(foodarch, food, wascursed)
// follows the state change on a link of 1.1 (docs/mud-phase3.md 4.4).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed class ConsumableLaneTests
{
    private const int OpApply = 10900;

    private const string Header = "mudorigstats 14 12 10 9 7 8 11\nmudlevhp 8 8 8 8 8 8 8 8 8 8\nmudlevsp 3 3 3 3 3 3 3 3 3 3\nmudlevgrace 2 2 2 2 2 2 2 2 2 2\nlevel 1\nfood 500\n";

    private const string Arc =
        ItemWorld.Arc
        + "Object apple\nname apple\nname_pl apples\ntype 6\nfood 100\nweight 10\nend\n"
        + "Object bad_booze\nname bad booze\ntype 7\nweight 10\nend\n"
        + "Object potion_heal\nname potion of healing\ntype 5\nsp 5\nweight 10\nend\n";

    private static ExtensionOptions Options() => new()
    {
        Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = b => b.AddFunction(Lane.Wave6C, new FunctionDef(OpApply, "t6capply", new[] { "string", "string" }, "int", Handler: Apply)),
    };

    // what 6B's manual_apply does for an item of the player's: ask the lane first; -1 when no lane owns the type
    private static Outcome Apply(Dispatcher d, IReadOnlyList<Val> a)
    {
        Player p = d.ControllerOf(a[0].AsString)!;
        CfObject ob = p.State.Ob;
        CfObject? item = null;
        for (CfObject? c = ob.Inv; c != null && item == null; c = c.Below)
        {
            item = c.Arch?.Name == a[1].AsString ? c : null;
        }
        int? r = d.ApplyByLane(ob, item!, 0);
        return Outcome.Ok(Val.Int(r ?? -1));
    }

    private static DirectExt Start(string inventory, long minor = 1)
    {
        TempFolder world = TestWorld.Make(arc: Arc);
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts"));
        foreach (string c in ObjectWorld.Classes)
        {
            File.WriteAllText(Path.Combine(world.Path, "scripts", c + ".txt"), "// class\n");
        }
        Directory.CreateDirectory(Path.Combine(world.Path, "mudlevels"));
        ItemWorld.Account(world, "bob", Header, inventory);
        var ext = new DirectExt(world, Options(), 1, true, 16_777_216, minor);
        Assert.Equal(0UL, ext.Call(5, new[] { S("bob") }, player: "bob").Status);
        return ext;
    }

    private static ReplyFrame Do(DirectExt ext, string arch) => ext.Call(OpApply, new[] { S("bob"), S(arch) }, player: "bob");

    private static List<string> Lines(ReplyFrame r) =>
        r.Events.Where(e => e.Get("name")!.AsString == "onRPGMessage").Select(e => e.Get("args")!.Items[0].AsString).ToList();

    [Fact]
    public void ApplyByLane_Given_an_apple_When_applied_Then_it_is_eaten_the_client_is_told_and_onEatFood_follows_with_three_typed_arguments()
    {
        // Arrange
        using DirectExt ext = Start("arch apple\nnrof 2\nend\n");

        // Act
        ReplyFrame r = Do(ext, "apple");

        // Assert
        Assert.Equal(1, r.Result.AsInt);
        Assert.Equal(new[] { "The apple tasted good." }, Lines(r));
        Val ev = r.Events.Single(e => e.Get("name")!.AsString == "onEatFood");
        Assert.Equal("player", ev.Get("target")!.AsString);
        Assert.Equal("bob", ev.Get("player")!.AsString);
        IReadOnlyList<Val> args = ev.Get("args")!.Items;
        Assert.Equal(3, args.Count);
        Assert.Equal(0, args[2].AsInt);
        // the first argument is the handle of the archetype apple, the same one the extension gives for it
        Assert.Equal(ArchHandle(ext, "apple"), args[0].AsHandle);
        // the event is after the line of text: the state changed before it is sent
        List<string> names = r.Events.Select(e => e.Get("name")!.AsString).ToList();
        Assert.True(names.IndexOf("onRPGMessage") < names.IndexOf("onEatFood"));
    }

    [Fact]
    public void ApplyByLane_Given_a_link_of_1_0_When_an_apple_is_eaten_Then_no_event_onEatFood_is_sent()
    {
        // Arrange: the event has typed parameters, a 1.1 key
        using DirectExt ext = Start("arch apple\nnrof 1\nend\n", minor: 0);

        // Act
        ReplyFrame r = Do(ext, "apple");

        // Assert
        Assert.Equal(1, r.Result.AsInt);
        Assert.DoesNotContain(r.Events, e => e.Get("name")!.AsString == "onEatFood");
        Assert.Equal("The apple tasted good.", Effects(r, "client.text").Single().Get("text")!.AsString);
    }

    [Fact]
    public void ApplyByLane_Given_a_cursed_apple_When_eaten_Then_onEatFood_says_it_was_cursed()
    {
        // Arrange
        using DirectExt ext = Start("arch apple\nnrof 1\ncursed 1\nend\n");

        // Act
        ReplyFrame r = Do(ext, "apple");

        // Assert
        Val ev = r.Events.Single(e => e.Get("name")!.AsString == "onEatFood");
        Assert.Equal(1, ev.Get("args")!.Items[2].AsInt);
    }

    [Fact]
    public void ApplyByLane_Given_a_potion_that_is_not_available_When_applied_Then_it_is_not_eaten_and_no_event_is_sent()
    {
        // Arrange
        using DirectExt ext = Start("arch potion_heal\nnrof 1\nend\n");

        // Act
        ReplyFrame r = Do(ext, "potion_heal");

        // Assert
        Assert.Equal(1, r.Result.AsInt);
        Assert.Equal(Consumable.NotAvailable, Lines(r)[^1]);
        Assert.DoesNotContain(r.Events, e => e.Get("name")!.AsString == "onEatFood");
        Assert.Equal("potion_heal", ((Player)ext.Dispatcher.ControllerOf("bob")!).State.Ob.Inv?.Arch?.Name);
    }

    [Fact]
    public void ApplyByLane_Given_poison_When_drunk_Then_it_is_used_up_without_an_eating_event()
    {
        // Arrange
        using DirectExt ext = Start("arch bad_booze\nnrof 1\nend\n");

        // Act
        ReplyFrame r = Do(ext, "bad_booze");

        // Assert
        Assert.Equal(1, r.Result.AsInt);
        Assert.Equal(new[] { "Yech!  That tasted poisonous!" }, Lines(r));
        Assert.Null(((Player)ext.Dispatcher.ControllerOf("bob")!).State.Ob.Inv);
        Assert.DoesNotContain(r.Events, e => e.Get("name")!.AsString == "onEatFood");
    }

    [Fact]
    public void ApplyByLane_Given_armour_When_asked_Then_the_lane_answers_null_so_that_6B_goes_on()
    {
        // Arrange
        using DirectExt ext = Start("arch mail\nnrof 1\nend\n");

        // Act
        ReplyFrame r = Do(ext, "mail");

        // Assert
        Assert.Equal(-1, r.Result.AsInt);
        Assert.Empty(Lines(r));
    }

    [Fact]
    public void Manifest_Given_the_lane_When_built_on_1_1_Then_onEatFood_is_a_player_event_with_three_typed_parameters_and_on_1_0_it_is_left_out()
    {
        // Act
        Val v11 = Manifest.Build(linkMinor: 1);
        Val v10 = Manifest.Build(linkMinor: 0);

        // Assert
        Val ev = v11.Get("events")!.Items.Single(e => e.Get("name")!.AsString == "onEatFood");
        Assert.Equal("player", ev.Get("target")!.AsString);
        Assert.Equal(new[] { "handle:TMudArch", "handle:TMudObject", "int" }, ev.Get("params")!.Items.Select(p => p.AsString).ToArray());
        Assert.DoesNotContain(v10.Get("events")!.Items, e => e.Get("name")!.AsString == "onEatFood");
    }
}
