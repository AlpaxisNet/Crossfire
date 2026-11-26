// Not ported from a Crossfire file: tests of lane 4D, the start path (docs/mud-phase3.md section 4.7): the methods setmudclass,
// setmudclassnoaction, rollstats, mudresetplayer and the account methods of TMudPlayer, the functions mudresetplayer and mudrollstats,
// and the accounts' extra lines for the stats.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>An invented world with a player, the six experience objects, three skills, a class and what its list gives.</summary>
internal static class StartWorld
{
    public const string Arc =
        "Object dwarf_player\nname dwarf\nface dwarf.171\ntype 1\nalive 1\ncan_use_armour 1\ncan_use_weapon 1\nhp 10\nmaxhp 12\nlevel 1\nStr 14\nDex 12\nCon 10\nInt 8\nWis 9\nPow 11\nCha 7\nweight 70000\nfood 999\nspeed 1\nend\n"
        + "Object experience_agility\nname agility\ntype 44\ninvisible 1\nlevel 1\nexp 0\nDex 1\nend\n"
        + "Object experience_personality\nname personality\ntype 44\ninvisible 1\nlevel 1\nexp 0\nCha 1\nend\n"
        + "Object experience_mental\nname mental\ntype 44\ninvisible 1\nlevel 1\nexp 0\nInt 1\nend\n"
        + "Object experience_physique\nname physique\ntype 44\ninvisible 1\nlevel 1\nexp 0\nStr 1\nCon 1\nend\n"
        + "Object experience_magic\nname magic\ntype 44\ninvisible 1\nlevel 1\nexp 0\nPow 1\nend\n"
        + "Object experience_wis\nname wisdom\ntype 44\ninvisible 1\nlevel 1\nexp 0\nWis 1\nend\n"
        + "Object skill_melee_weapon\nname melee weapons\ntype 43\ninvisible 1\nno_drop 1\nsp 23\nend\n"
        + "Object skill_praying\nname praying\ntype 43\ninvisible 1\nno_drop 1\nsp 30\nend\n"
        + "Object skill_stealing\nname stealing\ntype 43\ninvisible 1\nno_drop 1\nsp 0\nend\n"
        + "Object warrior_class\nname Warrior\ntype 37\nrandomitems warrior_class_items\nStr 2\nend\n"
        + "Object thief_class\nname Thief\ntype 37\nrandomitems thief_class_items\nend\n"
        + "Object sword\nname sword\nname_pl swords\ntype 15\nweight 3000\nvalue 50\nend\n"
        + "Object chain_mail\nname chain mail\ntype 16\nweight 20000\nvalue 100\nend\n"
        + "Object sack\nname sack\ntype 122\nweight 100\nvalue 7\nend\n"
        + "Object book\nname book\nname_pl books\ntype 8\nweight 200\nvalue 30\nend\n"
        + "Object cleric_book\nname cleric book\nname_pl cleric books\ntype 85\nweight 300\nvalue 40\nend\n"
        + "Object ability_fire\nname fire ability\ntype 110\ninvisible 1\nsp 9\nend\n"
        + "Object goldcoin\nname gold coin\nname_pl gold coins\ntype 36\nvalue 10\nend\n";

    public const string Treasures =
        "treasure warrior_class_items\n  arch skill_melee_weapon\n  more\n  arch skill_praying\n  more\n  arch sword\n  more\n  arch chain_mail\n  more\n  arch sack\n"
        + "  more\n  arch book\n  more\n  arch cleric_book\n  more\n  arch cleric_book\n  more\n  arch ability_fire\n  more\n  arch goldcoin\n  nrof 3\n  end\n"
        + "treasure thief_class_items\n  arch skill_stealing\n  more\n  arch sword\n  end\n";

    /// <summary>A world folder with the invented archetypes and its own treasures file.</summary>
    public static TempFolder Make(string? treasures = Treasures, string arc = Arc)
    {
        TempFolder world = TestWorld.Make(arc: arc);
        if (treasures != null)
        {
            File.WriteAllText(Path.Combine(world.Path, "treasures"), treasures);
        }
        return world;
    }

    public static DirectExt Start(TempFolder world, ExtensionOptions? options = null) =>
        new(world, options ?? new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) } }, ownsWorld: false);
}

/// <summary>The time of the backups: one that stays where it is put.</summary>
internal sealed class FixedTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 26, 12, 30, 45, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class StartPathTests : IDisposable
{
    private readonly TempFolder world = StartWorld.Make();

    public void Dispose() => world.Dispose();

    private static ReplyFrame Login(IExt ext, string account = "bob") => ext.Call(5, new[] { S(account) }, player: account);

    private static ReplyFrame Method(IExt ext, ulong player, int method, string account = "bob", params Val[] args) =>
        ext.Call(13, new[] { H(player), Val.Int(method), Val.List(args) }, player: account);

