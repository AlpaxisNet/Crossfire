namespace Crossfire.Tests;

/// <summary>Tests of the port of the prices and the money texts of server/shop.c. Not ported from a Crossfire file.</summary>
public class ShopTests
{
    private const string Coins =
        "Object platinacoin\nname platinum coin\nname_pl platinum coins\ntype 36\nvalue 50\nend\n"
        + "Object goldcoin\nname gold coin\nname_pl gold coins\ntype 36\nvalue 10\nend\n"
        + "Object silvercoin\nname silver coin\nname_pl silver coins\ntype 36\nvalue 1\nend\n";

    private static CfWorld World(string arc, out List<string> log) => T.WorldWith(Coins + arc, out log, new ItemHost());

    /// <summary>An object of the archetype "item" with the archetype's fields and then the ones of the object.</summary>
    private static CfObject Make(CfWorld w, string objectFields)
    {
        CfObject op = w.GetArchetype("item");
        w.LoadObject(new LoadSource(objectFields + "end\n"), op, Define.LO_NEWFILE, 0);
        return op;
    }

    private static CfObject Player(CfWorld w, int cha, int level = 1, bool bargaining = false)
    {
        CfObject pl = w.GetObject();
        pl.Name = "hero";
        pl.Type = Define.PLAYER;
        pl.Stats.Cha = (sbyte)cha;
        pl.Level = (short)level;
        if (bargaining)
        {
            CfObject skill = w.GetObject();
            skill.Type = Define.SKILL;
            skill.Stats.Sp = Define.SK_BARGAINING;
            pl.Inv = skill;
        }
        return pl;
    }

    // ---- query_cost: money and gems

    [Fact]
    public void QueryCost_Given_money_When_asked_Then_the_count_times_the_value_for_every_flag()
    {
        // Arrange
        CfWorld w = World("", out _);
        CfObject coins = w.GetArchetype("goldcoin");
        coins.Nrof = 5;

        // Act
        int[] costs = { w.QueryCost(coins, null, Define.F_BUY), w.QueryCost(coins, null, Define.F_SELL), w.QueryCost(coins, null, Define.F_TRUE) };

        // Assert
        Assert.Equal(new[] { 50, 50, 50 }, costs);
    }

    [Fact]
    public void QueryCost_Given_gems_When_asked_Then_buy_is_103_percent_sell_97_percent_and_the_double_arithmetic_truncates()
    {
        // Arrange
        CfWorld w = World("Object item\nname ruby\ntype 60\nvalue 100\nnrof 3\nend\n", out _);
        CfObject gem = w.GetArchetype("item");
        CfObject one = Make(w, "nrof 1\n");

        // Act
        int[] costs =
        {
            w.QueryCost(gem, null, Define.F_BUY), w.QueryCost(gem, null, Define.F_SELL), w.QueryCost(gem, null, Define.F_TRUE),
            w.QueryCost(one, null, Define.F_BUY), w.QueryCost(one, null, Define.F_SELL),
        };

        // Assert: 1.03*100 is 103.0 and 0.97*100 is 97.0 as doubles
        Assert.Equal(new[] { 309, 291, 300, 103, 97 }, costs);
    }

    [Fact]
    public void QueryCost_Given_a_gem_and_an_unknown_flag_When_asked_Then_zero_and_an_error()
    {
        // Arrange
        CfWorld w = World("Object item\nname ruby\ntype 60\nvalue 100\nnrof 3\nend\n", out List<string> log);
        CfObject gem = w.GetArchetype("item");

        // Act
        int cost = w.QueryCost(gem, null, 7);

        // Assert
        Assert.Equal(0, cost);
        Assert.Contains("Error: Query_cost: Gem type with unknown flag : 7\n", log);
    }

    // ---- query_cost: identified, cursed, unidentified

    [Fact]
    public void QueryCost_Given_an_identified_stack_When_asked_Then_the_value_times_the_number()
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\nname_pl swords\ntype 15\nvalue 100\nidentified 1\nend\n", out _);
        CfObject single = w.GetArchetype("item");
        CfObject stack = Make(w, "nrof 5\n");

