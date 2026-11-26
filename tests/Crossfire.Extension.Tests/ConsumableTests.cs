// Not ported from a Crossfire file: tests of lane 6C of phase 3, the consumables of server/apply.c (apply_food, eat_special_food,
// apply_potion, apply_poison, dragon_eat_flesh; docs/mud-phase3.md sections 4.4 and 8).
//
// The golden values come from golden/consumable_golden.c: the arithmetic of apply.c with C's declarations, compiled with gcc (the file's
// header says how to run it); the numbers below are its output lines.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>The host of these tests: the lines of text the player is shown, and the calls of fix_player counted (the real fix is LivingTests'; here it would work out the hit points again).</summary>
internal sealed class ConsumableHost : CrossfireHost
{
    public List<string> Lines { get; } = new();

    public int Fixes { get; private set; }

    public override void FixPlayer(CfObject player) => Fixes++;

    public override void DrawInfo(CfObject who, string text) => Lines.Add(text);

    public override int PlayerDigestion(IPlayerHandle contr) => 0;

    public override int PlayerGenHp(IPlayerHandle contr) => 0;
}

public sealed class ConsumableTests
{
    private const string Arc =
        StartWorld.Arc
        + "Object dragon_player\nname dragon\ntype 1\nalive 1\nrace dragon\nhp 10\nmaxhp 12\nlevel 1\nStr 14\nDex 12\nCon 10\nInt 8\nWis 9\nPow 11\nCha 7\nweight 70000\nfood 999\nspeed 1\nend\n"
        + "Object dragon_skin_force\nname dragon skin\ntype 114\ninvisible 1\nend\n"
        + "Object dragon_ability_force\nname dragon ability\ntype 114\ninvisible 1\nend\n"
        + "Object force\nname force\ntype 114\nend\n"
        + "Object apple\nname apple\nname_pl apples\ntype 6\nfood 100\nweight 10\nend\n"
        + "Object ale\nname ale\nname_pl ales\ntype 54\nfood 60\nweight 10\nend\n"
        + "Object meat\nname meat\nname_pl meats\ntype 72\nfood 100\nweight 10\nend\n"
        + "Object bad_booze\nname bad booze\ntype 7\nfood 0\nweight 10\nend\n"
        + "Object potion_str\nname potion of strength\ntype 5\nStr 1\nweight 10\nvalue 5\nend\n"
        + "Object potion_heal\nname potion of healing\ntype 5\nsp 5\nweight 10\nend\n"
        + "Object potion_cold\nname potion of cold protection\ntype 5\nresist_cold 30\nweight 10\nend\n"
        + "Object potion_rest\nname potion of restoration\ntype 5\nattacktype 65536\nweight 10\nend\n"
        + "Object potion_improve\nname potion of improvement\ntype 5\nattacktype 1048576\nweight 10\nend\n"
        + "Object potion_plain\nname potion of nothing\ntype 5\nweight 10\nend\n"
        + "Object depletion\nname depletion\ntype 114\nStr -2\nCon -1\nend\n"
        + "Object sword\nname sword\ntype 15\nweight 3000\nend\n";

    private sealed class Fixture
    {
        public ConsumableHost Host { get; } = new();

        public CfWorld World { get; }

        public Queue<int> Dice { get; } = new();

        public Fixture(string arc = Arc)
        {
            World = new CfWorld(Host, (_, _) => { });
            World.InitArchetypes("Object empty_archetype\nend\n" + arc);
            World.Random = () => Dice.Count > 0 ? Dice.Dequeue() : 0;
        }

        public CfObject Make(string arch) => World.ArchToObject(World.FindArchetype(arch))!;

        public (CfObject Ob, Player Contr) Player(string arch = "dwarf_player", int level = 1, int food = 500, int hp = 10, int maxhp = 20)
        {
            CfObject ob = Make(arch);
            ob.Name = "bob";
            ob.Level = (short)level;
            ob.Stats.Food = (short)food;
            ob.Stats.Hp = (short)hp;
            ob.Stats.Maxhp = (short)maxhp;
            var contr = new Player(new PlayerState("bob", ob), _ => { });
            ob.Contr = contr;
            contr.OrigStats.CopyFrom(ob.Stats);
            contr.StoreStats();
            return (ob, contr);
        }

