using System.Text;

namespace Crossfire.Tests;

/// <summary>Tests of the port of common/map.c (the header and the object list of a map file) and server/player.c get_player_archetype. Not ported from a Crossfire file.</summary>
public class MapFileTests
{
    private sealed class Square : IMapHandle
    {
        public CfObject? Bottom { get; set; }
    }

    private sealed class MapHost : CrossfireHost
    {
        public List<(CfObject Op, int Flag)> Inserted { get; } = new();

        public List<(int X, int Y)> Asked { get; } = new();

        public CfObject? Existing { get; set; }

        public override CfObject? InsertObInMap(CfObject op, IMapHandle map, CfObject? originator, int flag)
        {
            Inserted.Add((op, flag));
            return op;
        }

        public override CfObject? GetMapOb(IMapHandle map, int x, int y)
        {
            Asked.Add((x, y));
            return Existing;
        }
    }

    private const string Arc = "Object sword\nname sword\nname_pl swords\ntype 15\nweight 3000\nend\n"
        + "Object bag\nname bag\ntype 122\nweight 100\nend\n"
        + "Object human_player\nname human\ntype 1\nend\n"
        + "Object orc_player\nname orc\ntype 1\nend\n";

    private static CfWorld World(out List<string> log, MapHost? host = null) => T.WorldWith(Arc, out log, host);

    // ------------------------------------------------------------------ load_map_header

    [Fact]
    public void LoadMapHeader_Given_every_key_When_read_Then_the_fields_are_set_and_the_block_ends_at_end()
    {
        // Arrange
        CfWorld w = World(out List<string> log);
        string text = "arch map\nname The Hall\nreset_timeout 100\nswap_time 20\ndifficulty 3\ndarkness 2\nfixed_resettime 1\nwidth 30\nheight 40\n"
            + "enter_x 5\nenter_y 6\nunique 1\noutdoor 1\ntemp 7\npressure 8\nhumid 9\nwindspeed 10\nwinddir 3\nsky 4\n"
            + "tile_path_1 /a\ntile_path_4 /d\nend\narch sword\nend\n";
        var src = new LoadSource(text);
        var m = new MapHeader();

        // Act
        int r = w.LoadMapHeader(src, m);

        // Assert
        Assert.Equal(0, r);
        Assert.Equal("The Hall", m.Name);
        Assert.Equal(new[] { 100, 20, 3, 2, 1, 30, 40, 5, 6 }, new[] { m.ResetTimeout, m.Timeout, m.Difficulty, m.Darkness, m.FixedResettime, m.Width, m.Height, m.EnterX, m.EnterY });
        Assert.Equal(new[] { 1, 1, 7, 8, 9, 10, 3, 4 }, new[] { m.Unique, m.Outdoor, m.Temp, m.Pressure, m.Humid, m.Windspeed, m.Winddir, m.Sky });
        Assert.Equal(new[] { "/a", null, null, "/d" }, m.TilePath);
        Assert.Equal("arch sword\nend\n", text.Substring(src.Pos));
        Assert.DoesNotContain(log, l => l.StartsWith("Error"));
    }

    [Fact]
    public void LoadMapHeader_Given_the_old_names_of_the_keys_When_read_Then_they_set_the_same_fields()
    {
        // Arrange
        CfWorld w = World(out _);
        var m = new MapHeader();

        // Act
        w.LoadMapHeader(new LoadSource("arch map\nhp 3\nsp 4\nx 11\ny 12\nweight 50\nvalue 60\nlevel 7\ninvisible 2\nstand_still 1\nend\n"), m);

        // Assert
        Assert.Equal(new[] { 3, 4, 11, 12, 50, 60, 7, 2, 1 }, new[] { m.EnterX, m.EnterY, m.Width, m.Height, m.ResetTimeout, m.Timeout, m.Difficulty, m.Darkness, m.FixedResettime });
    }

    [Fact]
    public void LoadMapHeader_Given_no_keys_When_read_Then_the_defaults_of_get_linked_map_stay()
    {
        // Arrange
        CfWorld w = World(out _);
        var m = new MapHeader();

        // Act
        w.LoadMapHeader(new LoadSource("arch map\nend\n"), m);

        // Assert
        Assert.Equal(new[] { 16, 16, 7200, 300, 1, 1, 0 }, new[] { m.Width, m.Height, m.ResetTimeout, m.Timeout, m.EnterX, m.EnterY, m.SwapTime });
        Assert.Null(m.Name);
        Assert.Null(m.Msg);
    }

