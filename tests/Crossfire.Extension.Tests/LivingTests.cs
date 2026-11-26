// Not ported from a Crossfire file: tests of lane 5D of phase 3, the living core: the rest of common/living.c <c>fix_player</c> (armour
// class, weapon class, damage, speed, resistances, flags, regeneration), <c>change_abil</c> with its messages, <c>add_statbonus</c>,
// <c>check_skill_to_apply</c>, and the strings mud_ac, mud_wc, mud_dam and mud_speed that follow a fix (docs/mud-phase3.md section 8).
//
// The golden values of the scenarios of FixPlayer_Given_a_scenario... come from golden/living_golden.c: the arithmetic of fix_player
// with C's declarations, compiled with gcc (the file's header says how to run it); the numbers below are its output lines.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>The host of these tests: it fixes a player as the extension does and records the lines of text the player is shown.</summary>
internal sealed class LivingHost : CrossfireHost
{
    public CfWorld? World { get; set; }

    public List<string> Lines { get; } = new();

    public override void FixPlayer(CfObject player)
    {
        if (World != null)
        {
            Crossfire.Extension.Gk.FixPlayer.Fix(World, player);
        }
    }

    public override void DrawInfo(CfObject who, string text) => Lines.Add(text);

    public override int PlayerDigestion(IPlayerHandle contr) => ((Player)contr).Digestion;

    public override int PlayerGenHp(IPlayerHandle contr) => ((Player)contr).GenHp;
}

public sealed class LivingTests
{
    private const string Arc =
        StartWorld.Arc
        + "Object force\nname force\ntype 114\nend\n"
        + "Object helm\nname helm\ntype 34\nend\n"
        + "Object bow\nname bow\ntype 14\nend\n"
        + "Object skill_use_magic\nname use magic item\ntype 43\ninvisible 1\nno_drop 1\nsp 29\nend\n";

    private sealed class Fixture
    {
        public LivingHost Host { get; } = new();

        public CfWorld World { get; }

        public SkillTable Skills { get; }

        public Fixture()
        {
            World = new CfWorld(Host, (_, _) => { });
            Host.World = World;
            World.InitArchetypes("Object empty_archetype\nend\n" + Arc);
            Skills = SkillTable.Create(World, "# none\n");
        }

        public CfObject Make(string arch) => World.ArchToObject(World.FindArchetype(arch))!;

        /// <summary>A rolled player of the archetype dwarf_player (Str 14, Dex 12, Con 10, Int 8, Wis 9, Pow 11, Cha 7) with the given Str, Dex and level.</summary>
        public (CfObject Ob, Player Contr) Player(int str = 14, int dex = 12, int level = 1)
        {
            CfObject ob = Make("dwarf_player");
            ob.Name = "bob";
            ob.Stats.Str = (sbyte)str;
            ob.Stats.Dex = (sbyte)dex;
            var contr = new Player(new PlayerState("bob", ob), _ => { });
            ob.Contr = contr;
            contr.OrigStats.CopyFrom(ob.Stats);
            contr.StoreStats();
            ob.Level = (short)level;
            return (ob, contr);
        }

        /// <summary>An item of the archetype sword, made into anything with <paramref name="tweak"/>, and put into the player's tree (applied when asked).</summary>
        public CfObject Give(CfObject to, int type, Action<CfObject>? tweak = null, bool applied = true, string arch = "sword")
        {
            CfObject o = Make(arch);
            o.Type = (byte)type;
            tweak?.Invoke(o);
            if (applied)
            {
                o.SetFlag(Define.FLAG_APPLIED);
            }
            return World.InsertObInOb(o, to);
        }
    }

    // ------------------------------------------------------------------ fix_player: the scenarios of golden/living_golden.c

    private static void Give(Fixture f, CfObject ob, int type, int wc = 0, int ac = 0, int dam = 0, int magic = 0, int lastSp = 0, int weight = 0, int exp = 0, int glow = 0, int[]? resist = null, string arch = "sword", bool chosen = false, int level = 0)
    {
        CfObject it = f.Give(ob, type, o =>
        {
            o.Stats.Wc = (sbyte)wc;
            o.Stats.Ac = (sbyte)ac;
            o.Stats.Dam = (short)dam;
            o.Magic = (sbyte)magic;
            o.LastSp = lastSp;
            o.Weight = weight;
            o.Stats.Exp = exp;
            o.GlowRadius = (short)glow;
            o.Level = (short)level;
            for (int i = 0; resist != null && i < resist.Length; i++)
            {
                o.Resist[i] = (short)resist[i];
            }
        }, arch: arch);
        if (chosen)
        {
            ob.ChosenSkill = it;
        }
    }

    /// <summary>One scenario of the harness: the same player, items and numbers (the harness's names).</summary>
    private static (CfObject Ob, Player Contr, Fixture F) Scenario(string name)
    {
        var f = new Fixture();
        (CfObject ob, Player contr) = name switch
        {
            "slow_armour" => f.Player(16, 20, 3),
            "overloaded" => f.Player(10, 14, 5),
            "speedy" or "slowed" => f.Player(12, 16, 5),
            "fighter" => f.Player(22, 12, 14),
            "diseased" => f.Player(14, 14, 2),
            "skill" => f.Player(14, 12, 18),
            "noarmour" => f.Player(14, 12, 20),
            _ => f.Player(),
        };
        int carrying = 0;
        switch (name)
        {
            case "armed":
                Give(f, ob, Define.WEAPON, wc: 3, dam: 8, magic: 1, lastSp: 5, weight: 3000);
                Give(f, ob, Define.ARMOUR, ac: 5, weight: 20000);
                carrying = 23000;
                break;
            case "slow_armour":
                Give(f, ob, Define.ARMOUR, ac: 3, lastSp: 4, weight: 30000);
                Give(f, ob, Define.HELMET, ac: 1, weight: 2000, glow: 2);
                break;
            case "overloaded":
                carrying = 100000;
                break;
            case "speedy":
                Give(f, ob, Define.RING, weight: 100, exp: 3);
                Give(f, ob, Define.RING, weight: 100, exp: 6);
                break;
            case "slowed":
                Give(f, ob, Define.RING, weight: 100, exp: -2);
                break;
            case "resist":
                ob.Arch!.Clone.Resist[2] = 20;
                ob.Arch.Clone.Resist[4] = -30;
                Give(f, ob, Define.ARMOUR, ac: 2, weight: 1000, resist: new[] { 0, 50, 40 });
                Give(f, ob, Define.RING, weight: 100, resist: new[] { 0, 0, -25 });
                Give(f, ob, Define.POTION_EFFECT, weight: 100, resist: new[] { 0, 0, 60 }, arch: "force");
                break;
            case "noarmour":
                ob.ClearFlag(Define.FLAG_USE_ARMOUR);
                ob.Arch!.Clone.Stats.Ac = 1;
                ob.Arch.Clone.Resist[1] = 10;
                break;
            case "braced":
                contr.Braced = true;
                break;
            case "fighter":
                CfObject exp = f.Make("experience_physique");
                exp.Level = 13;
                exp.SetFlag(Define.FLAG_APPLIED);
                f.World.InsertObInOb(exp, ob);
                break;
            case "diseased":
                Give(f, ob, Define.SYMPTOM, lastSp: 50);
                break;
            case "skill":
                Give(f, ob, Define.SKILL, wc: 2, ac: 1, dam: 3, magic: 2, lastSp: 6, weight: 4000, chosen: true, level: 18);
                break;
        }
        ob.Carrying = carrying;
        FixPlayer.Fix(f.World, ob);
        return (ob, contr, f);
    }

