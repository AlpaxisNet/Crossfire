namespace Crossfire.Tests;

/// <summary>The spells, the regeneration of a player and the treasure abilities that the item code asks the host for.</summary>
internal sealed class ItemHost : CrossfireHost
{
    private static readonly string[] Spells = { "magic missile", "fireball", "heal" };
    private static readonly int[] Charges = { 0, 10, 4 };

    public Dictionary<string, string[]> Abilities { get; } = new();

    public override string? SpellName(int index) => index >= 0 && index < Spells.Length ? Spells[index] : null;

    public override int SpellCharges(int index) => index >= 0 && index < Charges.Length ? Charges[index] : 0;

    public override int PlayerDigestion(IPlayerHandle contr) => -2;

    public override int PlayerGenGrace(IPlayerHandle contr) => 3;

    public override int PlayerGenSp(IPlayerHandle contr) => 0;

    public override int PlayerGenHp(IPlayerHandle contr) => 1;

    public override IEnumerable<string> TreasureAbilityNames(TreasureList list) =>
        Abilities.TryGetValue(list.Name, out string[]? names) ? names : Array.Empty<string>();

    public List<string> Calls { get; } = new();

    public override void EsrvSendItem(CfObject player, CfObject op) => Calls.Add("send:" + player.Name + ":" + op.Name);

    public override void UpdateObject(CfObject op, int action) => Calls.Add("update:" + op.Name + ":" + action);
}

/// <summary>Tests of the port of common/item.c (names, descriptions, is_magical, identify). Not ported from a Crossfire file.</summary>
public class ItemTests
{
    private static (CfWorld World, CfObject Op) Item(string name, string fields, ItemHost? host = null)
    {
        string arc = "Object test_" + name.Replace(' ', '_') + "\nname " + name + "\nname_pl " + name + "s\n" + fields + "end\n";
        CfWorld w = T.WorldWith(arc, out _, host ?? new ItemHost());
        return (w, w.GetArchetype("test_" + name.Replace(' ', '_')));
    }

    // ---- query_short_name, query_name, query_base_name

    [Fact]
    public void QueryName_Given_an_item_without_nrof_weight_title_or_magic_When_asked_Then_its_name_as_it_is()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("rock", "type 0\n");

        // Act
        string name = w.QueryName(op);