    [Fact]
    public void LoadMapHeader_Given_a_message_When_read_Then_the_lines_up_to_endmsg_and_an_empty_message_is_not_kept()
    {
        // Arrange
        CfWorld w = World(out _);
        var full = new MapHeader();
        var empty = new MapHeader();

        // Act
        w.LoadMapHeader(new LoadSource("arch map\nmsg\nline one\nline two\nendmsg\nend\n"), full);
        w.LoadMapHeader(new LoadSource("arch map\nmsg\nendmsg\nend\n"), empty);

        // Assert
        Assert.Equal("line one\nline two\n", full.Msg);
        Assert.Null(empty.Msg);
    }

    [Fact]
    public void LoadMapHeader_Given_a_text_that_ends_before_end_When_read_Then_1_and_a_premature_eof_error()
    {
        // Arrange
        CfWorld w = World(out List<string> log);

        // Act
        int r = w.LoadMapHeader(new LoadSource("arch map\nname x\n"), new MapHeader());
        int none = w.LoadMapHeader(new LoadSource(""), new MapHeader());

        // Assert
        Assert.Equal(1, r);
        Assert.Equal(1, none);
        Assert.Contains(log, l => l.Contains("Got premature eof on map header!"));
    }

    [Fact]
    public void LoadMapHeader_Given_an_unknown_key_a_wrong_first_line_and_bad_tiles_When_read_Then_each_is_an_error_and_reading_goes_on()
    {
        // Arrange
        CfWorld w = World(out List<string> log);
        var m = new MapHeader();

        // Act
        int r = w.LoadMapHeader(new LoadSource("arch notmap\nbogus 1\ntile_path_5 /x\ntile_path_2 /b\ntile_path_2 /c\nname n\nend\n"), m);

        // Assert
        Assert.Equal(0, r);
        Assert.Equal("n", m.Name);
        Assert.Equal("/c", m.TilePath[1]);
        Assert.Contains(log, l => l.Contains("loading map and got a non 'arch map' line"));
        Assert.Contains(log, l => l.Contains("Got unknown value in map header: bogus 1"));
        Assert.Contains(log, l => l.Contains("tile location 5 out of bounds"));
        Assert.Contains(log, l => l.Contains("tile location 2 duplicated"));
    }

    [Fact]
    public void LoadMapHeader_Given_blank_lines_and_indented_keys_and_a_key_without_a_value_When_read_Then_they_are_skipped_trimmed_and_unknown()
    {
        // Arrange
        CfWorld w = World(out List<string> log);
        var m = new MapHeader();

        // Act
        int r = w.LoadMapHeader(new LoadSource("arch map\n\n   \n  width   9  \nlonely\nend\n"), m);

        // Assert
        Assert.Equal(0, r);
        Assert.Equal(9, m.Width);
        Assert.Contains(log, l => l.Contains("Got unknown value in map header: lonely (null)"));
    }

    // ------------------------------------------------------------------ new_save_map (the header)

    [Fact]
    public void SaveMapHeader_Given_a_header_When_saved_with_flag_1_Then_the_fields_that_are_not_0_in_the_order_of_C_and_it_reads_back()
    {
        // Arrange
        CfWorld w = World(out _);
        var m = new MapHeader
        {
            Name = "Hall", ResetTimeout = 100, FixedResettime = 1, Difficulty = 3, Darkness = 2, Width = 8, Height = 9, EnterX = 2, EnterY = 3,
            Msg = "hello\n", Unique = 1, Outdoor = 1, Temp = 4, Pressure = 5, Humid = 6, Windspeed = 7, Winddir = 8, Sky = 9, SwapTime = 11,
        };
        m.TilePath[0] = "/n";
        m.TilePath[3] = "/w";
        var sb = new StringBuilder();

        // Act
        w.SaveMapHeader(sb, m, 1);
        var back = new MapHeader();
        int r = w.LoadMapHeader(new LoadSource(sb.ToString()), back);

        // Assert
        Assert.Equal("arch map\nname Hall\nreset_timeout 100\nfixed_resettime 1\ndifficulty 3\ndarkness 2\nwidth 8\nheight 9\nenter_x 2\nenter_y 3\n"
            + "msg\nhello\nendmsg\nunique 1\noutdoor 1\ntemp 4\npressure 5\nhumid 6\nwindspeed 7\nwinddir 8\nsky 9\ntile_path_1 /n\ntile_path_4 /w\nend\n", sb.ToString());
        Assert.Equal(0, r);
        Assert.Equal("Hall", back.Name);
        Assert.Equal("hello\n", back.Msg);
        Assert.Equal(new[] { "/n", null, null, "/w" }, back.TilePath);
    }