        // Act
        int[] costs = { w.QueryCost(single, null, Define.F_BUY), w.QueryCost(single, null, Define.F_SELL), w.QueryCost(stack, null, Define.F_TRUE) };

        // Assert
        Assert.Equal(new[] { 100, 100, 500 }, costs);
    }

    [Theory]
    [InlineData("cursed 1\n")]
    [InlineData("damned 1\n")]
    public void QueryCost_Given_an_identified_cursed_item_When_asked_Then_it_is_worth_nothing(string flag)
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 100\nidentified 1\n" + flag + "end\n", out _);
        CfObject op = w.GetArchetype("item");

        // Act
        int cost = w.QueryCost(op, null, Define.F_TRUE);

        // Assert
        Assert.Equal(0, cost);
    }

    [Fact]
    public void QueryCost_Given_an_item_that_needs_no_identification_When_cursed_Then_it_is_worth_nothing()
    {
        // Arrange: a key (type 24) is never identified
        CfWorld w = World("Object item\nname key\ntype 24\nvalue 100\ncursed 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");

        // Act
        int cost = w.QueryCost(op, null, Define.F_TRUE);

        // Assert
        Assert.Equal(0, cost);
    }

    [Fact]
    public void QueryCost_Given_an_unidentified_weapon_When_sold_Then_a_third_or_two_thirds_when_applied()
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 100\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject used = Make(w, "been_applied 1\n");
        CfObject stack = Make(w, "nrof 4\n");

        // Act
        int[] costs = { w.QueryCost(op, null, Define.F_TRUE), w.QueryCost(used, null, Define.F_TRUE), w.QueryCost(stack, null, Define.F_SELL) };

        // Assert: 100/3, 100*2/3, 4*100/3
        Assert.Equal(new[] { 33, 66, 133 }, costs);
    }

    [Fact]
    public void QueryCost_Given_an_unidentified_potion_When_sold_Then_one_thousand_for_each()
    {
        // Arrange
        CfWorld w = World("Object item\nname potion\ntype 5\nvalue 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject stack = Make(w, "nrof 3\n");

        // Act
        int[] costs = { w.QueryCost(op, null, Define.F_TRUE), w.QueryCost(stack, null, Define.F_TRUE) };

        // Assert
        Assert.Equal(new[] { 1000, 3000 }, costs);
    }

    [Fact]
    public void QueryCost_Given_an_unidentified_item_When_bought_Then_fifty_times_the_value_and_an_error()
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 100\nend\n", out List<string> log);
        CfObject op = w.GetArchetype("item");

        // Act
        int cost = w.QueryCost(op, null, Define.F_BUY);

        // Assert
        Assert.Equal(5000, cost);
        Assert.Contains("Error: Asking for buy-value of unidentified object.", log);
    }

    [Fact]
    public void QueryCost_Given_an_unidentified_item_without_an_archetype_When_priced_Then_a_fifth_to_sell_and_ten_times_to_buy()
    {
        // Arrange
        CfWorld w = World("", out List<string> log);
        CfObject op = w.GetObject();
        op.Name = "orphan";
        op.Type = Define.WEAPON;
        op.Value = 100;

        // Act
        int sell = w.QueryCost(op, null, Define.F_SELL);
        int buy = w.QueryCost(op, null, Define.F_BUY);

        // Assert
        Assert.Equal(20, sell);
        Assert.Equal(1000, buy);
        Assert.Equal(2, log.Count(l => l.StartsWith("Debug: In sell item: Have object with no archetype: orphan\n")));
        Assert.Contains("Error: Asking for buy-value of unidentified object without arch.", log);
    }

    [Fact]
    public void QueryCost_Given_an_unidentified_item_When_sold_Then_at_most_600()
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 3000\nend\n", out _);
        CfObject op = w.GetArchetype("item");

        // Act
        int sell = w.QueryCost(op, null, Define.F_SELL);
        int truth = w.QueryCost(op, null, Define.F_TRUE);

        // Assert
        Assert.Equal(600, sell);
        Assert.Equal(1000, truth);
    }

    // ---- query_cost: magic, wands, the limit

    [Theory]
    [InlineData(2, 240)]
    [InlineData(1, 30)]
    [InlineData(-2, 3)]
    [InlineData(-1, 5)]
    [InlineData(0, 10)]
    public void QueryCost_Given_an_identified_magic_item_When_priced_Then_the_cube_of_the_bonus_times_three_or_a_division(int magic, int expected)
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 10\nidentified 1\nend\n", out _);
        CfObject op = Make(w, "magic " + magic + "\n");

        // Act
        int cost = w.QueryCost(op, null, Define.F_TRUE);

        // Assert
        Assert.Equal(expected, cost);
    }

    [Fact]
    public void QueryCost_Given_an_item_whose_archetype_is_magical_When_priced_Then_no_extra_for_the_magic()
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 10\nmagic 1\nidentified 1\nend\n", out _);
        CfObject op = Make(w, "magic 2\n");

        // Act
        int cost = w.QueryCost(op, null, Define.F_TRUE);

        // Assert
        Assert.Equal(10, cost);
    }

    [Fact]
    public void QueryCost_Given_a_magic_item_that_is_unidentified_and_unused_When_priced_Then_no_extra_for_the_magic()
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 90\nend\n", out _);
        CfObject plain = w.GetArchetype("item");
        CfObject magic = Make(w, "magic 2\n");
        CfObject applied = Make(w, "magic 2\nbeen_applied 1\n");

        // Act
        int[] costs = { w.QueryCost(plain, null, Define.F_TRUE), w.QueryCost(magic, null, Define.F_TRUE), w.QueryCost(applied, null, Define.F_TRUE) };

        // Assert: 90/3, 90/3, 90*2/3 and then the bonus 3*2^3 for the applied one
        Assert.Equal(new[] { 30, 30, 60 * 24 }, costs);
    }

    [Fact]
    public void QueryCost_Given_a_wand_When_identified_Then_the_value_by_charges_over_the_most_charges_of_its_spell()
    {
        // Arrange: spell 1 (fireball) has 10 charges in the host
        CfWorld w = World("Object item\nname wand\ntype 109\nvalue 100\nsp 1\nfood 5\nidentified 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject noSpellCharges = Make(w, "sp 0\n");

        // Act
        int cost = w.QueryCost(op, null, Define.F_TRUE);
        int neverZero = w.QueryCost(noSpellCharges, null, Define.F_TRUE);

        // Assert: 100*5/10, and 100*5/max(1,0)
        Assert.Equal(50, cost);
        Assert.Equal(500, neverZero);
    }

    [Fact]
    public void QueryCost_Given_an_unidentified_wand_When_priced_Then_a_third_of_a_third()
    {
        // Arrange
        CfWorld w = World("Object item\nname wand\ntype 109\nvalue 300\nsp 1\nfood 5\nend\n", out _);
        CfObject op = w.GetArchetype("item");

        // Act
        int cost = w.QueryCost(op, null, Define.F_TRUE);

        // Assert
        Assert.Equal(33, cost);
    }

    [Fact]
    public void QueryCost_Given_a_very_valuable_item_When_sold_or_valued_Then_the_price_is_limited_but_not_when_bought()
    {
        // Arrange
        CfWorld w = World("Object item\nname gem\ntype 15\nvalue 30000\nidentified 1\nend\n", out _);
        CfObject single = w.GetArchetype("item");
        CfObject stack = Make(w, "nrof 2\n");

        // Act
        int[] costs =
        {
            w.QueryCost(single, null, Define.F_TRUE), w.QueryCost(single, null, Define.F_SELL), w.QueryCost(single, null, Define.F_BUY),
            w.QueryCost(stack, null, Define.F_SELL), w.QueryCost(stack, null, Define.F_BUY),
        };

        // Assert: 8000 + isqrt(30000)*20 = 11460, for the stack 2 * 11460
        Assert.Equal(new[] { 11460, 11460, 30000, 22920, 60000 }, costs);
    }

    [Fact]
    public void QueryCost_Given_a_price_at_the_limit_When_sold_Then_it_is_not_limited()
    {
        // Arrange
        CfWorld w = World("Object item\nname gem\ntype 15\nvalue 25000\nidentified 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");

        // Act
        int cost = w.QueryCost(op, null, Define.F_SELL);

        // Assert
        Assert.Equal(25000, cost);
    }

    [Fact]
    public void QueryCost_Given_a_value_that_wraps_negative_When_priced_Then_a_million_to_buy_and_nothing_to_sell()
    {
        // Arrange: 1000000000 * 3 does not fit in 32 bits
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 1000000000\nidentified 1\nend\n", out _);
        CfObject op = Make(w, "magic 1\n");

        // Act
        int buy = w.QueryCost(op, null, Define.F_BUY);
        int sell = w.QueryCost(op, null, Define.F_SELL);

        // Assert
        Assert.Equal(1000000, buy);
        Assert.Equal(0, sell);
    }

    // ---- query_cost: the player's charisma

    [Theory]
    [InlineData(Define.F_BUY, 550)]
    [InlineData(Define.F_SELL, 250)]
    [InlineData(Define.F_TRUE, 400)]
    public void QueryCost_Given_a_player_of_charisma_18_When_priced_Then_the_prices_of_the_charisma_table(int flag, int expected)
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 100\nidentified 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject pl = Player(w, 18);

        // Act
        int cost = w.QueryCost(op, pl, flag);

        // Assert
        Assert.Equal(expected, cost);
    }

    [Theory]
    [InlineData(Define.F_BUY, 5288)]
    [InlineData(Define.F_SELL, 2711)]
    public void QueryCost_Given_a_player_with_the_bargaining_skill_When_priced_Then_a_third_of_the_level_is_added_to_the_charisma(int flag, int expected)
    {
        // Arrange: charisma 18 and level 7: (7+2)/3 = 3, so 21 (bonus 1.95)
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 1000\nidentified 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject pl = Player(w, 18, 7, true);

        // Act
        int cost = w.QueryCost(op, pl, flag);

        // Assert
        Assert.Equal(expected, cost);
    }

    [Theory]
    [InlineData(Define.F_BUY, 4800)]
    [InlineData(Define.F_SELL, 3199)]
    public void QueryCost_Given_a_bargaining_player_whose_charisma_passes_30_When_priced_Then_30_is_used_and_float_rounding_truncates(int flag, int expected)
    {
        // Arrange: 25 + 30 is capped at 30 (bonus 1.5, diff is the float 0.2, so 1000 sells for 3199)
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 1000\nidentified 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject pl = Player(w, 25, 90, true);

        // Act
        int cost = w.QueryCost(op, pl, flag);

        // Assert
        Assert.Equal(expected, cost);
    }

    [Fact]
    public void QueryCost_Given_a_bargaining_player_whose_charisma_is_exactly_31_When_priced_Then_it_is_cut_to_30()
    {
        // Arrange: 30 + (1+2)/3 = 31, which the table does not have
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 1000\nidentified 1\nend\n", out List<string> log);
        CfObject op = w.GetArchetype("item");
        CfObject pl = Player(w, 30, 1, true);

        // Act
        int buy = w.QueryCost(op, pl, Define.F_BUY);

        // Assert
        Assert.Equal(4800, buy);
        Assert.DoesNotContain(log, l => l.StartsWith("Error:"));
    }

    [Fact]
    public void QueryCost_Given_a_charisma_of_5_When_priced_Then_the_price_table_has_a_big_difference()
    {
        // Arrange: bonus 6.0, diff 5/7
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 70\nidentified 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject pl = Player(w, 5);

        // Act
        int buy = w.QueryCost(op, pl, Define.F_BUY);
        int sell = w.QueryCost(op, pl, Define.F_SELL);

        // Assert: 4*70*(1+5/7) = 480 and 4*70*(1-5/7) = 80 (to float and double precision: 479 and 79 or exact)
        Assert.InRange(buy, 479, 480);
        Assert.InRange(sell, 79, 80);
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(40)]
    public void QueryCost_Given_a_charisma_outside_the_table_When_priced_Then_an_error_and_the_bad_case_of_the_C_code(int cha)
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 100\nidentified 1\nend\n", out List<string> log);
        CfObject op = w.GetArchetype("item");
        CfObject pl = Player(w, cha);

        // Act
        int buy = w.QueryCost(op, pl, Define.F_BUY);

        // Assert: diff is the float 0.9, so 4*100*(1+0.89999998) is 759.99999 and truncates to 759
        Assert.Equal(759, buy);
        Assert.Single(log, l => l.StartsWith("Error: Illegal charisma bonus"));
    }

    [Fact]
    public void QueryCost_Given_something_that_is_not_a_player_When_priced_Then_charisma_does_not_count()
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 100\nidentified 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject monster = Player(w, 18);
        monster.Type = Define.WEAPON;

        // Act
        int cost = w.QueryCost(op, monster, Define.F_BUY);

        // Assert
        Assert.Equal(100, cost);
    }

    [Fact]
    public void QueryCost_Given_a_negative_value_When_sold_to_a_player_Then_zero()
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue -5\nidentified 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject pl = Player(w, 18);

        // Act
        int sell = w.QueryCost(op, pl, Define.F_SELL);
        int buy = w.QueryCost(op, pl, Define.F_BUY);

        // Assert
        Assert.Equal(0, sell);
        Assert.Equal(1000000, buy);
    }

    // ---- cost_string_from_value, query_cost_string

    [Theory]
    [InlineData(0, "nothing")]
    [InlineData(-4, "nothing")]
    [InlineData(1, "1 silver coin")]
    [InlineData(3, "3 silver coins")]
    [InlineData(10, "1 gold coin")]
    [InlineData(11, "1 gold coin and 1 silver coin")]
    [InlineData(49, "4 gold coins and 9 silver coins")]
    [InlineData(50, "1 platinum coin")]
    [InlineData(51, "1 platinum coin and 1 silver coin")]
    [InlineData(60, "1 platinum coin and 1 gold coin")]
    [InlineData(63, "1 platinum coin, 1 gold coin and 3 silver coins")]
    [InlineData(150, "3 platinum coins")]
    [InlineData(172, "3 platinum coins, 2 gold coins and 2 silver coins")]
    [InlineData(1000000, "20000 platinum coins")]
    public void CostStringFromValue_Given_an_amount_When_written_Then_the_coins_from_the_greatest_down(int cost, string expected)
    {
        // Arrange
        CfWorld w = World("", out _);

        // Act
        string text = w.CostStringFromValue(cost);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void CostStringFromValue_Given_a_missing_coin_archetype_When_written_Then_nothing()
    {
        // Arrange: without platinacoin the search stops at once, even with gold and silver there
        CfWorld w = T.WorldWith(
            "Object goldcoin\nname gold coin\ntype 36\nvalue 10\nend\nObject silvercoin\nname silver coin\ntype 36\nvalue 1\nend\n", out _);

        // Act
        string text = w.CostStringFromValue(15);

        // Assert
        Assert.Equal("nothing", text);
    }

    [Fact]
    public void CostStringFromValue_Given_a_missing_last_coin_When_written_Then_the_list_stops_before_it()
    {
        // Arrange
        CfWorld w = T.WorldWith(
            "Object platinacoin\nname platinum coin\ntype 36\nvalue 50\nend\nObject goldcoin\nname gold coin\ntype 36\nvalue 10\nend\n", out _);

        // Act
        string text = w.CostStringFromValue(63);

        // Assert: the 3 silver are not named
        Assert.Equal("1 platinum coin and 1 gold coin", text);
    }

    [Fact]
    public void QueryCostString_Given_an_item_and_a_player_When_asked_Then_the_text_of_the_price()
    {
        // Arrange
        CfWorld w = World("Object item\nname sword\ntype 15\nvalue 100\nidentified 1\nend\n", out _);
        CfObject op = w.GetArchetype("item");
        CfObject pl = Player(w, 18);

        // Act
        string buy = w.QueryCostString(op, pl, Define.F_BUY);
        string sell = w.QueryCostString(op, pl, Define.F_SELL);
        string free = w.QueryCostString(op, null, Define.F_TRUE);

        // Assert: 550 and 250 and 100
        Assert.Equal("11 platinum coins", buy);
        Assert.Equal("5 platinum coins", sell);
        Assert.Equal("2 platinum coins", free);
    }

    // ---- query_money

    private static void Link(CfObject parent, params CfObject[] children)
    {
        CfObject? prev = null;
        foreach (CfObject c in children)
        {
            c.Env = parent;
            if (prev == null)
            {
                parent.Inv = c;
            }
            else
            {
                prev.Below = c;
            }
            prev = c;
        }
    }

    private static CfObject Coin(CfWorld w, string arch, uint nrof)
    {
        CfObject c = w.GetArchetype(arch);
        c.Nrof = nrof;
        return c;
    }

    private static CfObject Bag(CfWorld w, bool applied, string? race)
    {
        CfObject bag = w.GetObject();
        bag.Name = "bag";
        bag.Type = Define.CONTAINER;
        bag.Race = race;
        if (applied)
        {
            bag.SetFlag(Define.FLAG_APPLIED);
        }
        return bag;
    }

    [Fact]
    public void QueryMoney_Given_a_player_with_coins_and_bags_When_asked_Then_the_coins_of_the_gold_bags_that_are_applied()
    {
        // Arrange
        CfWorld w = World("", out _);
        CfObject pl = Player(w, 10);
        CfObject sword = w.GetObject();
        sword.Value = 1000;
        sword.Type = Define.WEAPON;
        CfObject open = Bag(w, true, null);
        Link(open, Coin(w, "platinacoin", 3));
        CfObject arrows = Bag(w, true, "arrows");
        Link(arrows, Coin(w, "goldcoin", 50));
        CfObject closed = Bag(w, false, "gold");
        Link(closed, Coin(w, "goldcoin", 70));
        CfObject purse = Bag(w, true, "gold coins");
        Link(purse, Coin(w, "goldcoin", 1));
        CfObject inner = Bag(w, true, "gold");
        Link(inner, Coin(w, "silvercoin", 4));
        Link(open, open.Inv!, inner);
        Link(pl, Coin(w, "silvercoin", 5), Coin(w, "goldcoin", 2), sword, open, arrows, closed, purse);

        // Act
        int money = w.QueryMoney(pl);

        // Assert: 5 + 20 + (150 + 4) + 10
        Assert.Equal(189, money);
    }

    [Fact]
    public void QueryMoney_Given_an_object_that_is_neither_player_nor_container_When_asked_Then_zero_and_an_error()
    {
        // Arrange
        CfWorld w = World("", out List<string> log);
        CfObject sword = w.GetObject();
        sword.Type = Define.WEAPON;
        Link(sword, Coin(w, "goldcoin", 2));

        // Act
        int money = w.QueryMoney(sword);

        // Assert
        Assert.Equal(0, money);
        Assert.Contains("Error: Query money called with non player/container", log);
    }

    [Fact]
    public void QueryMoney_Given_a_container_When_asked_Then_its_coins()
    {
        // Arrange
        CfWorld w = World("", out _);
        CfObject bag = Bag(w, false, null);
        Link(bag, Coin(w, "goldcoin", 4));

        // Act
        int money = w.QueryMoney(bag);

        // Assert
        Assert.Equal(40, money);
    }
}
