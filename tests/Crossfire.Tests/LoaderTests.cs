namespace Crossfire.Tests;

/// <summary>Tests of the port of common/loader.l (the .arc reader and the saver). Not ported from a Crossfire file.</summary>
public class LoaderTests
{
    private static CfObject Load(CfWorld w, string text, int mapFlags = 0)
    {
        CfObject op = w.GetObject();
        w.LoadObject(new LoadSource(text), op, Define.LO_NEWFILE, mapFlags);
        return op;
    }

    private static CfObject Clone(CfWorld w, string name) => w.FindArchetype(name)!.Clone;

    [Fact]
    public void LoadObject_Given_the_fields_of_an_orc_When_read_Then_every_field_is_set()
    {
        // Arrange
        string arc = "Object orc\nname orc\nname_pl orcs\nrace orc\nslaying elf\ntitle of doom\nStr 12\nDex 13\nCon 14\nWis 15\nCha 16\nInt 17\nPow 18\n"
            + "hp 30\nmaxhp 31\nsp 32\nmaxsp 33\ngrace 34\nmaxgrace 35\nexp 36\nfood 37\ndam 5\nwc 6\nac -3\nluck -2\nlevel 4\ndirection 3\ntype 80\n"
            + "material 2\nvalue 99\nweight 70000\ncarrying 12\nattacktype 5\npath_attuned 1\npath_repelled 2\npath_denied 4\ninvisible 7\nmagic -1\n"
            + "state 2\nnrof 8\nx 4\ny 5\nlast_heal 1\nlast_sp 2\nlast_grace 3\nlast_eat 4\nrun_away 20\npick_up 6\nanim_speed 9\ncontainer 55\n"
            + "will_apply 2\ncan_apply 3\nattack_movement 33\nmove_state 4\nglow_radius 2\nexpmul 1.5\nweapontype 3\ncasting_speed 0.5\nelevation 7\nclient_type 9\n"
            + "speed 0.25\nspeed_left 0.75\nend\n";

        // Act
        CfWorld w = T.WorldWith(arc, out List<string> log);
        CfObject c = Clone(w, "orc");

        // Assert
        Assert.Equal("orc", c.Name);
        Assert.Equal("orcs", c.NamePl);
        Assert.Equal("orc", c.Race);
        Assert.Equal("elf", c.Slaying);
        Assert.Equal("of doom", c.Title);
        Assert.Equal(new sbyte[] { 12, 13, 14, 15, 16, 17, 18 }, new[] { c.Stats.Str, c.Stats.Dex, c.Stats.Con, c.Stats.Wis, c.Stats.Cha, c.Stats.Int, c.Stats.Pow });
        Assert.Equal(new short[] { 30, 31, 32, 33, 34, 35 }, new[] { c.Stats.Hp, c.Stats.Maxhp, c.Stats.Sp, c.Stats.Maxsp, c.Stats.Grace, c.Stats.Maxgrace });
        Assert.Equal(36, c.Stats.Exp);
        Assert.Equal(37, c.Stats.Food);
        Assert.Equal(5, c.Stats.Dam);
        Assert.Equal(6, c.Stats.Wc);
        Assert.Equal(-3, c.Stats.Ac);
        Assert.Equal(-2, c.Stats.Luck);
        Assert.Equal(4, c.Level);
        Assert.Equal(3, c.Direction);
        Assert.Equal(80, c.Type);
        Assert.Equal(2, c.Material);
        Assert.Equal(99, c.Value);
        Assert.Equal(70000, c.Weight);
        Assert.Equal(12, c.Carrying);
        Assert.Equal(5u, c.Attacktype);
        Assert.Equal(1u, c.PathAttuned);
        Assert.Equal(2u, c.PathRepelled);
        Assert.Equal(4u, c.PathDenied);
        Assert.Equal(7, c.Invisible);
        Assert.Equal(-1, c.Magic);
        Assert.Equal(2, c.State);
        Assert.Equal(8u, c.Nrof);
        Assert.Equal(4, c.X);
        Assert.Equal(4, c.Ox);
        Assert.Equal(5, c.Y);
        Assert.Equal(5, c.Oy);
        Assert.Equal(1, c.LastHeal);
        Assert.Equal(2, c.LastSp);
        Assert.Equal(3, c.LastGrace);
        Assert.Equal(4, c.LastEat);
        Assert.Equal(20, c.RunAway);
        Assert.Equal(6, c.PickUp);
        Assert.Equal(9, c.AnimSpeed);
        Assert.Equal(55, c.WeightLimit);
        Assert.Equal(2, c.WillApply);
        Assert.Equal(3, c.CanApply);
        Assert.Equal(33, c.MoveType);
        Assert.Equal(4, c.MoveStatus);
        Assert.Equal(2, c.GlowRadius);
        Assert.Equal(1.5, c.Expmul);
        Assert.Equal(3u, c.Weapontype);
        Assert.Equal(0.5f, c.CastingSpeed);
        Assert.Equal(7, c.Elevation);
        Assert.Equal(9, c.ClientType);
        Assert.Equal(0.25f, c.Speed);
        Assert.Equal(-0.1f, c.SpeedLeft);
        Assert.DoesNotContain(log, l => l.StartsWith("Error"));
    }

    [Fact]
    public void LoadObject_Given_the_object_line_in_an_archetype_When_read_Then_the_archetype_and_the_object_are_named()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object zepellin ticket   \nend\n", out _);

