namespace Crossfire.Tests;

/// <summary>Tests of the port of common/treasure.c (the reader of the treasures file and the making of items). Not ported from a Crossfire file.</summary>
public class TreasureTests
{
    private const string Arc =
        "Object sword\nname sword\nname_pl swords\ntype 15\nweight 3000\nvalue 50\nend\n"
        + "Object sack\nname sack\ntype 122\nvalue 7\nend\n"
        + "Object arrow\nname arrow\nname_pl arrows\ntype 13\nnrof 5\nvalue 2\nend\n"
        + "Object goldcoin\nname gold coin\nname_pl gold coins\ntype 36\nvalue 10\nend\n"
        + "Object spark\nname spark\ntype 110\ninvisible 1\nend\n"
        + "Object knife\nname knife\ntype 15\nvalue 3\nis_thrown 1\nend\n"
        + "Object bolt\nname bolt\nname_pl bolts\ntype 13\nnrof 1\nvalue 1\nend\n"
        + "Object pair\nname pair\nname_pl pairs\ntype 13\nnrof 2\nvalue 6\nend\n"
        + "Object meat\nname meat\nname_pl meats\ntype 72\nweight 100\nvalue 4\nfood 10\nend\n";

    private static CfWorld World(string treasures, out List<string> log, CrossfireHost? host = null, params int[] random)
    {
        CfWorld w = T.World(out log, host);
        if (random.Length > 0)
        {
            int i = 0;
            w.Random = () => random[i++ % random.Length];
        }
        w.TreasureText = treasures;
        w.InitArchetypes(T.Empty + Arc);
        return w;
    }

    private static CfObject Holder(CfWorld w, byte type = 0)
    {
        CfObject o = w.GetObject();
        o.Name = "holder";
        o.Type = type;
        return o;
    }

    /// <summary>The names of an inventory from the last item put in to the first (insert_ob_in_ob puts each at the head).</summary>
    private static string[] InOrder(CfObject op)
    {
        var l = new List<string>();
        for (CfObject? c = op.Inv; c != null; c = c.Below)
        {
            l.Add(c.Name + (c.Nrof > 1 ? "x" + c.Nrof : ""));
        }
        l.Reverse();
        return l.ToArray();
    }

    // ---- the reader

    [Fact]
    public void LoadTreasureLists_Given_a_list_with_every_command_When_read_Then_the_elements_have_the_values_of_C_and_are_in_the_file_order()
    {
        // Arrange
        const string text = "# a comment\ntreasure things\n# inside\n  arch sword\n  chance 60\n  magic 3\n  nrof 4\n  change_name big\n  change_title of doom\n  change_slaying orcs\n  more\n"
            + "  list other\n  magic 7\n  end\ntreasure other\n  arch sack\n  end\n";

        // Act
        CfWorld w = World(text, out List<string> log);

        // Assert
        TreasureList[] lists = w.TreasureLists.ToArray();
        Assert.Equal(new[] { "things", "other" }, lists.Select(l => l.Name));
        Treasure first = lists[0].Items!;
        Assert.Equal("sword", first.Item!.Name);
        Assert.Equal(60, first.Chance);
        Assert.Equal(3, first.Magic);
        Assert.Equal(4, first.Nrof);
        Assert.Equal("big", first.ChangeName);
        Assert.Equal("of", first.ChangeTitle); // a word, as %s reads it
        Assert.Equal("orcs", first.ChangeSlaying);
        Treasure second = first.Next!;
        Assert.Equal("other", second.Name);
        Assert.Null(second.Item);
        Assert.Equal(100, second.Chance);
        Assert.Equal(7, second.Magic);
        Assert.Equal(0, lists[0].TotalChance);
        Assert.Equal(3, w.NrOfTreasures);
        Assert.DoesNotContain(log, l => l.StartsWith("Error"));
    }

