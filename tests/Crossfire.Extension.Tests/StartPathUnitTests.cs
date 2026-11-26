// Not ported from a Crossfire file: tests of the ported code of lane 4D (living.c's level, die_roll and the part of fix_player, player.c's
// roll_stats and give_initial_items, skill_util.c's skills), straight on a world, without the link; and of the data they read.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;

namespace Crossfire.Extension.Tests;

public sealed class StartPathUnitTests
{
    private sealed class Fixture
    {
        public List<string> Log { get; } = new();

        public GkHost Host { get; } = new();

        public CfWorld World { get; }

        public SkillTable Skills { get; }

        public Fixture(string arc = StartWorld.Arc, string treasures = StartWorld.Treasures, string skillParams = "# none\n")
        {
            World = new CfWorld(Host, (level, text) => Log.Add(level + ": " + text.TrimEnd('\n')));
            Host.World = World;
            World.TreasureText = treasures;
            World.InitArchetypes("Object empty_archetype\nend\n" + arc);
            Log.Clear();
            Skills = SkillTable.Create(World, skillParams);
            Host.Skills = Skills;
        }

        public void Dice(params int[] numbers)
        {
            int i = 0;
            World.Random = () => i < numbers.Length ? numbers[i++] : 0;
        }

        public CfObject Make(string arch) => World.ArchToObject(World.FindArchetype(arch))!;

        /// <summary>A player with a controller (not rolled) in a state of an account.</summary>
        public (CfObject Ob, Player Contr) Player(string account = "bob")
        {
            CfObject ob = Make("dwarf_player");
            ob.Name = account;
            var contr = new Player(new PlayerState(account, ob), _ => { });
            ob.Contr = contr;
            return (ob, contr);
        }

        public CfObject Give(CfObject to, string arch, Action<CfObject>? tweak = null)
        {
            CfObject o = Make(arch);
            tweak?.Invoke(o);
            return World.InsertObInOb(o, to);
        }
    }

    private static IEnumerable<CfObject> Inv(CfObject op)
    {
        for (CfObject? c = op.Inv; c != null; c = c.Below)
        {
            yield return c;
        }
    }

    /// <summary>Dice (RANDOM() values) of one roll_stat that sums to <paramref name="value"/> (3 to 18): three dice and a 1 that is dropped.</summary>
    private static int[] Stat(int value)
    {
        int extra = value - 3;
        var d = new int[4];
        for (int i = 0; i < 3; i++)
        {
            int add = Math.Min(5, extra);
            d[i] = add; // a number that %6+1 makes 1 + add
            extra -= add;
        }
        d[3] = 0;
        return d;
    }

    private static int[] Stats(params int[] values) => values.SelectMany(Stat).ToArray();

    // ------------------------------------------------------------------ living.c

    [Theory]
    [InlineData(1, 1.0, 0u)]
    [InlineData(2, 1.0, 1000u)]
    [InlineData(10, 1.0, 250000u)]
    [InlineData(100, 1.0, 116500000u)]
    [InlineData(101, 1.0, 118150000u)]
    [InlineData(110, 1.0, 133000000u)] // past 100 the formula is used, the table's 110 is not
    [InlineData(5, 2.0, 16000u)]
    [InlineData(2, 0.55, 550u)]
    public void LevelExp_Given_a_level_and_a_multiplier_When_asked_Then_the_value_of_the_table_times_it(int level, double expmul, uint expected)
    {
        // Arrange / Act
        uint exp = LivingRules.LevelExp(level, expmul);

        // Assert
        Assert.Equal(expected, exp);
    }

    [Fact]
    public void DieRoll_Given_no_luck_When_rolled_Then_each_die_is_a_roll_mod_size_plus_one_and_two_numbers_are_used_for_each()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Dice(0, 2, 0, 3);

        // Act
        int total = LivingRules.DieRoll(f.World, 2, 4, ob, Define.PREFER_HIGH);

