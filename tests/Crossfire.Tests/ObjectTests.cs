namespace Crossfire.Tests;

/// <summary>Tests of the port of common/object.c. Not ported from a Crossfire file.</summary>
public class ObjectTests
{
    private static CfObject Placed(CfWorld w, string name, int weight = 0, uint nrof = 0, byte type = 0)
    {
        CfObject o = T.Obj(w, name, weight, nrof, type);
        o.ClearFlag(Define.FLAG_REMOVED);
        return o;
    }

    // ---- CAN_MERGE

    public static IEnumerable<object[]> Differences()
    {
        yield return new object[] { "name", (Action<CfObject>)(o => o.Name = "other") };
        yield return new object[] { "title", (Action<CfObject>)(o => o.Title = "of x") };
        yield return new object[] { "msg", (Action<CfObject>)(o => o.Msg = "text") };
        yield return new object[] { "weight", (Action<CfObject>)(o => o.Weight = 99) };
        yield return new object[] { "food", (Action<CfObject>)(o => o.Stats.Food = 3) };
        yield return new object[] { "stats.sp", (Action<CfObject>)(o => o.Stats.Sp = 3) };
        yield return new object[] { "attacktype", (Action<CfObject>)(o => o.Attacktype = 4) };
        yield return new object[] { "magic", (Action<CfObject>)(o => o.Magic = 1) };
        yield return new object[] { "slaying", (Action<CfObject>)(o => o.Slaying = "orc") };
        yield return new object[] { "value", (Action<CfObject>)(o => o.Value = 7) };
        yield return new object[] { "animation", (Action<CfObject>)(o => o.AnimationId = 2) };
        yield return new object[] { "client_type", (Action<CfObject>)(o => o.ClientType = 5) };
        yield return new object[] { "resist", (Action<CfObject>)(o => o.Resist[Define.ATNR_FIRE] = 10) };
        yield return new object[] { "flags0", (Action<CfObject>)(o => o.SetFlag(Define.FLAG_NO_PICK)) };
        yield return new object[] { "flags1", (Action<CfObject>)(o => o.SetFlag(Define.FLAG_UNDEAD)) };
        yield return new object[] { "flags2", (Action<CfObject>)(o => o.SetFlag(Define.FLAG_CURSED)) };
        yield return new object[] { "flags3", (Action<CfObject>)(o => o.SetFlag(Define.FLAG_NO_STEAL)) };
        yield return new object[] { "type", (Action<CfObject>)(o => o.Type = Define.FOOD) };
        yield return new object[] { "speed", (Action<CfObject>)(o => o.Speed = 0.00001f) };
    }