    [Theory]
    [InlineData("bare", 0, -2, 1, 1f, 0.516877651f, 0, 0, 0, 0, 0)]
    [InlineData("armed", -5, -6, 10, 1f, 0.670652747f, 0, 0, 0, 0, 0)]
    [InlineData("slow_armour", -7, -4, 2, 0.400000006f, 0.15469718f, 2, 0, 0, 0, 0)]
    [InlineData("overloaded", -1, -5, 1, 0.703499973f, 0.36117509f, 0, 0, 0, 0, 0)]
    [InlineData("speedy", -1, -5, 1, 1.89999998f, 0.705479562f, 0, 0, 0, 0, 0)]
    [InlineData("slowed", -1, -5, 1, 0.366666675f, 0.175352886f, 0, 0, 0, 0, 0)]
    [InlineData("resist", -2, -2, 1, 1f, 0.516877651f, 0, 0, 50, 60, -30)]
    [InlineData("noarmour", -5, -21, 1, 1f, 0.399964839f, 0, 12, 10, 0, 0)]
    [InlineData("braced", 2, 2, 1, 1f, 0.516877651f, 0, 0, 0, 0, 0)]
    [InlineData("fighter", 0, -18, 7, 1f, 0.297090411f, 0, 0, 0, 0, 0)]
    [InlineData("diseased", -1, -3, 1, 0.524999976f, 0.263951719f, 0, 0, 0, 0, 0)]
    [InlineData("skill", -3, -25, 12, 1f, 0.604905248f, 0, 0, 0, 0, 0)]
    public void FixPlayer_Given_a_scenario_of_the_golden_harness_When_fixed_Then_armour_class_weapon_class_damage_speed_light_and_resistances_are_the_c_values(
        string name, int ac, int wc, int dam, float speed, float weaponSp, int light, int res0, int res1, int res2, int res4)
    {
        // Arrange / Act
        (CfObject ob, Player contr, _) = Scenario(name);

        // Assert
        Assert.Equal(new[] { ac, wc, dam }, new int[] { ob.Stats.Ac, ob.Stats.Wc, ob.Stats.Dam });
        Assert.Equal(speed, ob.Speed);
        Assert.Equal(weaponSp, contr.WeaponSp);
        Assert.Equal(light, (int)ob.GlowRadius);
        Assert.Equal(new[] { res0, res1, res2, res4 }, new int[] { ob.Resist[0], ob.Resist[1], ob.Resist[2], ob.Resist[4] });
    }

    // ------------------------------------------------------------------ fix_player: what the golden harness does not hold