    [Fact]
    public void SaveMapHeader_Given_flag_0_When_saved_Then_swap_time_is_written_after_the_name_and_zero_fields_are_left_out()
    {
        // Arrange
        CfWorld w = World(out _);
        var m = new MapHeader { Name = "x", SwapTime = 0, ResetTimeout = 0, Width = 0, Height = 0, EnterX = 0, EnterY = 0 };
        var sb = new StringBuilder();

        // Act
        w.SaveMapHeader(sb, m, 0);

        // Assert
        Assert.Equal("arch map\nname x\nswap_time 0\nend\n", sb.ToString());
    }

    // ------------------------------------------------------------------ load_objects

    [Fact]
    public void LoadObjects_Given_blocks_When_read_Then_each_object_with_an_archetype_goes_to_insert_ob_in_map_in_order_with_the_C_flags()
    {
        // Arrange
        var host = new MapHost();
        CfWorld w = World(out _, host);
        var map = new Square();

        // Act
        w.LoadObjects(map, new LoadSource("arch sword\nx 3\ny 4\nend\narch bag\nend\n"), 0);

        // Assert
        Assert.Equal(new[] { "sword", "bag" }, host.Inserted.Select(i => i.Op.Name));
        Assert.All(host.Inserted, i => Assert.Equal(Define.INS_NO_MERGE | Define.INS_NO_WALK_ON | Define.INS_ON_TOP, i.Flag));
        Assert.Equal(3, host.Inserted[0].Op.X);
        Assert.Same(map, host.Inserted[0].Op.Map);
        Assert.Equal(new[] { (3, 4), (0, 0) }, host.Asked);
    }

    [Fact]
    public void LoadObjects_Given_a_block_of_an_unknown_archetype_When_read_Then_it_is_discarded_with_a_debug_message()
    {
        // Arrange
        var host = new MapHost();
        CfWorld w = World(out List<string> log, host);

        // Act
        w.LoadObjects(new Square(), new LoadSource("arch nosuch\nname ghost\nend\narch sword\nend\n"), 0);

        // Assert
        Assert.Equal(new[] { "sword" }, host.Inserted.Select(i => i.Op.Name));
        Assert.Contains(log, l => l.Contains("Discarded object ghost - invalid archetype."));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(Define.MAP_PLAYER_UNIQUE, false)]
    [InlineData(Define.MAP_OVERLAY, false)]
    [InlineData(Define.MAP_BLOCK, true)]
    public void LoadObjects_Given_the_map_flags_When_read_Then_FLAG_OBJ_ORIGINAL_is_set_unless_the_map_is_an_overlay_or_a_player_s(int mapFlags, bool original)
    {
        // Arrange
        var host = new MapHost();
        CfWorld w = World(out _, host);

        // Act
        w.LoadObjects(new Square(), new LoadSource("arch sword\nend\n"), mapFlags);

        // Assert
        Assert.Equal(original, host.Inserted.Single().Op.QueryFlag(Define.FLAG_OBJ_ORIGINAL));
    }

    [Fact]
    public void LoadObjects_Given_a_unique_item_or_a_unique_item_on_the_square_When_read_Then_the_object_is_not_original()
    {
        // Arrange
        var host = new MapHost();
        CfWorld w = World(out _, host);
        CfObject unique = w.GetObject();
        unique.SetFlag(Define.FLAG_UNIQUE);
        host.Existing = unique;

        // Act
        w.LoadObjects(new Square(), new LoadSource("arch sword\nend\n"), 0);
        host.Existing = null;
        host.Inserted.Clear();
        w.LoadObjects(new Square(), new LoadSource("arch sword\nunique 1\nend\narch sword\nend\n"), 0);

        // Assert
        Assert.False(host.Inserted[0].Op.QueryFlag(Define.FLAG_OBJ_ORIGINAL));
        Assert.True(host.Inserted[1].Op.QueryFlag(Define.FLAG_OBJ_ORIGINAL));
    }