        // Assert: (0 % 20 < 0 is false) then 2 % 4 + 1 = 3, then 3 % 4 + 1 = 4
        Assert.Equal(7, total);
    }

    [Fact]
    public void DieRoll_Given_luck_When_the_roll_is_lucky_Then_the_first_lucky_die_is_bent_once()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        ob.Stats.Luck = 5;
        f.Dice(0, 2, 0, 1);

        // Act
        int high = LivingRules.DieRoll(f.World, 2, 4, ob, Define.PREFER_HIGH);
        f.Dice(0, 2, 0, 1);
        int low = LivingRules.DieRoll(f.World, 2, 4, ob, Define.PREFER_LOW);

        // Assert: 0 % 20 < 5 and nobody was lucky yet: diff 4 - 1 = 3, min 1 + 1 (high) or 1 (low); 2 % 3 + min; the other die is tested
        // with 0 % 20 < 1 (luck is 1 now) which would be lucky again, but one die is lucky at most: 1 % 4 + 1 = 2
        Assert.Equal(4 + 2, high);
        Assert.Equal(3 + 2, low);
    }

    [Fact]
    public void DieRoll_Given_bad_luck_When_lucky_Then_the_die_gets_smaller_and_a_size_one_die_is_an_error()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        ob.Stats.Luck = -5;
        f.Dice(0, 2, 0, 1);

        // Act
        int total = LivingRules.DieRoll(f.World, 2, 4, ob, Define.PREFER_HIGH);
        int broken = LivingRules.DieRoll(f.World, 3, 1, ob, Define.PREFER_HIGH);

        // Assert: luck becomes -1: diff 5, min 0; 2 % 5 + 0 = 2 (not below 1); the second die 2
        Assert.Equal(2 + 2, total);
        Assert.Equal(3, broken);
        Assert.Contains(f.Log, l => l.Contains("Calling die_roll with num=3 size=1"));
    }

    [Fact]
    public void DieRoll_Given_a_monster_When_rolled_Then_its_luck_does_not_count()
    {
        // Arrange
        var f = new Fixture();
        CfObject orc = f.Make("sword");
        orc.Type = Define.MONSTER;
        orc.Stats.Luck = 9;
        f.Dice(0, 2, 1, 1);

        // Act
        int total = LivingRules.DieRoll(f.World, 2, 4, orc, Define.PREFER_HIGH);

        // Assert
        Assert.Equal(3 + 2, total);
    }

    [Fact]
    public void CheckStatBounds_Given_stats_outside_1_to_30_When_checked_Then_they_are_brought_in()
    {
        // Arrange
        var stats = new Living { Str = 0, Dex = 31, Con = -4, Int = 1, Wis = 30, Cha = 127, Pow = 15 };

        // Act
        LivingRules.CheckStatBounds(stats);

        // Assert
        Assert.Equal(new sbyte[] { 1, 30, 1, 1, 30, 30, 15 }, new[] { stats.Str, stats.Dex, stats.Con, stats.Int, stats.Wis, stats.Cha, stats.Pow });
    }

    [Fact]
    public void PlayerLvlAdj_Given_experience_for_three_levels_When_adjusted_Then_the_level_rises_one_by_one_with_life_points_for_each_and_the_player_is_fixed()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        contr.OrigStats.CopyFrom(ob.Stats);
        contr.StoreStats();
        ob.Level = 1;
        ob.Stats.Exp = 4000;
        f.Dice(0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 3, 0, 1, 0, 1);

        // Act
        LivingRules.PlayerLvlAdj(f.World, ob, null);

        // Assert: 4000 is level 4 (4000 needed); levels 2, 3 and 4 each got the three rolls
        Assert.Equal(4, ob.Level);
        Assert.NotEqual(0, contr.LevHp[2]);
        Assert.NotEqual(0, contr.LevHp[3]);
        Assert.NotEqual(0, contr.LevHp[4]);
        Assert.Equal(0, contr.LevHp[1]);
        Assert.Equal(0, contr.LevHp[5]);
        Assert.True(ob.Stats.Maxhp > 12);
    }

    [Fact]
    public void PlayerLvlAdj_Given_too_little_experience_When_adjusted_Then_the_level_falls_to_the_one_the_experience_has_and_never_below_1()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        ob.Level = 5;
        ob.Stats.Exp = 1500;

        // Act
        LivingRules.PlayerLvlAdj(f.World, ob, null);
        ob.Stats.Exp = 0;
        LivingRules.PlayerLvlAdj(f.World, ob, null);

        // Assert
        Assert.Equal(1, ob.Level);
    }

    [Fact]
    public void PlayerLvlAdj_Given_an_experience_object_When_adjusted_Then_its_level_follows_its_experience_and_no_life_points_are_rolled()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        CfObject exp = f.Make("experience_physique");
        exp.Stats.Exp = 5000;
        CfObject other = f.Make("experience_magic");
        other.Stats.Exp = 5000;

        // Act
        LivingRules.PlayerLvlAdj(f.World, ob, exp);
        LivingRules.PlayerLvlAdj(f.World, null, other);

        // Assert
        Assert.Equal(4, exp.Level);
        Assert.Equal(4, other.Level);
        Assert.All(contr.LevHp, v => Assert.Equal(0, v));
        Assert.All(contr.LevSp, v => Assert.Equal(0, v));
        Assert.Equal(1, ob.Level);
    }

    [Fact]
    public void PlayerLvlAdj_Given_a_rolled_player_who_loses_levels_When_adjusted_Then_the_player_is_fixed_for_the_level_it_has_then()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 3, con: 7);
        contr.LevHp[1] = 4;
        contr.LevHp[2] = 5;
        contr.LevHp[3] = 6;
        ob.Stats.Exp = 0;

        // Act
        LivingRules.PlayerLvlAdj(f.World, ob, null);

        // Assert
        Assert.Equal(1, ob.Level);
        Assert.Equal(4, ob.Stats.Maxhp);
    }

    [Fact]
    public void PlayerLvlAdj_Given_the_highest_level_When_adjusted_Then_it_does_not_rise()
    {
        // Arrange
        var f = new Fixture();
        CfObject exp = f.Make("experience_physique");
        exp.Level = 110;
        exp.Stats.Exp = int.MaxValue;

        // Act
        LivingRules.PlayerLvlAdj(f.World, null, exp);

        // Assert
        Assert.Equal(110, exp.Level);
    }

    [Fact]
    public void AddExpNone_Given_a_player_with_a_readied_skill_When_called_Then_the_experience_object_of_the_skill_is_adjusted_too_and_a_monster_is_left()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject exp = f.Make("experience_physique");
        exp.Stats.Exp = 2000;
        CfObject skill = f.Make("skill_melee_weapon");
        skill.ExpObj = exp;
        ob.ChosenSkill = skill;
        ob.ExpObj = exp;
        CfObject orc = f.Make("sword");
        orc.Type = Define.MONSTER;
        orc.Stats.Exp = 5000;
        orc.Level = 1;

        // Act
        LivingRules.AddExpNone(f.World, ob);
        LivingRules.AddExpNone(f.World, orc);

        // Assert
        Assert.Equal(3, exp.Level);
        Assert.Null(ob.ExpObj);
        Assert.Equal(1, orc.Level);
    }

    [Fact]
    public void DieRoll_Given_a_die_of_two_sides_and_luck_When_rolled_Then_luck_is_tested_against_50_and_for_larger_dice_against_20()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        ob.Stats.Luck = 5;
        f.Dice(24, 1, 0, 0);

        // Act
        f.Dice(24, 0, 24, 0);
        int two = LivingRules.DieRoll(f.World, 2, 2, ob, Define.PREFER_HIGH); // 24 % 50 = 24 is not below 5: two plain dice of 1
        f.Dice(24, 1, 24, 0);
        int four = LivingRules.DieRoll(f.World, 2, 4, ob, Define.PREFER_HIGH); // 24 % 20 = 4 is below 5: the first die is bent (1 % 3 + 2 = 3), the second is plain (1)

        // Assert
        Assert.Equal(2, two);
        Assert.Equal(4, four);
    }

    [Fact]
    public void PlayerLvlAdj_Given_experience_for_level_11_When_adjusted_Then_the_life_points_are_rolled_for_levels_up_to_10_and_not_for_11()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        contr.OrigStats.CopyFrom(ob.Stats);
        contr.StoreStats();
        ob.Level = 1;
        ob.Stats.Exp = 500000;
        f.Dice(3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3);

        // Act
        LivingRules.PlayerLvlAdj(f.World, ob, null);

        // Assert
        Assert.Equal(11, ob.Level);
        Assert.NotEqual(0, contr.LevHp[10]);
        Assert.NotEqual(0, contr.LevHp[2]);
        Assert.Equal(0, contr.LevHp[1]);
    }

    // ------------------------------------------------------------------ fix_player

    private static (CfObject Ob, Player Contr) Rolled(Fixture f, int level = 1, int con = 10, int pow = 11, int wis = 9)
    {
        (CfObject ob, Player contr) = f.Player();
        ob.Stats.Con = (sbyte)con;
        ob.Stats.Pow = (sbyte)pow;
        ob.Stats.Wis = (sbyte)wis;
        contr.OrigStats.CopyFrom(ob.Stats);
        contr.StoreStats();
        ob.Level = (short)level;
        return (ob, contr);
    }

    [Fact]
    public void Fix_Given_a_player_whose_stats_were_never_rolled_When_fixed_Then_stats_and_life_are_not_touched_and_the_weight_limit_is_set()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        ob.Stats.Str = 22;
        ob.Stats.Maxhp = 77;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(77, ob.Stats.Maxhp);
        Assert.Equal(22, ob.Stats.Str);
        Assert.Equal(2550000, contr.WeightLimit);
    }

    [Fact]
    public void Fix_Given_a_rolled_player_When_fixed_Then_the_stats_come_back_from_the_rolled_ones()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = Rolled(f);
        ob.Stats.Str = 3;
        ob.Stats.Dex = 29;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(14, ob.Stats.Str);
        Assert.Equal(12, ob.Stats.Dex);
    }

    [Fact]
    public void Fix_Given_applied_items_with_stat_bonuses_When_fixed_Then_only_the_applied_ones_other_than_potions_and_containers_add_theirs()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = Rolled(f);
        f.Give(ob, "sword", o => { o.Stats.Str = 3; o.SetFlag(Define.FLAG_APPLIED); });
        f.Give(ob, "chain_mail", o => { o.Stats.Con = 4; });
        f.Give(ob, "book", o => { o.Type = Define.POTION; o.Stats.Dex = 5; o.SetFlag(Define.FLAG_APPLIED); });
        f.Give(ob, "sack", o => { o.Stats.Int = 5; o.SetFlag(Define.FLAG_APPLIED); });
        f.Give(ob, "experience_mental", o => { o.Stats.Cha = 6; o.SetFlag(Define.FLAG_APPLIED); });
        f.Give(ob, "experience_wis", o => { o.Stats.Wis = 2; o.SetFlag(Define.FLAG_APPLIED); });

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(new sbyte[] { 17, 12, 10, 8, 11, 7, 11 }, new[] { ob.Stats.Str, ob.Stats.Dex, ob.Stats.Con, ob.Stats.Int, ob.Stats.Wis, ob.Stats.Cha, ob.Stats.Pow });
    }

    [Fact]
    public void Fix_Given_stats_pushed_outside_the_bounds_When_fixed_Then_they_are_brought_in()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = Rolled(f);
        f.Give(ob, "sword", o => { o.Stats.Str = 100; o.Stats.Dex = -50; o.SetFlag(Define.FLAG_APPLIED); });

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(30, ob.Stats.Str);
        Assert.Equal(1, ob.Stats.Dex);
    }

    [Theory]
    [InlineData(1, 10, 5, 3)]
    [InlineData(1, 11, 5, 4)] // an odd bonus gives the odd levels one more: con 11 has +1 (1/2 = 0, +1)
    [InlineData(2, 11, 5, 4 + 3)] // level 2: 5 + 0 = 5 for the even one, but 3 for... see below
    public void Fix_Given_levels_When_fixed_Then_maxhp_is_the_sum_of_the_levels_hp_with_the_constitution_bonus_and_at_least_1_each(int level, int con, int levhp, int expectedFirstLevel)
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, level, con);
        for (int i = 1; i <= level; i++)
        {
            contr.LevHp[i] = (short)levhp;
        }
        int bonus = LivingRules.ConBonus[con];

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        int expected = 0;
        for (int i = 1; i <= level; i++)
        {
            int j = levhp + bonus / 2;
            if (i % 2 == 1 && bonus % 2 != 0)
            {
                j += bonus > 0 ? 1 : -1;
            }
            expected += Math.Max(j, 1);
        }
        Assert.Equal(expected, ob.Stats.Maxhp);
        Assert.True(expectedFirstLevel > 0);
    }

    [Fact]
    public void Fix_Given_a_low_constitution_When_fixed_Then_a_level_gives_at_least_1_and_a_negative_odd_bonus_takes_one_more()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 3, con: 1);
        contr.LevHp[1] = 4;
        contr.LevHp[2] = 4;
        contr.LevHp[3] = 1;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert: con_bonus[1] = -5: -5 / 2 = -2, odd levels one less: 4 - 2 - 1 = 1; 4 - 2 = 2; 1 - 2 - 1 = -2 counts 1
        Assert.Equal(1 + 2 + 1, ob.Stats.Maxhp);
    }

    [Fact]
    public void Fix_Given_a_level_over_10_When_fixed_Then_each_level_past_10_adds_2_to_hp_and_the_rolled_levels_stop_at_10()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 13, con: 7); // con_bonus[7] = 0
        for (int i = 1; i <= 10; i++)
        {
            contr.LevHp[i] = 3;
        }

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(10 * 3 + 3 * 2, ob.Stats.Maxhp);
    }

    [Fact]
    public void Fix_Given_hp_and_sp_above_the_new_largest_When_fixed_Then_hp_is_cut_and_sp_is_cut_at_twice_the_largest()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 1, con: 7, pow: 11);
        contr.LevHp[1] = 5;
        contr.LevSp[1] = 3;
        ob.Stats.Hp = 50;
        ob.Stats.Sp = 100;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(5, ob.Stats.Maxhp);
        Assert.Equal(5, ob.Stats.Hp);
        Assert.Equal(3, ob.Stats.Maxsp);
        Assert.Equal(6, ob.Stats.Sp);
    }

    [Fact]
    public void Fix_Given_applied_experience_objects_When_fixed_Then_spell_points_follow_the_level_of_the_one_with_Pow_and_grace_the_one_with_Wis()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 1, pow: 11, wis: 10);
        for (int i = 1; i <= 10; i++)
        {
            contr.LevSp[i] = 2;
            contr.LevGrace[i] = 2;
        }
        f.Give(ob, "experience_magic", o => { o.Level = 3; o.SetFlag(Define.FLAG_APPLIED); });
        f.Give(ob, "experience_wis", o => { o.Level = 2; o.SetFlag(Define.FLAG_APPLIED); });
        f.Give(ob, "experience_physique", o => { o.Level = 9; o.SetFlag(Define.FLAG_APPLIED); });

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert: sp_bonus[11] = 0: 2 per level, 3 levels; grace: 2 + 0 per level, level 1 gets 1 + 0 more, 2 levels
        Assert.Equal(6, ob.Stats.Maxsp);
        Assert.Equal(2 + 1 + 2, ob.Stats.Maxgrace);
    }

    [Fact]
    public void Fix_Given_an_experience_object_that_is_not_applied_or_has_no_level_When_fixed_Then_the_player_s_own_level_counts()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 2, pow: 11, wis: 10);
        for (int i = 1; i <= 10; i++)
        {
            contr.LevSp[i] = 2;
            contr.LevGrace[i] = 2;
        }
        f.Give(ob, "experience_magic", o => { o.Level = 5; });
        f.Give(ob, "experience_wis", o => { o.Level = 0; o.SetFlag(Define.FLAG_APPLIED); });
        f.Give(ob, "experience_mental", o => { o.Level = 0; o.Stats.Pow = 1; o.SetFlag(Define.FLAG_APPLIED); });

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(4, ob.Stats.Maxsp);
        Assert.Equal(2 + 1 + 2, ob.Stats.Maxgrace);
    }

    [Fact]
    public void Fix_Given_a_level_over_10_When_fixed_Then_sp_and_grace_get_2_for_each_level_past_10_of_their_objects()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 1, pow: 11, wis: 10);
        for (int i = 1; i <= 10; i++)
        {
            contr.LevSp[i] = 2;
            contr.LevGrace[i] = 2;
        }
        f.Give(ob, "experience_magic", o => { o.Level = 12; o.SetFlag(Define.FLAG_APPLIED); });
        f.Give(ob, "experience_wis", o => { o.Level = 11; o.SetFlag(Define.FLAG_APPLIED); });

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(20 + 4, ob.Stats.Maxsp);
        Assert.Equal((2 + 1) + 9 * 2 + 2, ob.Stats.Maxgrace);
    }

    [Fact]
    public void Fix_Given_a_grace_bonus_When_fixed_Then_the_fractions_add_up_in_a_float_and_each_level_gives_at_least_1()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 3, pow: 20, wis: 20); // grace_bonus[20] = 9: (9 + 9) / 8 = 2.25
        contr.LevGrace[1] = 1;
        contr.LevGrace[2] = 1;
        contr.LevGrace[3] = 1;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert: level 1: 1 + 2.25 + 1 + 18 / 4 = 8.75; levels 2 and 3: 3.25 each; total 15.25
        Assert.Equal(15, ob.Stats.Maxgrace);
    }

    [Fact]
    public void Fix_Given_a_very_low_grace_bonus_When_fixed_Then_each_level_gives_at_least_one()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 3, pow: 1, wis: 1); // grace_bonus[1] = -10
        contr.LevGrace[1] = 0;
        contr.LevGrace[2] = 0;
        contr.LevGrace[3] = 0;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(3, ob.Stats.Maxgrace);
    }

    [Fact]
    public void Fix_Given_a_grace_bonus_of_a_half_per_level_When_fixed_Then_a_level_that_gives_less_than_one_counts_as_one()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 4, pow: 13, wis: 13); // grace_bonus[13] = 2: (2 + 2) / 8 = 0.5
        for (int i = 1; i <= 4; i++)
        {
            contr.LevGrace[i] = 0;
        }

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert: level 1: 0.5 + 1 + (2 + 2) / 4 = 2.5; levels 2 to 4: 0.5 is below 1, so 1 each: 5.5
        Assert.Equal(5, ob.Stats.Maxgrace);
    }

    [Fact]
    public void Fix_Given_different_Pow_and_Wis_When_fixed_Then_the_grace_bonus_of_each_is_its_own()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 1, pow: 13, wis: 20); // grace_bonus: 2 and 9
        contr.LevGrace[1] = 2;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert: 2 + (2 + 9) / 8 + 1 + (2 + 9) / 4 = 7.125
        Assert.Equal(7, ob.Stats.Maxgrace);
    }

    [Theory]
    [InlineData(0, 200000)]
    [InlineData(1, 250000)]
    [InlineData(14, 1400000)]
    [InlineData(30, 4500000)]
    [InlineData(100, 4500000)]
    [InlineData(-5, 200000)]
    public void Fix_Given_a_strength_When_fixed_Then_the_weight_limit_is_that_of_the_table_and_a_strength_out_of_it_is_cut(int str, int expected)
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        ob.Stats.Str = (sbyte)str;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(expected, contr.WeightLimit);
    }

    [Fact]
    public void Fix_Given_a_monster_or_an_object_without_a_controller_When_fixed_Then_nothing_happens()
    {
        // Arrange
        var f = new Fixture();
        CfObject orc = f.Make("sword");
        orc.Type = Define.MONSTER;
        orc.Stats.Str = 7;
        CfObject player = f.Make("dwarf_player");

        // Act
        FixPlayer.Fix(f.World, orc);
        FixPlayer.Fix(f.World, player);

        // Assert
        Assert.Equal(7, orc.Stats.Str);
        Assert.Equal(12, player.Stats.Maxhp);
    }

    [Fact]
    public void FixPlayer_Given_the_host_When_an_item_comes_into_a_player_with_a_controller_Then_the_player_is_fixed_by_the_world_s_call()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = Rolled(f, 1, con: 7);
        contr.LevHp[1] = 9;

        // Act
        f.Give(ob, "sword");

        // Assert
        Assert.Equal(9, ob.Stats.Maxhp);
    }

    // ------------------------------------------------------------------ roll_stats

    [Theory]
    [InlineData(18, 18, 18, 18, 18, 18, 8, true)] // 116
    [InlineData(18, 18, 18, 18, 18, 18, 9, false)] // 117
    [InlineData(12, 12, 12, 12, 12, 12, 10, true)] // 82
    [InlineData(12, 12, 12, 12, 12, 12, 9, false)] // 81
    public void RollStats_Given_a_sum_at_the_edge_When_rolled_Then_82_to_116_are_kept_and_the_rest_is_rolled_again(int a, int b, int c, int d, int e, int g, int h, bool kept)
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        int[] first = Stats(a, b, c, d, e, g, h);
        int[] second = Stats(13, 13, 13, 13, 13, 13, 13);
        f.Dice(first.Concat(kept ? Array.Empty<int>() : second).ToArray());

        // Act
        Start.RollStats(f.World, ob, contr);

        // Assert
        int sum = ob.Stats.Str + ob.Stats.Dex + ob.Stats.Con + ob.Stats.Int + ob.Stats.Wis + ob.Stats.Pow + ob.Stats.Cha;
        Assert.Equal(kept ? a + b + c + d + e + g + h : 91, sum);
    }

    [Fact]
    public void RollStat_Given_four_dice_When_rolled_Then_the_sum_of_the_best_three()
    {
        // Arrange
        var f = new Fixture();
        f.Dice(2, 5, 0, 3); // 3, 6, 1, 4

        // Act
        int stat = Start.RollStat(f.World);

        // Assert
        Assert.Equal(3 + 6 + 4, stat);
    }

    [Fact]
    public void RollStat_Given_four_equal_dice_When_rolled_Then_three_of_them_count()
    {
        // Arrange
        var f = new Fixture();
        f.Dice(3, 3, 3, 3);

        // Act
        int stat = Start.RollStat(f.World);

        // Assert
        Assert.Equal(12, stat);
    }

    [Fact]
    public void RollStats_Given_a_player_with_a_level_and_experience_When_rolled_Then_level_exp_and_the_other_values_start_again_and_orig_stats_are_the_rolled_ones()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        ob.Level = 9;
        ob.Stats.Exp = 99999;
        ob.Stats.Ac = 5;
        ob.Stats.Grace = 7;
        f.Dice(Stats(13, 18, 12, 15, 17, 14, 16));

        // Act
        Start.RollStats(f.World, ob, contr);

        // Assert
        Assert.Equal(1, ob.Level);
        Assert.Equal(0, ob.Stats.Exp);
        Assert.Equal(-2, ob.Stats.Ac); // roll_stats sets 0, then add_exp reaches fix_player: Dex 18 has an armour class bonus of 2 (wave 5D)
        Assert.Equal(0, ob.Stats.Grace);
        Assert.Equal(ob.Stats.Maxhp, ob.Stats.Hp);
        Assert.Equal(ob.Stats.Maxsp, ob.Stats.Sp);
        Assert.Equal(18, contr.OrigStats.Str);
        Assert.Equal(16, contr.OrigStats.Con);
        Assert.Equal(15, contr.OrigStats.Int);
        Assert.Equal(ob.Stats.Maxhp, contr.OrigStats.Maxhp);
        Assert.True(contr.StatsRolled);
    }

    // ------------------------------------------------------------------ give_initial_items

    [Fact]
    public void GiveInitialItems_Given_a_force_When_given_Then_it_is_applied_unless_it_is_neutral()
    {
        // Arrange
        string arc = StartWorld.Arc + "Object force\nname force\ntype 114\nend\nObject neutral_force\nname neutral\ntype 114\nneutral 1\nend\n";
        var f = new Fixture(arc);
        (CfObject ob, _) = f.Player();
        CfObject plain = f.Give(ob, "force");
        CfObject neutral = f.Give(ob, "neutral_force");

        // Act
        Start.GiveInitialItems(f.World, ob, null);

        // Assert
        Assert.True(plain.QueryFlag(Define.FLAG_APPLIED));
        Assert.False(neutral.QueryFlag(Define.FLAG_APPLIED));
    }

    [Fact]
    public void GiveInitialItems_Given_cursed_items_in_the_tree_When_given_Then_what_needs_identifying_is_identified_and_neither_cursed_nor_damned()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject book = f.Give(ob, "cleric_book", o => { o.SetFlag(Define.FLAG_CURSED); o.SetFlag(Define.FLAG_DAMNED); });
        CfObject coin = f.Give(ob, "goldcoin", o => o.SetFlag(Define.FLAG_CURSED));

        // Act
        Start.GiveInitialItems(f.World, ob, null);

        // Assert
        Assert.True(book.QueryFlag(Define.FLAG_IDENTIFIED));
        Assert.False(book.QueryFlag(Define.FLAG_CURSED));
        Assert.False(book.QueryFlag(Define.FLAG_DAMNED));
        Assert.False(coin.QueryFlag(Define.FLAG_IDENTIFIED));
        Assert.True(coin.QueryFlag(Define.FLAG_CURSED));
    }

    [Fact]
    public void GiveInitialItems_Given_each_kind_of_armour_for_a_race_without_armour_When_given_Then_all_of_them_go_and_a_weapon_goes_for_a_race_without_weapons()
    {
        // Arrange
        string arc = StartWorld.Arc.Replace("can_use_armour 1\ncan_use_weapon 1\n", "")
            + string.Concat(new[] { 16, 99, 87, 34, 33, 100, 104, 113 }.Select(t => "Object armour" + t + "\nname armour" + t + "\ntype " + t + "\nend\n"));
        var f = new Fixture(arc);
        (CfObject ob, _) = f.Player();
        foreach (int t in new[] { 16, 99, 87, 34, 33, 100, 104, 113 })
        {
            f.Give(ob, "armour" + t);
        }
        f.Give(ob, "sword");
        f.Give(ob, "book");

        // Act
        Start.GiveInitialItems(f.World, ob, null);

        // Assert
        Assert.Equal(new[] { "book" }, Inv(ob).Select(i => i.Arch!.Name!).ToArray());
    }

    [Fact]
    public void GiveInitialItems_Given_a_non_player_When_given_Then_armour_and_weapons_stay()
    {
        // Arrange
        var f = new Fixture();
        CfObject orc = f.Make("sword");
        orc.Type = Define.MONSTER;
        f.Give(orc, "sword");
        f.Give(orc, "chain_mail");

        // Act
        Start.GiveInitialItems(f.World, orc, null);

        // Assert
        Assert.Equal(2, Inv(orc).Count());
    }

    [Fact]
    public void GiveInitialItems_Given_an_ability_for_a_player_without_a_controller_When_given_Then_the_ability_still_goes()
    {
        // Arrange
        var f = new Fixture();
        CfObject ob = f.Make("dwarf_player");
        f.Give(ob, "ability_fire");

        // Act
        Start.GiveInitialItems(f.World, ob, null);

        // Assert
        Assert.Null(ob.Inv);
    }

    // ------------------------------------------------------------------ the skills

    private static string ExpArc(string extra = "") =>
        "Object dwarf_player\nname dwarf\ntype 1\nalive 1\nend\n"
        + "Object exp_a\nname alpha\ntype 44\ninvisible 1\nlevel 1\nDex 1\nend\n"
        + "Object exp_b\nname beta\ntype 44\ninvisible 1\nlevel 1\nStr 1\nDex 1\nend\n"
        + "Object exp_c\nname gamma\ntype 44\ninvisible 1\nlevel 1\nInt 1\nend\n"
        + "Object exp_d\nname delta\ntype 44\ninvisible 1\nlevel 1\nInt 1\nend\n" + extra;

    [Fact]
    public void Create_Given_experience_archetypes_When_the_skills_are_linked_Then_the_last_object_with_the_first_stat_wins_and_the_others_count_while_there_is_none()
    {
        // Arrange / Act
        var f = new Fixture(ExpArc());

        // Assert: stealing DEX, INT: exp_a and exp_b have Dex, the last (beta, 1) wins; karate STR, DEX: beta; woodsman INT, DEX: gamma (Int), then
        // beta has Dex but the category is no longer none; bargaining has no stat; praying WIS, POW, INT: gamma through the third stat
        SkillEntry[] s = f.Skills.Skills;
        Assert.Equal(new[] { "alpha", "beta", "gamma", "delta" }, f.Skills.ExpCat.Select(e => e.Name));
        Assert.Equal(1, s[0].Category);
        Assert.Equal(1, s[19].Category);
        Assert.Equal(3, s[21].Category); // woodsman: INT is the first stat, and delta is the last with it
        Assert.Equal(SkillTable.ExpNone, s[9].Category);
        Assert.Equal(2, s[30].Category); // praying: no WIS and no POW, the third stat INT: gamma, which delta does not take over
        Assert.Equal(2, s[11].Category); // sense magic: no POW, the second stat INT: gamma, which delta does not take over
        Assert.Equal(SkillTable.ExpNone, s[29].Category);
    }

    [Fact]
    public void Create_Given_a_skill_params_text_When_applied_Then_each_skill_the_line_names_takes_its_numbers_by_the_prefix_rule()
    {
        // Arrange
        const string text = "# a comment\njumping\n2 3 4 1.5 1 5 99\nuse magic item and more\n0 7 8 0.25 5 1 3\nfrobnicate\n1 1 1 1 1 1 1\nclawing\n";

        // Act
        var f = new Fixture(ExpArc(), skillParams: text);

        // Assert
        SkillEntry jumping = f.Skills.Skills[10];
        Assert.Equal((2, 3, 4L, 1.5f, 1, 5, 99), (jumping.Category, jumping.Time, jumping.Bexp, jumping.Lexp, jumping.Stat1, jumping.Stat2, jumping.Stat3));
        SkillEntry magic = f.Skills.Skills[29];
        Assert.Equal((0, 7, 8L, 0.25f, 5, 1, 3), (magic.Category, magic.Time, magic.Bexp, magic.Lexp, magic.Stat1, magic.Stat2, magic.Stat3));
        Assert.Contains(f.Log, l => l.Contains("skill_params has unrecognized skill: frobnicate"));
        Assert.DoesNotContain(f.Log, l => l.Contains("# a comment"));
        Assert.Contains(f.Log, l => l.Contains("skill_params has unrecognized skill: 1 1 1 1 1 1 1")); // the numbers of an unknown name are read as a name
    }

    [Fact]
    public void Create_Given_a_numbers_line_that_stops_early_When_applied_Then_the_values_that_were_read_are_kept_and_the_rest_are_those_of_the_last_line()
    {
        // Arrange
        const string text = "jumping\n2 3 4 1.5 1 5 99\nclawing\n0 9 x\n";

        // Act
        var f = new Fixture(ExpArc(), skillParams: text);

        // Assert
        SkillEntry clawing = f.Skills.Skills[31];
        Assert.Equal((0, 9, 4L, 1.5f, 1, 5, 99), (clawing.Category, clawing.Time, clawing.Bexp, clawing.Lexp, clawing.Stat1, clawing.Stat2, clawing.Stat3));
    }

    [Fact]
    public void LookupSkillByName_Given_names_When_asked_Then_the_first_skill_the_name_starts_with_and_minus_one_for_a_short_name()
    {
        // Arrange
        var f = new Fixture(ExpArc());

        // Act / Assert
        Assert.Equal(23, f.Skills.LookupSkillByName("melee weapons\n"));
        Assert.Equal(23, f.Skills.LookupSkillByName("melee weapons and more"));
        Assert.Equal(-1, f.Skills.LookupSkillByName("melee"));
        Assert.Equal(-1, f.Skills.LookupSkillByName("hi"));
        Assert.Equal(2, f.Skills.LookupSkillByName("hiding\n"));
    }

    [Fact]
    public void Create_Given_a_world_without_experience_archetypes_When_made_Then_an_error_is_logged_and_there_are_no_categories()
    {
        // Arrange / Act
        var f = new Fixture("Object dwarf_player\nname dwarf\ntype 1\nend\n");

        // Assert
        Assert.Equal(0, f.Skills.NrOfExpCat);
        Assert.Contains(f.Log, l => l.Contains("No experience objects found in archetypes"));
        Assert.All(f.Skills.Skills, s => Assert.Equal(SkillTable.ExpNone, s.Category));
    }

    [Fact]
    public void Create_Given_more_experience_archetypes_than_the_limit_When_made_Then_the_first_seven_are_kept_with_an_error()
    {
        // Arrange
        string many = string.Concat(Enumerable.Range(0, 9).Select(i => "Object exp_x" + i + "\nname x" + i + "\ntype 44\nStr 1\nend\n"));

        // Act
        var f = new Fixture("Object dwarf_player\nname dwarf\ntype 1\nend\n" + many);

        // Assert
        Assert.Equal(7, f.Skills.NrOfExpCat);
        Assert.Contains(f.Log, l => l.Contains("Reached limit of available experience"));
    }

    [Fact]
    public void InitPlayerExp_Given_a_player_without_experience_objects_When_called_Then_it_gets_all_with_its_share_applied_and_the_total_is_the_sum()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        ob.Stats.Exp = 6001;

        // Act
        bool ok = f.Skills.InitPlayerExp(f.World, ob);

        // Assert
        CfObject[] exp = Inv(ob).Where(i => i.Type == Define.EXPERIENCE).ToArray();
        Assert.True(ok);
        Assert.Equal(6, exp.Length);
        Assert.All(exp, e => Assert.Equal(1000, e.Stats.Exp));
        Assert.All(exp, e => Assert.True(e.QueryFlag(Define.FLAG_APPLIED)));
        Assert.All(exp, e => Assert.Equal(2, e.Level)); // 1000 is level 2
        Assert.Equal(6000, ob.Stats.Exp);
    }

    [Fact]
    public void InitPlayerExp_Given_a_player_that_lacks_some_When_called_Then_each_missing_object_is_added_once_and_the_ones_it_has_stay()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject mine = f.Give(ob, "experience_agility", o => o.Stats.Exp = 500);
        f.Give(ob, "experience_magic", o => o.Stats.Exp = 100);

        // Act
        bool ok = f.Skills.InitPlayerExp(f.World, ob);

        // Assert
        Assert.True(ok);
        Assert.Equal(6, Inv(ob).Count(i => i.Type == Define.EXPERIENCE));
        Assert.Equal(6, Inv(ob).Where(i => i.Type == Define.EXPERIENCE).Select(i => i.Name).Distinct().Count());
        Assert.Equal(500, mine.Stats.Exp);
        Assert.Equal(600, ob.Stats.Exp);
        Assert.Contains(f.Log, l => l.Contains("player has too few experience objects"));
    }

    [Fact]
    public void InitPlayerExp_Given_a_player_with_all_of_them_When_called_Then_no_object_is_added_and_the_experience_is_the_sum()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Skills.InitPlayerExp(f.World, ob);
        Inv(ob).First(i => i.Type == Define.EXPERIENCE).Stats.Exp = 777;
        Inv(ob).First(i => i.Type == Define.EXPERIENCE).ClearFlag(Define.FLAG_APPLIED);
        f.Log.Clear();

        // Act
        bool ok = f.Skills.InitPlayerExp(f.World, ob);

        // Assert
        Assert.True(ok);
        Assert.Equal(6, Inv(ob).Count());
        Assert.Equal(777, ob.Stats.Exp);
        Assert.All(Inv(ob), e => Assert.True(e.QueryFlag(Define.FLAG_APPLIED)));
        Assert.Empty(f.Log);
    }

    [Fact]
    public void InitPlayerExp_Given_too_many_objects_When_called_Then_an_error_is_logged_and_nothing_is_added()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Skills.InitPlayerExp(f.World, ob);
        f.Give(ob, "experience_agility");
        f.Log.Clear();

        // Act
        bool ok = f.Skills.InitPlayerExp(f.World, ob);

        // Assert
        Assert.True(ok);
        Assert.Equal(7, Inv(ob).Count());
        Assert.Contains(f.Log, l => l.Contains("player has too many experience objects"));
    }

    [Fact]
    public void InitPlayerExp_Given_something_that_is_not_a_player_When_called_Then_false_and_an_error()
    {
        // Arrange
        var f = new Fixture();
        CfObject sword = f.Make("sword");

        // Act
        bool ok = f.Skills.InitPlayerExp(f.World, sword);

        // Assert
        Assert.False(ok);
        Assert.Contains(f.Log, l => l.Contains("init_player_exp(): called non-player sword"));
    }

    [Fact]
    public void LinkPlayerSkills_Given_skills_and_experience_When_linked_Then_each_skill_is_unapplied_set_to_one_and_linked_to_the_object_of_its_category()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Skills.InitPlayerExp(f.World, ob);
        CfObject melee = f.Give(ob, "skill_melee_weapon", o => { o.SetFlag(Define.FLAG_APPLIED); o.Nrof = 3; });
        CfObject praying = f.Give(ob, "skill_praying");
        CfObject stealing = f.Give(ob, "skill_stealing");
        ob.ChosenSkill = melee;
        ob.SetFlag(Define.FLAG_READY_SKILL);
        Inv(ob).First(i => i.Type == Define.EXPERIENCE).Nrof = 4;

        // Act
        bool ok = f.Skills.LinkPlayerSkills(f.World, ob);

        // Assert
        Assert.True(ok);
        Assert.Null(ob.ChosenSkill);
        Assert.False(ob.QueryFlag(Define.FLAG_READY_SKILL));
        Assert.False(melee.QueryFlag(Define.FLAG_APPLIED));
        Assert.Equal(1u, melee.Nrof);
        Assert.All(Inv(ob).Where(i => i.Type == Define.EXPERIENCE), e => Assert.Equal(1u, e.Nrof));
        Assert.Equal("physique", melee.ExpObj!.Name);
        Assert.Equal("wisdom", praying.ExpObj!.Name);
        Assert.Equal("agility", stealing.ExpObj!.Name);
    }

    [Theory]
    [InlineData(33)]
    [InlineData(-1)]
    [InlineData(32767)]
    public void LinkPlayerSkill_Given_a_skill_number_outside_the_table_When_linked_Then_it_has_no_category_and_no_experience_object(int sp)
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Skills.InitPlayerExp(f.World, ob);
        CfObject odd = f.Make("skill_praying");
        odd.Stats.Sp = (short)sp;
        odd.ExpObj = f.Make("experience_wis");

        // Act
        bool linked = f.Skills.LinkPlayerSkill(ob, odd);

        // Assert
        Assert.False(linked);
        Assert.Null(odd.ExpObj);
    }

    [Fact]
    public void LinkPlayerSkill_Given_a_category_the_world_has_no_object_for_When_linked_Then_it_is_taken_as_none()
    {
        // Arrange
        var f = new Fixture(ExpArc(), skillParams: "jumping\n7 0 0 1 1 1 1\nclawing\n3 0 0 1 1 1 1\nstealing\n2 0 0 1 1 1 1\nhiding\n4 0 0 1 1 1 1\n");
        (CfObject ob, _) = f.Player();
        f.Skills.InitPlayerExp(f.World, ob);
        CfObject beyond = f.Make("exp_a");
        beyond.Stats.Sp = 10; // jumping: category 7
        CfObject last = f.Make("exp_a");
        last.Stats.Sp = 31; // clawing: category 3, the last one of four
        CfObject inside = f.Make("exp_a");
        inside.Stats.Sp = 0; // stealing: category 2

        CfObject edge = f.Make("exp_a");
        edge.Stats.Sp = 2; // hiding: category 4, which is one past the last of the four

        // Act
        bool edgeLinked = f.Skills.LinkPlayerSkill(ob, edge);
        bool a = f.Skills.LinkPlayerSkill(ob, beyond);
        bool b = f.Skills.LinkPlayerSkill(ob, last);
        bool c = f.Skills.LinkPlayerSkill(ob, inside);

        // Assert
        Assert.False(edgeLinked);
        Assert.Null(edge.ExpObj);
        Assert.False(a);
        Assert.True(b);
        Assert.Equal("delta", last.ExpObj!.Name);
        Assert.True(c);
        Assert.Equal("gamma", inside.ExpObj!.Name);
    }

    private sealed class SendHost : CrossfireHost
    {
        public List<string> Sent { get; } = new();

        public override void EsrvSendItem(CfObject player, CfObject op) => Sent.Add(player.Name + ":" + op.Name);
    }

    [Fact]
    public void InitPlayerExp_Given_a_client_When_objects_are_made_Then_each_new_experience_object_is_sent_and_one_that_was_there_is_not()
    {
        // Arrange
        var host = new SendHost();
        var world = new CfWorld(host);
        world.InitArchetypes("Object empty_archetype\nend\n" + ExpArc());
        SkillTable skills = SkillTable.Create(world, "# none\n");
        CfObject empty = world.ArchToObject(world.FindArchetype("dwarf_player"))!;
        empty.Name = "bob";
        CfObject partial = world.ArchToObject(world.FindArchetype("dwarf_player"))!;
        partial.Name = "ann";
        world.InsertObInOb(world.ArchToObject(world.FindArchetype("exp_a"))!, partial);
        host.Sent.Clear();

        // Act
        skills.InitPlayerExp(world, empty);
        skills.InitPlayerExp(world, partial);

        // Assert
        Assert.Equal(new[] { "bob:alpha", "bob:beta", "bob:gamma", "bob:delta", "ann:beta", "ann:gamma", "ann:delta" }, host.Sent.ToArray());
    }

    [Fact]
    public void LinkPlayerSkills_Given_a_skill_that_is_not_invisible_or_has_a_number_out_of_the_table_When_linked_Then_it_is_not_linked()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Skills.InitPlayerExp(f.World, ob);
        CfObject shown = f.Give(ob, "skill_melee_weapon", o => { o.Invisible = 0; o.Nrof = 5; });
        CfObject odd = f.Give(ob, "skill_praying", o => o.Stats.Sp = 200);
        CfObject none = f.Give(ob, "skill_stealing", o => o.Stats.Sp = -1);

        // Act
        f.Skills.LinkPlayerSkills(f.World, ob);

        // Assert
        Assert.Null(shown.ExpObj);
        Assert.Equal(5u, shown.Nrof);
        Assert.Null(odd.ExpObj);
        Assert.Null(none.ExpObj);
    }

    [Fact]
    public void LinkPlayerSkills_Given_a_player_with_the_wrong_number_of_experience_objects_When_linked_Then_they_are_made_first_and_the_skills_linked()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject melee = f.Give(ob, "skill_melee_weapon");
        ob.Stats.Exp = 600;

        // Act
        bool ok = f.Skills.LinkPlayerSkills(f.World, ob);

        // Assert
        Assert.True(ok);
        Assert.Equal(6, Inv(ob).Count(i => i.Type == Define.EXPERIENCE));
        Assert.Equal("physique", melee.ExpObj!.Name);
        Assert.Equal(600, ob.Stats.Exp);
        Assert.Contains(f.Log, l => l.Contains("player has bad number of exp obj"));
    }

    [Fact]
    public void LinkPlayerSkills_Given_more_experience_objects_than_categories_When_linked_Then_it_gives_up_with_an_error_instead_of_calling_itself_for_ever()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Skills.InitPlayerExp(f.World, ob);
        f.Give(ob, "experience_agility");
        f.Log.Clear();

        // Act
        bool ok = f.Skills.LinkPlayerSkills(f.World, ob);

        // Assert
        Assert.False(ok);
        Assert.Contains(f.Log, l => l.Contains("failed to correct problem"));
    }

    [Fact]
    public void LinkPlayerSkills_Given_experience_objects_When_linked_Then_the_player_s_experience_is_their_sum_and_each_is_applied_and_levelled()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Skills.InitPlayerExp(f.World, ob);
        CfObject[] exp = Inv(ob).Where(i => i.Type == Define.EXPERIENCE).ToArray();
        exp[0].Stats.Exp = 1000;
        exp[1].Stats.Exp = 4000;
        exp[1].ClearFlag(Define.FLAG_APPLIED);
        ob.Stats.Exp = 123456;

        // Act
        f.Skills.LinkPlayerSkills(f.World, ob);

        // Assert
        Assert.Equal(5000, ob.Stats.Exp);
        Assert.True(exp[1].QueryFlag(Define.FLAG_APPLIED));
        Assert.Equal(2, exp[0].Level);
        Assert.Equal(4, exp[1].Level);
    }

    [Fact]
    public void LinkPlayerSkill_Given_a_skill_with_a_category_When_linked_Then_it_finds_the_object_of_that_name_in_the_inventory_and_a_miscellaneous_skill_has_none()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Skills.InitPlayerExp(f.World, ob);
        CfObject melee = f.Make("skill_melee_weapon");
        CfObject misc = f.Make("skill_praying");
        misc.Stats.Sp = 16; // meditation: no category
        misc.ExpObj = f.Make("experience_wis");

        // Act
        bool a = f.Skills.LinkPlayerSkill(ob, melee);
        bool b = f.Skills.LinkPlayerSkill(ob, misc);

        // Assert
        Assert.True(a);
        Assert.Same(Inv(ob).First(i => i.Name == "physique"), melee.ExpObj);
        Assert.False(b);
        Assert.Null(misc.ExpObj);
    }

    [Fact]
    public void UnlinkSkill_Given_a_skill_of_a_player_When_unlinked_Then_it_has_no_experience_object_and_a_skill_outside_a_player_is_an_error()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject melee = f.Give(ob, "skill_melee_weapon");
        melee.ExpObj = f.Make("experience_physique");
        CfObject loose = f.Make("skill_praying");
        loose.ExpObj = f.Make("experience_wis");
        CfObject orc = f.Make("sword");
        orc.Type = Define.MONSTER;
        CfObject ofMonster = f.Give(orc, "skill_praying", o => o.ExpObj = f.Make("experience_wis"));

        // Act
        SkillTable.UnlinkSkill(f.World, melee);
        SkillTable.UnlinkSkill(f.World, loose);
        SkillTable.UnlinkSkill(f.World, ofMonster);
        SkillTable.UnlinkSkill(f.World, null);

        // Assert
        Assert.Null(melee.ExpObj);
        Assert.NotNull(loose.ExpObj);
        Assert.NotNull(ofMonster.ExpObj);
        Assert.Equal(3, f.Log.Count(l => l.Contains("unlink_skill() called for non-player")));
    }

    // ------------------------------------------------------------------ the data

    private static string Embedded(string name)
    {
        using Stream s = typeof(Dispatcher).Assembly.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return FileText.FromBytes(ms.ToArray());
    }

    [Fact]
    public void Treasures_Given_a_world_without_its_own_file_When_started_Then_the_lists_are_those_of_Crossfire_s_lib_treasures()
    {
        // Arrange
        using TempFolder world = StartWorld.Make(treasures: null);
        string text = Embedded("treasures");
        int expected = text.Split('\n').Count(l => l.StartsWith("treasure ", StringComparison.Ordinal) || l.StartsWith("treasureone ", StringComparison.Ordinal));

        // Act
        using DirectExt ext = StartWorld.Start(world);
        TreasureList[] lists = ext.Dispatcher.WorldOfTests!.TreasureLists.ToArray();

        // Assert
        Assert.Equal(expected, lists.Length);
        Assert.True(expected > 100);
        Assert.Contains(lists, l => l.Name == "warrior_class_items");
        Assert.Contains(lists, l => l.Name == "thief_class_items");
    }

    [Fact]
    public void Treasures_Given_a_world_with_its_own_file_When_started_Then_its_lists_are_the_ones_read()
    {
        // Arrange
        using TempFolder world = StartWorld.Make();

        // Act
        using DirectExt ext = StartWorld.Start(world);

        // Assert
        Assert.Equal(new[] { "warrior_class_items", "thief_class_items" }, ext.Dispatcher.WorldOfTests!.TreasureLists.Select(l => l.Name));
    }

    [Fact]
    public void SkillParams_Given_a_world_with_its_own_file_When_started_Then_it_is_read_and_otherwise_the_one_of_Crossfire()
    {
        // Arrange
        using TempFolder own = StartWorld.Make();
        File.WriteAllText(Path.Combine(own.Path, "skill_params"), "stealing\n4 8 15 1.5 1 5 99\n");
        using TempFolder plain = StartWorld.Make();

        // Act
        using DirectExt a = StartWorld.Start(own);
        using DirectExt b = StartWorld.Start(plain);

        // Assert
        Assert.Equal((4, 8, 15L), (a.Dispatcher.SkillsOfTests!.Skills[0].Category, a.Dispatcher.SkillsOfTests.Skills[0].Time, a.Dispatcher.SkillsOfTests.Skills[0].Bexp));
        Assert.Equal((0, 0, 20L), (b.Dispatcher.SkillsOfTests!.Skills[0].Category, b.Dispatcher.SkillsOfTests.Skills[0].Time, b.Dispatcher.SkillsOfTests.Skills[0].Bexp));
    }
}