    [Fact]
    public void FixPlayer_Given_potion_effects_When_fixed_Then_a_cursed_one_replaces_the_resistance_and_a_weaker_good_one_does_not_and_the_strongest_good_one_wins()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Give(ob, Define.ARMOUR, o => o.Resist[2] = 80, arch: "sword"); // fire 80
        f.Give(ob, Define.POTION_EFFECT, o => o.Resist[2] = 30, arch: "force"); // weaker: does not count
        f.Give(ob, Define.POTION_EFFECT, o => { o.Resist[4] = -40; o.Resist[5] = 20; }, arch: "force"); // a cursed one: cold -40
        f.Give(ob, Define.POTION_EFFECT, o => o.Resist[5] = 35, arch: "force"); // the bigger of two good ones: 35
        f.Give(ob, Define.POTION, o => o.Resist[6] = 90); // a potion that is not an effect: its resistance is not counted
        f.Give(ob, Define.POTION_EFFECT, o => o.Resist[7] = 50); // an effect that is not of the archetype force: counted as any item
        f.Give(ob, Define.POTION_EFFECT, o => o.Resist[7] = 50);

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(new[] { 80, -40, 35, 0, 75 }, new int[] { ob.Resist[2], ob.Resist[4], ob.Resist[5], ob.Resist[6], ob.Resist[7] });
    }

    [Fact]
    public void FixPlayer_Given_three_armours_in_any_order_When_fixed_Then_only_the_best_armour_class_counts()
    {
        // Arrange
        var f = new Fixture();
        (CfObject up, _) = f.Player();
        (CfObject down, _) = f.Player();
        foreach (int ac in new[] { 3, 5, 7 })
        {
            f.Give(up, Define.ARMOUR, o => o.Stats.Ac = (sbyte)ac);
        }
        foreach (int ac in new[] { 7, 5, 3 })
        {
            f.Give(down, Define.ARMOUR, o => o.Stats.Ac = (sbyte)ac);
        }

        // Act
        FixPlayer.Fix(f.World, up);
        FixPlayer.Fix(f.World, down);

        // Assert
        Assert.Equal(new[] { -7, -7 }, new int[] { up.Stats.Ac, down.Stats.Ac });
    }

    [Fact]
    public void FixPlayer_Given_a_player_that_was_never_rolled_When_fixed_Then_armour_class_speed_and_resistances_stay_as_they_are()
    {
        // Arrange
        var f = new Fixture();
        CfObject ob = f.Make("dwarf_player");
        ob.Contr = new Player(new PlayerState("bob", ob), _ => { });
        ob.Stats.Ac = 9;
        ob.Speed = 3;
        ob.Resist[2] = 44;
        f.Give(ob, Define.ARMOUR, o => o.Stats.Ac = 4);

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(new[] { 9, 44 }, new[] { (int)ob.Stats.Ac, (int)ob.Resist[2] });
        Assert.Equal(3f, ob.Speed);
    }

    [Fact]
    public void FixPlayer_Given_items_that_are_not_applied_or_are_containers_When_fixed_Then_they_change_nothing_but_the_light()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Give(ob, Define.ARMOUR, o => o.Stats.Ac = 6, applied: false);
        f.Give(ob, Define.CONTAINER, o => { o.Stats.Ac = 6; o.Stats.Luck = 3; }, arch: "sack");
        f.Give(ob, Define.RING, o => { o.Stats.Ac = 6; o.GlowRadius = 3; }, applied: false);

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(0, ob.Stats.Ac);
        Assert.Equal(0, ob.Stats.Luck);
        Assert.Equal(3, (int)ob.GlowRadius);
    }

    [Fact]
    public void FixPlayer_Given_applied_items_with_flags_When_fixed_Then_the_player_has_the_flags_and_loses_them_when_the_items_go()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        int[] flags =
        {
            Define.FLAG_LIFESAVE, Define.FLAG_REFL_SPELL, Define.FLAG_REFL_MISSILE, Define.FLAG_STEALTH, Define.FLAG_UNDEAD, Define.FLAG_MAKE_INVIS,
            Define.FLAG_FLYING, Define.FLAG_XRAYS, Define.FLAG_BLIND, Define.FLAG_SEE_IN_DARK,
        };
        CfObject ring = f.Give(ob, Define.RING, o =>
        {
            foreach (int flag in flags)
            {
                o.SetFlag(flag);
            }
        });

        // Act
        FixPlayer.Fix(f.World, ob);
        bool[] with = flags.Select(ob.QueryFlag).ToArray();
        short invisible = ob.Invisible;
        ring.ClearFlag(Define.FLAG_APPLIED);
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.All(with, Assert.True);
        Assert.Equal(1, invisible);
        Assert.All(flags.Select(ob.QueryFlag), Assert.False);
    }

    [Fact]
    public void FixPlayer_Given_flags_of_the_archetype_or_a_wizard_When_fixed_Then_those_flags_stay()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        ob.Arch!.Clone.SetFlag(Define.FLAG_FLYING);
        ob.Arch.Clone.SetFlag(Define.FLAG_REFL_SPELL);
        ob.Arch.Clone.SetFlag(Define.FLAG_REFL_MISSILE);
        ob.Arch.Clone.SetFlag(Define.FLAG_UNDEAD);
        ob.Arch.Clone.SetFlag(Define.FLAG_SEE_IN_DARK);
        foreach (int flag in new[] { Define.FLAG_FLYING, Define.FLAG_REFL_SPELL, Define.FLAG_REFL_MISSILE, Define.FLAG_UNDEAD, Define.FLAG_SEE_IN_DARK })
        {
            ob.SetFlag(flag);
        }
        (CfObject wiz, _) = f.Player();
        wiz.SetFlag(Define.FLAG_WIZ);
        foreach (int flag in new[] { Define.FLAG_FLYING, Define.FLAG_XRAYS, Define.FLAG_MAKE_INVIS })
        {
            wiz.SetFlag(flag);
        }

        // Act
        FixPlayer.Fix(f.World, ob);
        FixPlayer.Fix(f.World, wiz);

        // Assert
        Assert.All(new[] { Define.FLAG_FLYING, Define.FLAG_REFL_SPELL, Define.FLAG_REFL_MISSILE, Define.FLAG_UNDEAD, Define.FLAG_SEE_IN_DARK }.Select(ob.QueryFlag), Assert.True);
        Assert.All(new[] { Define.FLAG_FLYING, Define.FLAG_XRAYS, Define.FLAG_MAKE_INVIS }.Select(wiz.QueryFlag), Assert.True);
    }

    [Fact]
    public void FixPlayer_Given_a_flying_item_When_fixed_Then_the_speed_is_at_most_1_unless_the_player_is_a_wizard()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(dex: 20);
        (CfObject wiz, _) = f.Player(dex: 20);
        wiz.SetFlag(Define.FLAG_WIZ);
        f.Give(ob, Define.BOOTS, o => o.SetFlag(Define.FLAG_FLYING));
        f.Give(wiz, Define.BOOTS, o => o.SetFlag(Define.FLAG_FLYING));

        // Act
        FixPlayer.Fix(f.World, ob);
        FixPlayer.Fix(f.World, wiz);

        // Assert
        Assert.Equal(1f, ob.Speed);
        Assert.Equal(1.5f, wiz.Speed);
    }

    [Fact]
    public void FixPlayer_Given_applied_items_When_fixed_Then_the_regeneration_and_digestion_come_from_the_listed_types_only()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        void Add(int type, int food, int hp, int sp, int grace, int lastHeal, string arch = "sword") =>
            f.Give(ob, type, o => { o.Stats.Food = (short)food; o.Stats.Hp = (short)hp; o.Stats.Sp = (short)sp; o.Stats.Grace = (short)grace; o.LastHeal = lastHeal; }, arch: arch);
        Add(Define.WEAPON, 1, 2, 3, 4, 5);
        Add(Define.RING, 10, 20, 30, 40, 50);
        Add(Define.EXPERIENCE, 100, 200, 300, 400, 500, "experience_wis");
        Add(Define.EXPERIENCE, 90000, 90000, 90000, 90000, 90000, "experience_agility"); // only the wisdom experience object passes the stat rule
        Add(Define.POTION, 7, 7, 7, 7, 7); // not in the list
        Add(Define.SKILL, 8, 8, 8, 8, 8); // not in the list
        Add(Define.FORCE, 1000, 2000, 3000, 4000, 5000);

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(new[] { 1111, 2222, 3333, 4444 }, new[] { contr.Digestion, contr.GenHp, contr.GenSp, contr.GenGrace });
        Assert.Equal(10 + 5 + 50 + 5000, contr.GenSpArmour); // experience objects keep last_heal for themselves
        Assert.Equal(1111, f.World.Host.PlayerDigestion(contr));
        Assert.Equal(2222, f.World.Host.PlayerGenHp(contr));
    }

    [Fact]
    public void FixPlayer_Given_a_second_fix_When_nothing_changed_Then_the_regeneration_does_not_pile_up()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        f.Give(ob, Define.RING, o => { o.Stats.Food = 3; o.LastHeal = 2; });

        // Act
        FixPlayer.Fix(f.World, ob);
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(3, contr.Digestion);
        Assert.Equal(12, contr.GenSpArmour);
    }

    [Fact]
    public void FixPlayer_Given_attack_types_and_spell_paths_When_fixed_Then_they_are_or_ed_except_for_bows_and_symptoms_and_the_archetype_fills_in_when_there_are_none()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        ob.Arch!.Clone.Attacktype = Define.AT_FIRE;
        ob.Arch.Clone.PathAttuned = 1;
        CfObject ring = f.Give(ob, Define.RING, o => { o.Attacktype = Define.AT_CONFUSION; o.PathAttuned = 2; o.PathRepelled = 4; o.PathDenied = 8; });
        f.Give(ob, Define.BOW, o => o.Attacktype = Define.AT_COLD, arch: "bow");
        f.Give(ob, Define.SYMPTOM, o => o.Attacktype = Define.AT_POISON);

        // Act
        FixPlayer.Fix(f.World, ob);
        uint withRing = ob.Attacktype;
        uint[] paths = { ob.PathAttuned, ob.PathRepelled, ob.PathDenied };
        ring.ClearFlag(Define.FLAG_APPLIED);
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(Define.AT_CONFUSION, withRing);
        Assert.Equal(new uint[] { 3, 4, 8 }, paths);
        Assert.Equal(Define.AT_FIRE, ob.Attacktype);
        Assert.Equal(1u, ob.PathAttuned);
    }

    [Fact]
    public void FixPlayer_Given_a_weapon_with_slaying_When_fixed_Then_the_player_slays_and_forgets_it_when_the_weapon_goes()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject sword = f.Give(ob, Define.WEAPON, o => o.Slaying = "orc");

        // Act
        FixPlayer.Fix(f.World, ob);
        string? with = ob.Slaying;
        CfObject? current = ob.CurrentWeapon;
        sword.ClearFlag(Define.FLAG_APPLIED);
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal("orc", with);
        Assert.Same(sword, current);
        Assert.Null(ob.Slaying);
    }

    [Fact]
    public void FixPlayer_Given_the_chosen_skill_has_a_slaying_and_an_experience_object_When_fixed_Then_the_skill_takes_the_level_of_the_experience()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(level: 4);
        CfObject exp = f.Give(ob, Define.EXPERIENCE, o => o.Level = 9, arch: "experience_agility");
        CfObject skill = f.Give(ob, Define.SKILL, o => { o.Slaying = "dragon"; o.Level = 1; });
        ob.ChosenSkill = skill;
        skill.ExpObj = exp;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(9, skill.Level);
        Assert.Equal("dragon", ob.Slaying);
    }

    [Fact]
    public void FixPlayer_Given_items_that_add_luck_and_negative_dexterity_When_fixed_Then_luck_is_the_sum_and_the_stats_stay_in_bounds()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Give(ob, Define.RING, o => o.Stats.Luck = 3);
        f.Give(ob, Define.AMULET, o => o.Stats.Luck = -1);

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(2, ob.Stats.Luck);
    }

    [Fact]
    public void FixPlayer_Given_a_searching_player_When_fixed_Then_the_speed_is_1_lower()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player(dex: 14);
        contr.Searching = true;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(0.05f, ob.Speed, 5);
    }

    [Fact]
    public void FixPlayer_Given_a_very_slow_player_When_fixed_Then_the_speed_is_at_least_0_01()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(dex: 2);
        f.Give(ob, Define.RING, o => o.Stats.Exp = -500);

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal((float)0.01, ob.Speed);
    }

    [Fact]
    public void FixPlayer_Given_a_huge_bonus_When_fixed_Then_armour_and_weapon_class_stop_at_120()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        (CfObject cursed, _) = f.Player();
        f.Give(ob, Define.RING, o => { o.Stats.Ac = 100; o.Stats.Wc = 100; });
        f.Give(ob, Define.GIRDLE, o => { o.Stats.Ac = 100; o.Stats.Wc = 100; });
        f.Give(cursed, Define.RING, o => { o.Stats.Ac = -100; o.Stats.Wc = -100; });
        f.Give(cursed, Define.GIRDLE, o => { o.Stats.Ac = -100; o.Stats.Wc = -100; });

        // Act
        FixPlayer.Fix(f.World, ob);
        FixPlayer.Fix(f.World, cursed);

        // Assert
        Assert.Equal(new[] { -120, -120, 120, 120 }, new int[] { ob.Stats.Ac, ob.Stats.Wc, cursed.Stats.Ac, cursed.Stats.Wc });
    }

    [Fact]
    public void FixPlayer_Given_two_armours_and_a_bracer_When_fixed_Then_only_the_best_of_each_counts_and_the_armour_speed_limits_the_player()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(dex: 24);
        f.Give(ob, Define.ARMOUR, o => { o.Stats.Ac = 3; o.Stats.Wc = 1; });
        f.Give(ob, Define.BRACERS, o => { o.Stats.Ac = 5; o.Stats.Wc = 2; o.LastSp = 5; });
        f.Give(ob, Define.FORCE, o => { o.Stats.Ac = 4; o.Stats.Wc = 1; o.Magic = 1; o.LastSp = 8; }, arch: "force");

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert: ac 0 - 5 (the best) - dex_bonus[24] 4; wc -2 (the best) - (level 1 + thaco_bonus[14] 1); speed capped by 5 / 10
        Assert.Equal(-9, ob.Stats.Ac);
        Assert.Equal(-4, ob.Stats.Wc);
        Assert.Equal(0.5f, ob.Speed);
    }

    [Fact]
    public void FixPlayer_Given_a_shield_a_weapon_and_a_heavy_armour_When_fixed_Then_the_encumbrance_is_three_times_the_weapon_and_the_armour_in_kilograms()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        f.Give(ob, Define.SHIELD, o => o.Weight = 6000);
        f.Give(ob, Define.WEAPON, o => o.Weight = 5000);
        f.Give(ob, Define.ARMOUR, o => o.Weight = 20000);
        CfObject skill = f.Give(ob, Define.SKILL, o => o.Weight = 2000);
        ob.ChosenSkill = skill;

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert: 6000/2000 + 3*5000/1000 + 20000/1000 + 3*2000/1000
        Assert.Equal(3 + 15 + 20 + 6, contr.Encumbrance);
    }

    [Fact]
    public void FixPlayer_Given_an_experience_object_for_wisdom_When_fixed_Then_its_stat_bonus_counts_and_the_others_do_not()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Give(ob, Define.EXPERIENCE, o => o.Stats.Cha = 4, arch: "experience_agility");
        f.Give(ob, Define.EXPERIENCE, o => o.Stats.Wis = 4, arch: "experience_wis");

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(7, ob.Stats.Cha);
        Assert.Equal(13, ob.Stats.Wis);
    }

    [Fact]
    public void FixPlayer_Given_a_level_of_an_experience_object_and_a_player_level_1_When_fixed_Then_the_weapon_class_uses_the_player_s_level()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject exp = f.Make("experience_physique");
        exp.Level = 1;
        exp.SetFlag(Define.FLAG_APPLIED);
        f.World.InsertObInOb(exp, ob);

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert: level 1 of the object is not more than 1: wc = -(player level 1 + thaco_bonus[14] 1)
        Assert.Equal(-2, ob.Stats.Wc);
    }

    [Fact]
    public void FixPlayer_Given_a_dex_below_the_table_When_fixed_Then_the_armour_class_gets_the_penalty()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(dex: 3);

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert: dex_bonus[3] is -2; speed_bonus[3] is -0.3
        Assert.Equal(2, ob.Stats.Ac);
        Assert.Equal(0.7f, ob.Speed, 5);
    }

    [Fact]
    public void FixPlayer_Given_a_player_with_the_flag_no_fix_player_set_on_insert_When_fixed_directly_Then_the_numbers_are_still_worked_out()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        ob.SetFlag(Define.FLAG_NO_FIX_PLAYER);
        f.Give(ob, Define.ARMOUR, o => o.Stats.Ac = 4);

        // Act
        FixPlayer.Fix(f.World, ob);

        // Assert
        Assert.Equal(-4, ob.Stats.Ac);
    }

    // ------------------------------------------------------------------ change_abil

    /// <summary>A player and an item that is put in unapplied; the item is then applied (the flag set) as apply_special does before change_abil.</summary>
    private static (CfObject Ob, Player Contr, CfObject Item, Fixture F) Wearing(Action<CfObject> tweak, int type = Define.RING, Action<CfObject>? player = null, bool wasApplied = false)
    {
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        player?.Invoke(ob);
        CfObject item = f.Give(ob, type, tweak, applied: wasApplied);
        FixPlayer.Fix(f.World, ob);
        f.Host.Lines.Clear();
        return (ob, contr, item, f);
    }

    private static int Apply(CfObject ob, CfObject item, Fixture f)
    {
        item.SetFlag(Define.FLAG_APPLIED);
        return LivingChange.ChangeAbil(f.World, ob, item);
    }

    private static int TakeOff(CfObject ob, CfObject item, Fixture f)
    {
        item.ClearFlag(Define.FLAG_APPLIED);
        return LivingChange.ChangeAbil(f.World, ob, item);
    }

    [Fact]
    public void ChangeAbil_Given_an_item_that_changes_nothing_When_applied_Then_the_result_is_0_and_there_is_no_message()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(_ => { });

        // Act
        int result = Apply(ob, item, f);

        // Assert
        Assert.Equal(0, result);
        Assert.Empty(f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_an_item_that_gives_armour_class_When_applied_Then_the_stats_change_and_there_is_no_message()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.Stats.Ac = 3);

        // Act
        int result = Apply(ob, item, f);

        // Assert
        Assert.Equal(0, result);
        Assert.Equal(-3, ob.Stats.Ac);
        Assert.Empty(f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_an_item_with_confusion_When_applied_and_taken_off_Then_the_hands_glow_and_stop_and_the_attack_type_goes()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.Attacktype = Define.AT_CONFUSION);

        // Act
        int on = Apply(ob, item, f);
        uint attack = ob.Attacktype;
        int off = TakeOff(ob, item, f);

        // Assert
        Assert.Equal(new[] { 1, 1 }, new[] { on, off });
        Assert.Equal(new[] { "Your hands begin to glow red.", "Your hands stop glowing red." }, f.Host.Lines);
        Assert.Equal(Define.AT_CONFUSION, attack);
        Assert.Equal(0u, ob.Attacktype);
    }

    [Theory]
    [InlineData(Define.FLAG_LIFESAVE, "You feel very protected.", "You don't feel protected anymore.")]
    [InlineData(Define.FLAG_REFL_MISSILE, "A magic force shimmers around you.", "The magic force fades away.")]
    [InlineData(Define.FLAG_REFL_SPELL, "You feel more safe now, somehow.", "Suddenly you feel less safe, somehow.")]
    [InlineData(Define.FLAG_STEALTH, "You walk more quietly.", "You walk more noisily.")]
    [InlineData(Define.FLAG_MAKE_INVIS, "You become transparent.", "You can see yourself.")]
    [InlineData(Define.FLAG_SEE_IN_DARK, "Your vision is better in the dark.", "You see less well in the dark.")]
    public void ChangeAbil_Given_an_item_with_a_flag_When_applied_and_taken_off_Then_the_two_messages_of_the_flag(int flag, string on, string off)
    {
        // Arrange
        (CfObject ob, Player contr, CfObject item, Fixture f) = Wearing(o => o.SetFlag(flag));

        // Act
        int a = Apply(ob, item, f);
        bool had = ob.QueryFlag(flag);
        int b = TakeOff(ob, item, f);

        // Assert
        Assert.Equal(new[] { 1, 1 }, new[] { a, b });
        Assert.True(had);
        Assert.False(ob.QueryFlag(flag));
        Assert.Equal(flag == Define.FLAG_SEE_IN_DARK ? new[] { on, off } : new[] { on, off }, f.Host.Lines);
        Assert.Equal(flag == Define.FLAG_SEE_IN_DARK, contr.DoLos);
    }

    [Fact]
    public void ChangeAbil_Given_a_flag_the_player_has_from_another_item_When_applied_Then_there_is_no_message()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.SetFlag(Define.FLAG_STEALTH));
        f.Give(ob, Define.CLOAK, o => o.SetFlag(Define.FLAG_STEALTH));
        FixPlayer.Fix(f.World, ob);

        // Act
        int result = Apply(ob, item, f);

        // Assert
        Assert.Equal(0, result);
        Assert.Empty(f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_a_flying_item_When_applied_and_taken_off_Then_the_player_floats_up_and_down_and_is_slowed_to_1()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.SetFlag(Define.FLAG_FLYING), player: o => o.Stats.Dex = 20);
        ob.Speed = 3;

        // Act
        int on = Apply(ob, item, f);
        float speed = ob.Speed;
        int off = TakeOff(ob, item, f);

        // Assert
        Assert.Equal(new[] { 1, 1 }, new[] { on, off });
        Assert.Equal(new[] { "You start to float in the air!.", "You float down to the ground." }, f.Host.Lines);
        Assert.Equal(1f, speed);
        Assert.False(ob.QueryFlag(Define.FLAG_FLYING));
    }

    [Fact]
    public void ChangeAbil_Given_a_player_that_flies_already_When_a_flying_item_is_applied_and_taken_off_Then_it_floats_higher_and_lower()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.SetFlag(Define.FLAG_FLYING));
        f.Give(ob, Define.BOOTS, o => o.SetFlag(Define.FLAG_FLYING));
        FixPlayer.Fix(f.World, ob);

        // Act
        Apply(ob, item, f);
        TakeOff(ob, item, f);

        // Assert
        Assert.Equal(new[] { "You float a little higher in the air.", "You float a little lower in the air." }, f.Host.Lines);
        Assert.True(ob.QueryFlag(Define.FLAG_FLYING));
    }

    [Fact]
    public void ChangeAbil_Given_an_undead_item_When_applied_and_taken_off_Then_the_race_becomes_undead_and_comes_back()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.SetFlag(Define.FLAG_UNDEAD), player: o => o.Race = "dwarf");
        ob.Arch!.Clone.Race = "dwarf";

        // Act
        Apply(ob, item, f);
        string? with = ob.Race;
        TakeOff(ob, item, f);

        // Assert
        Assert.Equal("undead", with);
        Assert.Equal("dwarf", ob.Race);
        Assert.Equal(new[] { "Your lifeforce drains away!", "Your lifeforce returns!" }, f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_a_player_that_is_undead_by_birth_When_an_undead_item_is_applied_Then_there_is_no_message_and_the_race_stays()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.SetFlag(Define.FLAG_UNDEAD), player: o => o.Race = "ghoul");
        ob.Arch!.Clone.SetFlag(Define.FLAG_UNDEAD);

        // Act
        int result = Apply(ob, item, f);

        // Assert
        Assert.Equal(0, result);
        Assert.Equal("ghoul", ob.Race);
        Assert.Empty(f.Host.Lines);
    }

    [Theory]
    [InlineData(false, "You are blinded.", "Your vision returns.", true, false)]
    [InlineData(true, "Your mortal self is blinded.", "Your mortal self can now see again.", true, false)]
    public void ChangeAbil_Given_a_blinding_item_When_applied_and_taken_off_Then_a_player_is_blinded_and_sees_again_and_a_wizard_only_hears_of_it(bool wizard, string on, string off, bool blind, bool blindAfter)
    {
        // Arrange
        (CfObject ob, Player contr, CfObject item, Fixture f) = Wearing(o => o.SetFlag(Define.FLAG_BLIND), player: o =>
        {
            if (wizard)
            {
                o.SetFlag(Define.FLAG_WIZ);
            }
        });

        // Act
        Apply(ob, item, f);
        bool blindOn = ob.QueryFlag(Define.FLAG_BLIND);
        bool los = contr.DoLos;
        TakeOff(ob, item, f);

        // Assert
        Assert.Equal(new[] { on, off }, f.Host.Lines);
        Assert.Equal(blind, blindOn);
        Assert.Equal(blindAfter, ob.QueryFlag(Define.FLAG_BLIND));
        Assert.Equal(!wizard, los);
    }

    [Fact]
    public void ChangeAbil_Given_an_item_with_xray_vision_When_applied_and_taken_off_Then_a_player_sees_through_and_sees_solid_again_and_the_line_of_sight_is_asked_for()
    {
        // Arrange
        (CfObject ob, Player contr, CfObject item, Fixture f) = Wearing(o => o.SetFlag(Define.FLAG_XRAYS));

        // Act
        Apply(ob, item, f);
        bool los = contr.DoLos;
        contr.DoLos = false;
        TakeOff(ob, item, f);

        // Assert
        Assert.Equal(new[] { "Everything becomes transparent.", "Everything suddenly looks very solid." }, f.Host.Lines);
        Assert.True(los);
        Assert.True(contr.DoLos);
    }

    [Fact]
    public void ChangeAbil_Given_an_item_with_xray_vision_and_a_wizard_When_applied_and_taken_off_Then_the_wizard_hears_it_is_clearer_and_keeps_the_flag_silently()
    {
        // Arrange
        (CfObject ob, Player contr, CfObject item, Fixture f) = Wearing(o => o.SetFlag(Define.FLAG_XRAYS), player: o => o.SetFlag(Define.FLAG_WIZ));

        // Act
        Apply(ob, item, f);
        TakeOff(ob, item, f);

        // Assert: fix_player does not reset the flag of a wizard, so there is no change to tell of when the item goes
        Assert.Equal(new[] { "Your vision becomes a little clearer." }, f.Host.Lines);
        Assert.False(contr.DoLos);
        Assert.True(ob.QueryFlag(Define.FLAG_XRAYS));
    }

    [Fact]
    public void ChangeAbil_Given_items_with_luck_hit_points_spell_points_grace_and_food_When_applied_and_taken_off_Then_each_has_its_message_for_both_signs()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o =>
        {
            o.Stats.Luck = 2;
            o.Stats.Hp = 1;
            o.Stats.Sp = -1;
            o.Stats.Grace = 3;
            o.Stats.Food = -2;
        });

        // Act
        int on = Apply(ob, item, f);
        List<string> first = f.Host.Lines.ToList();
        f.Host.Lines.Clear();
        int off = TakeOff(ob, item, f);

        // Assert
        Assert.Equal(new[] { 1, 1 }, new[] { on, off });
        Assert.Equal(new[]
        {
            "You feel more lucky.", "You feel much more healthy!", "You suddenly feel very mundane.", "You feel closer to your god!",
            "You feel your digestion speeding up.",
        }, first);
        Assert.Equal(new[]
        {
            "You feel less lucky.", "You feel much less healthy!", "You feel one with the powers of magic!", "You suddenly feel less holy.",
            "You feel your digestion slowing down.",
        }, f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_a_positive_food_value_When_applied_Then_the_digestion_slows_down()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.Stats.Food = 4);

        // Act
        Apply(ob, item, f);

        // Assert
        Assert.Equal(new[] { "You feel your digestion slowing down." }, f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_a_skill_with_spell_points_When_applied_Then_the_spell_point_message_is_not_given()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.Stats.Sp = 29, Define.SKILL);

        // Act
        int result = Apply(ob, item, f);

        // Assert
        Assert.Equal(0, result);
        Assert.Empty(f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_a_resistance_that_changes_When_applied_and_taken_off_Then_the_message_names_it_and_the_new_value_and_armour_is_not_told()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o =>
        {
            o.Resist[Define.ATNR_PHYSICAL] = 30;
            o.Resist[2] = 40;
            o.Resist[4] = -20;
        });

        // Act
        Apply(ob, item, f);
        List<string> first = f.Host.Lines.ToList();
        f.Host.Lines.Clear();
        TakeOff(ob, item, f);

        // Assert
        Assert.Equal(new[] { "Your resistance to fire rises to 40%.", "Your resistance to cold drops to -20%." }, first);
        Assert.Equal(new[] { "Your resistance to fire drops to 0%.", "Your resistance to cold rises to 0%." }, f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_an_item_with_stat_bonuses_When_applied_and_taken_off_Then_each_stat_has_its_gain_or_lose_message()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o =>
        {
            o.Stats.Str = 2;
            o.Stats.Dex = -1;
            o.Stats.Con = 1;
            o.Stats.Wis = 1;
            o.Stats.Cha = 1;
            o.Stats.Int = -1;
            o.Stats.Pow = 1;
        });

        // Act
        Apply(ob, item, f);
        List<string> first = f.Host.Lines.ToList();
        f.Host.Lines.Clear();
        TakeOff(ob, item, f);

        // Assert
        Assert.Equal(new[]
        {
            "You feel stronger.", "You feel clumsy!", "You feel healthy.", "You feel wiser.", "You seem to look better.", "You feel stupid!",
            "You feel more potent.",
        }, first);
        Assert.Equal(new[]
        {
            "You feel weaker!", "You feel more agile.", "You feel less healthy!", "You lose some of your memory!", "You look ugly!",
            "You feel smarter.", "You feel less potent!",
        }, f.Host.Lines);
        Assert.Equal(14, ob.Stats.Str);
    }

    [Fact]
    public void ChangeAbil_Given_an_experience_object_with_stats_When_applied_Then_the_stats_get_no_message()
    {
        // Arrange
        (CfObject ob, _, CfObject item, Fixture f) = Wearing(o => o.Stats.Str = 3, Define.EXPERIENCE);

        // Act
        int result = Apply(ob, item, f);

        // Assert
        Assert.Equal(0, result);
        Assert.Empty(f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_a_potion_When_applied_Then_the_rolled_stats_and_the_stats_grow_and_the_extra_of_a_super_potion_is_spent()
    {
        // Arrange
        (CfObject ob, Player contr, CfObject potion, Fixture f) = Wearing(o => { o.Stats.Str = 2; o.Stats.Dex = 1; o.Stats.Sp = 3; }, Define.POTION);
        ob.Arch!.Clone.Stats.Str = 14;
        ob.Arch.Clone.Stats.Dex = 12;

        // Act
        int result = Apply(ob, potion, f);

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(new[] { 16, 13 }, new[] { (int)contr.OrigStats.Str, (int)contr.OrigStats.Dex });
        Assert.Equal(new[] { 16, 13 }, new[] { (int)ob.Stats.Str, (int)ob.Stats.Dex });
        Assert.Equal(0, potion.Stats.Sp);
        Assert.Equal("16 13 10 9 7 8 11", ob.Extra!.Get(Player.OrigStatsKey));
        Assert.Equal(new[] { "You feel stronger.", "You feel more agile." }, f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_a_potion_that_is_cursed_When_the_effect_ends_Then_the_rolled_stats_go_back()
    {
        // Arrange
        (CfObject ob, Player contr, CfObject potion, Fixture f) = Wearing(o => o.Stats.Str = 2, Define.POTION);
        Apply(ob, potion, f);
        f.Host.Lines.Clear();

        // Act
        TakeOff(ob, potion, f);

        // Assert
        Assert.Equal(14, contr.OrigStats.Str);
        Assert.Equal(14, ob.Stats.Str);
        Assert.Equal(new[] { "You feel weaker!" }, f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_a_potion_that_would_go_past_the_natural_maximum_When_applied_Then_it_is_useless_and_says_nothing_about_the_stat()
    {
        // Arrange: the archetype's Str is 14, the player has 33 rolled: 33 + 2 is more than 20 + 14
        (CfObject ob, Player contr, CfObject potion, Fixture f) = Wearing(o => o.Stats.Str = 2, Define.POTION);
        contr.OrigStats.Str = 33;
        ob.Arch!.Clone.Stats.Str = 14;

        // Act
        int result = Apply(ob, potion, f);

        // Assert
        Assert.Equal(33, contr.OrigStats.Str);
        Assert.Equal(0, result);
        Assert.Empty(f.Host.Lines);
        Assert.Equal(2, potion.Stats.Str);
    }

    [Fact]
    public void ChangeAbil_Given_a_potion_for_a_stat_that_is_zero_When_applied_Then_it_is_useless()
    {
        // Arrange
        (CfObject ob, Player contr, CfObject potion, Fixture f) = Wearing(o => o.Stats.Str = 1, Define.POTION);
        contr.OrigStats.Str = 0;

        // Act
        Apply(ob, potion, f);

        // Assert
        Assert.Equal(0, contr.OrigStats.Str);
        Assert.Empty(f.Host.Lines);
    }

    [Fact]
    public void ChangeAbil_Given_an_item_applied_to_something_that_is_not_a_player_When_changed_Then_nothing_of_a_player_happens()
    {
        // Arrange
        var f = new Fixture();
        CfObject monster = f.Make("sword");
        CfObject item = f.Make("sword");
        item.Type = Define.RING;
        item.Stats.Hp = 5;
        item.SetFlag(Define.FLAG_APPLIED);

        // Act
        int result = LivingChange.ChangeAbil(f.World, monster, item);

        // Assert
        Assert.Equal(0, result);
        Assert.Empty(f.Host.Lines);
    }

    // ------------------------------------------------------------------ add_statbonus and friends

    [Fact]
    public void AddStatbonus_Given_a_player_When_added_and_removed_Then_the_archetype_s_stats_go_into_the_stats_and_the_rolled_stats_and_come_out_again()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        ob.Arch!.Clone.Stats.Str = 2;
        ob.Arch.Clone.Stats.Dex = -1;
        ob.Arch.Clone.Stats.Con = 3;
        ob.Arch.Clone.Stats.Wis = 1;
        ob.Arch.Clone.Stats.Pow = 4;
        ob.Arch.Clone.Stats.Cha = 5;
        ob.Arch.Clone.Stats.Int = 6;

        // Act
        LivingChange.AddStatbonus(ob);
        int[] stats = { ob.Stats.Str, ob.Stats.Dex, ob.Stats.Con, ob.Stats.Wis, ob.Stats.Pow, ob.Stats.Cha, ob.Stats.Int };
        int[] rolled = { contr.OrigStats.Str, contr.OrigStats.Dex, contr.OrigStats.Con, contr.OrigStats.Wis, contr.OrigStats.Pow, contr.OrigStats.Cha, contr.OrigStats.Int };
        string kept = ob.Extra!.Get(Player.OrigStatsKey)!;
        LivingChange.RemoveStatbonus(ob);

        // Assert
        Assert.Equal(new[] { 16, 11, 13, 10, 15, 12, 14 }, stats);
        Assert.Equal(stats, rolled);
        Assert.Equal("16 11 13 10 12 14 15", kept);
        Assert.Equal(new[] { 14, 12, 10, 9, 11, 7, 8 }, new int[] { ob.Stats.Str, ob.Stats.Dex, ob.Stats.Con, ob.Stats.Wis, ob.Stats.Pow, ob.Stats.Cha, ob.Stats.Int });
        Assert.Equal(14, contr.OrigStats.Str);
    }

    [Fact]
    public void AddStatbonus_Given_something_that_is_not_a_player_When_added_Then_nothing_happens()
    {
        // Arrange
        var f = new Fixture();
        CfObject ob = f.Make("dwarf_player");
        ob.Arch!.Clone.Stats.Str = 2;

        // Act
        LivingChange.AddStatbonus(ob);
        LivingChange.RemoveStatbonus(ob);

        // Assert
        Assert.Equal(14, ob.Stats.Str);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void AllowedClass_Given_a_stat_of_0_or_1_When_asked_Then_only_all_stats_of_1_or_more_are_allowed(int low, bool allowed)
    {
        // Arrange
        var f = new Fixture();
        foreach (Action<Living, sbyte> set in new Action<Living, sbyte>[]
        {
            (l, v) => l.Str = v, (l, v) => l.Dex = v, (l, v) => l.Con = v, (l, v) => l.Int = v, (l, v) => l.Wis = v, (l, v) => l.Pow = v, (l, v) => l.Cha = v,
        })
        {
            CfObject ob = f.Make("dwarf_player");
            set(ob.Stats, (sbyte)low);

            // Act / Assert
            Assert.Equal(allowed, LivingChange.AllowedClass(ob));
        }
    }

    [Fact]
    public void ChangeAttr_Given_a_value_that_wraps_or_an_unknown_stat_When_changed_Then_it_wraps_as_a_signed_char_and_the_unknown_stat_is_left()
    {
        // Arrange
        var stats = new Living { Str = 120 };

        // Act
        LivingChange.ChangeAttr(stats, Define.STR, 10);
        LivingChange.ChangeAttr(stats, 9, 5);
        LivingChange.ChangeAttr(stats, Define.DEX, 0);
        LivingChange.ChangeAttr(stats, Define.DEX, 300); // a signed char parameter: 300 is 44

        // Assert
        Assert.Equal(-126, stats.Str);
        Assert.Equal(44, stats.Dex);
    }

    // ------------------------------------------------------------------ check_skill_to_apply

    [Theory]
    [InlineData(Define.WEAPON, Define.SK_MELEE_WEAPON, SkillTable.RangeSkill)]
    [InlineData(Define.BOW, Define.SK_MISSILE_WEAPON, SkillTable.RangeBow)]
    [InlineData(Define.POTION, Define.SK_USE_MAGIC_ITEM, SkillTable.RangeScroll)]
    [InlineData(Define.SCROLL, Define.SK_USE_MAGIC_ITEM, SkillTable.RangeScroll)]
    [InlineData(Define.ROD, Define.SK_USE_MAGIC_ITEM, SkillTable.RangeRod)]
    [InlineData(Define.WAND, Define.SK_USE_MAGIC_ITEM, SkillTable.RangeWand)]
    [InlineData(Define.HORN, Define.SK_USE_MAGIC_ITEM, SkillTable.RangeHorn)]
    public void CheckSkillToApply_Given_an_item_that_needs_a_skill_the_player_has_When_applied_Then_the_skill_is_readied_and_the_shoot_type_set(int type, int skill, int shoot)
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        CfObject sk = f.Give(ob, Define.SKILL, o => o.Stats.Sp = (short)skill, applied: false);
        f.Skills.ApplySpecial = (who, item, flag) =>
        {
            Assert.Equal(1, flag);
            who.ChosenSkill = item;
            return 0;
        };
        CfObject thing = f.Give(ob, type);

        // Act
        int result = f.Skills.CheckSkillToApply(f.World, ob, thing);

        // Assert
        Assert.Equal(1, result);
        Assert.Same(sk, ob.ChosenSkill);
        Assert.Equal(shoot, contr.ShootType);
    }

    [Fact]
    public void CheckSkillToApply_Given_the_skill_is_the_chosen_one_already_When_applied_Then_no_apply_is_needed_and_the_shoot_type_is_set()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        ob.ChosenSkill = f.Give(ob, Define.SKILL, o => o.Stats.Sp = Define.SK_MELEE_WEAPON, applied: false);
        CfObject sword = f.Give(ob, Define.WEAPON);

        // Act
        int result = f.Skills.CheckSkillToApply(f.World, ob, sword);

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(SkillTable.RangeSkill, contr.ShootType);
    }

    [Fact]
    public void CheckSkillToApply_Given_a_player_without_the_skill_When_applied_Then_it_is_refused_with_the_two_messages_and_the_flag_is_cleared()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject sword = f.Give(ob, Define.WEAPON);

        // Act
        int result = f.Skills.CheckSkillToApply(f.World, ob, sword);

        // Assert
        Assert.Equal(0, result);
        Assert.False(sword.QueryFlag(Define.FLAG_APPLIED));
        Assert.Equal(new[] { "You have no knowledge of " + f.Skills.Skills[Define.SK_MELEE_WEAPON].Name + ".", "You can't use that!" }, f.Host.Lines);
    }

    [Fact]
    public void CheckSkillToApply_Given_a_skill_that_cannot_be_applied_When_applied_Then_it_is_refused()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Give(ob, Define.SKILL, o => o.Stats.Sp = Define.SK_MELEE_WEAPON, applied: false);
        f.Skills.ApplySpecial = (_, _, _) => 1;
        CfObject sword = f.Give(ob, Define.WEAPON);

        // Act
        int result = f.Skills.CheckSkillToApply(f.World, ob, sword);

        // Assert
        Assert.Equal(0, result);
        Assert.False(sword.QueryFlag(Define.FLAG_APPLIED));
        Assert.Equal(new[] { "You can't use that!" }, f.Host.Lines);
    }

    [Fact]
    public void CheckSkillToApply_Given_no_apply_special_yet_When_a_skill_has_to_be_readied_Then_it_cannot_be_and_the_item_is_refused()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        f.Give(ob, Define.SKILL, o => o.Stats.Sp = Define.SK_MELEE_WEAPON, applied: false);
        CfObject sword = f.Give(ob, Define.WEAPON);

        // Act
        int result = f.Skills.CheckSkillToApply(f.World, ob, sword);

        // Assert
        Assert.Equal(0, result);
        Assert.Equal(new[] { "You can't use that!" }, f.Host.Lines);
    }

    [Fact]
    public void CheckSkillToApply_Given_a_weapon_that_is_taken_off_When_checked_Then_the_skill_is_unreadied_only_when_it_is_the_chosen_one()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject melee = f.Give(ob, Define.SKILL, o => o.Stats.Sp = Define.SK_MELEE_WEAPON, applied: false);
        CfObject sword = f.Give(ob, Define.WEAPON, applied: false);
        var calls = new List<(CfObject Item, int Flag)>();
        f.Skills.ApplySpecial = (who, item, flag) =>
        {
            calls.Add((item, flag));
            who.ChosenSkill = null;
            return 0;
        };
        ob.ChosenSkill = f.Give(ob, Define.SKILL, o => o.Stats.Sp = Define.SK_USE_MAGIC_ITEM, applied: false, arch: "skill_use_magic");

        // Act
        int other = f.Skills.CheckSkillToApply(f.World, ob, sword);
        int callsAfterOther = calls.Count;
        ob.ChosenSkill = melee;
        int same = f.Skills.CheckSkillToApply(f.World, ob, sword);

        // Assert
        Assert.Equal(new[] { 0, 0 }, new[] { other, same });
        Assert.Equal(0, callsAfterOther);
        Assert.Equal(new[] { (melee, 2) }, calls);
    }

    [Fact]
    public void CheckSkillToApply_Given_an_item_that_needs_no_skill_or_a_monster_When_checked_Then_0_and_1()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject ring = f.Give(ob, Define.RING);
        CfObject monster = f.Make("sword");

        // Act
        int ringResult = f.Skills.CheckSkillToApply(f.World, ob, ring);
        int monsterResult = f.Skills.CheckSkillToApply(f.World, monster, ring);

        // Assert
        Assert.Equal(0, ringResult);
        Assert.Equal(1, monsterResult);
    }

    [Fact]
    public void ChangeSkill_Given_minus_1_and_a_chosen_skill_When_changed_Then_the_chosen_skill_is_taken_off_without_a_message()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject melee = f.Give(ob, Define.SKILL, o => o.Stats.Sp = Define.SK_MELEE_WEAPON, applied: false);
        ob.ChosenSkill = melee;
        var flags = new List<int>();
        f.Skills.ApplySpecial = (_, _, flag) =>
        {
            flags.Add(flag);
            return 0;
        };

        // Act
        int result = f.Skills.ChangeSkill(f.World, ob, -1);

        // Assert
        Assert.Equal(0, result);
        Assert.Equal(new[] { 2 }, flags);
        Assert.Empty(f.Host.Lines);
    }

    // ------------------------------------------------------------------ the host

    [Fact]
    public void GkHost_Given_a_controller_that_is_not_a_player_of_the_extension_When_asked_for_its_regeneration_Then_0()
    {
        // Arrange
        var host = new GkHost();
        var contr = new Player(new PlayerState("bob", new CfObject()), _ => { });
        contr.Digestion = 3;
        contr.GenHp = 4;
        contr.GenSp = 5;
        contr.GenGrace = 6;

        // Act / Assert
        Assert.Equal(new[] { 3, 4, 5, 6 }, new[] { host.PlayerDigestion(contr), host.PlayerGenHp(contr), host.PlayerGenSp(contr), host.PlayerGenGrace(contr) });
        Assert.Equal(new[] { 0, 0, 0, 0 }, new[] { host.PlayerDigestion(new Other()), host.PlayerGenHp(new Other()), host.PlayerGenSp(new Other()), host.PlayerGenGrace(new Other()) });
    }

    private sealed class Other : IPlayerHandle
    {
    }
}