    [Fact]
    public void LoadObjects_Given_a_block_ended_as_a_further_part_When_read_Then_it_is_linked_to_the_object_before_it()
    {
        // Arrange
        var host = new MapHost();
        CfWorld w = World(out _, host);

        // Act
        w.LoadObjects(new Square(), new LoadSource("arch sword\nend\narch bag\nMore\nend\narch bag\nMore\nend\n"), 0);

        // Assert
        Assert.Equal(3, host.Inserted.Count);
        CfObject head = host.Inserted[0].Op;
        Assert.Equal(Define.INS_NO_MERGE | Define.INS_NO_WALK_ON | Define.INS_ABOVE_FLOOR_ONLY, host.Inserted[1].Flag);
        Assert.Same(head, host.Inserted[1].Op.Head);
        Assert.Same(host.Inserted[1].Op, head.More);
        Assert.Same(host.Inserted[2].Op, host.Inserted[1].Op.More);
    }

    [Fact]
    public void LoadObjects_Given_an_object_with_an_inventory_When_read_Then_the_weight_of_its_contents_is_summed()
    {
        // Arrange
        var host = new MapHost();
        CfWorld w = World(out _, host);

        // Act
        w.LoadObjects(new Square(), new LoadSource("arch bag\narch sword\nend\nend\n"), 0);

        // Assert
        CfObject bag = host.Inserted.Single().Op;
        Assert.NotNull(bag.Inv);
        Assert.Equal(3000, bag.Carrying);
    }

    // ------------------------------------------------------------------ save_objects

    private static string Save(CfWorld w, int flag, out string unique, params CfObject[][] squares)
    {
        var fp = new StringBuilder();
        var fp2 = new StringBuilder();
        w.SaveObjects(fp, fp2, squares, flag);
        unique = fp2.ToString();
        return fp.ToString();
    }

    private static CfObject Make(CfWorld w, string arch) => w.ArchToObject(w.FindArchetype(arch))!;

    [Fact]
    public void SaveObjects_Given_squares_When_saved_with_flag_0_Then_every_object_is_written_by_save_object_in_the_order_given()
    {
        // Arrange
        CfWorld w = World(out _);
        CfObject a = Make(w, "sword");
        CfObject b = Make(w, "bag");
        b.Stats.Hp = 9;

        // Act
        string text = Save(w, 0, out string unique, new[] { a }, new[] { b });

        // Assert
        Assert.Equal("arch sword\nend\narch bag\nhp 9\nend\n", text);
        Assert.Equal("", unique);
    }

    [Fact]
    public void SaveObjects_Given_unpaid_objects_When_saved_Then_they_are_saved_with_flag_0_and_left_out_with_flag_2_with_the_original_ones()
    {
        // Arrange
        CfWorld w = World(out _);
        CfObject unpaid = Make(w, "sword");
        unpaid.SetFlag(Define.FLAG_UNPAID);
        CfObject original = Make(w, "bag");
        original.SetFlag(Define.FLAG_OBJ_ORIGINAL);
        CfObject plain = Make(w, "sword");
        plain.Stats.Hp = 1;

        // Act
        string all = Save(w, 0, out _, new[] { unpaid, original, plain });
        string changed = Save(w, 2, out _, new[] { unpaid, original, plain });

        // Assert
        Assert.Equal("arch sword\nunpaid 1\nend\narch bag\nend\narch sword\nhp 1\nend\n", all);
        Assert.Equal("arch sword\nhp 1\nend\n", changed);
    }

    [Fact]
    public void SaveObjects_Given_players_parts_and_owned_objects_When_saved_Then_they_are_skipped()
    {
        // Arrange
        CfWorld w = World(out List<string> log);
        CfObject player = Make(w, "human_player");
        CfObject part = Make(w, "sword");
        part.Head = Make(w, "bag");
        CfObject owned = Make(w, "sword");
        owned.Owner = player;
        CfObject kept = Make(w, "bag");

        // Act
        string text = Save(w, 0, out _, new[] { player, part, owned, kept });

        // Assert
        Assert.Equal("arch bag\nend\n", text);
        Assert.Contains(log, l => l.Contains("Player on map that is being saved"));
    }