    [Fact]
    public void LoadTreasureLists_Given_yes_and_no_branches_When_read_Then_they_are_nested_after_the_element_that_named_them()
    {
        // Arrange
        const string text = "treasure t\n  arch sword\n  chance 50\n  yes\n    arch sack\n    end\n  no\n    arch arrow\n    end\n  more\n  arch goldcoin\n  end\n";

        // Act
        CfWorld w = World(text, out _);

        // Assert
        Treasure t = w.TreasureLists.Single().Items!;
        Assert.Equal("sack", t.NextYes!.Item!.Name);
        Assert.Equal("arrow", t.NextNo!.Item!.Name);
        Assert.Equal("goldcoin", t.Next!.Item!.Name);
    }

    [Fact]
    public void LoadTreasureLists_Given_a_treasureone_list_When_read_Then_the_total_chance_is_the_sum_of_the_chances_and_yes_is_an_error()
    {
        // Arrange
        const string text = "treasureone pick\n  arch sword\n  chance 30\n  more\n  arch sack\n  chance 20\n  yes\n    arch arrow\n    end\n  more\n  arch goldcoin\n  end\n";

        // Act
        CfWorld w = World(text, out List<string> log);

        // Assert
        TreasureList tl = w.TreasureLists.Single();
        Assert.Equal(30 + 20 + 100, tl.TotalChance);
        Assert.Contains(log, l => l.Contains("Treasure pick is one item, but on treasure sack"));
    }

    [Fact]
    public void LoadTreasureLists_Given_faults_in_the_text_When_read_Then_each_is_logged_as_C_logs_it()
    {
        // Arrange
        const string text = "garbage here\ntreasure bad\n  arch nothere\n  frobnicate 3\n  more\n  chance 100\n  list missing\n  yes\n    arch sack\n    end\n  more\n  arch sword\n  end\n"
            + "treasure noend\n  arch sack\n";

        // Act
        CfWorld w = World(text, out List<string> log);

        // Assert
        Assert.Contains(log, l => l.Contains("Treasure-list didn't understand: garbage here"));
        Assert.Contains(log, l => l.Contains("Treasure lacks archetype: nothere"));
        Assert.Contains(log, l => l.Contains("Unknown treasure-command: 'frobnicate 3', last entry null"));
        Assert.Contains(log, l => l.Contains("Couldn't find treasurelist missing"));
        Assert.Contains(log, l => l.Contains("Treasurelist bad has element with no name or archetype"));
        Assert.Contains(log, l => l.Contains("Treasurelist bad has element that has 100% generation, next_yes field as well as next or next_no"));
        Assert.Contains(log, l => l.Contains("treasure lacks 'end'."));
    }

    [Fact]
    public void LoadTreasureLists_Given_a_word_command_without_its_word_When_read_Then_the_last_word_stays_as_C_leaves_it_in_variable()
    {
        // Arrange: sscanf returns EOF for "arch" alone, which is true, and variable still holds "sword"
        const string text = "treasure t\n  arch sword\n  more\n  arch sword\n  arch\n  end\n";

        // Act
        CfWorld w = World(text, out List<string> log);

        // Assert
        Assert.Equal("sword", w.TreasureLists.Single().Items!.Next!.Item!.Name);
        Assert.DoesNotContain(log, l => l.StartsWith("Error"));
    }

    [Fact]
    public void FindTreasurelist_Given_two_lists_of_one_name_When_asked_Then_the_first_is_the_one()
    {
        // Arrange
        CfWorld w = World("treasure twin\n  arch sword\n  end\ntreasure twin\n  arch sack\n  end\n", out _);

        // Act
        TreasureList? tl = w.FindTreasurelist("twin");

        // Assert
        Assert.Equal("sword", tl!.Items!.Item!.Name);
        Assert.Equal(2, w.TreasureLists.Count());
    }