        public CfObject Give(CfObject to, string arch, Action<CfObject>? tweak = null, uint nrof = 1)
        {
            CfObject o = Make(arch);
            o.Nrof = nrof;
            tweak?.Invoke(o);
            return World.InsertObInOb(o, to);
        }
    }

    private static bool Held(CfObject owner, CfObject item) => CfWorld.IsPlayerInv(item.Env) == owner && item.Nrof > 0;

    // ------------------------------------------------------------------ apply_food: the numbers of golden/consumable_golden.c

    [Theory]
    [InlineData(500, 10, 100, 600, 12)] // food normal 600 12
    [InlineData(950, 10, 200, 999, 10)] // food overfull 999 10: capacity_remaining 49 < 200 adds 0
    [InlineData(900, 19, 400, 999, 20)] // food full_hp_clamp 999 20
    [InlineData(0, 18, 600, 600, 20)] // food hp_cap 600 20
    public void ApplyFood_Given_a_player_that_eats_When_the_numbers_are_those_of_C_Then_food_and_hit_points_are_the_golden_ones(int food, int hp, int meal, int wantFood, int wantHp)
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(food: food, hp: hp);
        CfObject apple = f.Give(ob, "apple", o => o.Stats.Food = (short)meal);

        // Act
        Consumable.ApplyFood(f.World, ob, apple);