/// <summary>The strings mud_ac, mud_wc, mud_dam and mud_speed of a player that wears a sword (docs/mud-phase3.md section 5), through the whole extension.</summary>
public sealed class LivingStringsTests
{
    private const int OpWear = 7101;

    private const string Header =
        "mudorigstats 14 12 10 9 7 8 11\nmudlevhp 8 8 8 8 8 8 8 8 8 8\nmudlevsp 3 3 3 3 3 3 3 3 3 3\nmudlevgrace 2 2 2 2 2 2 2 2 2 2\nlevel 1\n";

    private static ExtensionOptions Options() => new()
    {
        Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = b => b.AddFunction(Lane.Wave5D, new FunctionDef(OpWear, "t5dwear", new[] { "string", "bool", "int" }, "any", Handler: Wear)),
    };

    // what apply_special of wave 6 will do for a weapon: the item enters the tree, its flag is set, change_abil, esrv_update_item
    private static Outcome Wear(Dispatcher d, IReadOnlyList<Val> a)
    {
        CfWorld w = d.WorldOrNull!;
        Player p = d.ControllerOf(a[0].AsString)!;
        CfObject pl = p.State.Ob;
        CfObject item = d.NewItem(w.FindArchetype("sword")!);
        item.Magic = (sbyte)a[2].AsInt;
        w.InsertObInOb(item, pl);
        item.SetFlag(Define.FLAG_APPLIED);
        if (a[1].AsBool)
        {
            LivingChange.ChangeAbil(w, pl, item);
        }
        w.Host.EsrvUpdateItem(1, pl, item);
        return Outcome.Ok(Val.Nil);
    }