    [Fact]
    public void FindTreasurelist_Given_a_name_that_is_not_there_When_asked_Then_null_and_the_error_of_C()
    {
        // Arrange
        CfWorld w = World("treasure a\n  arch sword\n  end\n", out List<string> log);
        log.Clear();

        // Act
        TreasureList? none = w.FindTreasurelist("none");
        TreasureList? missing = w.FindTreasurelist("nope");

        // Assert
        Assert.Null(none);
        Assert.Null(missing);
        Assert.Single(log, l => l.Contains("Couldn't find treasurelist nope"));
    }

    [Fact]
    public void FindTreasurelist_Given_no_treasures_text_When_asked_Then_the_host_answers_as_before()
    {
        // Arrange
        CfWorld w = T.WorldWith(Arc, out _, new TestHost { KnownLists = new HashSet<string> { "known" } });

        // Act
        TreasureList? known = w.FindTreasurelist("known");
        TreasureList? unknown = w.FindTreasurelist("unknown");

        // Assert
        Assert.Equal("known", known!.Name);
        Assert.Null(unknown);
        Assert.Empty(w.TreasureLists);
    }

    [Fact]
    public void LoadArchetypes_Given_a_treasures_text_When_an_archetype_names_a_list_Then_the_second_pass_links_the_list_that_was_read()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.TreasureText = "treasure kit\n  arch sword\n  end\n";

        // Act
        w.InitArchetypes(T.Empty + Arc + "Object hero\nname hero\ntype 1\nrandomitems kit\nend\n");