    private static ulong Hello(DirectExt ext, string account = "bob") => Login(ext, account).Result.AsHandle;

    private static CfObject Ob(DirectExt ext, string account = "bob") => ext.Dispatcher.ControllerOf(account)!.State.Ob;

    private static List<CfObject> Items(CfObject op)
    {
        var l = new List<CfObject>();
        for (CfObject? c = op.Inv; c != null; c = c.Below)
        {
            l.Add(c);
        }
        return l;
    }

    private static string[] ArchNames(CfObject op) => Items(op).Select(i => i.Arch!.Name!).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    /// <summary>Makes the dice give these numbers in turn (then zeros).</summary>
    private static void Dice(DirectExt ext, params int[] numbers)
    {
        int i = 0;
        ext.Dispatcher.WorldOfTests!.Random = () => i < numbers.Length ? numbers[i++] : 0;
    }

    // dice that roll a stat: the best three of 6, 6, s and 1 (the numbers are RANDOM() values: x % 6 + 1)
    private static int[] Stat(int value) => value switch
    {
        18 => new[] { 5, 5, 5, 0 },
        17 => new[] { 5, 5, 4, 0 },
        16 => new[] { 5, 5, 3, 0 },
        15 => new[] { 5, 5, 2, 0 },
        14 => new[] { 5, 5, 1, 0 },
        13 => new[] { 5, 5, 0, 0 },
        12 => new[] { 5, 4, 0, 0 },
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    // ------------------------------------------------------------------ setmudclass

    [Fact]
    public void SetMudClass_Given_a_class_When_set_Then_the_tree_holds_the_class_the_experience_the_items_of_its_list_and_the_flags_of_a_start()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.AsBool);
        CfObject pl = Ob(ext);
        Assert.Equal(
            new[] { "book", "chain_mail", "cleric_book", "cleric_book", "experience_agility", "experience_magic", "experience_mental", "experience_personality", "experience_physique", "experience_wis", "goldcoin", "skill_melee_weapon", "skill_praying", "sack", "sword", "warrior_class" }.OrderBy(n => n, StringComparer.Ordinal),
            ArchNames(pl));
        CfObject Find(string arch) => Items(pl).First(i => i.Arch!.Name == arch);
        Assert.True(Find("sword").QueryFlag(Define.FLAG_STARTEQUIP));
        Assert.True(Find("skill_praying").QueryFlag(Define.FLAG_STARTEQUIP));
        Assert.False(Find("sack").QueryFlag(Define.FLAG_STARTEQUIP)); // a container
        Assert.Equal(0, Find("sack").Value);
        Assert.Equal(3u, Find("goldcoin").Nrof > 0 ? 3u : 0u);
        Assert.Equal(10, Find("goldcoin").Value); // money keeps its value
        Assert.Equal("warrior", Ob(ext).Extra!.Get("mudclass"));
        Assert.Equal("warrior", Get(ext, h, Ply("mudclass")).AsString);
    }

    [Fact]
    public void SetMudClass_Given_a_class_When_set_Then_what_needs_identifying_is_identified_and_an_ability_becomes_a_known_spell_and_is_gone()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);

        // Act
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Assert
        CfObject pl = Ob(ext);
        Assert.All(Items(pl).Where(i => i.Type is Define.SKILL or Define.SPELLBOOK), i => Assert.True(i.QueryFlag(Define.FLAG_IDENTIFIED)));
        Assert.DoesNotContain(Items(pl), i => i.Type == Define.ABILITY);
        Assert.Equal(new short[] { 9 }, ((Player)pl.Contr!).KnownSpells.ToArray());
    }

    [Fact]
    public void SetMudClass_Given_two_cleric_books_When_set_Then_each_gets_another_of_the_starting_prayers()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);

        // Act
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Assert
        short[] sp = Items(Ob(ext)).Where(i => i.Type == Define.SPELLBOOK).Select(i => i.Stats.Sp).OrderBy(s => s).ToArray();
        Assert.Equal(2, sp.Length);
        Assert.NotEqual(sp[0], sp[1]);
        Assert.All(sp, s => Assert.Contains((int)s, new[] { 19, 31, 32, 129 }));
    }

    [Fact]
    public void SetMudClass_Given_a_class_When_set_Then_the_skills_are_linked_to_the_experience_of_their_category()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);

        // Act
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Assert
        CfObject pl = Ob(ext);
        Assert.Equal("physique", Items(pl).First(i => i.Arch!.Name == "skill_melee_weapon").ExpObj!.Name);
        Assert.Equal("wisdom", Items(pl).First(i => i.Arch!.Name == "skill_praying").ExpObj!.Name);
        Assert.All(Items(pl).Where(i => i.Type == Define.EXPERIENCE), e => Assert.True(e.QueryFlag(Define.FLAG_APPLIED)));
    }

    [Fact]
    public void SetMudClass_Given_a_class_When_set_Then_the_items_are_marked_to_be_sent_and_the_strings_follow()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        ReplyFrame login = Login(ext);
        CfObject pl = Ob(ext);

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Assert
        var marked = ext.Dispatcher.ControllerOf("bob")!.Shown.Dirty;
        Assert.Equal(Items(pl).Select(i => i.Count).OrderBy(c => c), marked.OrderBy(c => c));
        Assert.NotEmpty(marked);
        Assert.NotNull(login);
        Assert.Equal(0UL, r.Status);
    }

    [Fact]
    public void SetMudClass_Given_a_second_class_When_set_Then_the_items_of_the_first_are_gone_with_their_handles_and_marks()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        CfObject pl = Ob(ext);
        CfObject oldSword = Items(pl).First(i => i.Arch!.Name == "sword");
        uint oldTag = oldSword.Count;
        ext.Dispatcher.HandlesOfTests.HandleOf(oldSword);
        ext.Dispatcher.ControllerOf("bob")!.Shown.ClearMarks();

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("thief_class"));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Equal(new[] { "experience_agility", "experience_magic", "experience_mental", "experience_personality", "experience_physique", "experience_wis", "skill_stealing", "sword", "thief_class" }, ArchNames(pl));
        Assert.False(ext.Dispatcher.HandlesOfTests.Has(oldSword));
        Assert.Contains(oldTag, ext.Dispatcher.ControllerOf("bob")!.Shown.Removed);
        Assert.Equal("thief", Ob(ext).Extra!.Get("mudclass"));
    }

    [Fact]
    public void SetMudClass_Given_a_name_that_is_no_archetype_When_set_Then_false_a_log_line_and_the_player_is_as_it_was()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        string[] before = ArchNames(Ob(ext));

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("wizard_class"));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.False(r.Result.AsBool);
        Assert.Equal(before, ArchNames(Ob(ext)));
        Assert.Equal("warrior", Ob(ext).Extra!.Get("mudclass"));
        Assert.Contains(ext.Logs, l => l.Text.Contains("setmudclass: the world has no archetype wizard_class"));
    }

    [Fact]
    public void SetMudClass_Given_a_wrong_argument_When_called_Then_bad_args()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);

        // Act
        ReplyFrame none = Method(ext, h, TMudPlayerMembers.MethodSetMudClass);
        ReplyFrame number = Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: Val.Int(3));

        // Assert
        Assert.Equal(new[] { (ulong)Status.BadArgs, (ulong)Status.BadArgs }, new[] { none.Status, number.Status });
    }

    [Fact]
    public void SetMudClass_Given_the_extension_is_busy_When_called_Then_busy_and_nothing_changes()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.Empty(Items(Ob(ext)));
    }

    [Fact]
    public void SetMudClassNoAction_Given_a_name_When_called_Then_only_mudclass_changes_and_the_empty_name_clears_it()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);

        // Act
        ReplyFrame set = Method(ext, h, TMudPlayerMembers.MethodSetMudClassNoAction, args: S("priest"));
        string named = Get(ext, h, Ply("mudclass")).AsString;
        int items = Items(Ob(ext)).Count;
        Method(ext, h, TMudPlayerMembers.MethodSetMudClassNoAction, args: S(""));

        // Assert
        Assert.Equal(0UL, set.Status);
        Assert.Equal("priest", named);
        Assert.Equal(0, items);
        Assert.Equal("", Get(ext, h, Ply("mudclass")).AsString);
        Assert.False(Ob(ext).Extra!.Contains("mudclass"));
    }

    [Fact]
    public void SetMudClassNoAction_Given_a_name_with_a_line_break_When_called_Then_bad_args()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodSetMudClassNoAction, args: S("a\nhp 99"));

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
        Assert.False(Ob(ext).Extra?.Contains("mudclass") ?? false);
    }

    [Fact]
    public void GiveInitialItems_Given_a_race_that_cannot_use_armour_or_weapons_When_given_Then_those_are_removed_and_the_rest_stays()
    {
        // Arrange
        string arc = StartWorld.Arc.Replace("can_use_armour 1\ncan_use_weapon 1\n", "");
        using TempFolder bare = StartWorld.Make(arc: arc);
        using DirectExt ext = StartWorld.Start(bare);
        ulong h = Hello(ext);

        // Act
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Assert
        string[] names = ArchNames(Ob(ext));
        Assert.DoesNotContain("sword", names);
        Assert.DoesNotContain("chain_mail", names);
        Assert.Contains("sack", names);
        Assert.Contains("skill_melee_weapon", names);
    }

    [Fact]
    public void GiveInitialItems_Given_more_cleric_books_than_prayers_When_given_Then_the_books_past_the_four_are_removed()
    {
        // Arrange
        string books = string.Concat(Enumerable.Repeat("  arch cleric_book\n  more\n", 5));
        using TempFolder many = StartWorld.Make("treasure warrior_class_items\n" + books + "  arch sack\n  end\n");
        using DirectExt ext = StartWorld.Start(many);
        ulong h = Hello(ext);

        // Act
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Assert
        short[] sp = Items(Ob(ext)).Where(i => i.Type == Define.SPELLBOOK).Select(i => i.Stats.Sp).OrderBy(s => s).ToArray();
        Assert.Equal(new short[] { 19, 31, 32, 129 }, sp);
    }

    [Fact]
    public void GiveInitialItems_Given_more_plain_books_than_spells_When_given_Then_each_spellbook_gets_one_of_the_seven_spells_only_once()
    {
        // Arrange
        string spellbooks = string.Concat(Enumerable.Repeat("  arch spellbook_fire\n  more\n", 8));
        string arc = StartWorld.Arc + "Object spellbook_fire\nname spellbook\ntype 85\nend\n";
        using TempFolder many = StartWorld.Make("treasure warrior_class_items\n" + spellbooks + "  arch sack\n  end\n", arc);
        using DirectExt ext = StartWorld.Start(many);
        ulong h = Hello(ext);

        // Act
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Assert
        short[] sp = Items(Ob(ext)).Where(i => i.Type == Define.SPELLBOOK).Select(i => i.Stats.Sp).OrderBy(s => s).ToArray();
        Assert.Equal(new short[] { 0, 1, 4, 5, 7, 17, 168 }, sp);
    }

    // ------------------------------------------------------------------ rollstats

    // seven stats in the order they are rolled (Str, Dex, Int, Con, Wis, Pow, Cha), then the dice of level 1: hp 2d4, sp 2d3, grace 2d2
    private static int[] RollDice(int str, int dex, int intel, int con, int wis, int pow, int cha) =>
        Stat(str).Concat(Stat(dex)).Concat(Stat(intel)).Concat(Stat(con)).Concat(Stat(wis)).Concat(Stat(pow)).Concat(Stat(cha))
            .Concat(new[] { 0, 2, 0, 3 }) // levhp[1] = 3 + 4 + 1 = 8
            .Concat(new[] { 0, 0, 0, 2 }) // levsp[1] = 1 + 3 = 4
            .Concat(new[] { 0, 0, 0, 1 }) // levgrace[1] = 1 + 2 - 1 = 2
            .ToArray();

    [Fact]
    public void RollStats_Given_dice_When_rolled_Then_the_stats_are_in_order_from_high_to_low_and_the_life_follows_from_the_first_level()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Dice(ext, RollDice(13, 18, 12, 15, 17, 14, 16));

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodRollStats);

        // Assert: Str 18, Dex 17, Con 16, Int 15, Wis 14, Pow 13, Cha 12
        Assert.Equal(0UL, r.Status);
        Assert.Equal(new long[] { 18, 17, 16, 15, 14, 13, 12 }, new[] { "mudstrength", "muddexterity", "mudconstitution", "mudintelligence", "mudwisdom", "mudpower", "mudcharisma" }.Select(n => Get(ext, h, Ply(n)).AsInt));
        Assert.Equal(1L, Get(ext, h, Ply("mudlevel")).AsInt);
        // maxhp: levhp 8 + con_bonus[16] / 2 = 3
        Assert.Equal(11L, Get(ext, h, Ply("mudmaxhp")).AsInt);
        Assert.Equal(11L, Get(ext, h, Ply("mudhp")).AsInt);
        // maxsp: levsp 4 + sp_bonus[13] / 2 = 1, odd level and an even bonus: 5
        Assert.Equal(5L, Get(ext, h, Ply("mudmaxsp")).AsInt);
        Assert.Equal(5L, Get(ext, h, Ply("mudsp")).AsInt);
        // maxgrace: (2 + (2 + 3) / 8) + 1 + (2 + 3) / 4 = 4.875
        Assert.Equal(4L, Get(ext, h, Ply("mudmaxgrace")).AsInt);
        Assert.Equal(0L, Get(ext, h, Ply("mudgrace")).AsInt);
    }

    [Fact]
    public void RollStats_Given_a_roll_When_done_Then_the_strings_of_the_stats_and_life_are_set_and_only_those_that_changed()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Dice(ext, RollDice(13, 18, 12, 15, 17, 14, 16));

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodRollStats);

        // Assert
        var sets = Effects(r, "client.set").ToDictionary(e => e.Get("name")!.AsString, e => e.Get("value")!.AsString);
        Assert.Equal("11", sets["clientr.mud_maxhp"]);
        Assert.Equal("18", sets["clientr.mud_str"]);
        Assert.Equal("5", sets["clientr.mud_maxsp"]);
        Assert.DoesNotContain("clientr.mud_food", sets.Keys); // 999 as before
        Assert.Equal(Get(ext, h, Ply("mudmaxhp")).AsInt.ToString(), sets["clientr.mud_maxhp"]);
    }

    [Fact]
    public void RollStats_Given_a_sum_outside_82_to_116_When_rolled_Then_it_is_rolled_again()
    {
        // Arrange: all 13 (sum 91 is fine) is replaced by a first try of all 18 (126, too much)
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Dice(ext, Stat(18).Concat(Stat(18)).Concat(Stat(18)).Concat(Stat(18)).Concat(Stat(18)).Concat(Stat(18)).Concat(Stat(18))
            .Concat(RollDice(13, 18, 12, 15, 17, 14, 16)).ToArray());

        // Act
        Method(ext, h, TMudPlayerMembers.MethodRollStats);

        // Assert
        Assert.Equal(18L, Get(ext, h, Ply("mudstrength")).AsInt);
        Assert.Equal(12L, Get(ext, h, Ply("mudcharisma")).AsInt);
    }

    [Fact]
    public void RollStats_Given_a_roll_When_the_account_is_saved_and_read_again_Then_the_rolled_stats_and_life_points_are_in_the_file()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Dice(ext, RollDice(13, 18, 12, 15, 17, 14, 16));
        Method(ext, h, TMudPlayerMembers.MethodRollStats);

        // Act
        ext.Call(6, new[] { S("bob") }, player: "bob");
        ext.Logs.Clear();
        ulong again = Hello(ext);
        Player contr = ext.Dispatcher.ControllerOf("bob")!;

        // Assert
        Assert.True(contr.StatsRolled);
        Assert.Equal(new short[] { 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, contr.LevHp);
        Assert.Equal(4, contr.LevSp[1]);
        Assert.Equal(2, contr.LevGrace[1]);
        Assert.Equal(18, contr.OrigStats.Str);
        Assert.Equal(13, contr.OrigStats.Pow);
        Assert.DoesNotContain(ext.Logs, l => l.Text.Contains("errors"));
        Assert.Equal(11L, Get(ext, again, Ply("mudmaxhp")).AsInt);
    }

    [Fact]
    public void RollStats_Given_a_player_who_never_rolled_When_an_item_comes_Then_the_stats_and_life_are_left_and_only_the_weight_limit_is_set()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        CfWorld w = ext.Dispatcher.WorldOfTests!;
        CfObject pl = Ob(ext);

        // Act
        w.InsertObInOb(w.ArchToObject(w.FindArchetype("sword"))!, pl);

        // Assert
        Assert.Equal(12, pl.Stats.Maxhp);
        Assert.Equal(14, pl.Stats.Str);
        Assert.Equal(1400000, ext.Dispatcher.ControllerOf("bob")!.WeightLimit);
        Assert.Equal(12L, Get(ext, h, Ply("mudmaxhp")).AsInt);
    }

    // ------------------------------------------------------------------ mudresetplayer

    [Fact]
    public void MudResetPlayer_Given_a_player_with_a_class_When_reset_Then_the_tree_is_empty_the_handles_and_marks_are_gone_and_the_stats_are_rolled_again()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        CfObject pl = Ob(ext);
        List<CfObject> before = Items(pl);
        uint[] tags = before.Select(i => i.Count).ToArray();
        foreach (CfObject i in before)
        {
            ext.Dispatcher.HandlesOfTests.HandleOf(i);
        }
        pl.Stats.Exp = 5000;
        pl.Stats.Food = 100;
        pl.Level = 4;
        Dice(ext, RollDice(13, 18, 12, 15, 17, 14, 16));

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodMudResetPlayer);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Null(pl.Inv);
        Assert.All(before, i => Assert.False(ext.Dispatcher.HandlesOfTests.Has(i)));
        Assert.Equal(tags.OrderBy(t => t), ext.Dispatcher.ControllerOf("bob")!.Shown.Removed.OrderBy(t => t));
        Assert.Equal(0L, Get(ext, h, Ply("mudexp")).AsInt);
        Assert.Equal(1L, Get(ext, h, Ply("mudlevel")).AsInt);
        Assert.Equal(999L, Get(ext, h, Ply("mudfood")).AsInt);
        Assert.Equal("", Get(ext, h, Ply("mudclass")).AsString);
        Assert.Equal(18L, Get(ext, h, Ply("mudstrength")).AsInt);
        Assert.Equal(11L, Get(ext, h, Ply("mudmaxhp")).AsInt);
        Assert.Empty(r.Events);
        Assert.Empty(((Player)pl.Contr!).KnownSpells);
    }

    [Fact]
    public void MudResetPlayer_Given_a_marked_item_When_reset_Then_the_mark_and_the_last_used_are_cleared()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        Player contr = ext.Dispatcher.ControllerOf("bob")!;
        contr.Mark = Items(Ob(ext)).First();
        contr.MarkCount = contr.Mark.Count;
        contr.LastUsed = contr.Mark;
        contr.LastUsedId = 7;

        // Act
        Method(ext, h, TMudPlayerMembers.MethodMudResetPlayer);

        // Assert
        Assert.Null(contr.Mark);
        Assert.Equal(0u, contr.MarkCount);
        Assert.Null(contr.LastUsed);
        Assert.Equal(0u, contr.LastUsedId);
    }

    [Fact]
    public void MudResetPlayer_Given_the_extension_is_busy_When_called_Then_busy_and_the_items_stay()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        ext.Dispatcher.Busy = "recovering";

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodMudResetPlayer);

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.NotEmpty(Items(Ob(ext)));
    }

    [Fact]
    public void MudResetPlayer_Given_the_function_with_a_player_in_the_context_When_called_Then_that_player_is_reset_and_without_one_bad_args()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Act
        ReplyFrame nobody = ext.Call(Manifest.OpMudResetPlayer);
        ReplyFrame extra = ext.Call(Manifest.OpMudResetPlayer, new[] { S("x") }, player: "bob");
        ReplyFrame stranger = ext.Call(Manifest.OpMudResetPlayer, player: "nobody");
        int stillThere = Items(Ob(ext)).Count;
        ReplyFrame r = ext.Call(Manifest.OpMudResetPlayer, player: "bob");

        // Assert
        Assert.Equal(new[] { (ulong)Status.BadArgs, (ulong)Status.BadArgs, (ulong)Status.BadArgs }, new[] { nobody.Status, extra.Status, stranger.Status });
        Assert.NotEqual(0, stillThere);
        Assert.Equal(0UL, r.Status);
        Assert.Null(Ob(ext).Inv);
    }

    [Fact]
    public void MudRollStats_Given_the_function_with_a_player_in_the_context_When_called_Then_the_stats_are_rolled()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Dice(ext, RollDice(13, 18, 12, 15, 17, 14, 16));

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudRollStats, player: "bob");
        ReplyFrame nobody = ext.Call(Manifest.OpMudRollStats);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(18L, Get(ext, h, Ply("mudstrength")).AsInt);
        Assert.Equal((ulong)Status.BadArgs, nobody.Status);
    }

    // ------------------------------------------------------------------ the account methods

    private static string AccountFile(TempFolder world, string name = "bob") => Path.Combine(world.Path, "data", "accounts", name + ".txt");

    private static void WaitForFile(string path)
    {
        for (int i = 0; i < 200 && !File.Exists(path); i++)
        {
            Thread.Sleep(10);
        }
        Assert.True(File.Exists(path), path + " was not written");
    }

    [Fact]
    public void LoadMudAccount_Given_the_account_is_loaded_When_called_with_the_empty_suffix_Then_nothing_happens_no_effect_and_no_event()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        CfObject before = Ob(ext);

        // Act
        ReplyFrame empty = Method(ext, h, TMudPlayerMembers.MethodLoadMudAccount, args: S(""));
        ReplyFrame nothing = Method(ext, h, TMudPlayerMembers.MethodLoadMudAccount);

        // Assert
        Assert.True(empty.Result.AsBool);
        Assert.True(nothing.Result.AsBool);
        Assert.Empty(empty.Effects);
        Assert.Empty(empty.Events);
        Assert.Same(before, Ob(ext));
        Assert.NotEmpty(Items(before));
    }

    [Fact]
    public void UnloadMudAccount_Given_a_loaded_account_When_unloaded_Then_the_tree_and_the_strings_go_and_the_file_is_kept_and_not_written_over()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        Method(ext, h, TMudPlayerMembers.MethodSaveMudAccount);
        WaitForFile(AccountFile(world));
        byte[] saved = File.ReadAllBytes(AccountFile(world));
        CfObject old = Ob(ext);
        Item(ext, old);

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodUnloadMudAccount);
        Method(ext, h, TMudPlayerMembers.MethodSaveMudAccount);
        ext.Call(6, new[] { S("bob") }, player: "bob"); // logout saves too
        ext.End();

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Contains("clientr.mud_hp", Effects(r, "client.del").Select(e => e.Get("name")!.AsString));
        Assert.Equal(saved, File.ReadAllBytes(AccountFile(world)));
    }

    private static void Item(DirectExt ext, CfObject pl) => ext.Dispatcher.HandlesOfTests.HandleOf(Items(pl).First());

    [Fact]
    public void UnloadMudAccount_Given_a_loaded_account_When_unloaded_Then_the_player_is_blank_its_old_items_have_no_handles_and_the_handle_of_the_player_lives()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        CfObject old = Ob(ext);
        CfObject first = Items(old).First();
        ext.Dispatcher.HandlesOfTests.HandleOf(first);

        // Act
        Method(ext, h, TMudPlayerMembers.MethodUnloadMudAccount);

        // Assert
        CfObject now = Ob(ext);
        Assert.NotSame(old, now);
        Assert.Null(now.Inv);
        Assert.False(ext.Dispatcher.HandlesOfTests.Has(first));
        Assert.Equal("bob", Get(ext, h, Ply("mudrealname")).AsString);
        Assert.NotNull(ext.Dispatcher.ControllerOf("bob"));
    }

    [Fact]
    public void LoadMudAccount_Given_an_unloaded_account_When_loaded_with_the_empty_suffix_Then_the_file_is_read_the_strings_sent_again_and_the_event_fires()
    {
        // Arrange
        var resendAtFlush = new List<bool>();
        var options = new ExtensionOptions
        {
            Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
            FlushHook = (d, ps) => resendAtFlush.Add(ps.Contr!.NeedsResend),
        };
        using DirectExt ext = StartWorld.Start(world, options);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        Method(ext, h, TMudPlayerMembers.MethodSaveMudAccount);
        WaitForFile(AccountFile(world));
        string[] saved = ArchNames(Ob(ext));
        Method(ext, h, TMudPlayerMembers.MethodUnloadMudAccount);
        resendAtFlush.Clear();

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodLoadMudAccount, args: S(""));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Equal(saved, ArchNames(Ob(ext)));
        Assert.Contains("clientr.mud_hp", Effects(r, "client.set").Select(e => e.Get("name")!.AsString));
        Val ev = Assert.Single(r.Events);
        Assert.Equal("onLoadedMudAccount", ev.Get("name")!.AsString);
        Assert.Equal(h, ev.Get("args")!.Items.Single().AsHandle);
        Assert.Equal("warrior", Get(ext, h, Ply("mudclass")).AsString);
        Assert.Equal(new[] { true }, resendAtFlush); // the part that sends item strings is told to send them all
        Assert.Equal(Items(Ob(ext)).Select(i => i.Count).OrderBy(c => c), ext.Dispatcher.ControllerOf("bob")!.Shown.Dirty.OrderBy(c => c));
    }

    [Fact]
    public void LoadMudAccount_Given_an_unloaded_account_without_a_file_When_loaded_Then_a_new_player_as_a_login_makes_it()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodUnloadMudAccount);

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodLoadMudAccount, args: S(""));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Equal(12L, Get(ext, h, Ply("mudmaxhp")).AsInt);
        Assert.Single(r.Events);
    }

    [Fact]
    public void LoadMudAccount_Given_a_suffix_with_a_file_When_loaded_Then_that_file_is_the_account_and_is_the_one_saved_to_after_that()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        Method(ext, h, TMudPlayerMembers.MethodSaveMudAccount);
        WaitForFile(AccountFile(world));
        File.Copy(AccountFile(world), AccountFile(world, "bob_2"));
        byte[] main = File.ReadAllBytes(AccountFile(world));
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("thief_class"));

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodMudLoadAccount, args: S("2"));
        ext.Call(6, new[] { S("bob") }, player: "bob");
        ext.End();

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Single(r.Events);
        Assert.Contains("warrior", File.ReadAllText(AccountFile(world, "bob_2")));
        Assert.Equal(main, File.ReadAllBytes(AccountFile(world)));
    }

    [Fact]
    public void LoadMudAccount_Given_a_suffix_without_a_file_When_called_Then_false_a_log_line_and_the_player_is_as_it_was()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));
        CfObject before = Ob(ext);

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodLoadMudAccount, args: S("_9"));

        // Assert
        Assert.False(r.Result.AsBool);
        Assert.Empty(r.Events);
        Assert.Same(before, Ob(ext));
        Assert.NotEmpty(Items(before));
        Assert.Contains(ext.Logs, l => l.Text.Contains("there is no file bob_9"));
    }

    [Fact]
    public void LoadMudAccount_Given_a_suffix_with_a_control_character_When_called_Then_bad_args()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);

        // Act
        ReplyFrame r = Method(ext, h, TMudPlayerMembers.MethodLoadMudAccount, args: S("_a\nb"));
        ReplyFrame two = Method(ext, h, TMudPlayerMembers.MethodLoadMudAccount, args: new[] { S("a"), S("b") });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
        Assert.Equal((ulong)Status.BadArgs, two.Status);
    }

    [Fact]
    public void MudLoadAccount_Given_a_slot_When_called_Then_it_is_loadmudaccount_with_an_underscore_and_the_slot()
    {
        // Arrange
        using DirectExt ext = StartWorld.Start(world);
        ulong h = Hello(ext);

        // Act
        ReplyFrame missing = Method(ext, h, TMudPlayerMembers.MethodMudLoadAccount, args: S("3"));
        ReplyFrame none = Method(ext, h, TMudPlayerMembers.MethodMudLoadAccount);

        // Assert
        Assert.False(missing.Result.AsBool);
        Assert.Contains(ext.Logs, l => l.Text.Contains("there is no file bob_3"));
        Assert.Equal((ulong)Status.BadArgs, none.Status);
    }

    [Fact]
    public void SaveMudAccount_Given_a_player_When_called_Then_the_file_is_written_at_once_not_after_the_quiet_time()
    {
        // Arrange
        var options = new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMinutes(5), MaxDelay = TimeSpan.FromMinutes(5) } };
        using DirectExt ext = StartWorld.Start(world, options);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Act
        Method(ext, h, TMudPlayerMembers.MethodSaveMudAccount);
        WaitForFile(AccountFile(world));

        // Assert
        Assert.Contains("mudclass warrior", File.ReadAllText(AccountFile(world)));
    }

    [Fact]
    public void BackupMudAccount_Given_a_player_When_called_Then_a_copy_with_the_time_is_in_the_backups_folder_and_the_file_is_current()
    {
        // Arrange
        var time = new FixedTime();
        var options = new ExtensionOptions { Time = time, Writer = new FileWriterOptions { Quiet = TimeSpan.FromMinutes(5), MaxDelay = TimeSpan.FromMinutes(5) } };
        using DirectExt ext = StartWorld.Start(world, options);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodSetMudClass, args: S("warrior_class"));

        // Act
        Method(ext, h, TMudPlayerMembers.MethodBackupMudAccount);
        string backup = Path.Combine(world.Path, "data", "backups", "bob.20261126-123045.txt");
        WaitForFile(backup);
        WaitForFile(AccountFile(world));

        // Assert
        Assert.Equal(File.ReadAllBytes(AccountFile(world)), File.ReadAllBytes(backup));
        Assert.Contains("mudclass warrior", File.ReadAllText(backup));
    }

    [Fact]
    public void BackupMudAccount_Given_an_unloaded_account_When_called_Then_nothing_is_written()
    {
        // Arrange
        var options = new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) } };
        using DirectExt ext = StartWorld.Start(world, options);
        ulong h = Hello(ext);
        Method(ext, h, TMudPlayerMembers.MethodUnloadMudAccount);

        // Act
        Method(ext, h, TMudPlayerMembers.MethodBackupMudAccount);
        Method(ext, h, TMudPlayerMembers.MethodSaveMudAccount);
        Thread.Sleep(300);

        // Assert
        Assert.False(Directory.Exists(Path.Combine(world.Path, "data", "backups")));
        Assert.False(File.Exists(AccountFile(world)));
    }

    // ------------------------------------------------------------------ the manifest

    [Fact]
    public void Manifest_Given_the_lane_When_built_Then_the_methods_of_TMudPlayer_and_the_two_functions_have_the_ids_of_the_lane()
    {
        // Arrange / Act
        Val manifest = Manifest.Build(false, 1);
        Val player = manifest.Get("types")!.Items.Single(t => t.Get("name")!.AsString == "TMudPlayer");
        var methods = player.Get("methods")!.Items.ToDictionary(m => m.Get("name")!.AsString, m => m.Get("id")!.AsInt);
        var functions = manifest.Get("functions")!.Items.ToDictionary(f => f.Get("name")!.AsString, f => f.Get("id")!.AsInt);

        // Assert
        Assert.Equal(2500L, methods["mudresetplayer"]);
        Assert.Equal(2501L, methods["rollstats"]);
        Assert.Equal(2502L, methods["setmudclass"]);
        Assert.Equal(2503L, methods["setmudclassnoaction"]);
        Assert.Equal(2504L, methods["loadmudaccount"]);
        Assert.Equal(2505L, methods["unloadmudaccount"]);
        Assert.Equal(2506L, methods["savemudaccount"]);
        Assert.Equal(2507L, methods["backupmudaccount"]);
        Assert.Equal(2508L, methods["Mud_LoadAccount"]);
        Assert.Equal(2000L, functions["mudresetplayer"]);
        Assert.Equal(2001L, functions["mudrollstats"]);
        Assert.Equal(new[] { "string" }, player.Get("methods")!.Items.Single(m => m.Get("name")!.AsString == "setmudclass").Get("params")!.Items.Select(p => p.AsString));
    }

    [Fact]
    public void Manifest_Given_the_lane_When_built_Then_no_function_has_the_name_of_a_method_of_a_type_with_the_host_npc()
    {
        // Arrange
        Val manifest = Manifest.Build(false, 1);
        var functions = manifest.Get("functions")!.Items.Select(f => f.Get("name")!.AsString).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var clash = manifest.Get("types")!.Items.Where(t => t.Get("host")!.AsString == "npc")
            .SelectMany(t => t.Get("methods")!.Items.Select(m => m.Get("name")!.AsString)).Where(functions.Contains).ToList();

        // Assert
        Assert.Empty(clash);
    }
}