        // Assert
        Assert.Equal("rock", name);
    }

    [Fact]
    public void QueryShortName_Given_an_object_without_a_name_When_asked_Then_null_in_parentheses()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject op = w.GetObject();

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("(null)", name);
    }

    [Fact]
    public void QueryShortName_Given_a_magical_item_without_weight_or_nrof_When_asked_Then_the_full_name_is_built()
    {
        // Arrange: without the magic the quick path would give the name as it is
        (CfWorld w, CfObject op) = Item("sword", "type 15\nmagic 2\nidentified 1\n");
        (CfWorld w2, CfObject plain) = Item("sword", "type 15\n");

        // Act
        string magic = w.QueryShortName(op);
        string quick = w2.QueryShortName(plain);

        // Assert: nrof 0 is not 1, so the plural name is used
        Assert.Equal("swords +2", magic);
        Assert.Equal("sword", quick);
    }

    [Theory]
    [InlineData(1, "arrow")]
    [InlineData(5, "arrows")]
    [InlineData(0, "arrows")]
    public void QueryShortName_Given_a_stack_When_nrof_is_not_one_Then_the_plural_name(int nrof, string expected)
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("arrow", "type 13\nweight 2\nnrof " + nrof + "\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal(expected, name);
    }

    [Fact]
    public void QueryShortName_Given_a_stack_without_a_plural_name_When_asked_Then_empty_and_an_error_is_logged()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        CfObject op = w.GetObject();
        op.Name = "arrow";
        op.Weight = 2;
        op.Nrof = 3;

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("", name);
        Assert.Single(log, l => l.Contains("no plural name"));
    }

    [Theory]
    [InlineData(1, "sword of Doom")]
    [InlineData(0, "sword of Doom")]
    public void QueryShortName_Given_a_titled_identified_item_When_asked_Then_the_title_follows_the_name(int identified, string expected)
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\nweight 10\nnrof 1\ntitle of Doom\nidentified 1\n");
        if (identified == 0)
        {
            op.ClearFlag(Define.FLAG_IDENTIFIED);
            expected = "sword";
        }

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(3, 1, 0, "sword +3")]
    [InlineData(-2, 1, 0, "sword -2")]
    [InlineData(3, 0, 1, "sword +3")]
    [InlineData(3, 0, 0, "sword")]
    public void QueryShortName_Given_a_magic_weapon_When_identified_or_applied_Then_the_bonus_is_shown(int magic, int identified, int beenApplied, string expected)
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\nweight 10\nnrof 1\nmagic " + magic + "\nidentified " + identified + "\nbeen_applied " + beenApplied + "\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal(expected, name);
    }

    [Fact]
    public void QueryShortName_Given_a_magic_item_that_needs_no_identification_When_applied_Then_no_bonus_is_shown()
    {
        // Arrange: a key is not on the list of need_identify
        (CfWorld w, CfObject op) = Item("key", "type 24\nweight 10\nnrof 1\nmagic 2\nbeen_applied 1\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("key", name);
    }

    [Theory]
    [InlineData(Define.SCROLL, 1, 5, "scroll of fireball (lvl 5)")]
    [InlineData(Define.WAND, 0, 7, "wand of magic missile (lvl 7)")]
    [InlineData(Define.ROD, 2, 1, "rod of heal (lvl 1)")]
    public void QueryShortName_Given_an_identified_spell_item_When_asked_Then_the_spell_and_level_follow(int type, int spell, int level, string expected)
    {
        // Arrange
        string name0 = type == Define.SCROLL ? "scroll" : type == Define.WAND ? "wand" : "rod";
        (CfWorld w, CfObject op) = Item(name0, "type " + type + "\nweight 10\nnrof 1\nsp " + spell + "\nlevel " + level + "\nidentified 1\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal(expected, name);
    }

    [Fact]
    public void QueryShortName_Given_an_unidentified_scroll_When_asked_Then_only_the_name()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("scroll", "type 111\nweight 10\nnrof 1\nsp 1\nlevel 3\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("scroll", name);
    }

    [Fact]
    public void QueryShortName_Given_a_scroll_with_a_title_When_identified_Then_the_title_replaces_the_spell()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("scroll", "type 111\nweight 10\nnrof 1\nsp 1\nlevel 3\ntitle of Fire\nidentified 1\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("scroll of Fire", name);
    }

    [Theory]
    [InlineData("", "spellbook of magic missile")]
    [InlineData("slaying Kerrigan\n", "spellbook of Kerrigan")]
    public void QueryShortName_Given_an_identified_spellbook_When_asked_Then_its_spell_or_slaying_and_no_level(string extra, string expected)
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("spellbook", "type 85\nweight 10\nnrof 1\nsp 0\nlevel 3\nidentified 1\n" + extra);

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal(expected, name);
    }

    [Fact]
    public void QueryShortName_Given_an_identified_ring_When_asked_Then_its_abilities_follow()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("ring", "type 70\nweight 10\nnrof 1\nPow 3\nCha -1\nexp 2\nluck 1\nidentified 1\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("ring (Cha-1)(Pow+3)(speed +2)(luck+1)", name);
    }

    [Fact]
    public void QueryShortName_Given_an_unidentified_ring_When_asked_Then_the_name_alone()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("ring", "type 70\nweight 10\nnrof 1\nPow 3\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("ring", name);
    }

    [Fact]
    public void QueryShortName_Given_a_plain_identified_ring_When_asked_Then_of_adornment()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("ring", "type 70\nweight 10\nnrof 1\nidentified 1\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("ring of adornment", name);
    }

    [Fact]
    public void QueryShortName_Given_an_identified_ring_with_a_title_When_asked_Then_the_title_and_no_abilities()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("ring", "type 70\nweight 10\nnrof 1\nPow 3\ntitle of War\nidentified 1\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("ring of War", name);
    }

    [Fact]
    public void QueryShortName_Given_a_skill_tool_that_is_identified_When_asked_Then_no_adornment_text()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("lockpicks", "type 43\nweight 10\nnrof 1\nsp 1\nidentified 1\n");

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal("lockpicks", name);
    }

    [Fact]
    public void QueryShortName_Given_a_name_that_leaves_no_room_for_the_title_When_asked_Then_the_text_is_cut_and_the_length_restarts_at_one()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("x", "type 15\nweight 10\nnrof 1\ntitle of Doom\nidentified 1\nmagic 2\n");
        op.Name = new string('a', 4090);

        // Act
        string name = w.QueryShortName(op);

        // Assert: " " and "of D" fill the buffer, the length goes back to 1 and " +2" is written from character 1 on
        Assert.Equal("a +2", name);
    }

    [Fact]
    public void QueryShortName_Given_a_name_that_fills_the_buffer_When_a_title_is_added_Then_nothing_more_is_appended()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("x", "type 15\nweight 10\nnrof 1\ntitle of Doom\nidentified 1\nmagic 2\n");
        op.Name = new string('a', 4094);

        // Act
        string name = w.QueryShortName(op);

        // Assert
        Assert.Equal(new string('a', 4094) + " ", name);
    }

    [Fact]
    public void SafeStrcat_Given_a_full_buffer_When_appended_Then_nothing_happens()
    {
        // Arrange
        string buf = new string('x', 7);
        int len = 7;

        // Act
        CfWorld.SafeStrcat(ref buf, "more", ref len, 8);

        // Assert
        Assert.Equal("xxxxxxx", buf);
        Assert.Equal(7, len);
    }

    [Fact]
    public void SafeStrcat_Given_a_text_that_does_not_fit_When_appended_Then_it_is_cut_and_the_length_is_one()
    {
        // Arrange
        string buf = "abc";
        int len = 3;

        // Act
        CfWorld.SafeStrcat(ref buf, "defghij", ref len, 8);

        // Assert
        Assert.Equal("abcdefg", buf);
        Assert.Equal(1, len);
    }

    [Fact]
    public void SafeStrcat_Given_a_text_that_fits_When_appended_Then_the_length_grows_by_its_size()
    {
        // Arrange
        string buf = "abc";
        int len = 3;

        // Act
        CfWorld.SafeStrcat(ref buf, "de", ref len, 8);

        // Assert
        Assert.Equal("abcde", buf);
        Assert.Equal(5, len);
    }

    [Fact]
    public void QueryName_Given_an_item_in_every_state_When_asked_Then_the_states_follow_in_the_order_of_the_C_code()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\nweight 10\nnrof 1\ninv_locked 1\nknown_cursed 1\ndamned 1\ncursed 1\nknown_magical 1\napplied 1\nunpaid 1\n");

        // Act
        string name = w.QueryName(op);

        // Assert
        Assert.Equal("sword * (damned) (magic) (wielded) (unpaid)", name);
    }

    [Fact]
    public void QueryName_Given_a_known_cursed_item_that_is_not_damned_When_asked_Then_cursed()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\nweight 10\nnrof 1\nknown_cursed 1\ncursed 1\n");

        // Act
        string name = w.QueryName(op);

        // Assert
        Assert.Equal("sword (cursed)", name);
    }

    [Fact]
    public void QueryName_Given_a_cursed_item_that_is_not_known_cursed_When_asked_Then_no_curse_text()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\nweight 10\nnrof 1\ncursed 1\n");

        // Act
        string name = w.QueryName(op);

        // Assert
        Assert.Equal("sword", name);
    }

    [Fact]
    public void QueryName_Given_an_item_known_magical_but_identified_When_asked_Then_no_magic_text()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\nweight 10\nnrof 1\nknown_magical 1\nidentified 1\n");

        // Act
        string name = w.QueryName(op);

        // Assert
        Assert.Equal("sword", name);
    }

    [Theory]
    [InlineData(Define.BOW, " (readied)")]
    [InlineData(Define.WAND, " (readied)")]
    [InlineData(Define.ROD, " (readied)")]
    [InlineData(Define.HORN, " (readied)")]
    [InlineData(Define.WEAPON, " (wielded)")]
    [InlineData(Define.ARMOUR, " (worn)")]
    [InlineData(Define.HELMET, " (worn)")]
    [InlineData(Define.SHIELD, " (worn)")]
    [InlineData(Define.RING, " (worn)")]
    [InlineData(Define.BOOTS, " (worn)")]
    [InlineData(Define.GLOVES, " (worn)")]
    [InlineData(Define.AMULET, " (worn)")]
    [InlineData(Define.GIRDLE, " (worn)")]
    [InlineData(Define.BRACERS, " (worn)")]
    [InlineData(Define.CLOAK, " (worn)")]
    [InlineData(Define.SKILL, " (applied)")]
    [InlineData(Define.FOOD, " (applied)")]
    public void QueryName_Given_an_applied_item_When_asked_Then_the_word_of_its_type(int type, string expected)
    {
        // Arrange: the type has no effect on the name itself (no weight, no magic, nothing identified)
        CfWorld w = T.World(out _, new ItemHost());
        CfObject op = w.GetObject();
        op.Name = "thing";
        op.NamePl = "things";
        op.Type = (byte)type;
        op.Nrof = 1;
        op.SetFlag(Define.FLAG_APPLIED);

        // Act
        string name = w.QueryName(op);

        // Assert
        Assert.Equal("thing" + expected, name);
    }

    [Fact]
    public void QueryName_Given_a_container_that_is_open_in_a_player_When_asked_Then_open_is_added()
    {
        // Arrange
        CfWorld w = T.World(out _, new ItemHost());
        CfObject pl = w.GetObject();
        pl.Name = "player";
        CfObject bag = w.GetObject();
        bag.Name = "bag";
        bag.Type = Define.CONTAINER;
        bag.Env = pl;
        pl.Container = bag;

        // Act
        string name = w.QueryName(bag);

        // Assert
        Assert.Equal("bag (open)", name);
    }

    [Fact]
    public void QueryName_Given_an_applied_container_on_the_floor_When_asked_Then_open_and_active()
    {
        // Arrange
        CfWorld w = T.World(out _, new ItemHost());
        CfObject bag = w.GetObject();
        bag.Name = "bag";
        bag.Type = Define.CONTAINER;
        bag.SetFlag(Define.FLAG_APPLIED);

        // Act
        string name = w.QueryName(bag);

        // Assert
        Assert.Equal("bag (open) (active)", name);
    }

    [Fact]
    public void QueryName_Given_a_container_that_is_not_open_When_asked_Then_no_open_text()
    {
        // Arrange
        CfWorld w = T.World(out _, new ItemHost());
        CfObject pl = w.GetObject();
        CfObject bag = w.GetObject();
        bag.Name = "bag";
        bag.Type = Define.CONTAINER;
        bag.Env = pl;

        // Act
        string name = w.QueryName(bag);

        // Assert
        Assert.Equal("bag", name);
    }

    [Fact]
    public void QueryBaseName_Given_a_stack_When_plural_is_asked_Then_the_plural_name_without_state()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("arrow", "type 13\nweight 2\nnrof 5\nmagic 1\nidentified 1\nunpaid 1\napplied 1\n");

        // Act
        string singular = w.QueryBaseName(op, 0);
        string plural = w.QueryBaseName(op, 1);

        // Assert
        Assert.Equal("arrow +1", singular);
        Assert.Equal("arrows +1", plural);
    }

    [Fact]
    public void QueryBaseName_Given_a_plain_item_When_plural_is_asked_Then_the_singular_name_as_in_C()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("rock", "type 0\n");

        // Act
        string plural = w.QueryBaseName(op, 1);

        // Assert
        Assert.Equal("rock", plural);
    }

    [Fact]
    public void QueryBaseName_Given_a_missing_name_When_asked_Then_null_in_parentheses()
    {
        // Arrange
        CfWorld w = T.World(out _, new ItemHost());
        CfObject op = w.GetObject();
        op.Name = "x";

        // Act
        string singular = w.QueryBaseName(op, 0);
        string plural = w.QueryBaseName(op, 1);

        // Assert
        Assert.Equal("x", singular);
        Assert.Equal("(null)", plural);
    }

    [Fact]
    public void QueryBaseName_Given_identified_spell_items_and_rings_When_asked_Then_like_the_short_name()
    {
        // Arrange
        (CfWorld w1, CfObject scroll) = Item("scroll", "type 111\nweight 10\nnrof 1\nsp 1\nlevel 4\nidentified 1\n");
        (CfWorld w2, CfObject book) = Item("book", "type 85\nweight 10\nnrof 1\nsp 2\nidentified 1\n");
        (CfWorld w3, CfObject ring) = Item("ring", "type 70\nweight 10\nnrof 1\nStr 1\nidentified 1\n");
        (CfWorld w4, CfObject titled) = Item("ring", "type 70\nweight 10\nnrof 1\nStr 1\nidentified 1\ntitle of War\n");

        // Act
        string[] names =
        {
            w1.QueryBaseName(scroll, 0), w1.QueryBaseName(scroll, 1), w2.QueryBaseName(book, 0), w3.QueryBaseName(ring, 0), w4.QueryBaseName(titled, 1),
        };

        // Assert
        Assert.Equal(new[] { "scroll of fireball (lvl 4)", "scrolls of fireball (lvl 4)", "book of heal", "ring (Str+1)", "rings of War" }, names);
    }

    [Fact]
    public void QueryBaseName_Given_a_slaying_spellbook_When_identified_Then_the_slaying_is_the_spell()
    {
        // Arrange
        (CfWorld w, CfObject book) = Item("book", "type 85\nweight 10\nnrof 1\nslaying Barrier\nidentified 1\n");

        // Act
        string name = w.QueryBaseName(book, 0);

        // Assert
        Assert.Equal("book of Barrier", name);
    }

    [Fact]
    public void QueryBaseName_Given_a_name_of_the_buffer_size_When_a_title_is_added_Then_the_text_is_cut_to_255_characters()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("x", "type 15\nweight 10\nnrof 1\ntitle of Doom\nidentified 1\n");
        op.Name = new string('b', 250);

        // Act
        string name = w.QueryBaseName(op, 0);

        // Assert
        Assert.Equal(255, name.Length);
        Assert.EndsWith(" of D", name);
    }

    // ---- ring_desc, describe_resistance

    [Fact]
    public void RingDesc_Given_an_identified_ring_with_everything_When_described_Then_every_piece_in_the_order_of_the_C_code()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("ring",
            "type 70\nidentified 1\nStr 1\nDex 2\nCon 3\nWis 4\nCha 5\nInt 6\nPow 7\nexp 8\nwc 9\ndam 10\nac -11\nresist_fire 30\nresist_cold -20\n"
            + "food 12\ngrace 13\nsp 14\nhp 15\nluck 16\nlifesave 1\nreflect_spell 1\nreflect_missile 1\nstealth 1\npath_attuned 5\npath_repelled 2\npath_denied 524288\n");

        // Act
        string text = w.RingDesc(op);

        // Assert
        Assert.Equal(
            "(Str+1)(Dex+2)(Con+3)(Wis+4)(Cha+5)(Int+6)(Pow+7)(speed +8)(wc+9)(dam+10)(ac-11)(resist fire +30)(resist cold -20)(sustenance+12)(grace+13)(magic+14)(regeneration+15)(luck+16)"
            + "(lifesaving)(reflect spells)(reflect missiles)(stealth)(Attuned: Protection, Frost)(Repelled: Fire)(Denied: Light)",
            text);
    }

    [Fact]
    public void RingDesc_Given_a_skill_with_magic_When_described_Then_no_magic_piece_and_no_adornment()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("skill", "type 43\nidentified 1\nsp 5\n");

        // Act
        string text = w.RingDesc(op);

        // Assert
        Assert.Equal("", text);
    }

    [Fact]
    public void RingDesc_Given_every_spell_path_in_three_lists_When_described_Then_each_list_names_all_twenty_paths()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("ring", "type 70\nidentified 1\npath_attuned 1048575\npath_repelled 1048575\npath_denied 1048575\n");
        string all = string.Join(", ", Tables.Spellpathnames);

        // Act
        string text = w.RingDesc(op);

        // Assert
        Assert.Equal("(Attuned: " + all + ")(Repelled: " + all + ")(Denied: " + all + ")", text);
    }

    [Fact]
    public void DescribeResistance_Given_resistances_When_called_with_newline_Then_lines_without_signs()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("cloak", "type 87\nresist_fire 30\nresist_magic -10\n");

        // Act
        string lines = w.DescribeResistance(op, true);
        string parens = w.DescribeResistance(op, false);

        // Assert
        Assert.Equal("resist magic -10\nresist fire 30\n", lines);
        Assert.Equal("(resist magic -10)(resist fire +30)", parens);
    }

    [Fact]
    public void DescribeResistance_Given_flesh_When_called_Then_only_the_dragon_resistances()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("steak", "type 72\nresist_fire 30\nresist_physical 10\nresist_drain 5\n");

        // Act
        string text = w.DescribeResistance(op, false);

        // Assert
        Assert.Equal("(resist fire +30)", text);
    }

    // ---- describe_item

    [Fact]
    public void DescribeItem_Given_an_identified_weapon_When_described_Then_the_whole_list_in_order()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword",
            "type 15\nidentified 1\nStr 2\nexp 1\nwc 3\ndam 4\nac -1\nlevel 4\nlast_eat 2\nlast_sp 10\nmagic 2\nxrays 1\nflying 1\ncan_see_in_dark 1\n"
            + "luck 3\nlifesave 1\nreflect_spell 1\nreflect_missile 1\nstealth 1\nslaying orc\nattacktype 5\nresist_fire 10\n"
            + "path_attuned 1\nfood 7\nsp 8\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert: weapon speed is (10*2-2)/2 = 9; food, grace, sp and hp of a weapon are shown after the weapon speed
        Assert.Equal(
            "(Str+2)(speed +1)(wc+3)(dam+4)(ac-1)(improved 2/4)(xray-vision)(levitate)(infravision)(weapon speed 9)(sustenance+7)(magic+8)"
            + "(luck+3)(lifesaving)(reflect spells)(reflect missiles)(stealth)(slay orc)(Attacks: physical, fire)(resist fire +10)(Attuned: Protection)",
            text);
    }

    [Fact]
    public void DescribeItem_Given_a_weapon_that_is_slower_than_its_magic_When_described_Then_the_speed_is_not_negative()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\nidentified 1\nlast_sp 1\nmagic 5\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("(weapon speed 0)", text);
    }

    [Fact]
    public void DescribeItem_Given_an_unidentified_weapon_When_described_Then_nothing_is_known()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\nStr 2\nwc 3\nlast_sp 10\nslaying orc\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("", text);
    }

    [Fact]
    public void DescribeItem_Given_an_unidentified_weapon_that_has_been_applied_When_described_Then_the_bonuses_but_not_the_rest()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\nbeen_applied 1\nStr 2\nwc 3\nlast_sp 10\nslaying orc\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("(Str+2)(wc+3)", text);
    }

    [Fact]
    public void DescribeItem_Given_identified_armour_When_described_Then_speed_and_spell_penalty()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("plate", "type 16\nidentified 1\nac 5\nlast_sp 25\nlast_heal 3\nfood 2\ngrace 1\nhp 4\nresist_physical 10\nresist_fire 5\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("(ac+5)(Max speed 2.50)(Spell regen penalty 3)(sustenance+2)(grace+1)(regeneration+4)(armour +10)(resist fire +5)", text);
    }

    [Fact]
    public void DescribeItem_Given_armour_without_speed_penalty_When_described_Then_only_the_spell_penalty()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("helmet", "type 34\nidentified 1\nlast_heal 0\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("(Spell regen penalty 0)", text);
    }

    [Theory]
    [InlineData(5, 1, "(Max speed 0.50)(Spell regen penalty 0)")]
    [InlineData(25, 1, "(Max speed 2.50)(Spell regen penalty 0)")]
    [InlineData(1, 1, "(Max speed 0.10)(Spell regen penalty 0)")]
    [InlineData(125, 1, "(Max speed 12.50)(Spell regen penalty 0)")]
    public void DescribeItem_Given_armour_speeds_When_described_Then_the_C_rounding_of_two_decimals(int lastSp, int identified, string expected)
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("plate", "type 16\nidentified " + identified + "\nlast_sp " + lastSp + "\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void DescribeItem_Given_identified_food_When_described_Then_food_and_what_it_heals()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("apple", "type 6\nidentified 1\nfood 40\nhp 2\nsp 1\n");
        (CfWorld w2, CfObject bad) = Item("apple", "type 6\nidentified 1\nfood 40\nhp 2\nsp 1\ncursed 1\n");

        // Act
        string good = w.DescribeItem(op);
        string cursed = w2.DescribeItem(bad);

        // Assert
        Assert.Equal("(food+40)(heals)(spellpoint regen)", good);
        Assert.Equal("(food+40)(damages)(spellpoint depletion)", cursed);
    }

    [Fact]
    public void DescribeItem_Given_flesh_of_a_dragon_attack_When_described_Then_the_metabolism()
    {
        // Arrange: last_eat 2 is fire
        (CfWorld w, CfObject op) = Item("steak", "type 72\nidentified 1\nfood 10\nlast_eat 2\nresist_fire 30\n");
        (CfWorld w2, CfObject other) = Item("steak", "type 72\nidentified 1\nfood 10\nlast_eat 7\n");
        (CfWorld w3, CfObject quetzal) = Item("steak", "type 72\nidentified 1\nfood 10\nlast_eat 2\nresist_fire 30\nsee_invisible 1\n");

        // Act
        string text = w.DescribeItem(op);
        string plain = w2.DescribeItem(other);
        string seen = w3.DescribeItem(quetzal);

        // Assert
        Assert.Equal("(food+10)(fire metabolism)", text);
        Assert.Equal("(food+10)", plain);
        Assert.Equal("(food+10)(fire metabolism)(resist fire +30)", seen);
    }

    [Fact]
    public void DescribeItem_Given_an_unidentified_food_When_described_Then_nothing()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("apple", "type 6\nfood 40\nhp 2\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("", text);
    }

    [Fact]
    public void DescribeItem_Given_a_wand_When_identified_Then_stats_but_no_charges_text()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("wand", "type 109\nidentified 1\nfood 5\nsp 1\nluck 1\nresist_cold 5\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("(luck+1)(resist cold +5)", text);
    }

    [Fact]
    public void DescribeItem_Given_a_ring_When_it_has_a_title_Then_the_ring_description_else_nothing()
    {
        // Arrange
        (CfWorld w, CfObject titled) = Item("ring", "type 70\nidentified 1\nStr 2\ntitle of Power\n");
        (CfWorld w2, CfObject plain) = Item("ring", "type 70\nidentified 1\nStr 2\n");

        // Act
        string a = w.DescribeItem(titled);
        string b = w2.DescribeItem(plain);

        // Assert
        Assert.Equal("(Str+2)", a);
        Assert.Equal("", b);
    }

    [Fact]
    public void DescribeItem_Given_an_item_of_another_type_When_described_Then_nothing()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("key", "type 24\nidentified 1\nStr 2\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("", text);
    }

    [Fact]
    public void DescribeItem_Given_a_force_or_an_arrow_When_identified_Then_the_combat_values_without_more_info()
    {
        // Arrange
        (CfWorld w, CfObject arrow) = Item("arrow", "type 13\nidentified 1\nwc 1\ndam 2\nfood 9\n");

        // Act
        string text = w.DescribeItem(arrow);

        // Assert
        Assert.Equal("(wc+1)(dam+2)", text);
    }

    [Theory]
    [InlineData("speed 0.0\n", "")]
    [InlineData("speed 0.05\n", "(very slow movement)")]
    [InlineData("speed 0.1\n", "(slow movement)")]
    [InlineData("speed 0.15\n", "(normal movement)")]
    [InlineData("speed 0.25\n", "(fast movement)")]
    [InlineData("speed 0.3\n", "(fast movement)")]
    [InlineData("speed 0.35\n", "(very fast movement)")]
    [InlineData("speed 0.4\n", "(very fast movement)")]
    [InlineData("speed 0.5\n", "(extremely fast movement)")]
    [InlineData("speed 0.7\n", "(extremely fast movement)")]
    [InlineData("speed 0.8\n", "(lightning fast movement)")]
    [InlineData("speed -0.15\n", "(normal movement)")]
    public void DescribeItem_Given_a_monster_with_a_speed_When_described_Then_the_word_of_its_range(string speed, string expected)
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("orc", "alive 1\nmonster 1\n" + speed);

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void DescribeItem_Given_a_monster_with_every_ability_When_described_Then_the_order_of_the_C_code()
    {
        // Arrange
        var host = new ItemHost();
        host.Abilities["spells"] = new[] { "firebolt", "heal" };
        (CfWorld w, CfObject op) = Item("mage",
            "monster 1\nspeed 0.15\nundead 1\ncan_pass_thru 1\nsee_invisible 1\ncan_use_weapon 1\ncan_use_bow 1\ncan_use_armour 1\ncan_use_ring 1\n"
            + "can_use_scroll 1\ncan_use_wand 1\ncan_use_rod 1\ncan_use_horn 1\ncan_use_skill 1\ncan_cast_spell 1\nfriendly 1\nunaggressive 1\nhitback 1\n"
            + "randomitems spells\npath_attuned 4\npath_repelled 8\npath_denied 16\n", host);

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal(
            "(normal movement)(undead)(pass through doors)(see invisible)(wield weapon)(archer)(wear armour)(wear ring)(read scroll)(fire wand)(use rod)(use horn)"
            + "(skill user)(spellcaster)(friendly)(unaggressive)(hitback)(Spell abilities:)(firebolt)(heal)(Attuned: Frost)(Repelled: Electricity)(Denied: Missiles)",
            text);
    }

    [Fact]
    public void DescribeItem_Given_a_player_When_described_Then_what_the_host_says_it_regenerates()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("hero", "type 1\n");
        op.Contr = new FakePlayer();

        // Act
        string text = w.DescribeItem(op);

        // Assert: digestion -2, grace +3, no spell point regeneration, hit points +1
        Assert.Equal("(sustenance-2)(grace+3)(regeneration+1)", text);
    }

    [Fact]
    public void DescribeItem_Given_a_dragon_player_When_described_Then_the_claws_of_its_clawing_skill()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("quetzal", "type 1\nrace dragon\nattacktype 1\n");
        op.Contr = new FakePlayer();
        CfObject claws = w.GetObject();
        claws.Type = Define.SKILL;
        claws.Name = "clawing";
        claws.Attacktype = 4 | 16;
        CfObject other = w.GetObject();
        other.Type = Define.SKILL;
        other.Name = "praying";
        other.Below = claws;
        op.Inv = other;

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("(sustenance-2)(grace+3)(regeneration+1)(Claws: fire, cold)", text);
    }

    [Fact]
    public void DescribeItem_Given_a_dragon_player_without_claws_When_described_Then_its_own_attacks()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("quetzal", "type 1\nrace dragon\nattacktype 1\n");
        op.Contr = new FakePlayer();

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("(sustenance-2)(grace+3)(regeneration+1)(Attacks: physical)", text);
    }

    [Fact]
    public void DescribeItem_Given_a_player_of_another_race_When_described_Then_no_attacks()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("human", "type 1\nrace human\nattacktype 1\n");

        // Act
        string text = w.DescribeItem(op);

        // Assert
        Assert.Equal("", text);
    }

    // ---- is_magical, need_identify

    [Theory]
    [InlineData("type 15\n", 0)]
    [InlineData("type 15\ntitle of Doom\n", 1)]
    [InlineData("type 15\ntitle of Doom\nalive 1\n", 0)]
    [InlineData("type 15\nmagic 1\n", 1)]
    [InlineData("type 70\nac 1\n", 1)]
    [InlineData("type 70\nfood 1\n", 1)]
    [InlineData("type 70\nexp 1\n", 1)]
    [InlineData("type 70\ndam 1\n", 1)]
    [InlineData("type 70\nwc 1\n", 1)]
    [InlineData("type 70\nsp 1\n", 1)]
    [InlineData("type 70\nhp 1\n", 1)]
    [InlineData("type 70\nluck 1\n", 1)]
    [InlineData("type 70\n", 0)]
    [InlineData("type 15\nac 1\n", 0)]
    [InlineData("type 99\nstealth 1\n", 1)]
    [InlineData("type 99\nflying 1\n", 1)]
    [InlineData("type 99\nexp 1\n", 1)]
    [InlineData("type 99\n", 0)]
    [InlineData("type 39\nreflect_spell 1\n", 1)]
    [InlineData("type 33\nreflect_missile 1\n", 1)]
    [InlineData("type 33\n", 0)]
    [InlineData("type 34\nxrays 1\n", 1)]
    [InlineData("type 16\nxrays 1\n", 0)]
    [InlineData("type 5\n", 1)]
    [InlineData("type 3\n", 1)]
    [InlineData("type 109\nfood 1\n", 1)]
    [InlineData("type 109\n", 0)]
    [InlineData("type 16\nresist_fire 1\n", 1)]
    [InlineData("type 16\nresist_physical 1\n", 0)]
    [InlineData("type 15\nresist_physical 1\n", 1)]
    [InlineData("type 34\nresist_physical 1\n", 0)]
    [InlineData("type 33\nresist_physical 1\n", 0)]
    [InlineData("type 99\nresist_physical 1\n", 0)]
    [InlineData("type 100\nresist_physical 1\n", 0)]
    [InlineData("type 156\n", 1)]
    [InlineData("type 85\n", 1)]
    [InlineData("type 111\n", 1)]
    [InlineData("type 113\n", 1)]
    [InlineData("type 24\nCha 1\n", 1)]
    public void IsMagical_Given_an_item_When_asked_Then_the_rules_of_the_C_code(string fields, int expected)
    {
        // Arrange
        (CfWorld _, CfObject op) = Item("thing", fields);

        // Act
        int magical = CfWorld.IsMagical(op);

        // Assert
        Assert.Equal(expected, magical);
    }

    [Theory]
    [InlineData(Define.RING, 1)]
    [InlineData(Define.WAND, 1)]
    [InlineData(Define.FLESH, 1)]
    [InlineData(Define.BOOK, 1)]
    [InlineData(Define.CLOAK, 1)]
    [InlineData(Define.GEM, 1)]
    [InlineData(Define.POISON, 1)]
    [InlineData(Define.MONEY, 0)]
    [InlineData(Define.DOOR, 0)]
    [InlineData(Define.PLAYER, 0)]
    [InlineData(Define.POTION, 1)]
    public void NeedIdentify_Given_a_type_When_asked_Then_the_list_of_the_C_code(int type, int expected)
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject op = w.GetObject();
        op.Type = (byte)type;

        // Act
        int need = CfWorld.NeedIdentify(op);

        // Assert
        Assert.Equal(expected, need);
    }

    // ---- the numbers and the weight

    [Theory]
    [InlineData(0, "zeroth")]
    [InlineData(1, "first")]
    [InlineData(3, "third")]
    [InlineData(18, "eighteen")]
    [InlineData(20, "twentieth")]
    [InlineData(21, "twentyfirst")]
    [InlineData(30, "thirtieth")]
    [InlineData(44, "fourtyfourth")]
    [InlineData(99, "ninetyninth")]
    [InlineData(100, "100.")]
    [InlineData(250, "250.")]
    [InlineData(-1, "")]
    public void GetLevelnumber_Given_a_number_When_asked_Then_the_ordinal_of_the_C_tables(int n, string expected)
    {
        // Arrange / Act
        string text = CfWorld.GetLevelnumber(n);

        // Assert
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(0, "no")]
    [InlineData(1, "")]
    [InlineData(2, "two")]
    [InlineData(20, "twenty")]
    [InlineData(21, "21")]
    [InlineData(-3, "")]
    public void GetNumber_Given_a_number_When_asked_Then_the_word_or_the_digits(int n, string expected)
    {
        // Arrange / Act
        string text = CfWorld.GetNumber(n);

        // Assert
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(2500, 0, 1u, "   2.5")]
    [InlineData(3000, 0, 1u, "   3  ")]
    [InlineData(250, 0, 1u, "   0.2")]
    [InlineData(750, 0, 1u, "   0.8")]
    [InlineData(1250, 0, 1u, "   1.2")]
    [InlineData(1750, 0, 1u, "   1.8")]
    [InlineData(100, 2000, 0u, "   2.1")]
    [InlineData(1500, 0, 3u, "   4.5")]
    [InlineData(-1, 0, 0u, "      ")]
    [InlineData(123456, 0, 1u, " 123.5")]
    [InlineData(5000, 0, 20u, " 100  ")]
    public void QueryWeight_Given_a_weight_When_asked_Then_the_C_format(int weight, int carrying, uint nrof, string expected)
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject op = w.GetObject();
        op.Weight = weight;
        op.Carrying = carrying;
        op.Nrof = nrof;

        // Act
        string text = CfWorld.QueryWeight(op);

        // Assert
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(0.25, 1, "0.2")]
    [InlineData(0.75, 1, "0.8")]
    [InlineData(0.125, 2, "0.12")]
    [InlineData(0.375, 2, "0.38")]
    [InlineData(2.5, 0, "2")]
    [InlineData(3.5, 0, "4")]
    [InlineData(0.05, 1, "0.1")]
    [InlineData(1234.5678, 2, "1234.57")]
    [InlineData(0.0, 1, "0.0")]
    [InlineData(-0.25, 1, "-0.2")]
    [InlineData(1e22, 1, "10000000000000000000000.0")]
    [InlineData(0.001, 1, "0.0")]
    public void FormatFixed_Given_a_double_When_formatted_Then_the_exact_value_is_rounded_with_ties_to_even(double value, int precision, string expected)
    {
        // Arrange / Act
        string text = CStr.FormatFixed(value, precision);

        // Assert
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(5, "+5")]
    [InlineData(0, "+0")]
    [InlineData(-5, "-5")]
    public void Plus_Given_a_number_When_formatted_Then_it_has_a_sign(int n, string expected)
    {
        // Arrange / Act
        string text = CStr.Plus(n);

        // Assert
        Assert.Equal(expected, text);
    }

    // ---- identify, look_up_spell_name

    [Fact]
    public void LookUpSpellName_Given_a_name_When_asked_Then_its_number_or_minus_one()
    {
        // Arrange
        CfWorld w = T.World(out _, new ItemHost());

        // Act
        int fireball = w.LookUpSpellName("fireball");
        int none = w.LookUpSpellName("nothing");

        // Assert
        Assert.Equal(1, fireball);
        Assert.Equal(-1, none);
    }

    [Fact]
    public void Identify_Given_a_cursed_unidentified_magic_item_When_identified_Then_flags_are_set_and_cleared()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\ncursed 1\nknown_magical 1\nno_skill_ident 1\n");

        // Act
        w.Identify(op);

        // Assert
        Assert.True(op.QueryFlag(Define.FLAG_IDENTIFIED));
        Assert.False(op.QueryFlag(Define.FLAG_KNOWN_MAGICAL));
        Assert.False(op.QueryFlag(Define.FLAG_NO_SKILL_IDENT));
        Assert.True(op.QueryFlag(Define.FLAG_KNOWN_CURSED));
    }

    [Fact]
    public void Identify_Given_a_damned_item_that_is_not_cursed_When_identified_Then_it_is_known_cursed()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\ndamned 1\n");

        // Act
        w.Identify(op);

        // Assert
        Assert.True(op.QueryFlag(Define.FLAG_KNOWN_CURSED));
    }

    [Fact]
    public void Identify_Given_an_item_that_is_not_cursed_When_identified_Then_it_is_not_known_cursed()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("sword", "type 15\n");

        // Act
        w.Identify(op);

        // Assert
        Assert.False(op.QueryFlag(Define.FLAG_KNOWN_CURSED));
    }

    [Fact]
    public void Identify_Given_a_potion_When_identified_Then_it_takes_the_name_of_its_archetype()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object potion_of_health\nname potion of health\ntype 5\nend\n", out _, new ItemHost());
        CfObject op = w.GetArchetype("potion_of_health");
        op.Name = "potion";

        // Act
        w.Identify(op);

        // Assert
        Assert.Equal("potion of health", op.Name);
    }

    [Fact]
    public void Identify_Given_a_spellbook_with_a_known_spell_When_identified_Then_the_spell_is_set_and_the_slaying_cleared()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("book", "type 85\nslaying heal\n");

        // Act
        w.Identify(op);

        // Assert
        Assert.Equal(2, op.Stats.Sp);
        Assert.Null(op.Slaying);
        Assert.Equal("book", op.Name);
    }

    [Fact]
    public void Identify_Given_a_spellbook_with_an_unknown_spell_When_identified_Then_the_name_is_a_formula_for_it()
    {
        // Arrange
        (CfWorld w, CfObject op) = Item("book", "type 85\nslaying zork\n");

        // Act
        w.Identify(op);

        // Assert
        Assert.Equal(-1, op.Stats.Sp);
        Assert.Equal("Spell forumla for zork", op.Name);
        Assert.Equal("zork", op.Slaying);
    }

    [Fact]
    public void Identify_Given_an_item_in_a_player_When_identified_Then_the_client_gets_the_item()
    {
        // Arrange
        var host = new ItemHost();
        (CfWorld w, CfObject op) = Item("sword", "type 15\n", host);
        CfObject pl = w.GetObject();
        pl.Name = "hero";
        pl.Type = Define.PLAYER;
        pl.Contr = new FakePlayer();
        op.Env = pl;

        // Act
        w.Identify(op);

        // Assert
        Assert.Equal(new[] { "send:hero:sword" }, host.Calls);
    }

    [Fact]
    public void Identify_Given_an_item_on_a_map_When_identified_Then_the_face_is_updated()
    {
        // Arrange
        var host = new ItemHost();
        (CfWorld w, CfObject op) = Item("sword", "type 15\n", host);
        op.Map = new FakeMap();

        // Act
        w.Identify(op);

        // Assert
        Assert.Equal(new[] { "update:sword:" + Define.UP_OBJ_FACE }, host.Calls);
    }

    // ---- the support functions

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(99, 9)]
    [InlineData(100, 10)]
    [InlineData(-5, 0)]
    [InlineData(25000, 158)]
    public void Isqrt_Given_a_number_When_asked_Then_the_integer_square_root(int n, int expected)
    {
        // Arrange / Act
        int root = CfWorld.Isqrt(n);

        // Assert
        Assert.Equal(expected, root);
    }

    [Fact]
    public void FindSkill_Given_a_visible_and_an_invisible_skill_When_asked_Then_the_visible_one_else_the_last_invisible()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject pl = w.GetObject();
        CfObject s1 = w.GetObject();
        s1.Type = Define.SKILL;
        s1.Stats.Sp = Define.SK_BARGAINING;
        s1.Invisible = 1;
        CfObject s2 = w.GetObject();
        s2.Type = Define.SKILL;
        s2.Stats.Sp = Define.SK_BARGAINING;
        s2.Invisible = 1;
        CfObject s3 = w.GetObject();
        s3.Type = Define.SKILL;
        s3.Stats.Sp = Define.SK_BARGAINING;
        s1.Below = s2;
        s2.Below = s3;
        pl.Inv = s1;

        // Act
        CfObject? visible = CfWorld.FindSkill(pl, Define.SK_BARGAINING);
        s3.Invisible = 1;
        CfObject? lastInvisible = CfWorld.FindSkill(pl, Define.SK_BARGAINING);
        CfObject? none = CfWorld.FindSkill(pl, Define.SK_HIDING);

        // Assert
        Assert.Same(s3, visible);
        Assert.Same(s3, lastInvisible);
        Assert.Null(none);
    }

    [Fact]
    public void GetAttrValue_Given_a_stat_number_When_asked_Then_the_stat_and_zero_for_other_numbers()
    {
        // Arrange
        var stats = new Living { Str = 1, Dex = 2, Con = 3, Wis = 4, Cha = 5, Int = 6, Pow = 7 };

        // Act
        var values = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            values.Add(CfWorld.GetAttrValue(stats, i));
        }

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 0 }, values);
    }

    [Fact]
    public void IsDragonPl_Given_players_and_others_When_asked_Then_only_a_player_of_the_race_dragon()
    {
        // Arrange
        (CfWorld w, CfObject dragon) = Item("d", "type 1\nrace dragon\n");
        (CfWorld w2, CfObject human) = Item("h", "type 1\nrace human\n");
        (CfWorld w3, CfObject monster) = Item("m", "type 80\nrace dragon\n");

        // Act
        int[] results = { CfWorld.IsDragonPl(dragon), CfWorld.IsDragonPl(human), CfWorld.IsDragonPl(monster), CfWorld.IsDragonPl(null) };

        // Assert
        Assert.Equal(new[] { 1, 0, 0, 0 }, results);
        _ = (w, w2, w3);
    }

    [Theory]
    [InlineData(Define.ATNR_MAGIC, 1)]
    [InlineData(Define.ATNR_FIRE, 1)]
    [InlineData(Define.ATNR_ELECTRICITY, 1)]
    [InlineData(Define.ATNR_COLD, 1)]
    [InlineData(Define.ATNR_ACID, 1)]
    [InlineData(Define.ATNR_POISON, 1)]
    [InlineData(Define.ATNR_PHYSICAL, 0)]
    [InlineData(Define.ATNR_DRAIN, 0)]
    public void AtnrIsDragonEnabled_Given_an_attack_number_When_asked_Then_the_six_of_the_dragon(int attack, int expected)
    {
        // Arrange / Act
        int enabled = CfWorld.AtnrIsDragonEnabled(attack);

        // Assert
        Assert.Equal(expected, enabled);
    }

    [Fact]
    public void ChaBonus_Given_the_table_When_read_Then_31_entries_from_ten_to_one_and_a_half()
    {
        // Arrange / Act
        float[] table = Tables.ChaBonus;

        // Assert
        Assert.Equal(Define.MAX_STAT + 1, table.Length);
        Assert.Equal(10.0f, table[0]);
        Assert.Equal(5.0f, table[6]);
        Assert.Equal(2.9f, table[11]);
        Assert.Equal(2.0f, table[20]);
        Assert.Equal(1.75f, table[25]);
        Assert.Equal(1.5f, table[30]);
    }

    // ---- Graal Kingdoms' archetypes (skipped when they are not on this machine)

    [GkFact]
    public void QueryName_Given_every_archetype_of_graal_kingdoms_When_asked_Then_no_name_fails_and_the_texts_are_not_empty()
    {
        // Arrange
        var w = new CfWorld(new ItemHost());
        ArchCollection collection = ArchCollector.Collect(GkFactAttribute.ArchFolder);
        w.InitArchetypes(collection.Archetypes);
        int named = 0;
        int described = 0;
        int priced = 0;
        int identifiedMagic = 0;

        // Act
        for (Archetype? at = w.FirstArchetype; at != null; at = at.More == null ? at.Next : at.More)
        {
            CfObject op = at.Clone;
            if (op.Name == null)
            {
                continue;
            }
            string name = w.QueryName(op);
            string baseName = w.QueryBaseName(op, 0);
            string desc = w.DescribeItem(op);
            op.SetFlag(Define.FLAG_IDENTIFIED);
            string full = w.QueryName(op);
            string fullDesc = w.DescribeItem(op);
            int cost = w.QueryCost(op, null, Define.F_TRUE);
            if (name.Length > 0 && baseName.Length > 0 && full.Length > 0)
            {
                named++;
            }
            if (desc.Length > 0 || fullDesc.Length > 0)
            {
                described++;
            }
            if (cost > 0)
            {
                priced++;
            }
            if (full.Contains(" +"))
            {
                identifiedMagic++;
            }
        }

        // Assert
        Assert.True(named > 1000, "named " + named);
        Assert.True(described > 100, "described " + described);
        Assert.True(priced > 100, "priced " + priced);
        Assert.True(identifiedMagic > 0, "magic " + identifiedMagic);
    }
}