        // Assert
        Assert.Same(w.TreasureLists.Single(), w.GetArchetype("hero").Randomitems);
    }

    // ---- create_treasure

    [Fact]
    public void CreateTreasure_Given_a_list_of_sure_items_When_made_Then_each_is_in_the_inventory_in_the_order_of_the_list()
    {
        // Arrange
        CfWorld w = World("treasure kit\n  arch sword\n  more\n  arch sack\n  more\n  arch goldcoin\n  end\n", out _);
        CfObject pl = Holder(w);

        // Act
        w.CreateTreasure(w.FindTreasurelist("kit"), pl, 0, 1, 0);

        // Assert
        Assert.Equal(new[] { "sword", "sack", "gold coin" }, InOrder(pl));
    }

    [Fact]
    public void CreateTreasure_Given_a_chance_When_the_roll_is_below_or_not_Then_the_item_is_made_or_the_no_branch_runs()
    {
        // Arrange: RANDOM()%100+1 < chance makes the item: 10 < 11 yes, 11 < 11 no
        const string text = "treasure t\n  arch sword\n  chance 11\n  yes\n    arch sack\n    end\n  no\n    arch goldcoin\n    end\n  end\n";
        CfWorld made = World(text, out _, null, 9);
        CfWorld missed = World(text, out _, null, 10);
        CfObject a = Holder(made);
        CfObject b = Holder(missed);

        // Act
        made.CreateTreasure(made.FindTreasurelist("t"), a, 0, 1, 0);
        missed.CreateTreasure(missed.FindTreasurelist("t"), b, 0, 1, 0);

        // Assert
        Assert.Equal(new[] { "sword", "sack" }, InOrder(a));
        Assert.Equal(new[] { "gold coin" }, InOrder(b));
    }

    [Fact]
    public void CreateTreasure_Given_nrof_When_the_archetype_has_one_Then_1_to_nrof_are_made_and_a_stack_in_the_archetype_stays()
    {
        // Arrange
        const string text = "treasure t\n  arch sword\n  nrof 4\n  more\n  arch arrow\n  nrof 4\n  more\n  arch bolt\n  nrof 4\n  end\n";
        CfWorld w = World(text, out _, null, 2, 3, 3);
        CfObject pl = Holder(w);

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), pl, 0, 1, 0);

        // Assert: the sword takes RANDOM()%4+1 = 3, the arrows (nrof 5 in the archetype) are as they are
        Assert.Equal(new[] { "swordx3", "arrowx5", "boltx4" }, InOrder(pl));
    }

    [Fact]
    public void CreateTreasure_Given_a_treasureone_list_When_made_Then_the_item_the_roll_falls_on_is_made()
    {
        // Arrange: chances 30, 20, 50: a roll of 29 is the first, 30 the second, 49 the second, 50 the third
        const string text = "treasureone pick\n  arch sword\n  chance 30\n  more\n  arch sack\n  chance 20\n  more\n  arch goldcoin\n  chance 50\n  end\n";
        var names = new List<string>();

        // Act
        foreach (int roll in new[] { 29, 30, 49, 50 })
        {
            CfWorld w = World(text, out _, null, roll);
            CfObject pl = Holder(w);
            w.CreateTreasure(w.FindTreasurelist("pick"), pl, 0, 1, 0);
            names.Add(string.Join(",", InOrder(pl)));
        }

        // Assert
        Assert.Equal(new[] { "sword", "sack", "sack", "gold coin" }, names);
    }

    [Fact]
    public void CreateTreasure_Given_the_list_NONE_in_a_treasureone_When_rolled_Then_nothing_is_made()
    {
        // Arrange
        const string text = "treasureone pick\n  list NONE\n  chance 100\n  end\n";
        CfWorld w = World(text, out List<string> log, null, 5);
        CfObject pl = Holder(w);

        // Act
        w.CreateTreasure(w.FindTreasurelist("pick"), pl, 0, 1, 0);

        // Assert
        Assert.Null(pl.Inv);
        Assert.DoesNotContain(log, l => l.StartsWith("Error"));
    }

    [Fact]
    public void CreateTreasure_Given_the_list_NONE_in_a_list_of_all_When_made_Then_it_is_skipped_without_an_error_and_the_rest_is_made()
    {
        // Arrange
        CfWorld w = World("treasure t\n  list NONE\n  more\n  arch sword\n  end\n", out List<string> log);
        CfObject pl = Holder(w);
        log.Clear();

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), pl, 0, 1, 0);

        // Assert
        Assert.Equal(new[] { "sword" }, InOrder(pl));
        Assert.Empty(log);
    }

    [Fact]
    public void CreateTreasure_Given_a_list_transition_When_the_difficulty_is_below_magic_Then_it_is_not_followed_and_when_enough_it_is()
    {
        // Arrange
        const string text = "treasure outer\n  list inner\n  magic 3\n  end\ntreasure inner\n  arch sack\n  end\n";
        CfWorld w = World(text, out _);
        CfObject low = Holder(w);
        CfObject enough = Holder(w);

        // Act
        w.CreateTreasure(w.FindTreasurelist("outer"), low, 0, 2, 0);
        w.CreateTreasure(w.FindTreasurelist("outer"), enough, 0, 3, 0);

        // Assert
        Assert.Null(low.Inv);
        Assert.Equal(new[] { "sack" }, InOrder(enough));
    }

    [Fact]
    public void CreateTreasure_Given_a_treasureone_transition_the_difficulty_does_not_reach_When_made_Then_it_rolls_again_with_nrof_and_makes_nothing_without()
    {
        // Arrange: the first roll (0) falls on the transition, which the difficulty does not reach; the second (60) on the sack
        const string again = "treasureone pick\n  list inner\n  chance 50\n  magic 5\n  nrof 1\n  more\n  arch sack\n  chance 50\n  end\ntreasure inner\n  arch sword\n  end\n";
        const string once = "treasureone pick\n  list inner\n  chance 50\n  magic 5\n  more\n  arch sack\n  chance 50\n  end\ntreasure inner\n  arch sword\n  end\n";
        CfWorld a = World(again, out _, null, 0, 60);
        CfWorld b = World(once, out _, null, 0, 60);
        CfObject pa = Holder(a);
        CfObject pb = Holder(b);

        // Act
        a.CreateTreasure(a.FindTreasurelist("pick"), pa, 0, 1, 0);
        b.CreateTreasure(b.FindTreasurelist("pick"), pb, 0, 1, 0);

        // Assert
        Assert.Equal(new[] { "sack" }, InOrder(pa));
        Assert.Null(pb.Inv);
    }

    [Fact]
    public void CreateTreasure_Given_a_list_that_leads_to_itself_When_made_Then_it_stops_after_the_tries_C_allows_and_makes_one_item_per_round()
    {
        // Arrange
        const string text = "treasure loop\n  arch sack\n  more\n  list loop\n  end\n";
        CfWorld w = World(text, out _);
        CfObject pl = Holder(w);

        // Act
        w.CreateTreasure(w.FindTreasurelist("loop"), pl, 0, 1, 0);

        // Assert: tries 0..100 each make a sack, the 102nd call (tries 101) returns
        Assert.Equal(101, InOrder(pl).Length);
    }

    [Fact]
    public void CreateTreasure_Given_a_missing_list_When_made_Then_nothing_happens_where_C_would_read_through_null()
    {
        // Arrange
        CfWorld w = World("treasure a\n  arch sack\n  end\n", out _);
        CfObject pl = Holder(w);

        // Act
        w.CreateTreasure(null, pl, 0, 1, 0);

        // Assert
        Assert.Null(pl.Inv);
    }

    [Fact]
    public void CreateTreasure_Given_GT_INVISIBLE_When_made_Then_only_the_invisible_items_of_the_list_are_made()
    {
        // Arrange
        const string text = "treasure t\n  arch sword\n  more\n  arch spark\n  end\ntreasureone u\n  arch sword\n  chance 50\n  more\n  arch spark\n  chance 50\n  end\n";
        CfWorld w = World(text, out _, null, 70);
        CfObject all = Holder(w);
        CfObject one = Holder(w);

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), all, GT.INVISIBLE, 1, 0);
        w.CreateTreasure(w.FindTreasurelist("u"), one, GT.INVISIBLE, 1, 0);

        // Assert: the one-of list is rolled 70%100 = 70 -> the second element, an ability; with flag == GT_INVISIBLE the sword would be refused
        Assert.Equal(new[] { "spark" }, InOrder(all));
        Assert.Equal(new[] { "spark" }, InOrder(one));
    }

    [Fact]
    public void CreateOneTreasure_Given_a_roll_C_cannot_place_When_made_Then_a_fatal_error_like_abort()
    {
        // Arrange: a chance of 0 in a list that sums to 0 is not "one of" (total 0), so the list is built by hand with a total that is too big
        CfWorld w = World("treasureone pick\n  arch sword\n  chance 10\n  end\n", out _, null, 5);
        TreasureList tl = w.FindTreasurelist("pick")!;
        tl.TotalChance = 99; // the roll 50 is past the only element

        // Act
        w.Random = () => 50;

        // Assert
        Assert.Throws<CrossfireFatalException>(() => w.CreateOneTreasure(tl, Holder(w), 0, 1, 0));
    }

    [Fact]
    public void CreateTreasure_Given_change_commands_When_made_Then_the_item_has_the_name_title_and_slaying_of_the_list()
    {
        // Arrange
        CfWorld w = World("treasure t\n  arch sword\n  change_name Excalibur\n  change_title of_kings\n  change_slaying dragons\n  end\n", out _);
        CfObject pl = Holder(w);

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), pl, 0, 1, 0);

        // Assert
        Assert.Equal("Excalibur", pl.Inv!.Name);
        Assert.Equal("Excalibur", pl.Inv.NamePl);
        Assert.Equal("of_kings", pl.Inv.Title);
        Assert.Equal("dragons", pl.Inv.Slaying);
    }

    [Fact]
    public void CreateTreasure_Given_GT_STARTEQUIP_When_made_Then_single_items_get_the_flag_and_stacks_containers_and_thrown_items_lose_their_value_while_money_keeps_it()
    {
        // Arrange
        const string text = "treasure t\n  arch sword\n  more\n  arch arrow\n  more\n  arch goldcoin\n  more\n  arch sack\n  more\n  arch knife\n  more\n  arch pair\n  end\n";
        CfWorld w = World(text, out _);
        CfObject pl = Holder(w);

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), pl, GT.STARTEQUIP | GT.ONLY_GOOD, 1, 0);

        // Assert
        CfObject Item(string name) => Items(pl).First(i => i.Name == name);
        Assert.True(Item("sword").QueryFlag(Define.FLAG_STARTEQUIP));
        Assert.Equal(50, Item("sword").Value);
        Assert.False(Item("arrow").QueryFlag(Define.FLAG_STARTEQUIP));
        Assert.Equal(0, Item("arrow").Value);
        Assert.False(Item("gold coin").QueryFlag(Define.FLAG_STARTEQUIP));
        Assert.Equal(10, Item("gold coin").Value);
        Assert.False(Item("sack").QueryFlag(Define.FLAG_STARTEQUIP));
        Assert.Equal(0, Item("sack").Value);
        Assert.False(Item("knife").QueryFlag(Define.FLAG_STARTEQUIP));
        Assert.Equal(0, Item("knife").Value);
        Assert.False(Item("pair").QueryFlag(Define.FLAG_STARTEQUIP));
        Assert.Equal(0, Item("pair").Value);
    }

    private static IEnumerable<CfObject> Items(CfObject op)
    {
        for (CfObject? c = op.Inv; c != null; c = c.Below)
        {
            yield return c;
        }
    }

    [Fact]
    public void CreateTreasure_Given_no_GT_STARTEQUIP_When_made_Then_items_have_no_flag_and_keep_their_value()
    {
        // Arrange
        CfWorld w = World("treasure t\n  arch sword\n  more\n  arch arrow\n  end\n", out _);
        CfObject pl = Holder(w);

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), pl, 0, 1, 0);

        // Assert
        Assert.All(Items(pl), i => Assert.False(i.QueryFlag(Define.FLAG_STARTEQUIP)));
        Assert.Equal(new[] { 2, 50 }, Items(pl).Select(i => i.Value).ToArray());
    }

    [Fact]
    public void CreateTreasure_Given_GT_UPDATE_INV_When_the_creator_is_a_player_Then_the_client_is_sent_each_item_and_without_the_flag_none()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = World("treasure t\n  arch sword\n  more\n  arch sack\n  end\n", out _, host);
        CfObject pl = Holder(w, Define.PLAYER);
        CfObject quiet = Holder(w, Define.PLAYER);

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), pl, GT.UPDATE_INV, 1, 0);
        int sent = host.Calls.Count(c => c.StartsWith("send:"));
        w.CreateTreasure(w.FindTreasurelist("t"), quiet, 0, 1, 0);

        // Assert
        Assert.Equal(new[] { "send:sword", "send:sack" }, host.Calls.Where(c => c.StartsWith("send:")).ToArray());
        Assert.Equal(2, sent);
    }

    private sealed class ApplyHost : CrossfireHost
    {
        public List<string> Applied { get; } = new();

        public override void MonsterCheckApply(CfObject monster, CfObject item) => Applied.Add(monster.Name + ":" + item.Name);
    }

    [Fact]
    public void CreateTreasure_Given_GT_APPLY_When_the_creator_is_a_monster_Then_it_applies_each_item_and_a_player_or_no_flag_does_not()
    {
        // Arrange
        var host = new ApplyHost();
        CfWorld w = World("treasure t\n  arch sword\n  more\n  arch sack\n  end\n", out _, host);
        CfObject orc = Holder(w, Define.MONSTER);
        orc.Name = "orc";
        orc.SetFlag(Define.FLAG_MONSTER);
        CfObject human = Holder(w, Define.PLAYER);
        CfObject plain = Holder(w, Define.MONSTER);
        plain.SetFlag(Define.FLAG_MONSTER);

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), orc, GT.APPLY, 1, 0);
        w.CreateTreasure(w.FindTreasurelist("t"), human, GT.APPLY, 1, 0);
        w.CreateTreasure(w.FindTreasurelist("t"), plain, 0, 1, 0);

        // Assert
        Assert.Equal(new[] { "orc:sword", "orc:sack" }, host.Applied.ToArray());
    }

    [Fact]
    public void CreateTreasure_Given_GT_ENVIRONMENT_When_made_Then_the_item_goes_to_the_map_of_the_creator_and_not_into_it()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = World("treasure t\n  arch sword\n  end\n", out _, host);
        CfObject creator = Holder(w);
        creator.Map = new FakeMap();
        creator.X = 4;
        creator.Y = 9;

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), creator, GT.ENVIRONMENT, 1, 0);

        // Assert
        Assert.Null(creator.Inv);
        Assert.Equal(new[] { "insertmap:sword" }, host.Calls.ToArray());
    }

    [Fact]
    public void FixFleshItem_Given_flesh_and_a_donor_When_fixed_Then_it_is_named_weighed_and_valued_after_the_donor()
    {
        // Arrange
        CfWorld w = World("treasure t\n  arch meat\n  end\n", out _);
        CfObject donor = Holder(w);
        donor.Name = "troll";
        donor.Weight = 50000;
        donor.Level = 8;
        donor.Stats.Hp = 250;
        donor.Stats.Con = 4;
        donor.Resist[Define.ATNR_FIRE] = 40;
        donor.Attacktype = Define.AT_POISON;
        CfObject meat = w.ArchToObject(w.FindArchetype("meat"))!;

        // Act
        w.FixFleshItem(meat, donor);

        // Assert
        Assert.Equal("troll's meat", meat.Name);
        Assert.Equal("troll's meats", meat.NamePl);
        Assert.Equal(50000 / 100 * 100, meat.Weight);
        Assert.Equal(4 * 4, meat.Value); // isqrt(16)
        Assert.Equal(10 + 2 + 4, meat.Stats.Food);
        Assert.Equal(20, meat.Resist[Define.ATNR_FIRE]);
        Assert.Equal(8, meat.Level);
        Assert.Equal(Define.POISON, meat.Type);
        Assert.True(meat.QueryFlag(Define.FLAG_NO_STEAL));
    }

    [Fact]
    public void FixFleshItem_Given_not_flesh_or_no_donor_When_fixed_Then_nothing_changes()
    {
        // Arrange
        CfWorld w = World("treasure t\n  arch sword\n  end\n", out _);
        CfObject sword = w.ArchToObject(w.FindArchetype("sword"))!;
        CfObject meat = w.ArchToObject(w.FindArchetype("meat"))!;
        CfObject donor = Holder(w);

        // Act
        w.FixFleshItem(sword, donor);
        w.FixFleshItem(meat, null);

        // Assert
        Assert.Equal("sword", sword.Name);
        Assert.Equal("meat", meat.Name);
    }

    [Fact]
    public void CreateTreasure_Given_flesh_in_a_list_When_made_into_a_creature_Then_the_flesh_is_the_creatures()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = World("treasure t\n  arch meat\n  end\n", out _, host);
        CfObject donor = Holder(w, Define.MONSTER);
        donor.Name = "orc";
        donor.Weight = 1000;

        // Act
        w.CreateTreasure(w.FindTreasurelist("t"), donor, 0, 1, 0);
        CfObject env = Holder(w);
        env.Name = "pile";
        env.Map = new FakeMap();
        w.CreateTreasure(w.FindTreasurelist("t"), env, GT.ENVIRONMENT, 1, 0);

        // Assert: on the ground the flesh is not made from a creature (fix_flesh_item is for items put into one)
        Assert.Equal(new[] { "insertmap:meat" }, host.Calls.ToArray());
        Assert.Equal("orc's meat", donor.Inv!.Name);
        Assert.Equal(1000, donor.Inv.Weight);
    }
}