    private static DirectExt Start(string account)
    {
        TempFolder world = ItemWorld.MakeWorld();
        ItemWorld.Account(world, account, Header, "");
        return new DirectExt(world, Options(), 1, true, 16_777_216, 1);
    }

    private static ReplyFrame Login(DirectExt ext, string account) => ext.Call(5, new[] { S(account) }, player: account);

    private static ReplyFrame Do(DirectExt ext, string account, bool change, int magic = 0) =>
        ext.Call(OpWear, new[] { S(account), Val.Bool(change), Val.Int(magic) }, player: account);

    private static Dictionary<string, string> Strings(ReplyFrame r) =>
        Effects(r, "client.set").ToDictionary(e => e.Get("name")!.AsString, e => e.Get("value")!.AsString);

    [Fact]
    public void Strings_Given_a_player_that_wields_a_sword_When_the_request_ends_Then_weapon_class_and_damage_are_written_and_what_did_not_change_is_not()
    {
        // Arrange
        using DirectExt ext = Start("bob");
        ulong h = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame r = Do(ext, "bob", change: true, magic: 1);

        // Assert: wc = -(2 + 1 magic) - (level 1 + thaco_bonus[14] 1) = -5, dam = 8 + 1 magic + dam_bonus[14] 1 = 10
        Dictionary<string, string> sets = Strings(r);
        Assert.Equal("-5", sets["clientr.mud_wc"]);
        Assert.Equal("10", sets["clientr.mud_dam"]);
        Assert.Equal(Get(ext, h, Ply("mudwc")).AsInt.ToString(), sets["clientr.mud_wc"]);
        Assert.DoesNotContain("clientr.mud_ac", sets.Keys);
        Assert.DoesNotContain("clientr.mud_speed", sets.Keys);
        Assert.Equal("14", sets["clientr.mud_str"]); // the first fix of the player brings the rolled stats in
    }

    [Fact]
    public void Strings_Given_a_change_that_is_not_followed_by_a_fix_When_the_request_ends_Then_the_strings_stay_as_they_were()
    {
        // Arrange: the insert of the item fixes the player, but the flag is set after it and change_abil is not called
        using DirectExt ext = Start("bob");
        Login(ext, "bob");

        // Act
        ReplyFrame r = Do(ext, "bob", change: false);

        // Assert: damage is 0 + dam_bonus[14] 1, without the sword
        Assert.Equal("1", Strings(r)["clientr.mud_dam"]);
    }

    [Fact]
    public void Strings_Given_a_request_that_changes_nothing_When_it_ends_Then_no_string_is_written_again()
    {
        // Arrange
        using DirectExt ext = Start("bob");
        ulong h = Login(ext, "bob").Result.AsHandle;
        Do(ext, "bob", change: true);

        // Act
        ReplyFrame r = Set(ext, h, Ply("mudlevel"), Val.Int(1));

        // Assert
        Assert.DoesNotContain("clientr.mud_wc", Strings(r).Keys);
        Assert.DoesNotContain("clientr.mud_dam", Strings(r).Keys);
    }
}