        // Assert
        Assert.Equal(new[] { wantFood, wantHp }, new[] { (int)ob.Stats.Food, (int)ob.Stats.Hp });
        Assert.False(Held(ob, apple));
    }

    [Fact]
    public void ApplyFood_Given_a_full_stomach_When_the_player_eats_Then_he_is_told_the_food_is_wasted_before_the_taste()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(food: 950);
        CfObject apple = f.Give(ob, "apple", o => o.Stats.Food = 200);

        // Act
        Consumable.ApplyFood(f.World, ob, apple);

        // Assert
        Assert.Equal(new[] { "You feel full, but what a waste of food!", "The apple tasted good." }, f.Host.Lines);
    }

    [Fact]
    public void ApplyFood_Given_a_drink_When_the_player_drinks_Then_the_messages_are_those_of_a_drink_and_the_waste_is_told_as_one()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(food: 980);
        CfObject ale = f.Give(ob, "ale");

        // Act
        Consumable.ApplyFood(f.World, ob, ale);

        // Assert
        Assert.Equal(new[] { "Most of the drink goes down your face not your throat!", "Ahhh...that ale tasted good." }, f.Host.Lines);
        Assert.Equal(999, ob.Stats.Food);
    }

    [Fact]
    public void ApplyFood_Given_flesh_When_a_normal_player_eats_it_Then_it_tastes_terrible_and_feeds_him()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject meat = f.Give(ob, "meat");

        // Act
        Consumable.ApplyFood(f.World, ob, meat);

        // Assert
        Assert.Equal(new[] { "The meat tasted terrible!" }, f.Host.Lines);
        Assert.Equal(600, ob.Stats.Food);
    }

    [Fact]
    public void ApplyFood_Given_a_stack_of_apples_When_one_is_eaten_Then_the_others_stay()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject apples = f.Give(ob, "apple", nrof: 3);

        // Act
        Consumable.ApplyFood(f.World, ob, apples);

        // Assert
        Assert.True(Held(ob, apples));
        Assert.Equal(2u, apples.Nrof);
    }

    [Fact]
    public void ApplyFood_Given_a_cursed_apple_When_eaten_Then_it_does_not_feed_and_the_special_food_runs()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(food: 500, hp: 10);
        CfObject apple = f.Give(ob, "apple", o =>
        {
            o.SetFlag(Define.FLAG_CURSED);
            o.Stats.Sp = 6;
        });
        ob.Stats.Sp = 3;

        // Act
        Consumable.ApplyFood(f.World, ob, apple);

        // Assert: special drain 5 0 (sp 3 - 6 below 0 stops at 0); no food; no normal message
        Assert.Equal(new[] { "You are drained of mana!" }, f.Host.Lines);
        Assert.Equal(new[] { 500, 10, 0 }, new[] { (int)ob.Stats.Food, (int)ob.Stats.Hp, (int)ob.Stats.Sp });
        Assert.False(Held(ob, apple));
    }

    // ------------------------------------------------------------------ eat_special_food

    [Theory]
    [InlineData(7, 0, false, 12, 3, "You begin to feel better.")] // special heal 12 3
    [InlineData(-4, 0, false, 1, 3, "Eck!...that was poisonous!")] // special poisonous 1 3
    [InlineData(0, 6, false, 5, 9, "You feel a rush of magical energy!")] // special mana 5 9
    [InlineData(0, 6, true, 5, 0, "You are drained of mana!")] // special drain 5 0
    [InlineData(9, 0, true, 5, 3, "Eck!...that was poisonous!")] // special cursed_hp 5 3: the poison hit does no damage
    public void EatSpecialFood_Given_a_food_that_changes_hit_points_or_spell_points_When_eaten_Then_the_numbers_are_the_golden_ones(int fhp, int fsp, bool cursed, int wantHp, int wantSp, string line)
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(hp: 5);
        ob.Stats.Sp = 3;
        CfObject food = f.Make("apple");
        food.Stats.Hp = (short)fhp;
        food.Stats.Sp = (short)fsp;
        if (cursed)
        {
            food.SetFlag(Define.FLAG_CURSED);
        }

        // Act
        Consumable.EatSpecialFood(f.World, ob, food);

        // Assert
        Assert.Equal(new[] { wantHp, wantSp }, new[] { (int)ob.Stats.Hp, (int)ob.Stats.Sp });
        Assert.Equal(new[] { line }, f.Host.Lines);
    }

    [Fact]
    public void EatSpecialFood_Given_a_food_that_changes_a_stat_or_protects_When_eaten_Then_the_player_is_told_once_that_it_is_not_available()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject food = f.Make("apple");
        food.Stats.Str = 1;
        food.Stats.Cha = 1;
        food.Resist[Define.ATNR_FIRE] = 20;

        // Act
        Consumable.EatSpecialFood(f.World, ob, food);

        // Assert
        Assert.Equal(new[] { Consumable.NotAvailable }, f.Host.Lines);
    }

    [Fact]
    public void EatSpecialFood_Given_a_resistance_that_no_spell_protects_against_When_eaten_Then_nothing_is_said()
    {
        // Arrange: acid, weapon magic and the others have sp_type -1 in C
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject food = f.Make("apple");
        food.Resist[Define.ATNR_ACID] = 50;
        food.Resist[Define.ATNR_BLIND] = 50;

        // Act
        Consumable.EatSpecialFood(f.World, ob, food);

        // Assert
        Assert.Empty(f.Host.Lines);
    }

    [Fact]
    public void ApplyFood_Given_a_food_with_a_title_When_eaten_Then_the_special_food_runs_after_the_normal_one()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(hp: 5);
        CfObject apple = f.Give(ob, "apple", o =>
        {
            o.Title = "of healing";
            o.Stats.Hp = 7;
        });

        // Act
        Consumable.ApplyFood(f.World, ob, apple);

        // Assert: 500 + 100 food, hp 5 + 100/50 = 7, then + 7
        Assert.Equal(new[] { "The apple tasted good.", "You begin to feel better." }, f.Host.Lines);
        Assert.Equal(14, ob.Stats.Hp);
    }

    [Fact]
    public void ApplyFood_Given_a_monster_When_it_eats_Then_it_is_healed_and_the_food_is_used_up()
    {
        // Arrange
        var f = new Fixture();
        CfObject mon = f.Make("sword");
        mon.Type = 0;
        mon.Stats.Hp = 1;
        mon.Stats.Maxhp = 30;
        CfObject apple = f.Make("apple");
        apple.Nrof = 1;
        f.World.InsertObInOb(apple, mon);

        // Act
        Consumable.ApplyFood(f.World, mon, apple);

        // Assert
        Assert.Equal(30, mon.Stats.Hp);
        Assert.Empty(f.Host.Lines);
        Assert.Null(mon.Inv);
    }

    // ------------------------------------------------------------------ apply_poison

    [Theory]
    [InlineData(999, 750)] // poison 750
    [InlineData(7, 6)] // poison 6
    public void ApplyPoison_Given_a_poisonous_drink_When_drunk_Then_a_quarter_of_the_food_level_is_lost_as_in_C(int food, int want)
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(food: food, hp: 10);
        CfObject booze = f.Give(ob, "bad_booze", o => o.Stats.Hp = 5);

        // Act
        Consumable.ApplyPoison(f.World, ob, booze);

        // Assert: the attack of type poison does no damage in C, hit points stay
        Assert.Equal(want, ob.Stats.Food);
        Assert.Equal(10, ob.Stats.Hp);
        Assert.Equal(new[] { "Yech!  That tasted poisonous!" }, f.Host.Lines);
        Assert.False(Held(ob, booze));
    }

    // ------------------------------------------------------------------ dragon_eat_flesh

    private static (CfObject Ob, CfObject Skin, CfObject Abil) Dragon(Fixture f, int level, int focus)
    {
        (CfObject ob, _) = f.Player("dragon_player", level: level);
        CfObject skin = f.Give(ob, "dragon_skin_force");
        CfObject abil = f.Give(ob, "dragon_ability_force", o =>
        {
            o.Stats.Exp = focus;
            o.Level = 4;
        });
        return (ob, skin, abil);
    }

    private static CfObject Flesh(Fixture f, CfObject ob, int level, int resist, int atnr = Define.ATNR_FIRE, int lastEat = 0)
    {
        return f.Give(ob, "meat", o =>
        {
            o.Level = (short)level;
            o.Resist[atnr] = (short)resist;
            o.LastEat = (short)lastEat;
        });
    }

    [Fact]
    public void DragonEatFlesh_Given_a_chance_of_7885_per_10000_When_the_die_is_7884_Then_the_skin_improves_with_the_message_of_a_delicious_meal()
    {
        // Arrange: golden "dragon focus_low 78.859504 7885": level 10 against flesh 40, resistance 50 to the fire the ability focuses on
        var f = new Fixture();
        (CfObject ob, CfObject skin, _) = Dragon(f, 10, Define.ATNR_FIRE);
        CfObject meat = Flesh(f, ob, 40, 50);
        f.Dice.Enqueue(7884);
        f.Dice.Enqueue(0);

        // Act
        int ate = Consumable.DragonEatFlesh(f.World, ob, meat);

        // Assert
        Assert.Equal(1, ate);
        Assert.Equal(1, skin.Resist[Define.ATNR_FIRE]);
        Assert.Equal(new[] { "Hmm! The meat tasted delicious!", "Your skin is now more resistant to fire!" }, f.Host.Lines);
    }

    [Fact]
    public void DragonEatFlesh_Given_a_chance_of_7885_per_10000_When_the_die_is_7885_Then_the_skin_does_not_improve()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, CfObject skin, _) = Dragon(f, 10, Define.ATNR_FIRE);
        CfObject meat = Flesh(f, ob, 40, 50);
        f.Dice.Enqueue(7885);

        // Act
        Consumable.DragonEatFlesh(f.World, ob, meat);

        // Assert
        Assert.Equal(0, skin.Resist[Define.ATNR_FIRE]);
        Assert.Equal(new[] { "Hmm! The meat tasted delicious!" }, f.Host.Lines);
    }

    [Fact]
    public void DragonEatFlesh_Given_a_chance_of_17_percent_When_eaten_Then_it_tastes_very_good_and_the_winner_is_chosen_among_those_who_won()
    {
        // Arrange: golden "dragon plain_low 17.442149 1744" for cold (no focus); fire also wins with the same numbers: winners cold(4), fire(2)
        var f = new Fixture();
        (CfObject ob, CfObject skin, _) = Dragon(f, 10, Define.ATNR_ACID);
        CfObject meat = Flesh(f, ob, 40, 25);
        meat.Resist[Define.ATNR_COLD] = 25;
        f.Dice.Enqueue(1743); // fire (2) wins
        f.Dice.Enqueue(1743); // cold (4) wins
        f.Dice.Enqueue(1); // the winner: the second of the list, cold

        // Act
        Consumable.DragonEatFlesh(f.World, ob, meat);

        // Assert
        Assert.Equal(new[] { 0, 1 }, new[] { (int)skin.Resist[Define.ATNR_FIRE], (int)skin.Resist[Define.ATNR_COLD] });
        Assert.Equal("The meat tasted very good.", f.Host.Lines[0]);
    }

    [Fact]
    public void DragonEatFlesh_Given_a_skin_far_above_the_flesh_When_eaten_Then_the_chance_is_zero_and_it_has_no_taste()
    {
        // Arrange: golden "dragon skin_high 0.000000 0"
        var f = new Fixture();
        (CfObject ob, CfObject skin, _) = Dragon(f, 60, Define.ATNR_ACID);
        skin.Resist[Define.ATNR_FIRE] = 80;
        CfObject meat = Flesh(f, ob, 20, 50);
        f.Dice.Enqueue(0);

        // Act
        Consumable.DragonEatFlesh(f.World, ob, meat);

        // Assert: the die 0 is not below 0
        Assert.Equal(80, skin.Resist[Define.ATNR_FIRE]);
        Assert.Equal(new[] { "The meat had no taste." }, f.Host.Lines);
    }

    [Fact]
    public void DragonEatFlesh_Given_a_skin_a_little_above_the_flesh_When_eaten_Then_the_chance_is_the_golden_1149_and_it_tastes_good()
    {
        // Arrange: golden "dragon skin_near 11.495868 1149"
        var f = new Fixture();
        (CfObject ob, CfObject skin, _) = Dragon(f, 60, Define.ATNR_ACID);
        skin.Resist[Define.ATNR_FIRE] = 30;
        CfObject meat = Flesh(f, ob, 20, 50);
        f.Dice.Enqueue(1148);
        f.Dice.Enqueue(0);

        // Act
        Consumable.DragonEatFlesh(f.World, ob, meat);

        // Assert: 11.49 is more than 10: very good
        Assert.Equal(31, skin.Resist[Define.ATNR_FIRE]);
        Assert.Equal("The meat tasted very good.", f.Host.Lines[0]);
    }

    [Fact]
    public void DragonEatFlesh_Given_flesh_that_would_pass_the_limit_of_95_When_the_die_wins_Then_the_skin_stays()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, CfObject skin, _) = Dragon(f, 100, Define.ATNR_FIRE);
        skin.Resist[Define.ATNR_FIRE] = 95;
        CfObject meat = Flesh(f, ob, 110, 50);
        f.Dice.Enqueue(0);
        f.Dice.Enqueue(0);

        // Act
        Consumable.DragonEatFlesh(f.World, ob, meat);

        // Assert
        Assert.Equal(95, skin.Resist[Define.ATNR_FIRE]);
        Assert.DoesNotContain(f.Host.Lines, l => l.StartsWith("Your skin"));
    }

    [Fact]
    public void DragonEatFlesh_Given_flesh_of_a_new_focus_When_eaten_Then_the_ability_force_remembers_it_and_the_level_of_the_change_is_told()
    {
        // Arrange: no resistance for the dragon to gain, last_eat 3 (electricity) differs from the focus fire
        var f = new Fixture();
        (CfObject ob, _, CfObject abil) = Dragon(f, 10, Define.ATNR_FIRE);
        CfObject meat = f.Give(ob, "meat", o => o.LastEat = Define.ATNR_ELECTRICITY);

        // Act
        Consumable.DragonEatFlesh(f.World, ob, meat);

        // Assert
        Assert.Equal(Define.ATNR_ELECTRICITY, abil.LastEat);
        Assert.Equal(
            new[] { "The meat tasted strange.", "Your metabolism prepares to focus on electricity!", "The change will happen at level 5" },
            f.Host.Lines);
    }

    [Fact]
    public void DragonEatFlesh_Given_flesh_of_the_focus_the_dragon_already_has_When_eaten_Then_the_metabolism_continues_and_the_mark_is_cleared()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _, CfObject abil) = Dragon(f, 10, Define.ATNR_FIRE);
        abil.LastEat = Define.ATNR_COLD;
        CfObject meat = f.Give(ob, "meat", o => o.LastEat = Define.ATNR_FIRE);

        // Act
        Consumable.DragonEatFlesh(f.World, ob, meat);

        // Assert
        Assert.Equal(0, abil.LastEat);
        Assert.Equal("Your metabolism will continue to focus on fire.", f.Host.Lines[^1]);
    }

    [Fact]
    public void DragonEatFlesh_Given_flesh_that_is_not_flesh_or_a_player_that_is_not_a_dragon_When_called_Then_it_does_not_work()
    {
        // Arrange
        var f = new Fixture();
        (CfObject dragon, _, _) = Dragon(f, 10, Define.ATNR_FIRE);
        CfObject apple = f.Give(dragon, "apple");
        (CfObject dwarf, _) = f.Player();
        CfObject meat = f.Give(dwarf, "meat");

        // Act / Assert
        Assert.Equal(0, Consumable.DragonEatFlesh(f.World, dragon, apple));
        Assert.Equal(0, Consumable.DragonEatFlesh(f.World, dwarf, meat));
        Assert.Empty(f.Host.Lines);
    }

    [Fact]
    public void ApplyFood_Given_a_dragon_that_is_an_old_player_without_the_forces_When_it_eats_flesh_Then_the_usual_case_runs_with_the_dragon_message()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player("dragon_player", food: 500, hp: 10);
        CfObject meat = f.Give(ob, "meat");

        // Act
        Consumable.ApplyFood(f.World, ob, meat);

        // Assert
        Assert.Equal(new[] { "The meat tasted terrible!" }, f.Host.Lines);
        Assert.Equal(600, ob.Stats.Food);
    }

    [Fact]
    public void ApplyFood_Given_a_dragon_that_eats_flesh_When_it_eats_Then_the_stomach_and_hit_points_follow_the_dragon_rule_and_the_flesh_is_used_up()
    {
        // Arrange: dragon_eat_flesh fills the stomach (food 950 + 100 is 999) and heals by (999 - 950) / 50 = 0
        var f = new Fixture();
        (CfObject ob, _, _) = Dragon(f, 10, Define.ATNR_FIRE);
        ob.Stats.Food = 950;
        ob.Stats.Hp = 10;
        ob.Stats.Maxhp = 20;
        CfObject meat = f.Give(ob, "meat");

        // Act
        Consumable.ApplyFood(f.World, ob, meat);

        // Assert
        Assert.Equal(new[] { 999, 10 }, new[] { (int)ob.Stats.Food, (int)ob.Stats.Hp });
        Assert.False(Held(ob, meat));
        Assert.DoesNotContain(f.Host.Lines, l => l.Contains("full, but"));
    }

    // ------------------------------------------------------------------ apply_potion

    [Fact]
    public void ApplyPotion_Given_a_potion_of_strength_that_is_not_identified_When_drunk_Then_it_is_identified_the_stat_and_the_stored_line_change_and_it_is_used_up()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        CfObject potion = f.Give(ob, "potion_str");
        Assert.False(potion.QueryFlag(Define.FLAG_IDENTIFIED));

        // Act
        int r = Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(1, r);
        Assert.Equal(15, contr.OrigStats.Str);
        Assert.Equal(15, ob.Stats.Str);
        Assert.StartsWith("15 ", ob.Extra!.Get(Player.OrigStatsKey));
        Assert.Equal(new[] { "You feel stronger." }, f.Host.Lines);
        Assert.False(Held(ob, potion));
        Assert.True(potion.QueryFlag(Define.FLAG_IDENTIFIED));
        Assert.False(potion.QueryFlag(Define.FLAG_APPLIED));
    }

    [Fact]
    public void ApplyPotion_Given_a_cursed_potion_of_strength_with_a_value_When_drunk_Then_the_stat_goes_down()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        CfObject potion = f.Give(ob, "potion_str", o => o.SetFlag(Define.FLAG_CURSED));

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(13, contr.OrigStats.Str);
        Assert.Equal(new[] { "You feel weaker!" }, f.Host.Lines);
    }

    [Fact]
    public void ApplyPotion_Given_a_cursed_potion_without_a_value_When_drunk_Then_it_is_applied_like_a_good_one()
    {
        // Arrange: C: cursed and value != 0 clears the flag, else it is set
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player();
        CfObject potion = f.Give(ob, "potion_str", o =>
        {
            o.SetFlag(Define.FLAG_CURSED);
            o.Value = 0;
        });

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(15, contr.OrigStats.Str);
    }

    [Fact]
    public void ApplyPotion_Given_a_potion_that_changes_nothing_When_drunk_Then_nothing_happened_and_it_is_used_up()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject potion = f.Give(ob, "potion_plain");

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(new[] { "Nothing happened." }, f.Host.Lines);
        Assert.False(Held(ob, potion));
    }

    [Fact]
    public void ApplyPotion_Given_a_potion_that_casts_a_spell_When_drunk_Then_the_player_is_told_it_is_not_available_and_the_potion_stays()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player(hp: 3);
        CfObject potion = f.Give(ob, "potion_heal", nrof: 2);

        // Act
        int r = Consumable.ApplyPotion(f.World, ob, potion);

        // Assert: identified (C identifies first), not eaten
        Assert.Equal(1, r);
        Assert.Equal(new[] { Consumable.NotAvailable }, f.Host.Lines);
        Assert.Equal(2u, potion.Nrof);
        Assert.True(Held(ob, potion));
        Assert.True(potion.QueryFlag(Define.FLAG_IDENTIFIED));
        Assert.Equal("potion of healing", potion.Name);
        Assert.Equal(3, ob.Stats.Hp);
    }

    [Fact]
    public void ApplyPotion_Given_a_protection_potion_When_drunk_Then_it_is_not_available_and_no_force_is_made()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject potion = f.Give(ob, "potion_cold");
        potion.Resist[Define.ATNR_COLD] = 30;

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(new[] { Consumable.NotAvailable }, f.Host.Lines);
        Assert.True(Held(ob, potion));
        Assert.Equal(potion, ob.Inv);
        Assert.Null(potion.Below);
    }

    [Fact]
    public void ApplyPotion_Given_restoration_and_a_depletion_When_drunk_Then_the_depletion_goes_with_a_line_for_each_stat_it_took()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject depl = f.Give(ob, "depletion");
        CfObject potion = f.Give(ob, "potion_rest");
        potion.Attacktype = Define.AT_DEPLETE;

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert: Str (0) and Con (2)
        Assert.Equal(new[] { "You feel your strength return.", "You feel your health return." }, f.Host.Lines);
        Assert.False(Held(ob, depl));
        Assert.False(Held(ob, potion));
    }

    [Fact]
    public void ApplyPotion_Given_restoration_and_no_depletion_When_drunk_Then_there_is_a_great_loss_and_it_is_used_up()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject potion = f.Give(ob, "potion_rest");
        potion.Attacktype = Define.AT_DEPLETE;

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(new[] { "You feel a great loss..." }, f.Host.Lines);
        Assert.False(Held(ob, potion));
    }

    [Fact]
    public void ApplyPotion_Given_a_cursed_restoration_When_drunk_Then_drain_stat_is_not_available_and_the_potion_stays()
    {
        // Arrange
        var f = new Fixture();
        (CfObject ob, _) = f.Player();
        CfObject potion = f.Give(ob, "potion_rest");
        potion.Attacktype = Define.AT_DEPLETE;
        potion.SetFlag(Define.FLAG_DAMNED);

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(new[] { Consumable.NotAvailable }, f.Host.Lines);
        Assert.True(Held(ob, potion));
    }

    [Fact]
    public void ApplyPotion_Given_restoration_in_a_world_without_the_depletion_archetype_When_drunk_Then_it_does_not_work_and_the_potion_stays()
    {
        // Arrange
        var f = new Fixture(Arc.Replace("Object depletion\nname depletion\ntype 114\nStr -2\nCon -1\nend\n", string.Empty));
        (CfObject ob, _) = f.Player();
        CfObject potion = f.Give(ob, "potion_rest");
        potion.Attacktype = Define.AT_DEPLETE;

        // Act
        int r = Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(0, r);
        Assert.True(Held(ob, potion));
        Assert.Empty(f.Host.Lines);
    }

    [Fact]
    public void ApplyPotion_Given_an_improvement_potion_When_drunk_Then_the_first_level_below_the_potion_is_raised_as_in_the_golden_values_and_stored()
    {
        // Arrange: golden "levels first 9 9 1": levels 1 is full, level 2 has 7 hit points and goes to 9
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player(level: 6);
        SetLevels(contr);
        CfObject potion = f.Give(ob, "potion_improve");
        potion.Attacktype = Define.AT_GODPOWER;

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(new[] { 9, 9 }, new[] { (int)contr.LevHp[1], (int)contr.LevHp[2] });
        Assert.StartsWith("9 9 8", ob.Extra!.Get(Player.LevHpKey));
        Assert.Equal(new[] { "The Gods smile upon you and remake you", "a little more in their image.", "You feel a little more perfect." }, f.Host.Lines);
        Assert.False(Held(ob, potion));
    }

    [Fact]
    public void ApplyPotion_Given_a_cursed_improvement_potion_When_drunk_Then_the_first_level_is_set_to_1_as_in_the_golden_values()
    {
        // Arrange: golden "levels cursed 1 7 1"
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player(level: 6);
        SetLevels(contr);
        CfObject potion = f.Give(ob, "potion_improve");
        potion.Attacktype = Define.AT_GODPOWER;
        potion.SetFlag(Define.FLAG_CURSED);

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(new[] { 1, 7 }, new[] { (int)contr.LevHp[1], (int)contr.LevHp[2] });
        Assert.Equal(new[] { "The Gods are angry and punish you." }, f.Host.Lines);
    }

    [Fact]
    public void ApplyPotion_Given_an_improvement_potion_and_a_player_of_level_2_When_drunk_Then_there_is_no_level_to_change_as_in_the_golden_values()
    {
        // Arrange: golden "levels level2 9 7 0": the loop runs for levels below the player's own, which is only level 1
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player(level: 2);
        SetLevels(contr);
        CfObject potion = f.Give(ob, "potion_improve");
        potion.Attacktype = Define.AT_GODPOWER;

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(new[] { 9, 7 }, new[] { (int)contr.LevHp[1], (int)contr.LevHp[2] });
        Assert.Equal(new[] { "The potion had no effect - you are already perfect" }, f.Host.Lines);
        Assert.False(Held(ob, potion));
    }

    [Fact]
    public void ApplyPotion_Given_a_cursed_improvement_potion_and_nothing_to_lower_When_drunk_Then_the_player_is_fortunate()
    {
        // Arrange: every point of level 1 is 1 already
        var f = new Fixture();
        (CfObject ob, Player contr) = f.Player(level: 2);
        contr.LevHp[1] = contr.LevSp[1] = contr.LevGrace[1] = 1;
        CfObject potion = f.Give(ob, "potion_improve");
        potion.Attacktype = Define.AT_GODPOWER;
        potion.SetFlag(Define.FLAG_CURSED);

        // Act
        Consumable.ApplyPotion(f.World, ob, potion);

        // Assert
        Assert.Equal(new[] { "You are fortunate that you are so pathetic." }, f.Host.Lines);
    }

    private static void SetLevels(Player contr)
    {
        // the golden harness's tables: hp 9 7 8 8 ..., sp 6 6 3 ..., grace 3 3 2 ...
        short[] hp = { 0, 9, 7, 8, 8, 8, 8, 8, 8, 8, 8 };
        short[] sp = { 0, 6, 6, 3, 3, 3, 3, 3, 3, 3, 3 };
        short[] gr = { 0, 3, 3, 2, 2, 2, 2, 2, 2, 2, 2 };
        for (int i = 0; i < 11; i++)
        {
            contr.LevHp[i] = hp[i];
            contr.LevSp[i] = sp[i];
            contr.LevGrace[i] = gr[i];
        }
    }
}
