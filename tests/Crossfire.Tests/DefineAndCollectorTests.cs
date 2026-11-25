namespace Crossfire.Tests;

/// <summary>Tests of the constants and tables, the C runtime helpers and collect.pl. Not ported from a Crossfire file.</summary>
public class DefineTests
{
    [Fact]
    public void Define_Given_the_flag_numbers_When_read_Then_they_are_those_of_define_h()
    {
        // Arrange / Act / Assert
        Assert.Equal(0, Define.FLAG_ALIVE);
        Assert.Equal(2, Define.FLAG_REMOVED);
        Assert.Equal(3, Define.FLAG_FREED);
        Assert.Equal(8, Define.FLAG_NO_PICK);
        Assert.Equal(31, Define.FLAG_CHANGING);
        Assert.Equal(32, Define.FLAG_SPLITTING);
        Assert.Equal(63, Define.FLAG_IS_FLOOR);
        Assert.Equal(64, Define.FLAG_LIFESAVE);
        Assert.Equal(95, Define.FLAG_DUST);
        Assert.Equal(96, Define.FLAG_NO_STEAL);
        Assert.Equal(98, Define.FLAG_CLIENT_SENT);
        Assert.Equal(104, Define.FLAG_OBJ_SAVE_ON_OVL);
        Assert.Equal(104, Define.NUM_FLAGS);
    }

    [Fact]
    public void Define_Given_the_object_types_When_read_Then_they_are_those_of_define_h()
    {
        // Arrange / Act / Assert
        Assert.Equal(1, Define.PLAYER);
        Assert.Equal(15, Define.WEAPON);
        Assert.Equal(16, Define.ARMOUR);
        Assert.Equal(23, Define.DOOR);
        Assert.Equal(36, Define.MONEY);
        Assert.Equal(70, Define.RING);
        Assert.Equal(71, Define.FLOOR);
        Assert.Equal(80, Define.MONSTER);
        Assert.Equal(122, Define.CONTAINER);
        Assert.Equal(154, Define.RUNE);
        Assert.Equal(159, Define.SYMPTOM);
    }

    [Fact]
    public void FlagLinks_Given_the_table_When_read_Then_there_is_one_entry_for_each_flag_in_order()
    {
        // Arrange / Act
        var links = Define.FlagLinks;

        // Assert
        Assert.Equal(Define.NUM_FLAGS + 1, links.Length);
        for (int i = 0; i < links.Length; i++)
        {
            Assert.True(links[i].Flag == i || links[i].Flag == -1, "entry " + i);
            Assert.Equal(links[i].Flag == -1 || links[i].Flag == Define.FLAG_SLOW_MOVE, links[i].Keyword == null);
        }
        Assert.Equal("alive", links[Define.FLAG_ALIVE].Keyword);
        Assert.Equal("is_animated", links[Define.FLAG_ANIMATE].Keyword);
        Assert.Equal("can_cast_spell", links[Define.FLAG_CAST_SPELL].Keyword);
        Assert.Equal("random_movement", links[Define.FLAG_RANDOM_MOVE].Keyword);
        Assert.Equal("make_invisible", links[Define.FLAG_MAKE_INVIS].Keyword);
        Assert.Equal("no_damage", links[Define.FLAG_NO_DAMAGE].Keyword);
        Assert.Null(links[Define.FLAG_OBJ_ORIGINAL].Keyword);
    }

    [Fact]
    public void Define_Given_the_attack_numbers_and_types_When_read_Then_each_type_is_the_bit_of_its_number()
    {
        // Arrange / Act / Assert
        Assert.Equal(25, Define.NROFATTACKS);
        Assert.Equal(1u, Define.AT_PHYSICAL);
        Assert.Equal(1u << Define.ATNR_FIRE, Define.AT_FIRE);
        Assert.Equal(1u << Define.ATNR_POISON, Define.AT_POISON);
        Assert.Equal(1u << Define.ATNR_BLIND, Define.AT_BLIND);
        Assert.Equal(1u << Define.ATNR_LIFE_STEALING, Define.AT_LIFE_STEALING);
        Assert.Equal(24, Define.ATNR_LIFE_STEALING);
        Assert.Equal(30, Define.RESIST_PROT);
        Assert.Equal(-100, Define.RESIST_VULN);
    }