        // Assert
        Archetype? at = w.FindArchetype("zepellin ticket");
        Assert.NotNull(at);
        Assert.Equal("zepellin ticket", at!.Clone.Name);
        Assert.Equal("zepellin ticket", at.Clone.NamePl);
        Assert.Same(at, at.Clone.Arch);
    }

    [Fact]
    public void LoadObject_Given_a_name_pl_When_read_Then_it_is_kept_and_otherwise_it_is_the_name()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nname heart\nend\nObject b\nname mouse\nname_pl mice\nend\n", out _);

        // Assert
        Assert.Equal("heart", Clone(w, "a").NamePl);
        Assert.Equal("mice", Clone(w, "b").NamePl);
    }

    [Fact]
    public void LoadObject_Given_a_changed_name_on_an_object_of_an_archetype_When_read_Then_the_plural_is_the_new_name()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object goblin\nname goblin\nname_pl goblins\nend\n", out _);

        // Act
        CfObject op = Load(w, "arch goblin\nname goblin heart\nend\n");
        CfObject keepsPlural = Load(w, "arch goblin\nname goblin\nname_pl gobbos\nend\n");
        CfObject sameName = Load(w, "arch goblin\nhp 3\nend\n");

        // Assert
        Assert.Equal("goblin heart", op.NamePl);
        Assert.Equal("gobbos", keepsPlural.NamePl);
        Assert.Equal("goblins", sameName.NamePl);
    }

    [Fact]
    public void LoadObject_Given_keys_in_any_case_When_read_Then_they_match()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nSTR 5\nDex 6\nCON 7\nend\n", out _);

        // Assert
        CfObject c = Clone(w, "a");
        Assert.Equal(5, c.Stats.Str);
        Assert.Equal(6, c.Stats.Dex);
        Assert.Equal(7, c.Stats.Con);
    }

    [Fact]
    public void LoadObject_Given_trailing_blanks_in_values_When_read_Then_they_are_cut_but_not_tabs()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nrace  elf   \nslaying orc\t\nname_pl  big  elves  \nend\n", out _);

        // Assert
        CfObject c = Clone(w, "a");
        Assert.Equal("elf", c.Race);
        Assert.Equal("orc\t", c.Slaying);
        Assert.Equal("big  elves", c.NamePl);
    }

    [Fact]
    public void LoadObject_Given_a_tab_after_the_key_When_read_Then_the_value_is_empty_as_yval_skips_only_spaces()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nhp 5\nmaxhp\t7\nend\n", out _);

        // Assert
        CfObject c = Clone(w, "a");
        Assert.Equal(5, c.Stats.Hp);
        Assert.Equal(0, c.Stats.Maxhp);
    }

    [Fact]
    public void LoadObject_Given_a_key_with_a_single_blank_and_no_value_When_read_Then_no_rule_matches()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nname \nhp \nend\n", out _);

        // Assert
        CfObject c = Clone(w, "a");
        Assert.Equal("a", c.Name);
        Assert.Equal(0, c.Stats.Hp);
        Assert.Equal(2, c.Extra!.Count);
    }

    [Fact]
    public void LoadObject_Given_a_value_that_overflows_When_read_Then_it_saturates_like_atol_and_is_cut_to_the_field()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\ndam 100000000000\nweight 99999999999\nvalue -99999999999\nac 300\nhp 70000\nend\n", out _);

        // Assert
        CfObject c = Clone(w, "a");
        Assert.Equal(-1, c.Stats.Dam);
        Assert.Equal(int.MaxValue, c.Weight);
        Assert.Equal(int.MinValue, c.Value);
        Assert.Equal(44, c.Stats.Ac);
        Assert.Equal(4464, c.Stats.Hp);
    }

    [Fact]
    public void LoadObject_Given_flags_When_read_Then_one_sets_and_zero_clears()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nalive 1\nno_pick 1\nno_pass 0\nwalk_on 5\nend\nObject b\nalive 1\nalive 0\nend\n", out _);

        // Assert
        CfObject a = Clone(w, "a");
        Assert.True(a.QueryFlag(Define.FLAG_ALIVE));
        Assert.True(a.QueryFlag(Define.FLAG_NO_PICK));
        Assert.False(a.QueryFlag(Define.FLAG_NO_PASS));
        Assert.True(a.QueryFlag(Define.FLAG_WALK_ON));
        Assert.False(Clone(w, "b").QueryFlag(Define.FLAG_ALIVE));
    }

    [Fact]
    public void LoadObject_Given_every_keyword_of_the_flag_table_When_read_Then_the_flag_is_set()
    {
        // Arrange
        var exceptions = new HashSet<string> { "berserk", "run_away", "pick_up" };
        int tested = 0;

        foreach ((int flag, string? keyword) in Define.FlagLinks)
        {
            if (keyword == null || exceptions.Contains(keyword))
            {
                continue;
            }
            CfWorld w = T.World(out _);

            // Act
            CfObject op = Load(w, keyword + " 1\nend\n");

            // Assert
            Assert.True(op.QueryFlag(flag), keyword);
            tested++;
        }
        Assert.True(tested >= 80);
    }

    [Fact]
    public void LoadObject_Given_the_flag_keywords_the_loader_lacks_When_read_Then_they_are_kept_as_unknown()
    {
        // Arrange
        CfWorld w = T.World(out _);

        // Act
        CfObject op = Load(w, "berserk 1\nend\n");

        // Assert
        Assert.False(op.QueryFlag(Define.FLAG_BERSERK));
        Assert.Equal("1", op.Extra!.Get("berserk"));
    }

    [Fact]
    public void LoadObject_Given_run_away_and_pick_up_When_read_Then_they_are_numbers_not_flags()
    {
        // Arrange
        CfWorld w = T.World(out _);

        // Act
        CfObject op = Load(w, "run_away 10\npick_up 3\nend\n");

        // Assert
        Assert.Equal(10, op.RunAway);
        Assert.Equal(3, op.PickUp);
        Assert.False(op.QueryFlag(Define.FLAG_RUN_AWAY));
        Assert.False(op.QueryFlag(Define.FLAG_PICK_UP));
    }

    [Fact]
    public void LoadObject_Given_special_flag_keywords_When_read_Then_their_side_effects_follow()
    {
        // Arrange
        CfWorld w = T.World(out _);

        // Act
        CfObject ident = Load(w, "known_magical 1\nidentified 1\nend\n");
        CfObject notIdent = Load(w, "known_magical 1\nidentified 0\nend\n");
        CfObject wiz = Load(w, "wiz 1\nend\n");
        CfObject unwiz = Load(w, "wiz 1\nwiz 0\nend\n");
        CfObject slow = Load(w, "slow_move 0.5\nend\n");
        CfObject moves = Load(w, "random_movement 1\nend\n");
        CfObject moves2 = Load(w, "random_move 1\nend\n");

        // Assert
        Assert.True(ident.QueryFlag(Define.FLAG_IDENTIFIED));
        Assert.False(ident.QueryFlag(Define.FLAG_KNOWN_MAGICAL));
        Assert.False(notIdent.QueryFlag(Define.FLAG_IDENTIFIED));
        Assert.True(notIdent.QueryFlag(Define.FLAG_KNOWN_MAGICAL));
        Assert.True(wiz.QueryFlag(Define.FLAG_WIZ) && wiz.QueryFlag(Define.FLAG_WAS_WIZ) && wiz.QueryFlag(Define.FLAG_WIZPASS));
        Assert.False(unwiz.QueryFlag(Define.FLAG_WIZ));
        Assert.False(unwiz.QueryFlag(Define.FLAG_WIZPASS));
        Assert.True(unwiz.QueryFlag(Define.FLAG_WAS_WIZ));
        Assert.Equal(500, slow.Stats.Exp);
        Assert.True(slow.QueryFlag(Define.FLAG_SLOW_MOVE));
        Assert.True(moves.QueryFlag(Define.FLAG_RANDOM_MOVE));
        Assert.True(moves2.QueryFlag(Define.FLAG_RANDOM_MOVE));
    }

    [Fact]
    public void LoadObject_Given_friendly_When_read_Then_a_non_player_is_put_on_the_friendly_list()
    {
        // Arrange
        CfWorld w = T.World(out _);

        // Act
        CfObject pet = Load(w, "friendly 1\nend\n");
        CfObject player = Load(w, "type 1\nfriendly 1\nend\n");
        CfObject foe = Load(w, "friendly 0\nend\n");

        // Assert
        Assert.Same(pet, w.FirstFriendlyObject!.Ob);
        Assert.Null(w.FirstFriendlyObject.Next);
        Assert.True(player.QueryFlag(Define.FLAG_FRIENDLY));
        Assert.False(foe.QueryFlag(Define.FLAG_FRIENDLY));
    }

    [Fact]
    public void LoadObject_Given_old_style_resistances_When_read_Then_the_masks_become_resistances()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nimmune 8\nprotected 3\nvulnerable 4\narmour 7\nresist_cold 20\nresist_internal 4\nresist_life_stealing 9\nend\n", out _);

        // Assert
        CfObject c = Clone(w, "a");
        Assert.Equal(7, c.Resist[Define.ATNR_PHYSICAL]);
        Assert.Equal(30, c.Resist[Define.ATNR_MAGIC]);
        Assert.Equal(-100, c.Resist[Define.ATNR_FIRE]);
        Assert.Equal(100, c.Resist[Define.ATNR_ELECTRICITY]);
        Assert.Equal(20, c.Resist[Define.ATNR_COLD]);
        Assert.Equal(4, c.Resist[Define.ATNR_INTERNAL]);
        Assert.Equal(0, c.Resist[Define.ATNR_LIFE_STEALING]);
        Assert.Equal("9", c.Extra!.Get("resist_life_stealing"));
    }

    [Fact]
    public void LoadObject_Given_every_resist_keyword_When_read_Then_the_right_index_is_set()
    {
        // Arrange
        string text = "";
        for (int i = 0; i <= Define.ATNR_INTERNAL; i++)
        {
            text += "resist_" + Tables.ResistSave[i] + " " + (i + 1) + "\n";
        }
        CfWorld w = T.World(out _);

        // Act
        CfObject op = Load(w, text + "end\n");

        // Assert
        for (int i = 0; i <= Define.ATNR_INTERNAL; i++)
        {
            Assert.Equal(i + 1, op.Resist[i]);
        }
    }

    [Fact]
    public void LoadObject_Given_a_message_When_read_Then_the_lines_are_joined_with_newlines()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object sign\nmsg\nHello\n  world\n\nendmsg\nhp 3\nend\nObject b\nmsg  \nx\nendmsg  \nend\n", out _);

        // Assert
        Assert.Equal("Hello\n  world\n\n", Clone(w, "sign").Msg);
        Assert.Equal(3, Clone(w, "sign").Stats.Hp);
        Assert.Equal("x\n", Clone(w, "b").Msg);
    }

    [Fact]
    public void LoadObject_Given_text_in_a_message_that_looks_like_keys_When_read_Then_it_is_text()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object sign\nmsg\nhp 5\nend\nendmsg x\nendmsg\nend\n", out _);

        // Assert
        Assert.Equal("hp 5\nend\nendmsg x\n", Clone(w, "sign").Msg);
        Assert.Equal(0, Clone(w, "sign").Stats.Hp);
    }

    [Fact]
    public void LoadObject_Given_a_long_message_When_read_Then_a_warning_is_logged()
    {
        // Arrange
        string line = new string('x', 100);
        string text = "Object a\nmsg\n" + string.Concat(Enumerable.Repeat(line + "\n", 25)) + "endmsg\nend\n";

        // Act
        CfWorld w = T.WorldWith(text, out List<string> log);

        // Assert
        Assert.Contains(log, l => l.StartsWith("Debug: \n\tWarning message length > 2048 (max allowed=4096): 2525"));
        Assert.Equal(2525, Clone(w, "a").Msg!.Length);
    }

    [Fact]
    public void LoadObject_Given_msg_with_text_after_it_When_read_Then_it_is_an_unknown_key()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nmsg <edit msg>\nend\n", out _);

        // Assert
        CfObject c = Clone(w, "a");
        Assert.Null(c.Msg);
        Assert.Equal("<edit msg>", c.Extra!.Get("msg"));
    }

    [Fact]
    public void LoadObject_Given_the_last_end_without_a_newline_When_read_Then_the_block_is_not_closed()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nend", out _);

        // Assert
        Assert.Null(w.FindArchetype("a"));
    }

    [Fact]
    public void LoadObject_Given_more_parts_When_read_Then_the_archetypes_are_linked_and_the_tail_is_found()
    {
        // Arrange
        string arc = "Object dragon_head\nface dragon.1\nend\nObject dragon_body\nface dragon.1\nx 1\ny 1\nMore\nend\n"
            + "Object dragon_tail\nface dragon.1\nx 2\ny 3\nMore\nend\nObject rat\nend\n";

        // Act
        CfWorld w = T.WorldWith(arc, out _);

        // Assert
        Archetype head = w.FindArchetype("dragon_head")!;
        Archetype body = w.FindArchetype("dragon_body")!;
        Archetype tail = w.FindArchetype("dragon_tail")!;
        Archetype rat = w.FindArchetype("rat")!;
        Assert.Same(body, head.More);
        Assert.Same(tail, body.More);
        Assert.Same(head, body.Head);
        Assert.Same(head, tail.Head);
        Assert.Same(body.Clone, head.Clone.More);
        Assert.Same(head.Clone, body.Clone.Head);
        Assert.Equal(2, head.TailX);
        Assert.Equal(3, head.TailY);
        Assert.Same(rat, head.Next);
        Assert.Same(rat, tail.Next);
        Assert.Null(body.Next);
    }

    [Fact]
    public void LoadObject_Given_parts_with_other_faces_When_read_Then_the_tail_stays_zero()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object big\nface big.1\nend\nObject big_p\nface big.2\nx 1\ny 1\nMore\nend\n", out _);

        // Assert
        Archetype head = w.FindArchetype("big")!;
        Assert.Equal(0, head.TailX);
        Assert.Equal(0, head.TailY);
        Assert.NotNull(head.More);
    }

    [Fact]
    public void LoadObject_Given_a_part_before_any_head_When_read_Then_an_error_is_logged_and_it_is_taken_as_an_archetype()
    {
        // Arrange / Act
        CfWorld w = T.World(out List<string> log);
        w.InitArchetypes("Object part\nMore\nend\n");

        // Assert
        Assert.NotNull(w.FindArchetype("part"));
        Assert.Contains(log, l => l.StartsWith("Error: Archetype part (More) without a head: part"));
    }

    [Fact]
    public void LoadObject_Given_duplicate_names_When_read_Then_the_first_in_the_text_is_found()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object club\nname first\nend\nObject club\nname second\nend\n", out _);

        // Assert
        Assert.Equal("first", Clone(w, "club").Name);
        Assert.Equal("second", w.FindArchetype("club")!.Next!.Clone.Name);
    }

    [Fact]
    public void LoadObject_Given_an_object_line_without_a_name_When_read_Then_it_is_ignored_with_an_error()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object  \nname x\nend\nObject y\nend\n", out List<string> log);

        // Assert
        Assert.Contains(log, l => l == "Error: Object lacks name.\n");
        Assert.NotNull(w.FindArchetype("y"));
        Assert.Null(w.FindArchetype("x"));
    }

    [Fact]
    public void LoadObject_Given_an_object_line_outside_the_archetypes_When_read_Then_an_error_and_no_name()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);

        // Act
        CfObject op = Load(w, "Object x\nend\n");

        // Assert
        Assert.Contains(log, l => l == "Error: Got object info when not in arch_init (x)?\n");
        Assert.Null(op.Name);
    }

    [Fact]
    public void LoadObject_Given_the_inventory_keyword_When_read_Then_an_error_is_logged()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);

        // Act
        Load(w, "inventory\nInventory foo bar\nend\n");

        // Assert
        Assert.Equal(2, log.Count(l => l == "Error: Got depreciated Inventory command?\n"));
    }

    [Fact]
    public void LoadObject_Given_a_nested_arch_block_When_read_Then_the_object_is_in_the_inventory()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object sword\nname sword\nweight 5\nend\n", out _);

        // Act
        CfObject chest = Load(w, "arch sword\nname chest\narch sword\nname magic sword\nhp 3\nend\narch sword\nend\nweight 100\nend\n");

        // Assert
        Assert.Equal("chest", chest.Name);
        Assert.Equal(100, chest.Weight);
        Assert.Equal("sword", chest.Inv!.Name);
        Assert.Equal("magic sword", chest.Inv.Below!.Name);
        Assert.Equal(3, chest.Inv.Below.Stats.Hp);
        Assert.Equal(10, chest.Carrying);
    }

    [Fact]
    public void LoadObject_Given_a_nested_block_in_an_archetype_When_read_Then_the_clone_keeps_it_in_its_inventory()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object chest\nweight 10\narch sword\nname magic sword\nend\nend\nObject next\nend\n", out List<string> log);

        // Assert
        CfObject chest = Clone(w, "chest");
        Assert.Equal("magic sword", chest.Inv!.Name);
        Assert.Same(chest, chest.Inv.Env);
        Assert.Null(chest.Inv.Arch);
        Assert.NotNull(w.FindArchetype("next"));
        Assert.DoesNotContain(log, l => l.Contains("Couldn't find archetype sword"));
    }

    [Fact]
    public void LoadObject_Given_an_arch_line_of_an_unknown_archetype_When_read_Then_the_object_still_gets_the_lines()
    {
        // Arrange
        CfWorld w = T.WorldWith("", out List<string> log);

        // Act
        CfObject op = Load(w, "arch nosuch\nname lost\nend\n");

        // Assert
        Assert.Null(op.Arch);
        Assert.Equal("lost", op.Name);
        Assert.Contains(log, l => l.StartsWith("Error: Couldn't find archetype nosuch"));
    }

    [Fact]
    public void LoadObject_Given_other_arch_When_read_Then_the_second_pass_links_it_even_forward()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nother_arch b\nend\nObject b\nend\nObject c\nother_arch zzz\nend\n", out List<string> log);

        // Assert
        Assert.Same(w.FindArchetype("b"), Clone(w, "a").OtherArch);
        Assert.Null(Clone(w, "c").OtherArch);
        Assert.Contains(log, l => l.StartsWith("Error: Warning: failed to find other_arch zzz"));
    }

    [Fact]
    public void LoadObject_Given_randomitems_When_read_Then_the_second_pass_sets_the_list_and_none_is_an_error()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nrandomitems door\nend\nObject b\nrandomitems none\nend\n", out List<string> log);

        // Assert
        Assert.Equal("door", Clone(w, "a").Randomitems!.Name);
        Assert.Null(Clone(w, "b").Randomitems);
        Assert.Contains(log, l => l.StartsWith("Error: Failed to link treasure to arch: randomitems"));
    }

    [Fact]
    public void LoadObject_Given_a_host_that_knows_only_some_lists_When_read_Then_an_unknown_one_is_an_error()
    {
        // Arrange
        var host = new TestHost { KnownLists = new HashSet<string> { "door" } };

        // Act
        CfWorld w = T.WorldWith("Object a\nrandomitems door\nend\nObject b\nrandomitems nosuch\nend\n", out List<string> log, host);

        // Assert
        Assert.NotNull(Clone(w, "a").Randomitems);
        Assert.Null(Clone(w, "b").Randomitems);
        Assert.Contains(log, l => l.StartsWith("Error: Couldn't find treasurelist nosuch"));
    }

    [Fact]
    public void LoadObject_Given_randomitems_on_an_object_read_after_the_archetypes_When_read_Then_the_list_is_set()
    {
        // Arrange
        CfWorld w = T.WorldWith("", out _);

        // Act
        CfObject op = Load(w, "randomitems chest\nend\n");
        CfObject none = Load(w, "randomitems none\nend\n");

        // Assert
        Assert.Equal("chest", op.Randomitems!.Name);
        Assert.Null(none.Randomitems);
    }

    [Fact]
    public void LoadObject_Given_randomitems_while_the_archetypes_are_read_When_read_Then_the_first_pass_leaves_it_empty()
    {
        // Arrange
        CfWorld w = T.World(out _);

        // Act
        CfObject op = Load(w, "randomitems chest\nend\n");

        // Assert
        Assert.Null(op.Randomitems);
    }

    [Fact]
    public void LoadObject_Given_events_When_read_Then_hook_plugin_and_options_are_set_by_event()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);

        // Act
        CfObject op = Load(w, "event_say hello\nevent_say_plugin python\nevent_say_options a b\nevent_timer t\nevent_close_plugin cp\nevent_trigger_options o\nevent_stop x\nevent_apply  \nevent_drop_plugin  \nend\n");

        // Assert
        Assert.Equal("hello", op.EventHook[Define.EVENT_SAY]);
        Assert.Equal("python", op.EventPlugin[Define.EVENT_SAY]);
        Assert.Equal("a b", op.EventOptions[Define.EVENT_SAY]);
        Assert.Equal("t", op.EventHook[Define.EVENT_TIMER]);
        Assert.Equal("cp", op.EventPlugin[Define.EVENT_CLOSE]);
        Assert.Equal("o", op.EventOptions[Define.EVENT_TRIGGER]);
        Assert.Equal("x", op.Extra!.Get("event_stop"));
        Assert.Null(op.EventHook[Define.EVENT_APPLY]);
        Assert.Contains(log, l => l == "Error: Event (apply) without val\n");
        Assert.Contains(log, l => l == "Error: Event (drop) without plugin\n");
    }

    [Fact]
    public void LoadObject_Given_every_event_name_When_read_Then_each_has_its_own_slot()
    {
        // Arrange
        string[] names = { "apply", "attack", "born", "crash", "death", "drop", "login", "logout", "pickup", "remove", "say", "shout", "tell", "time", "throw", "trigger", "close", "timer" };
        int[] numbers = { Define.EVENT_APPLY, Define.EVENT_ATTACK, Define.EVENT_BORN, Define.EVENT_CRASH, Define.EVENT_DEATH, Define.EVENT_DROP, Define.EVENT_LOGIN, Define.EVENT_LOGOUT, Define.EVENT_PICKUP, Define.EVENT_REMOVE, Define.EVENT_SAY, Define.EVENT_SHOUT, Define.EVENT_TELL, Define.EVENT_TIME, Define.EVENT_THROW, Define.EVENT_TRIGGER, Define.EVENT_CLOSE, Define.EVENT_TIMER };
        CfWorld w = T.World(out _);
        string text = "";
        foreach (string n in names)
        {
            text += "event_" + n + " h_" + n + "\nevent_" + n + "_plugin p_" + n + "\nevent_" + n + "_options o_" + n + "\n";
        }

        // Act
        CfObject op = Load(w, text + "end\n");

        // Assert
        for (int i = 0; i < names.Length; i++)
        {
            Assert.Equal("h_" + names[i], op.EventHook[numbers[i]]);
            Assert.Equal("p_" + names[i], op.EventPlugin[numbers[i]]);
            Assert.Equal("o_" + names[i], op.EventOptions[numbers[i]]);
        }
    }

    [Fact]
    public void LoadObject_Given_faces_When_read_Then_names_get_numbers_and_the_same_name_the_same_face()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nface door\nend\nObject b\nface door\nend\nObject c\nface other.1\nend\n", out _);

        // Assert
        Assert.Equal("door", Clone(w, "a").Face!.Name);
        Assert.Same(Clone(w, "a").Face, Clone(w, "b").Face);
        Assert.NotSame(Clone(w, "a").Face, Clone(w, "c").Face);
    }

    [Fact]
    public void FindFace_Given_names_When_asked_Then_integers_are_numbers_and_unknown_names_follow_the_mode()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        int door = w.Faces.Register("door");

        // Act
        int integer = w.Faces.FindFace("12", -1);
        int cut = w.Faces.FindFace("door\nrest", -1);
        w.Faces.AutoRegister = false;
        int missing = w.Faces.FindFace("nosuch", -1);

        // Assert
        Assert.Equal(12, integer);
        Assert.Equal(door, cut);
        Assert.Equal(-1, missing);
        Assert.Contains(log, l => l.StartsWith("Error: Warning: Integer face name used: 12"));
        Assert.Same(w.Faces[0], w.Faces[9999]);
    }

    [Fact]
    public void LoadObject_Given_animation_names_When_read_Then_the_id_and_the_flag_follow_none_clears_them()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        w.Faces.Register("f0");
        w.Faces.Register("f1");
        w.Animations.Load("# comment\nanim aaa\nf0\nmina\nanim spin\nfacings 2\nf0\nf1\nmina\n");

        // Act
        CfObject spin = Load(w, "animation spin\nend\n");
        CfObject none = Load(w, "animation spin\nanimation NONE\nend\n");
        CfObject unknown = Load(w, "animation nosuch\nend\n");

        // Assert
        Assert.Equal(2, spin.AnimationId);
        Assert.True(spin.QueryFlag(Define.FLAG_ANIMATE));
        Assert.Equal(2, w.Animations[2].NumAnimations);
        Assert.Equal(2, w.Animations[2].Facings);
        Assert.Equal(0, none.AnimationId);
        Assert.False(none.QueryFlag(Define.FLAG_ANIMATE));
        Assert.Equal(0, unknown.AnimationId);
        Assert.True(unknown.QueryFlag(Define.FLAG_ANIMATE));
        Assert.Contains(log, l => l.StartsWith("Error: Unable to find animation nosuch"));
        Assert.Equal(2, w.Animations.NumAnimations);
    }

    [Fact]
    public void LoadObject_Given_speed_When_read_Then_the_active_list_and_speed_left_follow_the_map_flags()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.Random = () => 50;

        // Act
        CfObject fast = Load(w, "speed 0.5\nend\n");
        CfObject backwards = Load(w, "speed -0.5\nend\n");
        CfObject styled = Load(w, "speed 0.5\nend\n", Define.MAP_STYLE);

        // Assert
        Assert.Equal(0.5f, fast.Speed);
        Assert.Equal(0f, fast.SpeedLeft);
        Assert.Equal(-0.5f, backwards.SpeedLeft);
        Assert.Same(backwards, w.ActiveObjects);
        Assert.Same(fast, backwards.ActiveNext);
        Assert.Null(fast.ActiveNext);
        Assert.Null(styled.ActiveNext);
        Assert.Null(styled.ActivePrev);
    }

    [Fact]
    public void LoadObject_Given_speed_zero_with_the_map_style_flag_When_read_Then_the_object_leaves_the_active_list()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject op = w.GetObject();
        op.Speed = 0.5f;
        w.UpdateObSpeed(op);

        // Act
        w.LoadObject(new LoadSource("speed 0\nend\n"), op, Define.LO_REPEAT, Define.MAP_STYLE);

        // Assert
        Assert.Null(w.ActiveObjects);
    }

    [Fact]
    public void LoadObject_Given_editable_and_connected_When_read_Then_the_archetype_and_the_host_are_told()
    {
        // Arrange
        var host = new TestHost();

        // Act
        CfWorld w = T.WorldWith("Object a\neditable 16\nconnected 5\nend\n", out _, host);

        // Assert
        Assert.Equal(16u, w.FindArchetype("a")!.Editable);
        Assert.Contains("button:5", host.Calls);
    }

    [Fact]
    public void LoadObject_Given_editable_on_an_object_without_an_archetype_When_read_Then_an_error_not_a_crash()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);

        // Act
        Load(w, "editable 3\nend\n");

        // Assert
        Assert.Contains(log, l => l.StartsWith("Error: editable on an object without an archetype"));
    }

    [Fact]
    public void LoadObject_Given_the_ignored_combat_keys_When_read_Then_they_are_dropped_without_a_trace()
    {
        // Arrange
        CfWorld w = T.World(out _);

        // Act
        CfObject op = Load(w, "can_knockback 1\ncan_parry 1\ncan_impale 1\ncan_cut 1\ncan_dam_armour 1\nneed_an 1\nneed_ie 1\nend\n");

        // Assert
        Assert.Null(op.Extra);
    }

    [Fact]
    public void LoadObject_Given_unknown_keys_of_graal_kingdoms_When_read_Then_they_are_kept_in_order_with_repeats()
    {
        // Arrange
        string arc = "Object x\ngraalnpctype 2\nGraalNPCParam khat9.png\nflag gender=male\nflag arms=kmarms117.png\nhpbuff 5\n# a comment\n\nflag   spaced   \nend\n";

        // Act
        CfWorld w = T.WorldWith(arc, out _);

        // Assert
        ExtraKeys e = Clone(w, "x").Extra!;
        Assert.Equal(6, e.Count);
        Assert.Equal("2", e.Get("graalnpctype"));
        Assert.Equal("khat9.png", e.Get("graalnpcparam"));
        Assert.Equal("5", e.Get("HPBUFF"));
        Assert.Equal(new[] { "gender=male", "arms=kmarms117.png", "spaced" }, e.GetAll("flag").ToArray());
        Assert.Equal("graalnpctype", e.Entries[0].Key);
        Assert.Equal("GraalNPCParam", e.Entries[1].Key);
        Assert.Equal(3, w.UnknownKeys["flag"]);
        Assert.Equal(1, w.UnknownKeys["graalnpcparam"]);
        Assert.False(w.UnknownKeys.ContainsKey("#"));
    }

    [Fact]
    public void LoadObject_Given_an_archetype_with_unknown_keys_When_an_object_is_made_Then_it_has_a_copy()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object x\ngraalnpctype 2\nend\n", out _);

        // Act
        CfObject o = w.GetArchetype("x");
        o.Extra!.Add("more", "1");

        // Assert
        Assert.Equal(2, o.Extra.Count);
        Assert.Equal(1, Clone(w, "x").Extra!.Count);
        Assert.True(o.Extra.Contains("graalnpctype"));
        Assert.False(Clone(w, "x").Extra!.Contains("more"));
    }

    [Fact]
    public void LoadObject_Given_an_unknown_line_in_an_object_between_blocks_When_read_Then_it_belongs_to_the_next_object()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object a\nend\nhp 7\nObject b\nend\n", out _);

        // Assert
        Assert.Equal(0, Clone(w, "a").Stats.Hp);
        Assert.Equal(7, Clone(w, "b").Stats.Hp);
    }

    [Fact]
    public void LoadObject_Given_the_linemode_When_read_Then_each_block_ends_one_call_and_the_end_is_reported()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        var src = new LoadSource("name a\nmsg\ntext\nendmsg\nend\nname b\nend\n");
        CfObject first = w.GetObject();
        CfObject second = w.GetObject();
        CfObject third = w.GetObject();

        // Act
        int r1 = w.LoadObject(src, first, Define.LO_LINEMODE, 0);
        int r2 = w.LoadObject(src, second, Define.LO_LINEMODE, 0);
        int r3 = w.LoadObject(src, third, Define.LO_LINEMODE, 0);

        // Assert
        Assert.Equal(Define.LL_NORMAL, r1);
        Assert.Equal("a", first.Name);
        Assert.Equal("text\n", first.Msg);
        Assert.Equal(Define.LL_NORMAL, r2);
        Assert.Equal("b", second.Name);
        Assert.Equal(Define.LL_EOF, r3);
        Assert.Contains(log, l => l == "Debug: Got eof while scanning strings\n");
    }

    [Fact]
    public void LoadObject_Given_noread_When_called_Then_nothing_is_read()
    {
        // Arrange
        CfWorld w = T.World(out _);
        var src = new LoadSource("name a\nend\n");
        CfObject op = w.GetObject();

        // Act
        int r = w.LoadObject(src, op, Define.LO_NOREAD, 0);

        // Assert
        Assert.Equal(Define.LL_NORMAL, r);
        Assert.Equal(0, src.Pos);
        Assert.Null(op.Name);
    }

    [Fact]
    public void LoadObject_Given_a_current_weapon_script_When_read_Then_the_host_finds_the_weapon()
    {
        // Arrange
        var host = new WeaponHost();
        CfWorld w = T.World(out _, host);
        CfObject op = w.GetObject();
        op.CurrentWeaponScript = "sword";

        // Act
        w.LoadObject(new LoadSource("end\n"), op, Define.LO_NEWFILE, 0);

        // Assert
        Assert.Same(host.Weapon, op.CurrentWeapon);
    }

    private sealed class WeaponHost : CrossfireHost
    {
        public CfObject Weapon { get; } = new();

        public override CfObject? FindBestWeaponUsedMatch(CfObject player, string parameters) => parameters == "sword" ? Weapon : null;
    }

    [Fact]
    public void SetVariable_Given_lines_When_set_Then_the_object_changes_and_the_end_of_text_is_reported()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject op = w.GetObject();

        // Act
        int r = w.SetVariable(op, "hp 12\nStr 3\nmsg\nhi\nendmsg\n");

        // Assert
        Assert.Equal(Define.LL_EOF, r);
        Assert.Equal(12, op.Stats.Hp);
        Assert.Equal(3, op.Stats.Str);
        Assert.Equal("hi\n", op.Msg);
    }

    [Fact]
    public void SetVariable_Given_an_unfinished_message_When_set_Then_the_scanner_state_of_the_caller_is_restored()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject first = w.GetObject();
        w.SetVariable(first, "msg\nopen\n");
        CfObject op = w.GetObject();

        // Act
        w.LoadObject(new LoadSource("hp 4\nend\n"), op, Define.LO_NEWFILE, 0);

        // Assert
        Assert.Equal(4, op.Stats.Hp);
    }

    [Fact]
    public void Yval_Given_lines_When_asked_Then_the_value_follows_the_rules_of_the_c_function()
    {
        // Arrange / Act / Assert
        Assert.Equal("b c", CfWorld.Yval("name   b c   "));
        Assert.Equal("", CfWorld.Yval("name"));
        Assert.Equal("", CfWorld.Yval("name    "));
        Assert.Equal("", CfWorld.Yval("name\tx"));
        Assert.Equal("x\ty", CfWorld.Yval("name x\ty\t"[..^1]));
        Assert.Equal("v", CfWorld.Yval("k v"));
    }

    // ---- saving

    [Fact]
    public void GetObDiff_Given_an_object_equal_to_its_archetype_When_compared_Then_null()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object sword\nname sword\nweight 5\ntype 15\nface sword.1\nspeed 0.3\nalive 1\nend\n", out _);
        CfObject o = w.GetArchetype("sword");

        // Act
        string? diff = w.GetObDiff(o, w.FindArchetype("sword")!.Clone);

        // Assert
        Assert.Null(diff);
    }

    [Fact]
    public void GetObDiff_Given_changes_When_compared_Then_the_lines_come_in_the_order_of_the_c_function()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object sword\nname sword\nweight 5\nend\n", out _);
        CfObject o = w.GetArchetype("sword");
        o.Name = "blade";
        o.Title = "of doom";
        o.Race = "r";
        o.Slaying = "s";
        o.Msg = "m\n";
        o.Stats.Str = 2;
        o.Stats.Hp = 3;
        o.Stats.Dam = 7;
        o.Resist[Define.ATNR_FIRE] = 10;
        o.Magic = 2;
        o.Level = 5;
        o.Nrof = 4;
        o.Speed = 0.5f;
        o.SpeedLeft = 0.25f;
        o.MoveType = 33;
        o.SetFlag(Define.FLAG_CURSED);
        o.ClearFlag(Define.FLAG_ALIVE);
        o.SetFlag(Define.FLAG_SLOW_MOVE);
        o.Stats.Exp = 500;
        o.EventHook[Define.EVENT_SAY] = "hello";
        o.Weapontype = 3;

        // Act
        string? diff = w.GetObDiff(o, w.FindArchetype("sword")!.Clone);

        // Assert
        Assert.Equal(
            "name blade\ntitle of doom\nrace r\nslaying s\nmsg\nm\nendmsg\nevent_say hello\nStr 2\nhp 3\nexp 500\ndam 7\nspeed 0.500000\nspeed_left 0.250000\n"
            + "attack_movement 33\nnrof 4\nlevel 5\nresist_fire 10\nmagic 2\nweapontype 3\nslow_move 0.500000\ncursed 1\n",
            diff);
    }

    [Fact]
    public void GetObDiff_Given_a_speed_left_that_differs_When_the_speed_is_zero_Then_it_is_not_written()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "x");
        CfObject b = T.Obj(w, "x");
        a.SpeedLeft = 5f;

        // Act
        string? diff = w.GetObDiff(a, b);

        // Assert
        Assert.Null(diff);
    }

    [Fact]
    public void GetObDiff_Given_an_event_with_a_number_that_has_no_name_When_compared_Then_the_last_line_is_repeated_as_in_c()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "x");
        CfObject b = T.Obj(w, "x");
        a.EventHook[Define.EVENT_APPLY] = "h";
        a.EventHook[Define.EVENT_STOP] = "stop";
        a.EventPlugin[Define.EVENT_CLOCK] = "clock";

        // Act
        string? diff = w.GetObDiff(a, b);

        // Assert
        Assert.Equal("event_apply h\nevent_apply h\nevent_apply h\n", diff);
    }

    [Fact]
    public void GetObDiff_Given_an_animation_and_a_face_When_compared_Then_they_are_written()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.Faces.Register("f0");
        w.Animations.Load("anim spin\nf0\nmina\n");
        CfObject a = T.Obj(w, "x");
        CfObject b = T.Obj(w, "x");
        a.AnimationId = 1;
        a.Face = w.Faces[w.Faces.FindFace("f0", 0)];
        CfObject c = T.Obj(w, "x");
        c.AnimationId = 1;
        c.SetFlag(Define.FLAG_ANIMATE);
        CfObject d = T.Obj(w, "x");
        d.AnimationId = 0;
        c.Face = b.Face;

        // Act
        string? plain = w.GetObDiff(a, b);
        string? animated = w.GetObDiff(c, b);
        string? none = w.GetObDiff(d, c);

        // Assert
        Assert.Equal("face f0\nanimation spin\nis_animated 0\n", plain);
        Assert.Equal("animation spin\nis_animated 1\n", animated);
        Assert.Equal("animation NONE\nis_animated 0\n", none);
    }

    [Fact]
    public void GetObDiff_Given_randomitems_connected_and_other_arch_When_compared_Then_they_are_written()
    {
        // Arrange
        var host = new ButtonHost();
        CfWorld w = T.WorldWith("Object a\nend\nObject b\nend\n", out _, host);
        CfObject o = w.GetArchetype("a");
        CfObject plain = w.GetArchetype("a");
        o.Randomitems = new TreasureList("door");
        o.OtherArch = w.FindArchetype("b");
        o.SetFlag(Define.FLAG_IS_LINKED);

        // Act
        string? diff = w.GetObDiff(o, plain);
        string? reverse = w.GetObDiff(plain, o);

        // Assert
        Assert.Equal("other_arch b\nconnected 7\nrandomitems door\n", diff);
        Assert.Equal("randomitems none\n", reverse);
    }

    private sealed class ButtonHost : CrossfireHost
    {
        public override int GetButtonValue(CfObject op) => 7;
    }

    [Fact]
    public void SaveObject_Given_a_changed_object_When_saved_Then_the_text_is_arch_differences_end_and_it_loads_back_equal()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object sword\nname sword\nweight 5\ntype 15\nend\n", out _);
        CfObject o = w.GetArchetype("sword");
        o.Stats.Dam = 7;
        o.SetFlag(Define.FLAG_CURSED);
        o.Magic = 2;
        o.Resist[Define.ATNR_FIRE] = 10;
        o.Msg = "engraved\n";
        var sb = new System.Text.StringBuilder();

        // Act
        w.SaveObject(sb, o, 2);
        CfObject back = Load(w, sb.ToString());

        // Assert
        Assert.Equal("arch sword\nmsg\nengraved\nendmsg\ndam 7\nresist_fire 10\nmagic 2\ncursed 1\nend\n", sb.ToString());
        Assert.False(o.QueryFlag(Define.FLAG_FREED));
        Assert.Same(w.FindArchetype("sword"), back.Arch);
        Assert.Equal(7, back.Stats.Dam);
        Assert.Equal(2, back.Magic);
        Assert.Equal(10, back.Resist[Define.ATNR_FIRE]);
        Assert.Equal("engraved\n", back.Msg);
        Assert.True(back.QueryFlag(Define.FLAG_CURSED));
        Assert.Null(w.GetObDiff(back, o));
    }

    [Fact]
    public void SaveObject_Given_an_inventory_When_saved_with_bit_one_Then_the_objects_are_removed_and_freed()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object bag\nweight 1\nend\nObject gem\nvalue 5\nend\n", out _);
        CfObject bag = w.GetArchetype("bag");
        bag.ClearFlag(Define.FLAG_REMOVED);
        CfObject gem = w.GetArchetype("gem");
        gem.Value = 9;
        w.InsertObInOb(gem, bag);
        var sb = new System.Text.StringBuilder();

        // Act
        w.SaveObject(sb, bag, 0);

        // Assert
        Assert.Equal("arch bag\narch gem\nvalue 9\nend\nend\n", sb.ToString());
        Assert.True(bag.QueryFlag(Define.FLAG_FREED));
        Assert.True(gem.QueryFlag(Define.FLAG_FREED));
    }

    [Fact]
    public void SaveObject_Given_an_inventory_When_saved_with_bit_two_Then_the_inventory_comes_first_listed_to_last_and_stays()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object bag\nweight 1\nend\nObject gem\nvalue 5\nend\n", out _);
        CfObject bag = w.GetArchetype("bag");
        CfObject g1 = w.GetArchetype("gem");
        g1.Value = 9;
        CfObject g2 = w.GetArchetype("gem");
        g2.Value = 8;
        w.InsertObInOb(g1, bag);
        w.InsertObInOb(g2, bag);
        var sb = new System.Text.StringBuilder();

        // Act
        w.SaveObject(sb, bag, 2);
        CfObject back = Load(w, sb.ToString());

        // Assert
        Assert.Equal("arch bag\narch gem\nvalue 8\nend\narch gem\nvalue 9\nend\nend\n", sb.ToString());
        Assert.False(g1.QueryFlag(Define.FLAG_FREED));
        Assert.Equal(9, back.Inv!.Value);
        Assert.Equal(8, back.Inv.Below!.Value);
    }

    [Fact]
    public void SaveObject_Given_an_owned_or_unpaid_object_When_saved_Then_it_is_skipped_unless_unpaid_is_asked_for()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object a\nend\n", out _);
        CfObject owner = w.GetObject();
        CfObject owned = w.GetArchetype("a");
        CfWorld.SetOwner(owned, owner);
        CfObject unpaid = w.GetArchetype("a");
        unpaid.SetFlag(Define.FLAG_UNPAID);
        var sb = new System.Text.StringBuilder();

        // Act
        w.SaveObject(sb, owned, 2);
        w.SaveObject(sb, unpaid, 2);
        string skipped = sb.ToString();
        w.SaveObject(sb, unpaid, 3);

        // Assert
        Assert.Equal("", skipped);
        Assert.Equal("arch a\nunpaid 1\nend\n", sb.ToString());
    }

    [Fact]
    public void SaveObject_Given_an_object_without_an_archetype_When_saved_Then_the_empty_archetype_is_named()
    {
        // Arrange
        CfWorld w = T.WorldWith("", out _);
        CfObject o = T.Obj(w, "loose", 3);
        var sb = new System.Text.StringBuilder();

        // Act
        w.SaveObject(sb, o, 2);

        // Assert
        Assert.Equal("arch empty_archetype\nname loose\nname_pl loose\nweight 3\nend\n", sb.ToString());
    }

    [Fact]
    public void SaveObject_Given_unknown_keys_on_an_object_When_saved_Then_only_those_the_archetype_lacks_are_written()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object x\ngraalnpctype 2\nflag a=1\nend\n", out _);
        CfObject o = w.GetArchetype("x");
        o.Extra!.Add("flag", "b=2");
        var sb = new System.Text.StringBuilder();

        // Act
        w.SaveObject(sb, o, 2);

        // Assert
        Assert.Equal("arch x\nflag b=2\nend\n", sb.ToString());
    }

    // ---- the archetype table

    [Fact]
    public void Hasharch_Given_names_When_hashed_Then_the_values_of_the_c_function_come_out()
    {
        // Arrange / Act / Assert
        Assert.Equal(97ul, CfWorld.Hasharch("a", 5003));
        Assert.Equal(489ul, CfWorld.Hasharch("ab", 5003));
        Assert.Equal(CfWorld.Hasharch(new string('a', 20), 5003), CfWorld.Hasharch(new string('a', 20) + "XYZ", 5003));
        Assert.NotEqual(CfWorld.Hasharch(new string('a', 19), 5003), CfWorld.Hasharch(new string('a', 20), 5003));
        Assert.Equal(0ul, CfWorld.Hasharch("", 5003));
    }

    [Fact]
    public void FindArchetype_Given_colliding_names_When_asked_Then_each_is_found_by_probing()
    {
        // Arrange: "a" and a name with the same hash
        string other = "";
        ulong h = CfWorld.Hasharch("a", 5003);
        for (int i = 0; other == "" && i < 100000; i++)
        {
            string cand = "n" + i;
            if (CfWorld.Hasharch(cand, 5003) == h)
            {
                other = cand;
            }
        }
        Assert.NotEqual("", other);

        // Act
        CfWorld w = T.WorldWith("Object a\nhp 1\nend\nObject " + other + "\nhp 2\nend\n", out _);

        // Assert
        Assert.Equal(1, Clone(w, "a").Stats.Hp);
        Assert.Equal(2, Clone(w, other).Stats.Hp);
    }

    [Fact]
    public void FindArchetype_Given_a_missing_name_When_asked_Then_null_and_the_message_comes_only_after_the_load()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);

        // Act
        Archetype? before = w.FindArchetype("nosuch");
        int quiet = log.Count;
        w.InitArchetypes(T.Empty);
        Archetype? after = w.FindArchetype("nosuch");
        Archetype? nothing = w.FindArchetype(null);

        // Assert
        Assert.Null(before);
        Assert.Equal(0, quiet);
        Assert.Null(after);
        Assert.Null(nothing);
        Assert.Contains(log, l => l == "Error: Couldn't find archetype nosuch\n");
        Assert.True(w.ArchSearch >= 2);
        Assert.True(w.ArchCmp >= 1);
        Assert.Contains("searches and", w.ArchInfo());
    }

    [Fact]
    public void AddArch_Given_more_archetypes_than_the_table_has_When_added_Then_it_is_fatal()
    {
        // Arrange
        CfWorld w = T.World(out _);
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < Define.ARCHTABLE + 1; i++)
        {
            text.Append("Object o").Append(i).Append("\nend\n");
        }

        // Act
        Action act = () => w.InitArchetypes(text.ToString());

        // Assert
        Assert.Throws<CrossfireFatalException>(act);
    }

    [Fact]
    public void InitArchetypes_Given_a_second_call_When_called_Then_nothing_is_read_again()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object a\nend\n", out _);

        // Act
        w.InitArchetypes("Object b\nend\n");

        // Assert
        Assert.NotNull(w.FindArchetype("a"));
        Assert.Null(w.FindArchetype("b"));
    }

    [Fact]
    public void GetArchetype_Given_a_known_name_When_asked_Then_a_copy_with_the_archetype_is_made()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object sword\nname sword\nweight 5\nend\n", out _);

        // Act
        CfObject a = w.GetArchetype("sword");
        CfObject b = w.GetArchetype("sword");

        // Assert
        Assert.Equal("sword", a.Name);
        Assert.Equal(5, a.Weight);
        Assert.Same(w.FindArchetype("sword"), a.Arch);
        Assert.NotSame(a, b);
        Assert.NotEqual(a.Count, b.Count);
        Assert.True(a.QueryFlag(Define.FLAG_REMOVED));
    }

    [Fact]
    public void GetArchetype_Given_an_unknown_name_When_asked_Then_a_singularity_is_made()
    {
        // Arrange
        CfWorld w = T.WorldWith("", out List<string> log);

        // Act
        CfObject o = w.GetArchetype("nosuch");

        // Assert
        Assert.Equal("singluarity (nosuch)", o.Name);
        Assert.Equal("singluarity (nosuch)", o.NamePl);
        Assert.True(o.QueryFlag(Define.FLAG_NO_PICK));
        Assert.Null(o.Arch);
        Assert.Contains(log, l => l == "Error: Couldn't find archetype nosuch\n");
    }

    [Fact]
    public void ArchToObject_Given_null_When_asked_Then_null_with_an_error_after_the_load()
    {
        // Arrange
        CfWorld w = T.WorldWith("", out List<string> log);
        CfWorld fresh = T.World(out List<string> freshLog);

        // Act
        CfObject? o = w.ArchToObject(null);
        CfObject? quiet = fresh.ArchToObject(null);

        // Assert
        Assert.Null(o);
        Assert.Null(quiet);
        Assert.Contains(log, l => l == "Error: Couldn't find archetype.\n");
        Assert.Empty(freshLog);
    }

    [Fact]
    public void TypeToArchetype_Given_types_When_asked_Then_the_first_in_list_order_including_parts_is_found()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object a\ntype 5\nend\nObject b\ntype 9\nend\nObject b2\ntype 7\nMore\nend\nObject c\ntype 9\nend\n", out _);

        // Act
        Archetype? nine = w.TypeToArchetype(9);
        Archetype? seven = w.TypeToArchetype(7);
        Archetype? none = w.TypeToArchetype(99);

        // Assert
        Assert.Equal("b", nine!.Name);
        Assert.Equal("b2", seven!.Name);
        Assert.Null(none);
    }

    [Fact]
    public void CloneArch_Given_a_type_When_asked_Then_a_copy_of_the_first_archetype_of_it_and_null_when_none()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object a\nname apple\ntype 6\nend\n", out List<string> log);

        // Act
        CfObject? food = w.CloneArch(6);
        CfObject? none = w.CloneArch(99);

        // Assert
        Assert.Equal("apple", food!.Name);
        Assert.Same(w.FindArchetype("a"), food.Arch);
        Assert.Null(none);
        Assert.Contains(log, l => l == "Error: Can't clone archetype 99\n");
        Assert.Null(w.FindObjectName("nonexistent"));
    }

    [Fact]
    public void ObjectCreateArch_Given_a_multipart_archetype_When_made_Then_the_parts_are_linked_at_their_places()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object big\nface b.1\nend\nObject big2\nface b.1\nx 1\ny 0\nMore\nend\nObject big3\nface b.1\nx 1\ny 1\nMore\nend\n", out _);

        // Act
        CfObject? head = w.ObjectCreateArch(w.FindArchetype("big"));
        CfObject? none = w.ObjectCreateArch(null);

        // Assert
        Assert.NotNull(head);
        Assert.Null(none);
        CfObject p2 = head!.More!;
        CfObject p3 = p2.More!;
        Assert.Equal("big2", p2.Name);
        Assert.Equal(1, p2.X);
        Assert.Equal(1, p3.X);
        Assert.Equal(1, p3.Y);
        Assert.Same(head, p2.Head);
        Assert.Same(head, p3.Head);
        Assert.Null(p3.More);
        Assert.Null(head.Head);
    }

    [Fact]
    public void FindArchetypeByObjectName_Given_a_game_name_When_asked_Then_the_clone_name_is_searched_and_prefixes_are_tried()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object stylus\nname writing pen\nend\nObject ruby\nname ruby\nend\nObject r\nname r\nend\n", out _);

        // Act
        Archetype? pen = w.FindArchetypeByObjectName("writing pen");
        Archetype? none = w.FindArchetypeByObjectName("pen");
        CfObject exact = w.GetArchetypeByObjectName("writing pen");
        CfObject shortened = w.GetArchetypeByObjectName("rubyring");
        CfObject nothing = w.GetArchetypeByObjectName("zzz");
        CfObject oneLetter = w.GetArchetypeByObjectName("rzz");

        // Assert
        Assert.Equal("stylus", pen!.Name);
        Assert.Null(none);
        Assert.Null(w.FindArchetypeByObjectName(null));
        Assert.Same(pen, exact.Arch);
        Assert.Equal("ruby", shortened.Name);
        Assert.Equal("singluarity (zzz)", nothing.Name);
        Assert.Equal("r", oneLetter.Name);
    }

    [Fact]
    public void SecondArchPass_Given_text_with_long_lines_comments_and_crlf_When_read_Then_the_c_line_logic_applies()
    {
        // Arrange: a comment line is skipped; the value is cut at trailing white space; a missing Object is an error
        CfWorld w = T.World(out List<string> log);

        // Act
        w.InitArchetypes(T.Empty + "# Object zzz\nObject a\nother_arch b  \r\nend\nObject b\nend\n");

        // Assert
        Assert.Same(w.FindArchetype("b"), Clone(w, "a").OtherArch);
        Assert.DoesNotContain(log, l => l.Contains("zzz"));
    }

    [Fact]
    public void CheckGenerators_Given_a_generator_without_other_arch_When_loaded_Then_a_warning_is_logged()
    {
        // Arrange / Act
        CfWorld w = T.WorldWith("Object spawner\ngenerator 1\nend\nObject ok\ngenerator 1\nother_arch spawner\nend\n", out List<string> log);

        // Assert
        Assert.Contains(log, l => l == "Error: Warning: spawner is generator but lacks other_arch.\n");
        Assert.DoesNotContain(log, l => l.Contains("ok is generator"));
        Assert.NotNull(w.FindArchetype("ok"));
    }

    [Fact]
    public void GetArchetypeStruct_Given_a_call_When_made_Then_the_clone_is_cleared_and_removed_not_freed()
    {
        // Arrange
        CfWorld w = T.World(out _);

        // Act
        Archetype at = w.GetArchetypeStruct();

        // Assert
        Assert.True(at.Clone.QueryFlag(Define.FLAG_REMOVED));
        Assert.False(at.Clone.QueryFlag(Define.FLAG_FREED));
        Assert.Null(at.Name);
        Assert.Null(at.More);
        Assert.Equal(1.0, at.Clone.Expmul);
    }

    [Fact]
    public void DumpAllArchetypes_Given_archetypes_When_dumped_Then_every_one_has_its_text()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object a\nhp 3\nend\n", out _);

        // Act
        string all = w.DumpAllArchetypes();

        // Assert
        Assert.Contains("arch empty_archetype\nend\n", all);
        Assert.Contains("arch a\nname a\nname_pl a\nhp 3\nend\n", all);
        Assert.Equal("arch a\nname a\nname_pl a\nhp 3\nend\n", w.DumpArch(w.FindArchetype("a")!));
    }
}
