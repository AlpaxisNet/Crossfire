namespace Crossfire.Tests;

/// <summary>Tests added after the mutation check showed behaviour no test had pinned down. Not ported from a Crossfire file.</summary>
public class SurvivorTests
{
    private static CfObject Placed(CfWorld w, string name, int weight = 0, uint nrof = 0)
    {
        CfObject o = T.Obj(w, name, weight, nrof);
        o.ClearFlag(Define.FLAG_REMOVED);
        return o;
    }

    private static CfObject Load(CfWorld w, string text, int mapFlags = 0)
    {
        CfObject op = w.GetObject();
        w.LoadObject(new LoadSource(text), op, Define.LO_NEWFILE, mapFlags);
        return op;
    }

    [Fact]
    public void MergeOb_Given_a_matching_object_above_When_merged_Then_the_search_starts_from_the_top()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject low = Placed(w, "arrow", 2, 4);
        CfObject op = Placed(w, "arrow", 2, 3);
        CfObject high = Placed(w, "arrow", 2, 9);
        low.Above = op;
        op.Below = low;
        op.Above = high;
        high.Below = op;

        // Act
        CfObject? r = w.MergeOb(op, null);

        // Assert
        Assert.Same(high, r);
        Assert.Equal(12u, high.Nrof);
    }

    [Fact]
    public void DecreaseObNr_Given_zero_for_an_object_in_an_inventory_When_decreased_Then_the_client_hears_nothing()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        CfObject arrows = T.Obj(w, "arrow", 2, 5);
        w.InsertObInOb(arrows, player);
        host.Calls.Clear();

        // Act
        w.DecreaseObNr(arrows, 0);

        // Assert
        Assert.Empty(host.Calls);
    }

    [Fact]
    public void DecreaseObNr_Given_more_than_there_is_for_a_removed_object_When_decreased_Then_it_is_freed_with_nrof_zero()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject arrows = T.Obj(w, "arrow", 2, 3);

        // Act
        CfObject? r = w.DecreaseObNr(arrows, 5);

        // Assert
        Assert.Null(r);
        Assert.Equal(0u, arrows.Nrof);
        Assert.True(arrows.QueryFlag(Define.FLAG_FREED));
    }

    [Fact]
    public void FreeObject_Given_a_map_that_is_not_in_memory_When_freed_Then_the_inventory_is_freed()
    {
        // Arrange
        var host = new TestHostNotInMemory();
        CfWorld w = T.World(out _, host);
        CfObject monster = Placed(w, "orc");
        monster.Map = new FakeMap();
        CfObject sword = T.Obj(w, "sword", 1);
        w.InsertObInOb(sword, monster);
        monster.SetFlag(Define.FLAG_REMOVED);

        // Act
        w.FreeObject(monster);

        // Assert
        Assert.True(sword.QueryFlag(Define.FLAG_FREED));
        Assert.Empty(host.Calls);
    }

    private sealed class TestHostNotInMemory : CrossfireHost
    {
        public List<string> Calls { get; } = new();

        public override CfObject? InsertObInMap(CfObject op, IMapHandle map, CfObject? originator, int flag)
        {
            Calls.Add("insert");
            return op;
        }
    }

    [Fact]
    public void FreeObject_Given_a_wall_square_When_freed_Then_the_inventory_is_freed()
    {
        // Arrange
        var host = new WallHost();
        CfWorld w = T.World(out _, host);
        CfObject monster = Placed(w, "orc");
        monster.Map = new FakeMap();
        CfObject sword = T.Obj(w, "sword", 1);
        w.InsertObInOb(sword, monster);
        monster.SetFlag(Define.FLAG_REMOVED);

        // Act
        w.FreeObject(monster);

        // Assert
        Assert.True(sword.QueryFlag(Define.FLAG_FREED));
    }

    private sealed class WallHost : CrossfireHost
    {
        public override bool MapInMemory(IMapHandle map) => true;

        public override bool Wall(IMapHandle map, int x, int y) => true;
    }

    [Fact]
    public void CountActive_Given_two_active_objects_When_counted_Then_two()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "a");
        CfObject b = T.Obj(w, "b");
        a.Speed = b.Speed = 0.5f;
        w.UpdateObSpeed(a);
        w.UpdateObSpeed(b);

        // Act
        int n = w.CountActive();

        // Assert
        Assert.Equal(2, n);
    }

    [Fact]
    public void RemoveFriendlyObject_Given_an_object_When_removed_Then_its_friendly_flag_is_cleared()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "pet");
        a.SetFlag(Define.FLAG_FRIENDLY);
        w.AddFriendlyObject(a);

        // Act
        w.RemoveFriendlyObject(a);

        // Assert
        Assert.False(a.QueryFlag(Define.FLAG_FRIENDLY));
    }

    [Fact]
    public void InitArchetypes_Given_a_load_When_done_Then_arch_init_is_off_again_and_later_objects_go_on_the_active_list()
    {
        // Arrange
        CfWorld w = T.WorldWith("", out _);

        // Act
        CfObject o = Load(w, "speed 0.5\nend\n");

        // Assert
        Assert.False(w.ArchInit);
        Assert.Same(o, w.ActiveObjects);
    }

    [Fact]
    public void SetVariable_Given_an_endmsg_without_a_newline_When_set_Then_it_is_text_and_the_message_stays_open()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject op = w.GetObject();

        // Act
        w.SetVariable(op, "msg\nx\nendmsg");

        // Assert
        Assert.Null(op.Msg);
    }

    [Fact]
    public void LoadObject_Given_an_object_line_without_a_name_When_read_Then_ll_ignored_is_returned()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.ArchInit = true;
        CfObject op = w.GetObject();

        // Act
        int r = w.LoadObject(new LoadSource("Object  \nend\n"), op, Define.LO_NEWFILE, 0);

        // Assert
        Assert.Equal(Define.LL_IGNORED, r);
    }

    [Fact]
    public void LoadObject_Given_other_arch_and_speed_left_on_a_later_object_When_read_Then_they_are_set()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object b\nend\n", out _);

        // Act
        CfObject o = Load(w, "other_arch b\nspeed_left 0.75\nend\n");

        // Assert
        Assert.Same(w.FindArchetype("b"), o.OtherArch);
        Assert.Equal(0.75f, o.SpeedLeft);
    }

    [Fact]
    public void LoadObject_Given_a_negative_speed_and_a_large_random_When_read_Then_the_modulus_is_100()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.Random = () => 150;

        // Act
        CfObject o = Load(w, "speed -0.5\nend\n");

        // Assert
        Assert.Equal(-0.5f, o.SpeedLeft);
    }

    [Fact]
    public void LoadObject_Given_friendly_one_then_zero_When_read_Then_the_flag_is_cleared()
    {
        // Arrange
        CfWorld w = T.World(out _);

        // Act
        CfObject o = Load(w, "friendly 1\nfriendly 0\nend\n");

        // Assert
        Assert.False(o.QueryFlag(Define.FLAG_FRIENDLY));
    }

    [Fact]
    public void GetObDiff_Given_weaponmagic_resistance_move_state_and_expmul_When_compared_Then_the_c_names_are_written()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "x");
        CfObject b = T.Obj(w, "x");
        a.Resist[Define.ATNR_WEAPONMAGIC] = 5;
        a.MoveStatus = 7;
        a.Expmul = 2.5;

        // Act
        string? diff = w.GetObDiff(a, b);
        a.Weapontype = 0;
        b.Weapontype = 3;
        string? zero = w.GetObDiff(a, b);

        // Assert
        Assert.StartsWith("expmul 2.500000\n", diff);
        Assert.Contains("resist_weaponmagic 5\n", diff);
        Assert.Contains("move_state 7\n", diff);
        Assert.DoesNotContain("weapontype", zero);
    }

    [Fact]
    public void AnimationTable_Given_facings_When_loaded_Then_defaults_and_zero_become_one_and_animation_zero_is_none()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.Faces.Register("f0");

        // Act
        w.Animations.Load("anim a\nf0\nmina\nanim b\nfacings 0\nf0\nmina\nanim c\nfacings 4\nf0\nf0\nf0\nf0\nmina\n");

        // Assert
        Assert.Equal("###none", w.Animations[0].Name);
        Assert.Equal(1, w.Animations[1].Facings);
        Assert.Equal(1, w.Animations[2].Facings);
        Assert.Equal(4, w.Animations[3].Facings);
        Assert.Equal(4, w.Animations[3].NumAnimations);
        Assert.Equal("b", w.Animations[2].Name);
    }

    [Fact]
    public void AnimationTable_Given_a_missing_mina_When_loaded_Then_the_next_anim_logs_an_error()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        w.Faces.Register("f0");

        // Act
        w.Animations.Load("anim a\nf0\nanim b\nf0\nmina\n");

        // Assert
        Assert.Contains(log, l => l.StartsWith("Error: Didn't get a mina before anim b"));
        Assert.Equal(1, w.Animations[2].NumAnimations);
    }

    [Fact]
    public void Collect_Given_a_file_that_only_ends_in_arc_text_and_a_multi_word_name_When_collected_Then_only_dot_arc_files_count_and_the_name_is_joined()
    {
        // Arrange
        string root = T.TempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "a.arc2"), "Object skipped\nend\n");
            File.WriteAllText(Path.Combine(root, "notarc"), "Object skipped\nend\n");
            File.WriteAllText(Path.Combine(root, "x.arc"), "Object big sprite\nanim\nf.1\nmina\nend\n");

            // Act
            ArchCollection c = ArchCollector.Collect(root);

            // Assert
            Assert.Single(c.Files);
            Assert.Equal("Object big sprite\nanimation big_sprite\nend\n", c.Archetypes);
            Assert.Equal("anim big_sprite\nf.1\nmina\n", c.Animations);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FreeArrays_Given_the_tables_When_read_Then_the_last_entries_are_those_of_the_c_arrays()
    {
        // Arrange / Act / Assert
        Assert.Equal(8, FreeArrays.FreeDir[24]);
        Assert.Equal(8, FreeArrays.FreeDir[48]);
        Assert.Equal(49, FreeArrays.MaxFree[48]);
        Assert.Equal(-1, FreeArrays.X[48]);
        Assert.Equal(-3, FreeArrays.Y[48]);
    }

    [Fact]
    public void ToUInt_Given_a_negative_double_When_converted_Then_it_wraps_like_a_64_bit_conversion()
    {
        // Arrange / Act / Assert
        Assert.Equal(uint.MaxValue, CStr.ToUInt(-1.0));
        Assert.Equal(3u, CStr.ToUInt(3.9));
    }
}