    [Fact]
    public void SaveObjects_Given_unique_objects_and_a_square_with_a_unique_floor_When_saved_Then_they_go_to_the_second_text_and_the_floor_does_not_carry_to_the_next_square()
    {
        // Arrange
        CfWorld w = World(out _);
        CfObject floor = Make(w, "sword");
        floor.SetFlag(Define.FLAG_IS_FLOOR);
        floor.SetFlag(Define.FLAG_UNIQUE);
        CfObject above = Make(w, "bag");
        CfObject unique = Make(w, "bag");
        unique.SetFlag(Define.FLAG_UNIQUE);
        CfObject other = Make(w, "sword");

        // Act
        string text = Save(w, 0, out string uniqueText, new[] { floor, above }, new[] { unique }, new[] { other });

        // Assert
        Assert.Equal("arch sword\nend\n", text);
        Assert.Equal(2, uniqueText.Split("arch bag").Length - 1);
        Assert.Equal(1, uniqueText.Split("arch sword").Length - 1);
        Assert.Contains("arch bag\nunique 1\nend\n", uniqueText);
    }

    [Fact]
    public void SaveObjects_Given_a_saved_text_When_loaded_by_load_objects_Then_the_objects_come_back_equal()
    {
        // Arrange
        var host = new MapHost();
        CfWorld w = World(out _, host);
        CfObject bag = Make(w, "bag");
        bag.Stats.Hp = 4;
        bag.Name = "my bag";
        w.InsertObInOb(Make(w, "sword"), bag);
        string text = Save(w, 0, out _, new[] { bag });

        // Act
        w.LoadObjects(new Square(), new LoadSource(text), Define.MAP_PLAYER_UNIQUE);

        // Assert
        CfObject back = host.Inserted.Single().Op;
        Assert.Equal("my bag", back.Name);
        Assert.Equal(4, back.Stats.Hp);
        Assert.Equal("sword", back.Inv!.Name);
    }

    // ------------------------------------------------------------------ get_player_archetype

    [Fact]
    public void GetPlayerArchetype_Given_null_When_asked_Then_the_first_archetype_of_type_player_in_list_order()
    {
        // Arrange
        CfWorld w = World(out _);

        // Act
        Archetype? first = w.GetPlayerArchetype(null);

        // Assert
        Assert.Equal("human_player", first!.Name);
    }

    [Fact]
    public void GetPlayerArchetype_Given_a_player_archetype_When_asked_Then_the_next_one_and_after_the_last_the_first_again()
    {
        // Arrange
        CfWorld w = World(out _);
        Archetype first = w.GetPlayerArchetype(null)!;

        // Act
        Archetype second = w.GetPlayerArchetype(first)!;
        Archetype wrapped = w.GetPlayerArchetype(second)!;

        // Assert
        Assert.Equal("orc_player", second.Name);
        Assert.Same(first, wrapped);
    }

    [Fact]
    public void GetPlayerArchetype_Given_a_world_without_players_When_asked_Then_null_and_an_error_where_C_would_exit_or_loop()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object sword\ntype 15\nend\n", out List<string> log);

        // Act
        Archetype? none = w.GetPlayerArchetype(null);
        Archetype? fromSword = w.GetPlayerArchetype(w.FindArchetype("sword"));

        // Assert
        Assert.Null(none);
        Assert.Null(fromSword);
        Assert.Equal(2, log.Count(l => l.Contains("No Player achetypes")));
    }

    // ------------------------------------------------------------------ ExtraKeys (not in C)

    [Fact]
    public void ExtraKeys_Given_set_and_remove_When_used_Then_the_last_value_is_the_only_one_and_case_does_not_matter()
    {
        // Arrange
        var extra = new ExtraKeys();
        extra.Add("flag", "a=1");
        extra.Add("Flag", "b=2");
        extra.Add("other", "x");

        // Act
        extra.Set("FLAG", "c=3");
        string? got = extra.Get("flag");
        extra.Remove("other");

        // Assert
        Assert.Equal("c=3", got);
        Assert.Equal(1, extra.Count);
        extra.Remove("flag");
        Assert.Equal(0, extra.Count);
        extra.Set("new", "v");
        Assert.Equal("v", extra.Get("new"));
    }
}