    [Fact]
    public void Define_Given_the_masks_and_movement_When_read_Then_the_values_of_define_h_come_out()
    {
        // Arrange / Act / Assert
        Assert.Equal(0x80000000u, Define.PU_NEWMODE);
        Assert.Equal(0x00080000u, Define.PATH_LIGHT);
        Assert.Equal(16, Define.PETMOVE);
        Assert.Equal(32, Define.CIRCLE1);
        Assert.Equal(240, Define.HI4);
        Assert.Equal(4096, Define.M_ICE);
        Assert.Equal(13, Define.NROFMATERIALS);
        Assert.Equal(0x0008, Define.INS_ON_TOP);
        Assert.Equal(4, Define.UP_OBJ_FACE);
        Assert.Equal(26, Define.NR_EVENTS);
        Assert.Equal(5003, Define.ARCHTABLE);
        Assert.Equal(30, Define.MAX_STAT);
        Assert.Equal(4096, Define.HUGE_BUF);
    }

    [Fact]
    public void Tables_Given_the_names_When_read_Then_each_table_has_the_size_of_its_c_array()
    {
        // Arrange / Act / Assert
        Assert.Equal(Define.NROFATTACKS, Tables.ResistSave.Length);
        Assert.Equal(Define.NROFATTACKS, Tables.AttacktypeDesc.Length);
        Assert.Equal(Define.NROFATTACKS, Tables.ResistPlus.Length);
        Assert.Equal(Define.NROFATTACKS, Tables.ChangeResistMsg.Length);
        Assert.Equal(Define.NROFATTACKS, Tables.Attacks.Length);
        Assert.Equal(Define.NRSPELLPATHS, Tables.Spellpathnames.Length);
        Assert.Equal(7, Tables.Statname.Length);
        Assert.Equal(Tables.NumResistTable, Tables.ResistTable.Length);
        Assert.Equal("acid", Tables.ResistSave[Define.ATNR_ACID]);
        Assert.Equal("life_stealing", Tables.ResistSave[Define.ATNR_LIFE_STEALING]);
        Assert.Equal("resist weaponmagic", Tables.ResistPlus[Define.ATNR_WEAPONMAGIC]);
        Assert.Equal("Light", Tables.Spellpathnames[19]);
        Assert.Equal("Pow", Tables.ShortStatName[Define.POW]);
        Assert.Equal("power", Tables.Statname[Define.POW]);
    }

    [Fact]
    public void Tables_Given_the_materials_When_read_Then_the_thirteen_of_item_c_are_there()
    {
        // Arrange / Act
        MaterialType[] m = Tables.Material;

        // Assert
        Assert.Equal(Define.NROFMATERIALS, m.Length);
        Assert.Equal("paper", m[0].Name);
        Assert.Equal("soft metal", m[10].Name);
        Assert.Equal("ice", m[12].Name);
        Assert.Equal(2, m[1].Save[Define.ATNR_PHYSICAL]);
        Assert.Equal(12, m[1].Save[Define.ATNR_MAGIC]);
        Assert.Equal(17, m[9].Save[Define.ATNR_COLD]);
        Assert.Equal(12, m[9].Save[Define.ATNR_POISON]);
        Assert.Equal(20, m[0].Save[Define.ATNR_WEAPONMAGIC]);
        Assert.Equal(Define.NROFATTACKS, m[0].Save.Length);
        Assert.Equal(0, m[0].Save[Define.ATNR_LIFE_STEALING]);
    }

    [Fact]
    public void FreeArrays_Given_the_tables_When_read_Then_they_have_the_size_of_the_search_area()
    {
        // Arrange / Act / Assert
        Assert.Equal(Define.SIZEOFFREE, FreeArrays.X.Length);
        Assert.Equal(Define.SIZEOFFREE, FreeArrays.Y.Length);
        Assert.Equal(Define.SIZEOFFREE, FreeArrays.MaxFree.Length);
        Assert.Equal(Define.SIZEOFFREE, FreeArrays.FreeDir.Length);
        Assert.Equal(-1, FreeArrays.Y[1]);
        Assert.Equal(3, FreeArrays.X[28]);
    }

