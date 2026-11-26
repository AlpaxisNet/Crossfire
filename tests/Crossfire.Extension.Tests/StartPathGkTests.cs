// Not ported from a Crossfire file: tests of the start path on Graal Kingdoms' own archetypes, which the environment names (nothing of
// it is part of this repository):
//
//   CROSSFIRE_GK_ARCH       the Arch folder
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using Xunit.Abstractions;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed class StartPathGkTests
{
    // the six classes the start screen offers (server.classes) and the lists their class archetypes name
    private static readonly string[] Classes = { "warrior", "barbarian", "thief", "priest", "alchemist", "sorcerer" };

    private readonly ITestOutputHelper output;

    public StartPathGkTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static string Embedded(string name)
    {
        using Stream s = typeof(Dispatcher).Assembly.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return FileText.FromBytes(ms.ToArray());
    }

    private static (CfWorld World, GkHost Host, List<string> Log) LoadWorld()
    {
        var log = new List<string>();
        var host = new GkHost();
        var world = new CfWorld(host, (level, text) => log.Add(level + ": " + text.TrimEnd('\n')));
        host.World = world;
        world.TreasureText = Embedded("treasures");
        world.InitArchetypes(ArchCollector.Collect(GkWorldFactAttribute.Folder("CROSSFIRE_GK_ARCH")).Archetypes);
        host.Skills = SkillTable.Create(world, Embedded("skill_params"));
        return (world, host, log);
    }

    // the archetype names a list holds, through the lists it leads to (each list once); archetypes the world lacks are returned as null
    private static void Walk(CfWorld world, Treasure? t, List<Archetype?> items, HashSet<string> seen)
    {
        for (; t != null; t = t.Next)
        {
            if (t.Name != null && t.Name != "NONE" && seen.Add(t.Name) && world.FindTreasurelist(t.Name) is { Items: { } inner })
            {
                Walk(world, inner, items, seen);
            }
            else if (t.Name == null)
            {
                items.Add(t.Item);
            }
            Walk(world, t.NextYes, items, seen);
            Walk(world, t.NextNo, items, seen);
        }
    }

    private static List<Archetype?> ItemsOf(CfWorld world, string list)
    {
        var items = new List<Archetype?>();
        Walk(world, world.FindTreasurelist(list)!.Items, items, new HashSet<string> { list });
        return items;
    }

    [GkWorldFact("CROSSFIRE_GK_ARCH")]
    public void ClassItems_Given_the_six_class_lists_When_read_with_Graal_Kingdoms_archetypes_Then_every_archetype_of_every_list_exists()
    {
        // Arrange
        (CfWorld world, _, _) = LoadWorld();

        // Act
        var all = new List<Archetype?>();
        foreach (string c in Classes)
        {
            all.AddRange(ItemsOf(world, c + "_class_items"));
        }

        // Assert
        Assert.DoesNotContain(null, all);
        string[] distinct = all.Select(a => a!.Name!).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        output.WriteLine(distinct.Length + " distinct archetypes in " + all.Count + " items: " + string.Join(", ", distinct));
        Assert.Equal(42, distinct.Length);
        Assert.Equal(84, all.Count);
    }

    [GkWorldFact("CROSSFIRE_GK_ARCH")]
    public void ClassItems_Given_each_of_the_six_lists_When_made_into_a_player_Then_the_player_holds_the_items_of_the_list_with_Graal_Kingdoms_archetypes()
    {
        // Arrange
        (CfWorld world, _, _) = LoadWorld();

        foreach (string c in Classes)
        {
            // Act
            CfObject pl = world.ArchToObject(world.GetPlayerArchetype(null))!;
            Start.GiveInitialItems(world, pl, world.FindTreasurelist(c + "_class_items"));

            // Assert: what the list holds, less the abilities (a known spell) and what the race cannot use; two equal books merge into a stack
            var held = new List<string>();
            for (CfObject? i = pl.Inv; i != null; i = i.Below)
            {
                held.AddRange(Enumerable.Repeat(i.Arch!.Name!, (int)Math.Max(1, i.Nrof)));
            }
            bool armsAllowed = pl.QueryFlag(Define.FLAG_USE_ARMOUR) && pl.QueryFlag(Define.FLAG_USE_WEAPON);
            string[] expected = ItemsOf(world, c + "_class_items")
                .Where(a => a!.Clone.Type != Define.ABILITY)
                .Where(a => armsAllowed || !(a!.Clone.Type is Define.ARMOUR or Define.BOOTS or Define.CLOAK or Define.HELMET or Define.SHIELD or Define.GLOVES or Define.BRACERS or Define.GIRDLE or Define.WEAPON))
                .Select(a => a!.Name!).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(expected, held.OrderBy(n => n, StringComparer.Ordinal).ToArray());
            Assert.True(held.Count > 0, c);
        }
    }

    private static TempFolder GkWorld()
    {
        var world = new TempFolder();
        Directory.CreateSymbolicLink(Path.Combine(world.Path, "Arch"), GkWorldFactAttribute.Folder("CROSSFIRE_GK_ARCH"));
        return world;
    }

    private static CfObject Ob(DirectExt ext, string account) => ext.Dispatcher.ControllerOf(account)!.State.Ob;

    private static IEnumerable<CfObject> Inv(CfObject op)
    {
        for (CfObject? c = op.Inv; c != null; c = c.Below)
        {
            yield return c;
        }
    }

    [GkWorldFact("CROSSFIRE_GK_ARCH")]
    public void StartScreen_Given_the_calls_of_the_select_stats_screen_When_run_Then_the_player_has_life_right_after_rollstats_and_the_items_of_the_class_after_setmudclass()
    {
        // Arrange
        using TempFolder world = GkWorld();
        using var ext = new DirectExt(world, new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) } }, ownsWorld: false);
        ulong h = ext.Call(5, new[] { S("hero") }, player: "hero").Result.AsHandle;

        // Act: the order of npcSelectStats: mudresetplayer(), player.rollstats(), player.setmudclass("<class>_class")
        ext.Call(Manifest.OpMudResetPlayer, player: "hero");
        ReplyFrame rolled = ext.Call(13, new[] { H(h), Val.Int(TMudPlayerMembers.MethodRollStats), Val.List() }, player: "hero");
        long maxhp = Get(ext, h, Ply("mudmaxhp")).AsInt;
        long maxsp = Get(ext, h, Ply("mudmaxsp")).AsInt;
        long maxgrace = Get(ext, h, Ply("mudmaxgrace")).AsInt;
        ReplyFrame chosen = ext.Call(13, new[] { H(h), Val.Int(TMudPlayerMembers.MethodSetMudClass), Val.List(S("warrior_class")) }, player: "hero");

        // Assert
        Assert.Equal(0UL, rolled.Status);
        Assert.True(maxhp >= 1);
        Assert.True(maxsp >= 1);
        Assert.True(maxgrace >= 1);
        Assert.Equal(maxhp, Get(ext, h, Ply("mudhp")).AsInt);
        Assert.Equal(long.Parse(Effects(rolled, "client.set").Single(e => e.Get("name")!.AsString == "clientr.mud_maxhp").Get("value")!.AsString), maxhp);
        long sum = new[] { "mudstrength", "muddexterity", "mudconstitution", "mudintelligence", "mudwisdom", "mudpower", "mudcharisma" }.Sum(n => Get(ext, h, Ply(n)).AsInt);
        Assert.InRange(sum, 82, 116);
        Assert.Equal(1L, Get(ext, h, Ply("mudlevel")).AsInt);
        Assert.True(chosen.Result.AsBool);
        string[] names = Inv(Ob(ext, "hero")).Select(i => i.Arch!.Name!).ToArray();
        Assert.Contains("warrior_class", names);
        Assert.Contains("sword", names);
        Assert.Equal(6, names.Count(n => n.StartsWith("experience_", StringComparison.Ordinal)));
        Assert.Equal("warrior", Get(ext, h, Ply("mudclass")).AsString);
        SkillTable skills = ext.Dispatcher.SkillsOfTests!;
        CfObject[] skillObs = Inv(Ob(ext, "hero")).Where(i => i.Type == Define.SKILL).ToArray();
        Assert.NotEmpty(skillObs);
        Assert.All(skillObs, s => Assert.Equal(skills.Skills[s.Stats.Sp].Category != SkillTable.ExpNone, s.ExpObj != null));
        Assert.Contains(skillObs, s => s.ExpObj != null);
    }

    [GkWorldFact("CROSSFIRE_GK_ARCH")]
    public void StartScreen_Given_each_of_the_six_classes_chosen_in_turn_When_run_Then_only_the_last_choice_is_in_the_tree_and_every_choice_works()
    {
        // Arrange
        using TempFolder world = GkWorld();
        using var ext = new DirectExt(world, new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) } }, ownsWorld: false);
        ulong h = ext.Call(5, new[] { S("hero") }, player: "hero").Result.AsHandle;
        ext.Call(Manifest.OpMudRollStats, player: "hero");

        // Act / Assert
        foreach (string c in Classes)
        {
            ReplyFrame r = ext.Call(13, new[] { H(h), Val.Int(TMudPlayerMembers.MethodSetMudClass), Val.List(S(c + "_class")) }, player: "hero");
            Assert.Equal(0UL, r.Status);
            Assert.True(r.Result.AsBool, c);
            string[] names = Inv(Ob(ext, "hero")).Select(i => i.Arch!.Name!).ToArray();
            Assert.Single(names, n => n.EndsWith("_class", StringComparison.Ordinal));
            Assert.Contains(c + "_class", names);
            Assert.Equal(c, Get(ext, h, Ply("mudclass")).AsString);
        }
        // the last one, the sorcerer, has the books of its list and nothing of the barbarian's
        string[] last = Inv(Ob(ext, "hero")).Select(i => i.Arch!.Name!).ToArray();
        Assert.Contains("guide_wizard", last);
        Assert.DoesNotContain("horned_helmet", last);
    }
}