    [Fact]
    public void CanMerge_Given_two_equal_stacks_When_compared_Then_true()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "arrow", 2, 5, Define.ARROW);
        CfObject b = T.Obj(w, "arrow", 2, 3, Define.ARROW);

        // Act
        bool merge = CfWorld.CanMerge(a, b);

        // Assert
        Assert.True(merge);
    }

    [Theory]
    [MemberData(nameof(Differences))]
    public void CanMerge_Given_a_difference_When_compared_Then_false(string what, Action<CfObject> change)
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "arrow", 2, 5, Define.ARROW);
        CfObject b = T.Obj(w, "arrow", 2, 3, Define.ARROW);
        change(b);

        // Act
        bool merge = CfWorld.CanMerge(a, b);

        // Assert
        Assert.False(merge, what);
    }

    [Fact]
    public void CanMerge_Given_a_different_archetype_When_compared_Then_false()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object a1\nend\nObject a2\nend\n", out _);
        CfObject a = w.GetArchetype("a1");
        CfObject b = w.GetArchetype("a2");
        b.Name = a.Name;

        // Act
        bool merge = CfWorld.CanMerge(a, b);

        // Assert
        Assert.False(merge);
    }

    [Fact]
    public void CanMerge_Given_the_same_object_When_compared_Then_false()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "arrow", 2, 5, Define.ARROW);

        // Act
        bool merge = CfWorld.CanMerge(a, a);

        // Assert
        Assert.False(merge);
    }

    [Fact]
    public void CanMerge_Given_a_moving_object_that_is_not_animated_When_compared_Then_false_but_true_when_animated()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "bat", 1, 1);
        CfObject b = T.Obj(w, "bat", 1, 1);
        a.Speed = 0.5f;
        b.Speed = 0.5f;

        // Act
        bool notAnimated = CfWorld.CanMerge(a, b);
        a.SetFlag(Define.FLAG_ANIMATE);
        b.SetFlag(Define.FLAG_ANIMATE);
        bool animated = CfWorld.CanMerge(a, b);

        // Assert
        Assert.False(notAnimated);
        Assert.True(animated);
    }

    [Fact]
    public void CanMerge_Given_a_speed_just_over_the_limit_When_compared_Then_false()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "x", 1, 1);
        CfObject b = T.Obj(w, "x", 1, 1);
        a.Speed = 0.00002f;
        b.Speed = 0.00002f;
        CfObject c = T.Obj(w, "x", 1, 1);
        CfObject d = T.Obj(w, "x", 1, 1);
        c.Speed = 0.000001f;
        d.Speed = 0.000001f;

        // Act
        bool over = CfWorld.CanMerge(a, b);
        bool under = CfWorld.CanMerge(c, d);

        // Assert
        Assert.False(over);
        Assert.True(under);
    }

    [Fact]
    public void CanMerge_Given_a_negative_speed_When_compared_Then_false_unless_animated()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "x", 1, 1);
        CfObject b = T.Obj(w, "x", 1, 1);
        a.Speed = -0.5f;
        b.Speed = -0.5f;

        // Act
        bool merge = CfWorld.CanMerge(a, b);

        // Assert
        Assert.False(merge);
    }

    [Fact]
    public void CanMerge_Given_inv_locked_or_client_sent_differ_When_compared_Then_true()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "x", 1, 1);
        CfObject b = T.Obj(w, "x", 1, 1);
        b.SetFlag(Define.FLAG_INV_LOCKED);
        b.SetFlag(Define.FLAG_CLIENT_SENT);

        // Act
        bool merge = CfWorld.CanMerge(a, b);

        // Assert
        Assert.True(merge);
    }

    [Fact]
    public void CanMerge_Given_an_identified_object_When_compared_Then_been_applied_is_set_on_it()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "x", 1, 1);
        CfObject b = T.Obj(w, "x", 1, 1);
        a.SetFlag(Define.FLAG_IDENTIFIED);
        b.SetFlag(Define.FLAG_IDENTIFIED);
        b.SetFlag(Define.FLAG_BEEN_APPLIED);

        // Act
        bool merge = CfWorld.CanMerge(a, b);

        // Assert
        Assert.True(merge);
        Assert.True(a.QueryFlag(Define.FLAG_BEEN_APPLIED));
    }

    [Fact]
    public void CanMerge_Given_an_identified_object_and_a_plain_one_When_compared_Then_false()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "x", 1, 1);
        CfObject b = T.Obj(w, "x", 1, 1);
        b.SetFlag(Define.FLAG_IDENTIFIED);

        // Act
        bool merge = CfWorld.CanMerge(a, b);

        // Assert
        Assert.False(merge);
        Assert.True(b.QueryFlag(Define.FLAG_BEEN_APPLIED));
    }

    [Fact]
    public void CanMerge_Given_two_scrolls_of_other_levels_When_compared_Then_false()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "scroll", 1, 1, Define.SCROLL);
        CfObject b = T.Obj(w, "scroll", 1, 1, Define.SCROLL);
        b.Level = 3;
        CfObject c = T.Obj(w, "weapon", 1, 1, Define.WEAPON);
        CfObject d = T.Obj(w, "weapon", 1, 1, Define.WEAPON);
        d.Level = 3;

        // Act
        bool scrolls = CfWorld.CanMerge(a, b);
        bool others = CfWorld.CanMerge(c, d);

        // Assert
        Assert.False(scrolls);
        Assert.True(others);
    }

    [Theory]
    [InlineData(Define.POTION)]
    [InlineData(Define.AMULET)]
    [InlineData(Define.RING)]
    public void CanMerge_Given_other_stats_When_potion_amulet_or_ring_Then_false(int type)
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "p", 1, 1, (byte)type);
        CfObject b = T.Obj(w, "p", 1, 1, (byte)type);
        b.Stats.Str = 2;

        // Act
        bool merge = CfWorld.CanMerge(a, b);

        // Assert
        Assert.False(merge);
    }

    [Fact]
    public void CanMerge_Given_other_stats_When_a_weapon_Then_true()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "p", 1, 1, Define.WEAPON);
        CfObject b = T.Obj(w, "p", 1, 1, Define.WEAPON);
        b.Stats.Str = 2;

        // Act
        bool merge = CfWorld.CanMerge(a, b);

        // Assert
        Assert.True(merge);
    }

    [Fact]
    public void CanMerge_Given_an_applied_ring_When_compared_Then_false()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "ring", 1, 1, Define.RING);
        CfObject b = T.Obj(w, "ring", 1, 1, Define.RING);
        a.SetFlag(Define.FLAG_APPLIED);
        b.SetFlag(Define.FLAG_APPLIED);
        CfObject c = T.Obj(w, "ring", 1, 1, Define.RING);
        CfObject d = T.Obj(w, "ring", 1, 1, Define.RING);

        // Act
        bool applied = CfWorld.CanMerge(a, b);
        bool plain = CfWorld.CanMerge(c, d);

        // Assert
        Assert.False(applied);
        Assert.True(plain);
    }

    // ---- weights

    [Fact]
    public void SumWeight_Given_a_container_with_str_When_summed_Then_the_content_weighs_the_rest_percent()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        CfObject bag = T.Obj(w, "bag", 5, 0, Define.CONTAINER);
        bag.Stats.Str = 50;
        CfObject gem = T.Obj(w, "gem", 100, 0);
        CfObject sword = T.Obj(w, "sword", 10, 0);
        CfObject arrows = T.Obj(w, "arrows", 2, 5);
        w.InsertObInOb(gem, bag);
        w.InsertObInOb(bag, player);
        w.InsertObInOb(sword, player);
        w.InsertObInOb(arrows, player);
        player.Carrying = 0;
        bag.Carrying = 0;

        // Act
        int sum = CfWorld.SumWeight(player);

        // Assert
        Assert.Equal(50, bag.Carrying);
        Assert.Equal(50 + 5 + 10 + 10, sum);
        Assert.Equal(sum, player.Carrying);
    }

    [Fact]
    public void SumWeight_Given_a_container_that_is_not_a_container_type_When_summed_Then_str_is_ignored()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject box = T.Obj(w, "box", 5, 0, Define.WEAPON);
        box.Stats.Str = 50;
        CfObject gem = T.Obj(w, "gem", 100, 0);
        w.InsertObInOb(gem, box);

        // Act
        int sum = CfWorld.SumWeight(box);

        // Assert
        Assert.Equal(100, sum);
    }

    [Fact]
    public void AddWeight_Given_a_container_in_a_player_When_added_Then_each_environment_gets_its_share()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        CfObject bag = T.Obj(w, "bag", 5, 0, Define.CONTAINER);
        bag.Stats.Str = 50;
        bag.Env = player;

        // Act
        CfWorld.AddWeight(bag, 100);

        // Assert
        Assert.Equal(50, bag.Carrying);
        Assert.Equal(50, player.Carrying);
    }

    [Fact]
    public void SubWeight_Given_a_container_in_a_player_When_subtracted_Then_each_environment_gets_its_share()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        CfObject bag = T.Obj(w, "bag", 5, 0, Define.CONTAINER);
        bag.Stats.Str = 20;
        bag.Env = player;
        bag.Carrying = 100;
        player.Carrying = 100;

        // Act
        CfWorld.SubWeight(bag, 50);

        // Assert
        Assert.Equal(60, bag.Carrying);
        Assert.Equal(60, player.Carrying);
    }

    // ---- insert_ob_in_ob

    [Fact]
    public void InsertObInOb_Given_a_stack_that_can_merge_When_inserted_Then_the_old_stack_grows_and_the_new_one_is_freed()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag", 0, 0, Define.CONTAINER);
        CfObject first = T.Obj(w, "arrow", 2, 5);
        CfObject second = T.Obj(w, "arrow", 2, 3);
        w.InsertObInOb(first, bag);

        // Act
        CfObject result = w.InsertObInOb(second, bag);

        // Assert
        Assert.Same(first, result);
        Assert.Equal(8u, first.Nrof);
        Assert.Same(first, bag.Inv);
        Assert.Null(first.Below);
        Assert.Equal(16, bag.Carrying);
        Assert.True(second.QueryFlag(Define.FLAG_FREED));
        Assert.False(first.QueryFlag(Define.FLAG_REMOVED));
    }

    [Fact]
    public void InsertObInOb_Given_objects_that_do_not_merge_When_inserted_Then_the_newest_is_first()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject a = T.Obj(w, "a", 3);
        CfObject b = T.Obj(w, "b", 4);

        // Act
        w.InsertObInOb(a, bag);
        w.InsertObInOb(b, bag);

        // Assert
        Assert.Same(b, bag.Inv);
        Assert.Same(a, b.Below);
        Assert.Same(b, a.Above);
        Assert.Null(a.Below);
        Assert.Same(bag, a.Env);
        Assert.Equal(7, bag.Carrying);
    }

    [Fact]
    public void InsertObInOb_Given_a_container_with_contents_When_inserted_Then_its_carrying_is_added_to_the_weight()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject sack = T.Obj(w, "sack", 10);
        sack.Carrying = 4;

        // Act
        w.InsertObInOb(sack, bag);

        // Assert
        Assert.Equal(14, bag.Carrying);
    }

    [Fact]
    public void InsertObInOb_Given_an_object_that_is_not_removed_When_inserted_Then_it_is_refused_with_an_error()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        CfObject bag = T.Obj(w, "bag");
        CfObject a = Placed(w, "a", 3);

        // Act
        CfObject result = w.InsertObInOb(a, bag);

        // Assert
        Assert.Same(a, result);
        Assert.Null(bag.Inv);
        Assert.Null(a.Env);
        Assert.Contains(log, l => l.StartsWith("Error: Trying to insert (ob) inserted object."));
    }

    [Fact]
    public void InsertObInOb_Given_no_environment_When_inserted_Then_it_is_refused_with_an_error()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        CfObject a = T.Obj(w, "a", 3);

        // Act
        CfObject result = w.InsertObInOb(a, null);

        // Assert
        Assert.Same(a, result);
        Assert.True(a.QueryFlag(Define.FLAG_REMOVED));
        Assert.Contains(log, l => l.StartsWith("Error: Trying to put object in NULL."));
    }

    [Fact]
    public void InsertObInOb_Given_a_tail_part_as_environment_When_inserted_Then_it_goes_into_the_head()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject head = T.Obj(w, "head");
        CfObject tail = T.Obj(w, "tail");
        tail.Head = head;
        CfObject a = T.Obj(w, "a", 1);

        // Act
        w.InsertObInOb(a, tail);

        // Assert
        Assert.Same(head, a.Env);
        Assert.Same(a, head.Inv);
        Assert.Null(tail.Inv);
    }

    [Fact]
    public void InsertObInOb_Given_a_multipart_object_When_inserted_Then_it_is_refused()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        CfObject bag = T.Obj(w, "bag");
        CfObject a = T.Obj(w, "a", 1);
        a.More = T.Obj(w, "part");

        // Act
        CfObject result = w.InsertObInOb(a, bag);

        // Assert
        Assert.Same(a, result);
        Assert.Null(bag.Inv);
        Assert.Contains(log, l => l.StartsWith("Error: Tried to insert multipart object a"));
    }

    [Fact]
    public void InsertObInOb_Given_a_player_with_a_controller_When_inserted_Then_the_player_is_fixed_unless_no_fix_player()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        player.Contr = new FakePlayer();
        CfObject bag = T.Obj(w, "bag");
        bag.Env = player;
        CfObject a = T.Obj(w, "a", 1);
        CfObject b = T.Obj(w, "b", 1);

        // Act
        w.InsertObInOb(a, bag);
        int afterFirst = host.Calls.Count(c => c == "fix:pl");
        player.SetFlag(Define.FLAG_NO_FIX_PLAYER);
        w.InsertObInOb(b, bag);
        int afterSecond = host.Calls.Count(c => c == "fix:pl");

        // Assert
        Assert.Equal(1, afterFirst);
        Assert.Equal(1, afterSecond);
    }

    [Fact]
    public void InsertObInOb_Given_a_player_without_a_controller_When_inserted_Then_the_player_is_not_fixed()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        CfObject a = T.Obj(w, "a", 1);

        // Act
        w.InsertObInOb(a, player);

        // Assert
        Assert.Empty(host.Calls);
    }

    [Fact]
    public void InsertObInOb_Given_a_removed_object_with_a_place_When_inserted_Then_place_and_map_are_cleared()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject a = T.Obj(w, "a", 1);
        a.X = 5;
        a.Y = 6;
        a.Ox = 5;
        a.Oy = 6;
        a.Map = new FakeMap();
        a.SetFlag(Define.FLAG_OBJ_ORIGINAL);

        // Act
        w.InsertObInOb(a, bag);

        // Assert
        Assert.Equal(0, a.X);
        Assert.Equal(0, a.Y);
        Assert.Equal(0, a.Ox);
        Assert.Equal(0, a.Oy);
        Assert.Null(a.Map);
        Assert.False(a.QueryFlag(Define.FLAG_OBJ_ORIGINAL));
        Assert.False(a.QueryFlag(Define.FLAG_REMOVED));
    }

    // ---- remove_ob

    [Fact]
    public void RemoveOb_Given_the_middle_object_of_a_bag_in_a_player_When_removed_Then_links_weights_and_place_follow()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        player.Contr = new FakePlayer();
        var map = new FakeMap();
        CfObject bag = T.Obj(w, "bag", 0, 0, Define.CONTAINER);
        bag.Stats.Str = 50;
        w.InsertObInOb(bag, player);
        bag.X = 7;
        bag.Y = 8;
        bag.Map = map;
        CfObject a = T.Obj(w, "a", 10);
        CfObject b = T.Obj(w, "b", 20);
        CfObject c = T.Obj(w, "c", 40);
        w.InsertObInOb(a, bag);
        w.InsertObInOb(b, bag);
        w.InsertObInOb(c, bag);
        host.Calls.Clear();

        // Act
        w.RemoveOb(b);

        // Assert
        Assert.Same(c, bag.Inv);
        Assert.Same(a, c.Below);
        Assert.Same(c, a.Above);
        Assert.Null(b.Env);
        Assert.Null(b.Above);
        Assert.Null(b.Below);
        Assert.True(b.QueryFlag(Define.FLAG_REMOVED));
        Assert.Equal(7, b.X);
        Assert.Equal(8, b.Y);
        Assert.Same(map, b.Map);
        Assert.Equal(7, b.Ox);
        Assert.Equal(8, b.Oy);
        Assert.Equal(25, bag.Carrying);
        Assert.Equal(25, player.Carrying);
        Assert.Equal(new[] { "fix:pl" }, host.Calls);
    }

    [Fact]
    public void RemoveOb_Given_the_first_object_of_an_inventory_When_removed_Then_the_environment_points_to_the_next()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject a = T.Obj(w, "a", 10);
        CfObject b = T.Obj(w, "b", 20);
        w.InsertObInOb(a, bag);
        w.InsertObInOb(b, bag);

        // Act
        w.RemoveOb(b);

        // Assert
        Assert.Same(a, bag.Inv);
        Assert.Null(a.Above);
        Assert.Equal(10, bag.Carrying);
    }

    [Fact]
    public void RemoveOb_Given_a_stack_When_removed_Then_weight_times_nrof_is_subtracted()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject arrows = T.Obj(w, "arrows", 2, 5);
        CfObject sack = T.Obj(w, "sack", 10);
        sack.Carrying = 7;
        w.InsertObInOb(arrows, bag);
        w.InsertObInOb(sack, bag);

        // Act
        w.RemoveOb(arrows);
        int afterStack = bag.Carrying;
        w.RemoveOb(sack);

        // Assert
        Assert.Equal(17, afterStack);
        Assert.Equal(0, bag.Carrying);
    }

    [Fact]
    public void RemoveOb_Given_no_fix_player_flag_When_removed_Then_the_player_is_not_fixed()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        player.Contr = new FakePlayer();
        CfObject a = T.Obj(w, "a", 10);
        w.InsertObInOb(a, player);
        host.Calls.Clear();
        player.SetFlag(Define.FLAG_NO_FIX_PLAYER);

        // Act
        w.RemoveOb(a);

        // Assert
        Assert.Empty(host.Calls);
    }

    [Fact]
    public void RemoveOb_Given_an_object_that_is_removed_already_When_removed_Then_it_aborts()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        CfObject a = T.Obj(w, "a");

        // Act
        Action act = () => w.RemoveOb(a);

        // Assert
        Assert.Throws<CrossfireFatalException>(act);
        Assert.Contains(log, l => l.StartsWith("Error: Trying to remove removed object."));
    }

    [Fact]
    public void RemoveOb_Given_an_object_on_a_map_When_removed_Then_the_host_unlinks_it_and_the_flag_is_set()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject a = Placed(w, "a");
        a.Map = new FakeMap();

        // Act
        w.RemoveOb(a);

        // Assert
        Assert.Equal(new[] { "removemap:a" }, host.Calls);
        Assert.True(a.QueryFlag(Define.FLAG_REMOVED));
    }

    [Fact]
    public void RemoveOb_Given_an_object_with_no_map_and_no_environment_When_removed_Then_nothing_else_happens()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject a = Placed(w, "a");

        // Act
        w.RemoveOb(a);

        // Assert
        Assert.Empty(host.Calls);
        Assert.True(a.QueryFlag(Define.FLAG_REMOVED));
    }

    [Fact]
    public void RemoveOb_Given_a_multipart_object_When_removed_Then_the_parts_are_removed_first()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject head = Placed(w, "head");
        CfObject part = Placed(w, "part");
        head.More = part;

        // Act
        w.RemoveOb(head);

        // Assert
        Assert.True(part.QueryFlag(Define.FLAG_REMOVED));
        Assert.True(head.QueryFlag(Define.FLAG_REMOVED));
    }

    // ---- merge_ob, get_split_ob, decrease_ob_nr

    [Fact]
    public void MergeOb_Given_a_stack_on_a_square_When_merged_Then_it_joins_the_first_match_below_and_is_freed()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bottom = Placed(w, "arrow", 2, 4);
        CfObject top = Placed(w, "arrow", 2, 3);
        top.Below = bottom;
        bottom.Above = top;

        // Act
        CfObject? result = w.MergeOb(top, null);

        // Assert
        Assert.Same(bottom, result);
        Assert.Equal(7u, bottom.Nrof);
        Assert.True(top.QueryFlag(Define.FLAG_FREED));
        Assert.Equal(0, top.Weight);
    }

    [Fact]
    public void MergeOb_Given_an_object_with_no_nrof_When_merged_Then_nothing_happens()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = Placed(w, "a", 2, 0);
        CfObject b = Placed(w, "a", 2, 0);
        a.Below = b;

        // Act
        CfObject? result = w.MergeOb(a, null);

        // Assert
        Assert.Null(result);
        Assert.False(a.QueryFlag(Define.FLAG_FREED));
    }

    [Fact]
    public void MergeOb_Given_nothing_that_matches_When_merged_Then_null()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bottom = Placed(w, "coin", 2, 4);
        CfObject top = Placed(w, "arrow", 2, 3);
        top.Below = bottom;
        bottom.Above = top;

        // Act
        CfObject? result = w.MergeOb(top, null);

        // Assert
        Assert.Null(result);
        Assert.Equal(4u, bottom.Nrof);
        Assert.False(top.QueryFlag(Define.FLAG_FREED));
    }

    [Fact]
    public void MergeOb_Given_an_explicit_top_When_merged_Then_the_search_starts_there()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject low = Placed(w, "arrow", 2, 4);
        CfObject mid = Placed(w, "coin", 2, 1);
        CfObject op = Placed(w, "arrow", 2, 3);
        CfObject high = Placed(w, "arrow", 2, 9);
        low.Above = mid;
        mid.Below = low;
        mid.Above = op;
        op.Below = mid;
        op.Above = high;
        high.Below = op;

        // Act
        CfObject? result = w.MergeOb(op, mid);

        // Assert
        Assert.Same(low, result);
        Assert.Equal(9u, high.Nrof);
        Assert.Equal(7u, low.Nrof);
    }

    [Fact]
    public void GetSplitOb_Given_more_than_there_is_When_split_Then_null_and_the_message_names_the_count()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject arrows = T.Obj(w, "arrow", 2, 3);

        // Act
        CfObject? r = w.GetSplitOb(arrows, 4);

        // Assert
        Assert.Null(r);
        Assert.Equal("There are only 3 arrows.", w.ErrMsg);
    }

    [Fact]
    public void GetSplitOb_Given_an_object_without_nrof_When_split_Then_the_message_says_one()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject sword = T.Obj(w, "sword", 2, 0);

        // Act
        CfObject? r = w.GetSplitOb(sword, 1);

        // Assert
        Assert.Null(r);
        Assert.Equal("There are only 1 swords.", w.ErrMsg);
    }

    [Fact]
    public void GetSplitOb_Given_a_stack_in_a_bag_When_part_is_split_Then_the_copy_has_nr_and_the_weight_goes_down()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject arrows = T.Obj(w, "arrow", 2, 10);
        w.InsertObInOb(arrows, bag);

        // Act
        CfObject? piece = w.GetSplitOb(arrows, 4);

        // Assert
        Assert.NotNull(piece);
        Assert.Equal(4u, piece!.Nrof);
        Assert.Equal(6u, arrows.Nrof);
        Assert.Equal("arrow", piece.Name);
        Assert.Equal(12, bag.Carrying);
        Assert.NotEqual(arrows.Count, piece.Count);
    }

    [Fact]
    public void GetSplitOb_Given_the_whole_stack_When_split_Then_the_original_is_removed_and_freed()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject arrows = T.Obj(w, "arrow", 2, 4);
        w.InsertObInOb(arrows, bag);

        // Act
        CfObject? piece = w.GetSplitOb(arrows, 4);

        // Assert
        Assert.Equal(4u, piece!.Nrof);
        Assert.True(arrows.QueryFlag(Define.FLAG_FREED));
        Assert.Null(bag.Inv);
        // C takes nrof to 0 first, so remove_ob subtracts the weight of one object, not of the stack: 8 - 2
        Assert.Equal(6, bag.Carrying);
    }

    [Fact]
    public void GetSplitOb_Given_a_removed_object_When_split_Then_it_is_not_removed_again()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject arrows = T.Obj(w, "arrow", 2, 4);

        // Act
        CfObject? piece = w.GetSplitOb(arrows, 4);

        // Assert
        Assert.Equal(4u, piece!.Nrof);
        Assert.True(arrows.QueryFlag(Define.FLAG_FREED));
    }

    [Fact]
    public void GetSplitOb_Given_an_object_on_a_map_that_is_not_in_memory_When_split_Then_null_with_a_message()
    {
        // Arrange
        var host = new NotInMemoryHost();
        CfWorld w = T.World(out _, host);
        CfObject arrows = Placed(w, "arrow", 2, 4);
        arrows.Map = new FakeMap();

        // Act
        CfObject? piece = w.GetSplitOb(arrows, 1);

        // Assert
        Assert.Null(piece);
        Assert.Equal("Tried to split object whose map is not in memory.", w.ErrMsg);
    }

    private sealed class NotInMemoryHost : CrossfireHost
    {
    }

    [Fact]
    public void DecreaseObNr_Given_zero_When_decreased_Then_the_object_is_returned_unchanged()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "a", 1, 5);

        // Act
        CfObject? r = w.DecreaseObNr(a, 0);

        // Assert
        Assert.Same(a, r);
        Assert.Equal(5u, a.Nrof);
    }

    [Fact]
    public void DecreaseObNr_Given_a_removed_object_When_decreased_Then_only_nrof_changes()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "a", 1, 5);

        // Act
        CfObject? r = w.DecreaseObNr(a, 2);

        // Assert
        Assert.Same(a, r);
        Assert.Equal(3u, a.Nrof);
    }

    [Fact]
    public void DecreaseObNr_Given_a_stack_in_a_players_inventory_When_part_is_taken_Then_weight_and_client_follow()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        player.Contr = new FakePlayer();
        CfObject arrows = T.Obj(w, "arrow", 2, 10);
        w.InsertObInOb(arrows, player);
        host.Calls.Clear();

        // Act
        CfObject? r = w.DecreaseObNr(arrows, 4);

        // Assert
        Assert.Same(arrows, r);
        Assert.Equal(6u, arrows.Nrof);
        Assert.Equal(12, player.Carrying);
        Assert.Equal(new[] { "send:arrow", "update:4" }, host.Calls);
    }

    [Fact]
    public void DecreaseObNr_Given_more_than_there_is_When_decreased_in_an_inventory_Then_the_object_is_removed_and_freed()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        player.Contr = new FakePlayer();
        CfObject arrows = T.Obj(w, "arrow", 2, 10);
        w.InsertObInOb(arrows, player);
        uint tag = arrows.Count;
        host.Calls.Clear();

        // Act
        CfObject? r = w.DecreaseObNr(arrows, 99);

        // Assert
        Assert.Null(r);
        Assert.True(arrows.QueryFlag(Define.FLAG_FREED));
        Assert.Null(player.Inv);
        Assert.Equal(0, player.Carrying);
        Assert.Equal(new[] { "fix:pl", "del:" + tag, "update:4" }, host.Calls);
    }

    [Fact]
    public void DecreaseObNr_Given_a_container_a_player_has_open_When_decreased_Then_that_player_is_told()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        player.Contr = new FakePlayer();
        host.PlayerWithContainer = player;
        CfObject chest = T.Obj(w, "chest", 0, 0, Define.CONTAINER);
        CfObject arrows = T.Obj(w, "arrow", 2, 10);
        w.InsertObInOb(arrows, chest);
        host.Calls.Clear();

        // Act
        w.DecreaseObNr(arrows, 3);

        // Assert
        Assert.Equal(new[] { "send:arrow", "update:4" }, host.Calls);
        Assert.Equal(14, chest.Carrying);
    }

    [Fact]
    public void DecreaseObNr_Given_a_stack_on_a_square_with_a_player_above_When_removed_Then_the_player_is_told_to_delete()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject arrows = Placed(w, "arrow", 2, 3);
        CfObject player = Placed(w, "pl", 0, 0, Define.PLAYER);
        player.Contr = new FakePlayer();
        arrows.Above = player;
        player.Below = arrows;
        uint tag = arrows.Count;

        // Act
        CfObject? r = w.DecreaseObNr(arrows, 3);

        // Assert
        Assert.Null(r);
        Assert.Equal(new[] { "del:" + tag }, host.Calls);
    }

    [Fact]
    public void DecreaseObNr_Given_a_stack_on_a_square_with_a_player_above_When_part_taken_Then_the_item_is_sent()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject arrows = Placed(w, "arrow", 2, 5);
        CfObject player = Placed(w, "pl", 0, 0, Define.PLAYER);
        player.Contr = new FakePlayer();
        arrows.Above = player;

        // Act
        CfObject? r = w.DecreaseObNr(arrows, 2);

        // Assert
        Assert.Same(arrows, r);
        Assert.Equal(3u, arrows.Nrof);
        Assert.Equal(new[] { "send:arrow" }, host.Calls);
    }

    // ---- ids, lists

    [Fact]
    public void GetObject_Given_a_new_world_When_objects_are_made_Then_counts_rise_and_the_newest_is_first()
    {
        // Arrange
        CfWorld w = T.World(out _);

        // Act
        CfObject a = w.GetObject();
        CfObject b = w.GetObject();

        // Assert
        Assert.Equal(1u, a.Count);
        Assert.Equal(2u, b.Count);
        Assert.Same(b, w.Objects);
        Assert.Same(a, b.Next);
        Assert.Same(b, a.Prev);
        Assert.True(a.QueryFlag(Define.FLAG_REMOVED));
        Assert.False(a.QueryFlag(Define.FLAG_FREED));
        Assert.Equal(2, w.ObCount);
    }

    [Fact]
    public void FindObject_Given_a_count_When_searched_Then_the_object_is_found_until_it_is_freed()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = w.GetObject();
        CfObject b = w.GetObject();

        // Act
        CfObject? found = w.FindObject((int)a.Count);
        w.FreeObject(a);
        CfObject? gone = w.FindObject((int)a.Count);

        // Assert
        Assert.Same(a, found);
        Assert.Null(gone);
        Assert.Same(b, w.FindObject((int)b.Count));
        Assert.Null(w.FindObject(99));
    }

    [Fact]
    public void FindObjectName_Given_names_When_searched_Then_the_newest_with_the_name_is_found()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "bob");
        CfObject b = T.Obj(w, "bob");
        CfObject c = T.Obj(w, "alice");

        // Act
        CfObject? bob = w.FindObjectName("bob");
        CfObject? nobody = w.FindObjectName("carol");

        // Assert
        Assert.Same(b, bob);
        Assert.Null(nobody);
        Assert.Same(c, w.FindObjectName("alice"));
        Assert.NotSame(a, bob);
    }

    [Fact]
    public void FreeObject_Given_the_middle_of_the_list_When_freed_Then_it_is_unlinked_and_marked()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = w.GetObject();
        CfObject b = w.GetObject();
        CfObject c = w.GetObject();
        b.Name = "named";

        // Act
        w.FreeObject(b);

        // Assert
        Assert.Same(c, w.Objects);
        Assert.Same(a, c.Next);
        Assert.Same(c, a.Prev);
        Assert.True(b.QueryFlag(Define.FLAG_FREED));
        Assert.Equal(0u, b.Count);
        Assert.Null(b.Name);
    }

    [Fact]
    public void FreeObject_Given_the_head_of_the_list_When_freed_Then_the_list_starts_at_the_next()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = w.GetObject();
        CfObject b = w.GetObject();

        // Act
        w.FreeObject(b);

        // Assert
        Assert.Same(a, w.Objects);
        Assert.Null(a.Prev);
    }

    [Fact]
    public void FreeObject_Given_an_object_freed_already_When_freed_Then_an_error_is_logged()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        CfObject a = w.GetObject();
        w.FreeObject(a);
        a.SetFlag(Define.FLAG_REMOVED);

        // Act
        w.FreeObject(a);

        // Assert
        Assert.Contains(log, l => l.StartsWith("Error: Trying to free freed object."));
    }

    [Fact]
    public void FreeObject_Given_an_object_that_is_not_removed_When_freed_Then_a_debug_message_is_logged()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        CfObject a = Placed(w, "a");

        // Act
        w.FreeObject(a);

        // Assert
        Assert.Contains(log, l => l == "Debug: Free object called with non removed object\n");
        Assert.True(a.QueryFlag(Define.FLAG_FREED));
    }

    [Fact]
    public void FreeObject_Given_an_object_with_an_inventory_and_no_map_When_freed_Then_the_inventory_is_freed_too()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject a = T.Obj(w, "a", 1);
        CfObject b = T.Obj(w, "b", 1);
        w.InsertObInOb(a, bag);
        w.InsertObInOb(b, bag);

        // Act
        w.FreeObject(bag);

        // Assert
        Assert.True(a.QueryFlag(Define.FLAG_FREED));
        Assert.True(b.QueryFlag(Define.FLAG_FREED));
        Assert.Null(w.Objects);
    }

    [Fact]
    public void FreeObject_Given_an_object_on_a_map_in_memory_When_freed_Then_its_inventory_falls_on_the_square_except_start_equipment()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        CfObject monster = Placed(w, "orc");
        monster.Map = new FakeMap();
        monster.X = 3;
        monster.Y = 4;
        CfObject sword = T.Obj(w, "sword", 1);
        CfObject club = T.Obj(w, "club", 1);
        club.SetFlag(Define.FLAG_STARTEQUIP);
        CfObject rune = T.Obj(w, "rune", 1, 0, Define.RUNE);
        CfObject nodrop = T.Obj(w, "nodrop", 1);
        nodrop.SetFlag(Define.FLAG_NO_DROP);
        w.InsertObInOb(sword, monster);
        w.InsertObInOb(club, monster);
        w.InsertObInOb(rune, monster);
        w.InsertObInOb(nodrop, monster);
        monster.SetFlag(Define.FLAG_REMOVED);
        host.Calls.Clear();

        // Act
        w.FreeObject(monster);

        // Assert
        Assert.Equal(new[] { "insertmap:sword" }, host.Calls);
        Assert.Equal(3, sword.X);
        Assert.Equal(4, sword.Y);
        Assert.False(sword.QueryFlag(Define.FLAG_FREED));
        Assert.True(club.QueryFlag(Define.FLAG_FREED));
        Assert.True(rune.QueryFlag(Define.FLAG_FREED));
        Assert.True(nodrop.QueryFlag(Define.FLAG_FREED));
    }

    [Fact]
    public void FreeObject_Given_a_friendly_object_When_freed_Then_it_leaves_the_friendly_list()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "pet");
        a.SetFlag(Define.FLAG_FRIENDLY);
        w.AddFriendlyObject(a);

        // Act
        w.FreeObject(a);

        // Assert
        Assert.Null(w.FirstFriendlyObject);
    }

    [Fact]
    public void FreeObject_Given_a_multipart_object_When_freed_Then_the_parts_are_freed()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject head = T.Obj(w, "head");
        CfObject part = T.Obj(w, "part");
        head.More = part;

        // Act
        w.FreeObject(head);

        // Assert
        Assert.True(part.QueryFlag(Define.FLAG_FREED));
        Assert.Null(head.More);
    }

    [Fact]
    public void FreeObject_Given_an_object_with_speed_When_freed_Then_it_leaves_the_active_list()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "bat");
        a.Speed = 0.5f;
        w.UpdateObSpeed(a);

        // Act
        w.FreeObject(a);

        // Assert
        Assert.Null(w.ActiveObjects);
        Assert.Equal(0f, a.Speed);
    }

    [Fact]
    public void FriendlyObjects_Given_three_When_the_middle_one_is_removed_Then_the_list_keeps_the_others()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        CfObject a = T.Obj(w, "a");
        CfObject b = T.Obj(w, "b");
        CfObject c = T.Obj(w, "c");
        w.AddFriendlyObject(a);
        w.AddFriendlyObject(b);
        w.AddFriendlyObject(c);
        w.AddFriendlyObject(b);

        // Act
        w.RemoveFriendlyObject(b);

        // Assert
        Assert.Same(c, w.FirstFriendlyObject!.Ob);
        Assert.Same(a, w.FirstFriendlyObject.Next!.Ob);
        Assert.Null(w.FirstFriendlyObject.Next.Next);
        Assert.Contains(log, l => l.StartsWith("Error: add_friendly_object: Trying to add object already on list (b)"));
    }

    // ---- clear_object, copy_object

    [Fact]
    public void ClearObject_Given_a_used_object_When_cleared_Then_every_field_has_its_default()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject o = w.GetObject();
        o.Name = "x";
        o.NamePl = "xs";
        o.Title = "t";
        o.Race = "r";
        o.Slaying = "s";
        o.Msg = "m";
        o.Weight = 5;
        o.Carrying = 6;
        o.WeightLimit = 7;
        o.Level = 8;
        o.Value = 9;
        o.RunAway = 10;
        o.Nrof = 11;
        o.Attacktype = 12;
        o.Resist[3] = 13;
        o.Stats.Hp = 14;
        o.Stats.Str = 15;
        o.Expmul = 3.0;
        o.Material = 16;
        o.Magic = 3;
        o.State = 4;
        o.Type = 5;
        o.Speed = 1.5f;
        o.SpeedLeft = 2.5f;
        o.PickUp = 6;
        o.CanApply = 7;
        o.WillApply = 8;
        o.MoveStatus = 9;
        o.MoveType = 10;
        o.PathAttuned = 1;
        o.PathRepelled = 2;
        o.PathDenied = 3;
        o.AnimationId = 4;
        o.Weapontype = 5;
        o.ClientType = 6;
        o.GlowRadius = 2;
        o.Invisible = 5;
        o.LastHeal = 1;
        o.LastSp = 2;
        o.LastGrace = 3;
        o.LastEat = 4;
        o.Randomitems = new TreasureList("x");
        o.Extra = new ExtraKeys();
        o.SetFlag(Define.FLAG_ALIVE);
        o.Face = new NewFace("other", 77);
        o.X = 1;
        o.Y = 2;
        o.Ox = 3;
        o.Oy = 4;
        o.Direction = 3;
        o.AnimSpeed = 4;
        o.Ownercount = 5;
        o.Arch = new Archetype();
        o.OtherArch = new Archetype();
        o.Spelltype = 4;
        o.AttackedByCount = 5;
        o.CastingSpeed = 4f;

        // Act
        w.ClearObject(o);

        // Assert
        Assert.Null(o.Name);
        Assert.Null(o.NamePl);
        Assert.Null(o.Title);
        Assert.Null(o.Race);
        Assert.Null(o.Slaying);
        Assert.Null(o.Msg);
        Assert.Equal(0, o.Weight + o.Carrying + o.WeightLimit + o.Level + o.Value + o.RunAway + o.Resist[3] + o.Stats.Hp + o.Stats.Str);
        Assert.Equal(0u, o.Nrof + o.Attacktype + o.PathAttuned + o.PathRepelled + o.PathDenied + o.Weapontype + o.Ownercount);
        Assert.Equal(1.0, o.Expmul);
        Assert.Equal(0, o.Material + o.Magic + o.State + o.Type + o.PickUp + o.CanApply + o.WillApply + o.MoveStatus + o.MoveType);
        Assert.Equal(0, o.AnimationId + o.ClientType + o.GlowRadius + o.Invisible + o.LastHeal + o.LastSp + o.LastGrace + o.LastEat);
        Assert.Equal(0, o.X + o.Y + o.Ox + o.Oy + o.Direction + o.AnimSpeed);
        Assert.Equal(0f, o.Speed + o.SpeedLeft + o.CastingSpeed);
        Assert.Null(o.Randomitems);
        Assert.Null(o.Extra);
        Assert.Null(o.Arch);
        Assert.Null(o.OtherArch);
        Assert.Same(w.Faces.BlankFace, o.Face);
        Assert.True(o.QueryFlag(Define.FLAG_REMOVED));
        Assert.False(o.QueryFlag(Define.FLAG_ALIVE));
        Assert.Equal(unchecked((uint)-1), o.AttackedByCount);
        Assert.Equal(-1, o.Casting);
        Assert.Equal(0u, o.Spelltype);
    }

    [Fact]
    public void ResetObject_Given_events_and_strings_When_reset_Then_they_are_forgotten()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject o = w.GetObject();
        o.EventHook[Define.EVENT_SAY] = "hook";
        o.EventPlugin[Define.EVENT_SAY] = "plug";
        o.EventOptions[Define.EVENT_SAY] = "opt";
        o.CurrentWeaponScript = "script";
        o.Name = "n";

        // Act
        w.ResetObject(o);

        // Assert
        Assert.Null(o.EventHook[Define.EVENT_SAY]);
        Assert.Null(o.EventPlugin[Define.EVENT_SAY]);
        Assert.Null(o.EventOptions[Define.EVENT_SAY]);
        Assert.Null(o.CurrentWeaponScript);
        Assert.Null(o.Name);
    }

    [Fact]
    public void CopyObject_Given_a_full_object_When_copied_Then_values_come_over_and_links_do_not()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject src = T.Obj(w, "src", 9, 3, Define.WEAPON);
        src.NamePl = "srcs";
        src.Title = "of x";
        src.Race = "orc";
        src.Slaying = "troll";
        src.Msg = "hello\n";
        src.Stats.Hp = 33;
        src.Stats.Dam = 5;
        src.Resist[Define.ATNR_COLD] = 20;
        src.Flags[2] = 0x1234;
        src.Value = 77;
        src.Magic = 2;
        src.Expmul = 2.5;
        src.EventHook[Define.EVENT_APPLY] = "h";
        src.Extra = new ExtraKeys();
        src.Extra.Add("flag", "gender=male");
        src.Below = T.Obj(w, "below");
        src.Env = T.Obj(w, "env");
        src.Face = new NewFace("f", 5);
        src.Weapontype = 4;
        CfObject dst = w.GetObject();
        uint dstCount = dst.Count;

        // Act
        w.CopyObject(src, dst);
        src.Extra.Add("flag", "later");
        src.Resist[Define.ATNR_COLD] = 1;

        // Assert
        Assert.Equal("src", dst.Name);
        Assert.Equal("srcs", dst.NamePl);
        Assert.Equal("of x", dst.Title);
        Assert.Equal("orc", dst.Race);
        Assert.Equal("troll", dst.Slaying);
        Assert.Equal("hello\n", dst.Msg);
        Assert.Equal(9, dst.Weight);
        Assert.Equal(3u, dst.Nrof);
        Assert.Equal(Define.WEAPON, dst.Type);
        Assert.Equal(33, dst.Stats.Hp);
        Assert.Equal(5, dst.Stats.Dam);
        Assert.Equal(20, dst.Resist[Define.ATNR_COLD]);
        Assert.Equal(0x1234u, dst.Flags[2]);
        Assert.Equal(77, dst.Value);
        Assert.Equal(2, dst.Magic);
        Assert.Equal(2.5, dst.Expmul);
        Assert.Equal("h", dst.EventHook[Define.EVENT_APPLY]);
        Assert.Equal(1, dst.Extra!.Count);
        Assert.Equal(4u, dst.Weapontype);
        Assert.Same(src.Face, dst.Face);
        Assert.Null(dst.Below);
        Assert.Null(dst.Env);
        Assert.Equal(dstCount, dst.Count);
    }

    [Fact]
    public void CopyObject_Given_a_destination_that_is_freed_or_removed_When_copied_Then_those_flags_stay()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject src = T.Obj(w, "src");
        src.ClearFlag(Define.FLAG_REMOVED);
        CfObject dst = w.GetObject();
        dst.SetFlag(Define.FLAG_FREED);

        // Act
        w.CopyObject(src, dst);

        // Assert
        Assert.True(dst.QueryFlag(Define.FLAG_FREED));
        Assert.True(dst.QueryFlag(Define.FLAG_REMOVED));
    }

    [Fact]
    public void CopyObject_Given_a_negative_speed_When_copied_Then_speed_left_is_made_random()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.Random = () => 150;
        CfObject src = T.Obj(w, "src");
        src.Speed = -0.5f;
        src.SpeedLeft = 1.0f;
        CfObject dst = w.GetObject();

        // Act
        w.CopyObject(src, dst);

        // Assert
        Assert.Equal(-0.5f, dst.SpeedLeft);
    }

    [Fact]
    public void CopyObject_Given_a_negative_speed_in_the_editor_When_copied_Then_speed_left_is_copied()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.Editor = true;
        w.Random = () => 150;
        CfObject src = T.Obj(w, "src");
        src.Speed = -0.5f;
        src.SpeedLeft = 1.0f;
        CfObject dst = w.GetObject();

        // Act
        w.CopyObject(src, dst);

        // Assert
        Assert.Equal(1.0f, dst.SpeedLeft);
    }

    [Fact]
    public void CopyObject_Given_a_positive_speed_When_copied_Then_the_copy_is_on_the_active_list()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject src = T.Obj(w, "src");
        src.Speed = 0.5f;
        src.SpeedLeft = 0.25f;
        CfObject dst = w.GetObject();

        // Act
        w.CopyObject(src, dst);

        // Assert
        Assert.Same(dst, w.ActiveObjects);
        Assert.Equal(0.25f, dst.SpeedLeft);
    }

    // ---- active list

    [Fact]
    public void UpdateObSpeed_Given_speeds_When_changed_Then_the_active_list_follows()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "a");
        CfObject b = T.Obj(w, "b");
        CfObject c = T.Obj(w, "c");
        a.Speed = 0.5f;
        b.Speed = -0.5f;
        c.Speed = 0.00001f;

        // Act
        w.UpdateObSpeed(a);
        w.UpdateObSpeed(a);
        w.UpdateObSpeed(b);
        w.UpdateObSpeed(c);
        CfObject? first = w.ActiveObjects;
        CfObject? second = first?.ActiveNext;
        a.Speed = 0;
        w.UpdateObSpeed(a);

        // Assert
        Assert.Same(b, first);
        Assert.Same(a, second);
        Assert.Null(a.ActiveNext);
        Assert.Same(b, w.ActiveObjects);
        Assert.Null(b.ActiveNext);
        Assert.Null(a.ActivePrev);
        Assert.Null(c.ActiveNext);
    }

    [Fact]
    public void UpdateObSpeed_Given_the_head_of_the_list_When_stopped_Then_the_next_becomes_the_head()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "a");
        CfObject b = T.Obj(w, "b");
        a.Speed = 0.5f;
        b.Speed = 0.5f;
        w.UpdateObSpeed(a);
        w.UpdateObSpeed(b);

        // Act
        b.Speed = 0;
        w.UpdateObSpeed(b);

        // Assert
        Assert.Same(a, w.ActiveObjects);
        Assert.Null(a.ActivePrev);
    }

    [Fact]
    public void UpdateObSpeed_Given_the_end_of_the_list_When_stopped_Then_the_previous_has_no_next()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "a");
        CfObject b = T.Obj(w, "b");
        a.Speed = 0.5f;
        b.Speed = 0.5f;
        w.UpdateObSpeed(a);
        w.UpdateObSpeed(b);

        // Act
        a.Speed = 0;
        w.UpdateObSpeed(a);

        // Assert
        Assert.Same(b, w.ActiveObjects);
        Assert.Null(b.ActiveNext);
    }

    [Fact]
    public void UpdateObSpeed_Given_an_object_in_the_middle_When_stopped_Then_its_neighbours_are_joined()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "a");
        CfObject b = T.Obj(w, "b");
        CfObject c = T.Obj(w, "c");
        a.Speed = b.Speed = c.Speed = 0.5f;
        w.UpdateObSpeed(a);
        w.UpdateObSpeed(b);
        w.UpdateObSpeed(c);

        // Act
        b.Speed = 0;
        w.UpdateObSpeed(b);

        // Assert
        Assert.Same(c, w.ActiveObjects);
        Assert.Same(a, c.ActiveNext);
        Assert.Same(c, a.ActivePrev);
    }

    [Fact]
    public void UpdateObSpeed_Given_the_archetypes_are_read_When_updated_Then_the_list_is_not_touched()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.ArchInit = true;
        CfObject a = T.Obj(w, "a");
        a.Speed = 0.5f;

        // Act
        w.UpdateObSpeed(a);

        // Assert
        Assert.Null(w.ActiveObjects);
    }

    [Fact]
    public void UpdateObSpeed_Given_a_freed_object_with_speed_When_updated_Then_the_speed_is_cleared_with_an_error()
    {
        // Arrange
        CfWorld w = T.World(out List<string> log);
        CfObject a = T.Obj(w, "a");
        a.SetFlag(Define.FLAG_FREED);
        a.Speed = 0.5f;

        // Act
        w.UpdateObSpeed(a);

        // Assert
        Assert.Equal(0f, a.Speed);
        Assert.Contains(log, l => l == "Error: Object a is freed but has speed.\n");
    }

    // ---- owners

    [Fact]
    public void SetOwner_Given_an_owner_When_set_Then_get_owner_returns_it_and_the_refcount_rises()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject owner = T.Obj(w, "owner");
        CfObject op = T.Obj(w, "op");

        // Act
        CfWorld.SetOwner(op, owner);

        // Assert
        Assert.Same(owner, CfWorld.GetOwner(op));
        Assert.Equal(owner.Count, op.Ownercount);
        Assert.Equal(1, owner.Refcount);
    }

    [Fact]
    public void GetOwner_Given_an_owner_that_was_freed_When_asked_Then_null_and_the_owner_is_cleared()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject owner = T.Obj(w, "owner");
        CfObject op = T.Obj(w, "op");
        CfWorld.SetOwner(op, owner);
        w.FreeObject(owner);

        // Act
        CfObject? got = CfWorld.GetOwner(op);

        // Assert
        Assert.Null(got);
        Assert.Null(op.Owner);
        Assert.Equal(0u, op.Ownercount);
    }

    [Fact]
    public void GetOwner_Given_an_owner_whose_count_changed_When_asked_Then_null()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject owner = T.Obj(w, "owner");
        CfObject op = T.Obj(w, "op");
        CfWorld.SetOwner(op, owner);
        owner.Count = 999;

        // Act
        CfObject? got = CfWorld.GetOwner(op);

        // Assert
        Assert.Null(got);
    }

    [Fact]
    public void GetOwner_Given_no_owner_When_asked_Then_null()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject op = T.Obj(w, "op");

        // Act
        CfObject? got = CfWorld.GetOwner(op);

        // Assert
        Assert.Null(got);
    }

    [Fact]
    public void SetOwner_Given_an_owner_that_has_an_owner_When_set_Then_the_final_owner_is_taken()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "player");
        CfObject ball = T.Obj(w, "ball");
        CfObject cloud = T.Obj(w, "cloud");
        CfWorld.SetOwner(ball, player);

        // Act
        CfWorld.SetOwner(cloud, ball);

        // Assert
        Assert.Same(player, cloud.Owner);
        Assert.Equal(2, player.Refcount);
    }

    [Fact]
    public void SetOwner_Given_an_owner_whose_owner_was_replaced_When_set_Then_nothing_is_set()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "a");
        CfObject b = T.Obj(w, "b");
        CfObject c = T.Obj(w, "c");
        a.Owner = b;
        a.Ownercount = b.Count + 5;

        // Act
        CfWorld.SetOwner(c, a);

        // Assert
        Assert.Null(c.Owner);
        Assert.Equal(0, a.Refcount);
    }

    [Fact]
    public void SetOwner_Given_a_player_with_a_skill_When_set_Then_the_skill_and_experience_objects_follow_and_flags_clear()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "player", 0, 0, Define.PLAYER);
        CfObject skill = T.Obj(w, "skill");
        CfObject exp = T.Obj(w, "exp");
        skill.ExpObj = exp;
        player.ChosenSkill = skill;
        CfObject op = T.Obj(w, "op");
        op.SetFlag(Define.FLAG_CAN_USE_SKILL);
        op.SetFlag(Define.FLAG_READY_SKILL);

        // Act
        CfWorld.SetOwner(op, player);

        // Assert
        Assert.Same(skill, op.ChosenSkill);
        Assert.Same(exp, op.ExpObj);
        Assert.False(op.QueryFlag(Define.FLAG_CAN_USE_SKILL));
        Assert.False(op.QueryFlag(Define.FLAG_READY_SKILL));
    }

    [Fact]
    public void SetOwner_Given_an_owner_without_a_skill_When_set_Then_ready_skill_is_cleared_unless_op_is_a_player()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject owner = T.Obj(w, "owner");
        CfObject op = T.Obj(w, "op");
        op.SetFlag(Define.FLAG_READY_SKILL);
        CfObject pl = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        pl.SetFlag(Define.FLAG_READY_SKILL);

        // Act
        CfWorld.SetOwner(op, owner);
        CfWorld.SetOwner(pl, owner);

        // Assert
        Assert.False(op.QueryFlag(Define.FLAG_READY_SKILL));
        Assert.True(pl.QueryFlag(Define.FLAG_READY_SKILL));
    }

    [Fact]
    public void SetOwner_Given_null_arguments_When_set_Then_nothing_happens()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject op = T.Obj(w, "op");

        // Act
        CfWorld.SetOwner(op, null);
        CfWorld.SetOwner(null, op);

        // Assert
        Assert.Null(op.Owner);
    }

    [Fact]
    public void ClearOwner_Given_an_owner_When_cleared_Then_the_refcount_goes_down()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject owner = T.Obj(w, "owner");
        CfObject op = T.Obj(w, "op");
        CfWorld.SetOwner(op, owner);

        // Act
        CfWorld.ClearOwner(op);
        CfWorld.ClearOwner(null);

        // Assert
        Assert.Null(op.Owner);
        Assert.Equal(0u, op.Ownercount);
        Assert.Equal(0, owner.Refcount);
    }

    [Fact]
    public void CopyOwner_Given_a_clone_with_an_owner_and_skill_When_copied_Then_both_follow_the_clone()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "player", 0, 0, Define.PLAYER);
        CfObject clone = T.Obj(w, "clone");
        CfWorld.SetOwner(clone, player);
        CfObject skill = T.Obj(w, "skill");
        CfObject exp = T.Obj(w, "exp");
        clone.ChosenSkill = skill;
        clone.ExpObj = exp;
        CfObject op = T.Obj(w, "op");

        // Act
        CfWorld.CopyOwner(op, clone);

        // Assert
        Assert.Same(player, op.Owner);
        Assert.Same(skill, op.ChosenSkill);
        Assert.Same(exp, op.ExpObj);
    }

    [Fact]
    public void CopyOwner_Given_a_player_clone_When_copied_Then_the_player_is_the_owner()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "player", 0, 0, Define.PLAYER);
        CfObject op = T.Obj(w, "op");
        op.SetFlag(Define.FLAG_READY_SKILL);

        // Act
        CfWorld.CopyOwner(op, player);

        // Assert
        Assert.Same(player, op.Owner);
        Assert.False(op.QueryFlag(Define.FLAG_READY_SKILL));
    }

    [Fact]
    public void CopyOwner_Given_a_clone_without_an_owner_that_is_not_a_player_When_copied_Then_nothing_happens()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject clone = T.Obj(w, "clone");
        CfObject op = T.Obj(w, "op");

        // Act
        CfWorld.CopyOwner(op, clone);

        // Assert
        Assert.Null(op.Owner);
    }

    // ---- searches, small functions

    [Fact]
    public void PresentInOb_Given_an_inventory_When_searched_by_type_Then_the_first_of_the_type_is_found()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject a = T.Obj(w, "a", 1, 0, Define.WEAPON);
        CfObject b = T.Obj(w, "b", 1, 0, Define.WEAPON);
        CfObject c = T.Obj(w, "c", 1, 0, Define.FOOD);
        w.InsertObInOb(a, bag);
        w.InsertObInOb(b, bag);
        w.InsertObInOb(c, bag);

        // Act
        CfObject? weapon = CfWorld.PresentInOb(Define.WEAPON, bag);
        CfObject? none = CfWorld.PresentInOb(Define.RING, bag);

        // Assert
        Assert.Same(b, weapon);
        Assert.Null(none);
    }

    [Fact]
    public void PresentArchInOb_Given_an_inventory_When_searched_by_archetype_Then_the_first_of_it_is_found()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object sword\nend\nObject club\nend\n", out _);
        CfObject bag = T.Obj(w, "bag");
        CfObject s1 = w.GetArchetype("sword");
        CfObject s2 = w.GetArchetype("sword");
        CfObject cl = w.GetArchetype("club");
        s2.Weight = 4;
        w.InsertObInOb(s1, bag);
        w.InsertObInOb(s2, bag);
        w.InsertObInOb(cl, bag);

        // Act
        CfObject? sword = CfWorld.PresentArchInOb(w.FindArchetype("sword")!, bag);
        CfObject? none = CfWorld.PresentArchInOb(w.FindArchetype("empty_archetype")!, bag);

        // Assert
        Assert.Same(s2, sword);
        Assert.Null(none);
    }

    [Fact]
    public void CanPick_Given_items_When_asked_Then_the_rules_of_pickup_apply()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "pl", 90, 0, Define.PLAYER);
        CfObject monster = T.Obj(w, "orc", 90, 0, Define.MONSTER);
        CfObject light = T.Obj(w, "light", 29);
        CfObject heavy = T.Obj(w, "heavy", 30);
        CfObject weightless = T.Obj(w, "air", 0);
        CfObject fixedItem = T.Obj(w, "fixed", 1);
        fixedItem.SetFlag(Define.FLAG_NO_PICK);
        CfObject alive = T.Obj(w, "alive", 1);
        alive.SetFlag(Define.FLAG_ALIVE);
        CfObject invisible = T.Obj(w, "ghost", 1);
        invisible.Invisible = 1;

        // Act / Assert
        Assert.True(CfWorld.CanPick(monster, light));
        Assert.False(CfWorld.CanPick(monster, heavy));
        Assert.True(CfWorld.CanPick(player, heavy));
        Assert.False(CfWorld.CanPick(player, weightless));
        Assert.False(CfWorld.CanPick(player, fixedItem));
        Assert.False(CfWorld.CanPick(player, alive));
        Assert.False(CfWorld.CanPick(player, invisible));
    }

    [Fact]
    public void Distance_Given_two_objects_When_measured_Then_the_square_of_the_distance()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = T.Obj(w, "a");
        CfObject b = T.Obj(w, "b");
        b.X = 3;
        b.Y = -4;

        // Act
        int d = CfWorld.Distance(a, b);

        // Assert
        Assert.Equal(25, d);
    }

    [Fact]
    public void GetNearestPart_Given_a_multipart_object_When_asked_Then_the_closest_part_is_returned()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject head = T.Obj(w, "head");
        CfObject p1 = T.Obj(w, "p1");
        CfObject p2 = T.Obj(w, "p2");
        head.More = p1;
        p1.More = p2;
        head.X = 0;
        p1.X = 5;
        p2.X = 10;
        CfObject pl = T.Obj(w, "pl");
        pl.X = 9;

        // Act
        CfObject near = CfWorld.GetNearestPart(head, pl);
        CfObject single = CfWorld.GetNearestPart(pl, head);

        // Assert
        Assert.Same(p2, near);
        Assert.Same(pl, single);
    }

    [Theory]
    [InlineData(0, 0, 5)]
    [InlineData(0, 1, 1)]
    [InlineData(0, -1, 5)]
    [InlineData(1, 1, 8)]
    [InlineData(-1, 1, 2)]
    [InlineData(10, 1, 7)]
    [InlineData(-10, 1, 3)]
    [InlineData(1, -1, 6)]
    [InlineData(-1, -1, 4)]
    [InlineData(10, -1, 7)]
    [InlineData(-10, -1, 3)]
    [InlineData(5, 0, 7)]
    [InlineData(-5, 0, 3)]
    public void FindDir2_Given_a_difference_When_asked_Then_the_direction_of_the_table(int x, int y, int expected)
    {
        // Arrange / Act
        int dir = CfWorld.FindDir2(x, y);

        // Assert
        Assert.Equal(expected, dir);
    }

    [Fact]
    public void Absdir_And_Dirdiff_Given_directions_When_asked_Then_they_wrap_and_measure()
    {
        // Arrange / Act / Assert
        Assert.Equal(8, CfWorld.Absdir(0));
        Assert.Equal(1, CfWorld.Absdir(9));
        Assert.Equal(4, CfWorld.Absdir(-4));
        Assert.Equal(5, CfWorld.Absdir(5));
        Assert.Equal(1, CfWorld.Dirdiff(1, 8));
        Assert.Equal(4, CfWorld.Dirdiff(1, 5));
        Assert.Equal(2, CfWorld.Dirdiff(3, 1));
        Assert.Equal(3, CfWorld.Dirdiff(1, 6));
    }

    [Fact]
    public void IsPlayerInv_Given_nested_inventories_When_asked_Then_the_player_is_found_and_a_self_environment_is_repaired()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "pl", 0, 0, Define.PLAYER);
        CfObject bag = T.Obj(w, "bag");
        CfObject gem = T.Obj(w, "gem");
        w.InsertObInOb(bag, player);
        w.InsertObInOb(gem, bag);
        CfObject loner = T.Obj(w, "loner");
        loner.Env = loner;

        // Act
        CfObject? found = CfWorld.IsPlayerInv(gem);
        CfObject? none = CfWorld.IsPlayerInv(loner);

        // Assert
        Assert.Same(player, found);
        Assert.Null(none);
        Assert.Null(loner.Env);
    }

    [Fact]
    public void SetCheat_Given_an_inventory_When_set_Then_every_object_gets_was_wiz()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject player = T.Obj(w, "pl");
        CfObject bag = T.Obj(w, "bag");
        CfObject gem = T.Obj(w, "gem");
        w.InsertObInOb(bag, player);
        w.InsertObInOb(gem, bag);

        // Act
        CfWorld.SetCheat(player);

        // Assert
        Assert.True(player.QueryFlag(Define.FLAG_WAS_WIZ));
        Assert.True(bag.QueryFlag(Define.FLAG_WAS_WIZ));
        Assert.True(gem.QueryFlag(Define.FLAG_WAS_WIZ));
    }

    [Fact]
    public void ObjectCreateClone_Given_a_multipart_object_with_inventory_When_cloned_Then_parts_and_contents_are_copied_relative_to_the_head()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject head = T.Obj(w, "head");
        head.X = 10;
        head.Y = 20;
        CfObject part = T.Obj(w, "part");
        part.X = 11;
        part.Y = 22;
        part.Head = head;
        head.More = part;
        CfObject item = T.Obj(w, "item", 3);
        w.InsertObInOb(item, head);

        // Act
        CfObject? clone = w.ObjectCreateClone(part);

        // Assert
        Assert.NotNull(clone);
        Assert.NotSame(head, clone);
        Assert.Equal(0, clone!.X);
        Assert.Equal(0, clone.Y);
        Assert.Equal("part", clone.More!.Name);
        Assert.Equal(1, clone.More.X);
        Assert.Equal(2, clone.More.Y);
        Assert.Same(clone, clone.More.Head);
        Assert.Null(clone.More.More);
        Assert.Equal("item", clone.Inv!.Name);
        Assert.NotSame(item, clone.Inv);
        Assert.Equal(6, clone.Carrying); // C copies the carrying of the head and then inserts the copy of the item: the weight counts twice
        Assert.Null(w.ObjectCreateClone(null));
    }

    [Fact]
    public void WasDestroyed_Given_a_tag_When_the_object_is_freed_Then_true()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = w.GetObject();
        uint tag = a.Count;
        bool before = CfWorld.WasDestroyed(a, tag);

        // Act
        w.FreeObject(a);

        // Assert
        Assert.False(before);
        Assert.True(CfWorld.WasDestroyed(a, tag));
        Assert.True(CfWorld.WasDestroyed(w.GetObject(), tag));
    }

    [Fact]
    public void UpdateTurnFace_Given_a_turnable_object_When_updated_Then_the_face_of_the_direction_is_set_and_the_map_told()
    {
        // Arrange
        var host = new TestHost();
        CfWorld w = T.World(out _, host);
        w.Faces.Register("f0");
        int f1 = w.Faces.Register("f1");
        w.Animations.Load("anim spin\nf0\nf1\nmina\n");
        CfObject o = T.Obj(w, "o");
        o.Arch = new Archetype();
        o.AnimationId = (ushort)w.Animations.FindAnimation("spin");
        o.SetFlag(Define.FLAG_IS_TURNABLE);
        o.Direction = 1;

        // Act
        w.UpdateTurnFace(o);

        // Assert
        Assert.Same(w.Faces[f1], o.Face);
    }

    [Fact]
    public void UpdateTurnFace_Given_an_object_that_is_not_turnable_When_updated_Then_nothing_changes()
    {
        // Arrange
        CfWorld w = T.World(out _);
        w.Faces.Register("f0");
        w.Animations.Load("anim spin\nf0\nmina\n");
        CfObject o = T.Obj(w, "o");
        o.Arch = new Archetype();
        o.AnimationId = (ushort)w.Animations.FindAnimation("spin");
        NewFace? before = o.Face;

        // Act
        w.UpdateTurnFace(o);

        // Assert
        Assert.Same(before, o.Face);
    }

    [Fact]
    public void DumpObject_Given_an_object_with_an_archetype_When_dumped_Then_the_text_is_arch_diff_end()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object sword\nname sword\nweight 5\nend\n", out _);
        CfObject o = w.GetArchetype("sword");
        o.Stats.Hp = 4;

        // Act
        string text = w.DumpObject(o);

        // Assert
        Assert.StartsWith("arch sword\n", text);
        Assert.Contains("name sword\n", text);
        Assert.Contains("hp 4\n", text);
        Assert.EndsWith("end\n", text);
        Assert.Equal(text, w.ErrMsg);
        Assert.Equal("[NULL pointer]", w.DumpObject(null));
        Assert.Equal("Object loose\nend\n", w.DumpMe(T.Obj(w, "loose")));
    }

    [Fact]
    public void CountUsed_And_CountActive_Given_objects_When_counted_Then_the_lists_are_walked()
    {
        // Arrange
        CfWorld w = T.World(out _);
        CfObject a = w.GetObject();
        CfObject b = w.GetObject();
        w.GetObject();
        b.Speed = 0.5f;
        w.UpdateObSpeed(b);

        // Act
        int used = w.CountUsed();
        int active = w.CountActive();
        w.FreeObject(a);

        // Assert
        Assert.Equal(3, used);
        Assert.Equal(1, active);
        Assert.Equal(2, w.CountUsed());
    }

    [Fact]
    public void LoadObjectStr_Given_the_text_of_an_object_When_loaded_Then_the_object_is_made_and_not_removed()
    {
        // Arrange
        CfWorld w = T.WorldWith("Object sword\nname sword\nweight 5\nend\n", out List<string> log);

        // Act
        CfObject o = w.LoadObjectStr("arch sword\nhp 4\nend\n");

        // Assert
        Assert.Same(w.FindArchetype("sword"), o.Arch);
        Assert.Equal(4, o.Stats.Hp);
        Assert.Equal(5, o.Weight);
        Assert.False(o.QueryFlag(Define.FLAG_REMOVED));
        Assert.Contains(log, l => l == "Debug:  load str completed, object=sword\n");
    }
}