    [Fact]
    public void SetFlag_Given_every_flag_When_set_and_cleared_Then_only_its_bit_changes()
    {
        // Arrange
        var o = new CfObject();

        for (int f = 0; f <= Define.NUM_FLAGS; f++)
        {
            // Act
            o.SetFlag(f);

            // Assert
            Assert.True(o.QueryFlag(f));
            Assert.Equal(1U << (f % 32), o.Flags[f / 32]);
            o.ClearFlag(f);
            Assert.Equal(0u, o.Flags[f / 32]);
        }
    }
}

public class CStrTests
{
    [Theory]
    [InlineData("12", 12)]
    [InlineData("  -7xyz", -7)]
    [InlineData("+3", 3)]
    [InlineData("abc", 0)]
    [InlineData("", 0)]
    [InlineData("1.9", 1)]
    [InlineData("99999999999", int.MaxValue)]
    [InlineData("-99999999999999999999999", int.MinValue)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("2147483648", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void Atoi_Given_text_When_converted_Then_the_c_result_comes_out(string text, int expected)
    {
        // Arrange / Act
        int v = CStr.Atoi(text);

        // Assert
        Assert.Equal(expected, v);
        Assert.Equal(expected, CStr.Atol(text));
    }

    [Theory]
    [InlineData("0.3", 0.3)]
    [InlineData("1e2x", 100.0)]
    [InlineData(".5", 0.5)]
    [InlineData("-.5e-1", -0.05)]
    [InlineData("e5", 0.0)]
    [InlineData("1e", 1.0)]
    [InlineData("  2.5 more", 2.5)]
    [InlineData("1.", 1.0)]
    [InlineData("-", 0.0)]
    public void Atof_Given_text_When_converted_Then_the_longest_decimal_prefix_is_used(string text, double expected)
    {
        // Arrange / Act
        double v = CStr.Atof(text);

        // Assert
        Assert.Equal(expected, v, 12);
    }

    [Fact]
    public void Atoi_Given_null_When_converted_Then_zero()
    {
        // Arrange / Act / Assert
        Assert.Equal(0, CStr.Atoi(null));
        Assert.Equal(0.0, CStr.Atof(null));
    }

    [Fact]
    public void ToInt_Given_doubles_When_converted_Then_they_truncate_toward_zero()
    {
        // Arrange / Act / Assert
        Assert.Equal(1, CStr.ToInt(1.9));
        Assert.Equal(-1, CStr.ToInt(-1.9));
        Assert.Equal(500, CStr.ToInt(0.5 * 1000));
        Assert.Equal(3u, CStr.ToUInt(3.7));
    }

    [Fact]
    public void Fmt_Given_a_number_When_formatted_Then_six_decimals_like_percent_f()
    {
        // Arrange / Act / Assert
        Assert.Equal("0.500000", CStr.Fmt(0.5));
        Assert.Equal("-0.100000", CStr.Fmt(-0.1f));
        Assert.Equal("1.000000", CStr.Fmt(1));
    }
}

public class CollectorTests
{
    private static string Write(string root, string rel, string content)
    {
        string path = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, CStr.Latin1.GetBytes(content));
        return path;
    }

    [Fact]
    public void Collect_Given_a_folder_of_arc_files_When_collected_Then_the_files_come_in_sorted_order_and_skip_the_dev_folders()
    {
        // Arrange
        string root = T.TempDir();
        try
        {
            Write(root, "b.arc", "Object plain\nhp 3\nend\n");
            Write(root, "a/x.arc", "Object first\nend\n");
            Write(root, "dev/skip.arc", "Object skipped1\nend\n");
            Write(root, "trashbin/skip.arc", "Object skipped2\nend\n");
            Write(root, "CVS/skip.arc", "Object skipped3\nend\n");
            Write(root, ".hidden/skip.arc", "Object skipped4\nend\n");
            Write(root, ".hidden.arc", "Object skipped5\nend\n");
            Write(root, "notes.txt", "Object skipped6\nend\n");
            Write(root, "Z.arc", "Object upper\nend\n");

            // Act
            ArchCollection c = ArchCollector.Collect(root);

            // Assert
            Assert.Equal("Object upper\nend\nObject first\nend\nObject plain\nhp 3\nend\n", c.Archetypes);
            Assert.Equal(3, c.Files.Count);
            Assert.Equal("", c.Animations);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Collect_Given_an_anim_block_When_collected_Then_it_becomes_an_animation_line_and_an_animations_file()
    {
        // Arrange
        string root = T.TempDir();
        try
        {
            Write(root, "x.arc", "Object sprite\ncolor_fg red\nvisibility 3\nanim\nsprite.1\nfacings 2\nsprite.2\nmina\nface sprite.1\nend\n");
            Write(root, "y.arc", "Object ghost\nanim\nghost.1\nmina\nend\n");

            // Act
            ArchCollection c = ArchCollector.Collect(root);

            // Assert
            Assert.Equal("Object sprite\nanimation sprite\nface sprite.1\nend\nObject ghost\nanimation ghost\nend\n", c.Archetypes);
            Assert.Equal("anim ghost\nghost.1\nmina\nanim sprite\nsprite.1\nfacings 2\nsprite.2\nmina\n", c.Animations);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Collect_Given_missing_faces_and_duplicate_animations_When_collected_Then_warnings_are_listed()
    {
        // Arrange
        string root = T.TempDir();
        try
        {
            Write(root, "x.arc", "Object sprite\nanim\nsprite.1\nsprite.2\nmina\nface sprite.9\nend\nObject sprite\nanim\nsprite.1\nmina\nend\n");

            // Act
            ArchCollection c = ArchCollector.Collect(root, name => name != "sprite.2" && name != "sprite.9");

            // Assert
            Assert.Contains("sprite is missing face sprite.2", c.Warnings);
            Assert.Contains("sprite is missing face sprite.9", c.Warnings);
            Assert.Contains("sprite is a duplicate animation name", c.Warnings);
            Assert.Equal("anim sprite\nsprite.1\nmina\n", c.Animations);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Collect_Given_a_file_without_a_final_newline_When_collected_Then_the_last_character_is_chopped_as_perl_does()
    {
        // Arrange
        string root = T.TempDir();
        try
        {
            Write(root, "x.arc", "Object z\nend");

            // Act
            ArchCollection c = ArchCollector.Collect(root);

            // Assert
            Assert.Equal("Object z\nen\n", c.Archetypes);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Collect_Given_the_result_When_loaded_Then_the_animation_of_the_object_is_found()
    {
        // Arrange
        string root = T.TempDir();
        try
        {
            Write(root, "x.arc", "Object sprite\nanim\nsprite.1\nsprite.2\nmina\nface sprite.1\nname sprite\nend\n");
            ArchCollection c = ArchCollector.Collect(root);
            CfWorld w = T.World(out _);
            w.Faces.Register("sprite.1");
            w.Faces.Register("sprite.2");

            // Act
            w.Animations.Load(c.Animations);
            w.InitArchetypes(T.Empty + c.Archetypes);

            // Assert
            CfObject clone = w.FindArchetype("sprite")!.Clone;
            Assert.Equal(1, clone.AnimationId);
            Assert.True(clone.QueryFlag(Define.FLAG_ANIMATE));
            Assert.Equal(2, w.Animations[1].NumAnimations);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

public class NoticeTests
{
    [Fact]
    public void Sources_Given_every_file_of_the_port_When_read_Then_it_starts_with_its_notices()
    {
        // Arrange
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Crossfire.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        if (dir == null)
        {
            return; // the sources are not beside the tests
        }
        string[] files = Directory.GetFiles(Path.Combine(dir, "src", "Crossfire"), "*.cs");

        // Act / Assert
        Assert.True(files.Length >= 10);
        foreach (string f in files)
        {
            string head = string.Join("\n", File.ReadLines(f).Take(30));
            bool ported = head.StartsWith("// Port to C# of Crossfire's server (commit 8b7ee7f90");
            bool notPorted = head.StartsWith("// Not ported from a Crossfire file");
            Assert.True(ported || notPorted, f);
            if (ported)
            {
                Assert.Contains("Copyright (C)", head);
                Assert.Contains("GNU General Public License", head);
                Assert.Contains("either version 2 of the License, or", head);
            }
        }
    }
}
