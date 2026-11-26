// Not ported from a Crossfire file: tests of objects in levels: level.load, level.unload, objects.blobs, object.destroyed, mudaddobject,
// mudaddspecialobject, cloneMudObject, destroymudobject, the members of TMudObject, onUpdateObject and the classes rule. Each test
// runs twice: against the dispatcher in this process, and over a Unix socket.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed class LevelObjectDirectTests : LevelObjectTests
{
    internal override IExt Start(TempFolder world, ExtensionOptions? options = null, long epoch = 1, bool owns = true) => new DirectExt(world, options, epoch, owns);

    internal override IExt StartSmallFrames(TempFolder world) => new DirectExt(world, null, 1, true, 65_536);
}

public sealed class LevelObjectSocketTests : LevelObjectTests
{
    internal override IExt Start(TempFolder world, ExtensionOptions? options = null, long epoch = 1, bool owns = true) => new SocketExt(world, options, epoch, owns);

    internal override IExt StartSmallFrames(TempFolder world) => new SocketExt(world, null, 1, true, 65_536);
}

public abstract class LevelObjectTests
{
    internal abstract IExt Start(TempFolder world, ExtensionOptions? options = null, long epoch = 1, bool owns = true);

    /// <summary>The extension with the smallest frames the protocol allows (65,536 bytes).</summary>
    internal abstract IExt StartSmallFrames(TempFolder world);

    private IExt Hall(ExtensionOptions? options = null) => Start(ObjectWorld.Make(("hall.nw", ObjectWorld.SeedHall), ("many.nw", ObjectWorld.Many(150))), options);

    private static ulong Add(IExt ext, string level, string arch, double x = 1, double y = 2) => Ok(ext.Call(Manifest.OpMudAddObject, new[] { N(x), N(y), S(arch) }, level: level));

    // ------------------------------------------------------------------ level.load

    [Fact]
    public void LevelLoad_Given_a_seed_level_When_loaded_Then_each_object_is_placed_with_floats_image_classes_and_script_variables()
    {
        // Arrange
        using IExt ext = Hall();

        // Act
        (List<Val> places, List<int> pages) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.Equal(new[] { 3 }, pages);
        Val banana = places[0];
        Assert.Equal(new[] { "k", "handle", "type", "level", "x", "y", "image", "classes", "props", "blob" }, banana.Pairs.Select(p => p.Key));
        Assert.Equal("TMudObject", banana.Get("type")!.AsString);
        Assert.Equal("hall.nw", banana.Get("level")!.AsString);
        Assert.Equal(3.328125, banana.Get("x")!.AsFloat);
        Assert.Equal(1.125, banana.Get("y")!.AsFloat);
        Assert.Equal("mud_banana.png", banana.Get("image")!.AsString);
        Assert.Equal(new[] { "mudobjects" }, banana.Get("classes")!.Items.Select(c => c.AsString));
        Assert.True(banana.Get("blob")!.IsNil);
        Val props = banana.Get("props")!;
        Assert.Equal(ValKind.Float, props.Get("this.born")!.Kind);
        Assert.Equal(1524297788.081527948, props.Get("this.born")!.AsFloat, 6);
        Assert.Equal(1L, props.Get("this.ismudobject")!.AsInt);
        Assert.Equal("\"magic bullet\",\"cause\"", props.Get("this.spells")!.AsString);
        Assert.Equal(0.5, places[1].Get("x")!.AsFloat);
        Assert.Equal(2.0, places[1].Get("y")!.AsFloat);
        Assert.Equal(3, places.Select(p => p.Get("handle")!.AsHandle).Distinct().Count());
        Assert.All(places, p => Assert.Equal(1UL, p.Get("handle")!.AsHandle >> 32));
    }

    [Fact]
    public void LevelLoad_Given_an_unknown_level_When_loaded_Then_no_more_no_cursor_and_no_effects()
    {
        // Arrange
        using IExt ext = Hall();

        // Act
        ReplyFrame r = ext.Call(7, new[] { S("nowhere.nw"), Val.Nil }, level: "nowhere.nw");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.False(r.Result.Get("more")!.AsBool);
        Assert.True(r.Result.Get("cursor")!.IsNil);
        Assert.Empty(r.Effects);
        Assert.Empty(r.Events);
    }

    [Fact]
    public void LevelLoad_Given_a_level_already_placed_When_loaded_again_Then_no_effects()
    {
        // Arrange
        using IExt ext = Hall();
        LoadAll(ext, "hall.nw");

        // Act
        (List<Val> places, List<int> pages) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.Empty(places);
        Assert.Equal(new[] { 0 }, pages);
    }

    [Fact]
    public void LevelLoad_Given_more_than_64_objects_When_loaded_Then_pages_of_at_most_64_with_an_int_cursor_and_every_object_once()
    {
        // Arrange
        using IExt ext = Hall();

        // Act
        ReplyFrame first = ext.Call(7, new[] { S("many.nw"), Val.Nil }, level: "many.nw");
        ReplyFrame second = ext.Call(7, new[] { S("many.nw"), first.Result.Get("cursor")! }, level: "many.nw");
        ReplyFrame third = ext.Call(7, new[] { S("many.nw"), second.Result.Get("cursor")! }, level: "many.nw");

        // Assert
        Assert.Equal(64, first.Effects.Count);
        Assert.True(first.Result.Get("more")!.AsBool);
        Assert.Equal(64L, first.Result.Get("cursor")!.AsInt);
        Assert.Equal(64, second.Effects.Count);
        Assert.Equal(128L, second.Result.Get("cursor")!.AsInt);
        Assert.Equal(22, third.Effects.Count);
        Assert.False(third.Result.Get("more")!.AsBool);
        Assert.True(third.Result.Get("cursor")!.IsNil);
        var all = first.Effects.Concat(second.Effects).Concat(third.Effects).ToList();
        Assert.Equal(150, all.Select(e => e.Get("handle")!.AsHandle).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 150).Select(i => (double)((i - 1) % 64)), all.Select(e => e.Get("x")!.AsFloat));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(151)]
    public void LevelLoad_Given_a_cursor_outside_the_pages_When_loaded_Then_bad_args(int cursor)
    {
        // Arrange
        using IExt ext = Hall();
        ext.Call(7, new[] { S("many.nw"), Val.Nil }, level: "many.nw");

        // Act
        ReplyFrame r = ext.Call(7, new[] { S("many.nw"), Val.Int(cursor) }, level: "many.nw");

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
        Assert.Empty(r.Effects);
    }

    [Fact]
    public void LevelLoad_Given_a_cursor_that_is_not_an_int_or_the_wrong_arguments_When_loaded_Then_bad_args()
    {
        // Arrange
        using IExt ext = Hall();
        ext.Call(7, new[] { S("many.nw"), Val.Nil }, level: "many.nw");

        // Act
        ReplyFrame text = ext.Call(7, new[] { S("many.nw"), S("64") });
        ReplyFrame count = ext.Call(7, new[] { S("many.nw") });
        ReplyFrame kind = ext.Call(7, new[] { Val.Int(1), Val.Nil });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, text.Status);
        Assert.Equal((ulong)Status.BadArgs, count.Status);
        Assert.Equal((ulong)Status.BadArgs, kind.Status);
    }

    [Fact]
    public void LevelLoad_Given_a_cursor_for_a_level_that_is_not_held_When_loaded_Then_nothing()
    {
        // Arrange
        using IExt ext = Hall();

        // Act
        ReplyFrame r = ext.Call(7, new[] { S("hall.nw"), Val.Int(2) }, level: "hall.nw");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.False(r.Result.Get("more")!.AsBool);
        Assert.Empty(r.Effects);
    }

    [Fact]
    public void LevelLoad_Given_a_seed_with_an_unknown_archetype_and_a_nested_object_When_loaded_Then_the_unknown_one_is_discarded_and_the_contents_stay()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong bag = places[2].Get("handle")!.AsHandle;

        // Act
        string text = Flushed(ext, "levels/hall.nw.map");

        // Assert
        Assert.Equal(3, places.Count);
        Assert.DoesNotContain("notinthisworld", text);
        Assert.Contains("arch bag\n", text);
        Assert.Matches("arch bag\\n(?:.*\\n)*arch banana\\nnrof 2\\nend\\nend\\n", text);
        Assert.Equal(7.0, places[2].Get("x")!.AsFloat);
        Assert.NotEqual(0UL, bag);
    }

    [Fact]
    public void LevelLoad_Given_the_same_block_twice_in_a_seed_When_loaded_Then_both_are_kept()
    {
        // Arrange
        string seed = "arch map\nname dup.nw\nend\narch sword\nx 1\ny 1\nend\narch sword\nx 1\ny 1\nend\narch sword\nx 1\ny 1\nend\n";
        using IExt ext = Start(ObjectWorld.Make(("dup.nw", seed)));

        // Act
        (List<Val> places, _) = LoadAll(ext, "dup.nw");

        // Assert
        Assert.Equal(3, places.Count);
        Assert.Equal(3, places.Select(p => p.Get("handle")!.AsHandle).Distinct().Count());
    }

    [Fact]
    public void LevelLoad_Given_a_name_that_is_a_path_When_loaded_Then_the_seed_folder_is_not_left()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make(("hall.nw", ObjectWorld.SeedHall));
        File.WriteAllText(Path.Combine(world.Path, "secret.nw"), ObjectWorld.SeedHall);
        using IExt ext = Start(world);

        // Act
        (List<Val> up, _) = LoadAll(ext, "../secret.nw");
        (List<Val> sub, _) = LoadAll(ext, "mudlevels/hall.nw");

        // Assert
        Assert.Empty(up);
        Assert.Empty(sub);
    }

    // ------------------------------------------------------------------ save and load again

    [Fact]
    public void Level_Given_a_seed_a_change_a_blob_and_an_unload_When_loaded_again_Then_the_saved_state_comes_back_with_new_handles()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> first, _) = LoadAll(ext, "hall.nw");
        ulong banana = first[0].Get("handle")!.AsHandle;
        ulong sword = first[1].Get("handle")!.AsHandle;
        Assert.Equal(0UL, Set(ext, sword, Obj("mudrealname"), S("Excalibur")).Status);
        Assert.Equal(0UL, ext.Call(16, new[] { Val.List(Val.List(H(banana), N(4.5), N(5.25), S("mode=2\nlist=\"a\",\"b\"\\x"))) }).Status);

        // Act
        Assert.Equal(0UL, ext.Call(8, new[] { S("hall.nw") }).Status);
        (List<Val> second, _) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.Equal(3, second.Count);
        Assert.Empty(first.Select(p => p.Get("handle")!.AsHandle).Intersect(second.Select(p => p.Get("handle")!.AsHandle)));
        Assert.Equal(4.5, second[0].Get("x")!.AsFloat);
        Assert.Equal(5.25, second[0].Get("y")!.AsFloat);
        Assert.Equal("mode=2\nlist=\"a\",\"b\"\\x", second[0].Get("blob")!.AsString);
        Assert.Empty(second[0].Get("props")!.Pairs);
        Assert.Equal("Excalibur", Get(ext, second[1].Get("handle")!.AsHandle, Obj("mudrealname")).AsString);
        Assert.Equal((ulong)Status.BadHandle, ext.Call(9, new[] { H(banana), Val.Int(Obj("mudname")) }).Status);
    }

    [Fact]
    public void Level_Given_an_unloaded_level_When_a_new_process_loads_it_Then_the_file_on_disk_is_the_state()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make(("hall.nw", ObjectWorld.SeedHall));
        using (IExt ext = Start(world, owns: false))
        {
            (List<Val> first, _) = LoadAll(ext, "hall.nw");
            Assert.Equal(0UL, Set(ext, first[1].Get("handle")!.AsHandle, Obj("mudtitle"), S("of Doom")).Status);
            Assert.Equal(0UL, ext.Call(8, new[] { S("hall.nw") }).Status);
        }
        File.Delete(Path.Combine(world.Path, "mudlevels", "hall.nw")); // only the saved file is left

        // Act
        using IExt again = Start(world, epoch: 2);
        (List<Val> second, _) = LoadAll(again, "hall.nw");

        // Assert
        Assert.Equal(3, second.Count);
        Assert.Equal("of Doom", Get(again, second[1].Get("handle")!.AsHandle, Obj("mudtitle")).AsString);
        Assert.Equal(2UL, second[1].Get("handle")!.AsHandle >> 32);
    }

    [Fact]
    public void Level_Given_a_saved_file_and_a_seed_When_loaded_Then_the_saved_file_wins()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make(("hall.nw", ObjectWorld.SeedHall));
        Directory.CreateDirectory(Path.Combine(world.Path, "data", "levels"));
        File.WriteAllText(Path.Combine(world.Path, "data", "levels", "hall.nw.map"), "arch map\nname hall.nw\nend\narch sword\nmudx 9.5\nmudy 8.5\nend\n");
        using IExt ext = Start(world);

        // Act
        (List<Val> places, _) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.Single(places);
        Assert.Equal(9.5, places[0].Get("x")!.AsFloat);
    }

    [Fact]
    public void Level_Given_a_saved_level_with_no_objects_When_loaded_Then_the_seed_is_not_used()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make(("hall.nw", ObjectWorld.SeedHall));
        Directory.CreateDirectory(Path.Combine(world.Path, "data", "levels"));
        File.WriteAllText(Path.Combine(world.Path, "data", "levels", "hall.nw.map"), "arch map\nname hall.nw\nend\n");
        using IExt ext = Start(world);

        // Act
        (List<Val> places, _) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.Empty(places);
    }

    [Fact]
    public void Level_Given_all_objects_destroyed_When_saved_and_loaded_in_a_new_process_Then_the_level_is_empty_and_the_seed_stays_unused()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make(("hall.nw", ObjectWorld.SeedHall));
        using (IExt ext = Start(world, owns: false))
        {
            (List<Val> first, _) = LoadAll(ext, "hall.nw");
            foreach (Val p in first)
            {
                Assert.Equal(0UL, ext.Call(14, new[] { p.Get("handle")! }).Status);
            }
        }

        // Act
        using IExt again = Start(world, epoch: 2);
        (List<Val> places, _) = LoadAll(again, "hall.nw");

        // Assert
        Assert.Empty(places);
    }

    [Fact]
    public void Level_Given_a_level_that_was_loaded_and_not_changed_When_the_process_ends_Then_no_file_is_made_for_an_empty_seed()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make(("empty.nw", "arch map\nname empty.nw\nwidth 8\nheight 8\nend\n"));
        using (IExt ext = Start(world, owns: false))
        {
            LoadAll(ext, "empty.nw");
        }

        // Act / Assert
        Assert.False(File.Exists(ObjectWorld.Data(world, "levels/empty.nw.map")));
        world.Dispose();
    }

    [Fact]
    public void Level_Given_script_variables_of_the_seed_and_no_blob_When_saved_Then_flag_lines_are_kept_and_a_blob_replaces_them()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        string before = Flushed(ext, "levels/hall.nw.map");
        ulong banana = places[0].Get("handle")!.AsHandle;

        // Act
        Assert.Equal(0UL, ext.Call(16, new[] { Val.List(Val.List(H(banana), N(3.328125), N(1.125), S("v=1"))) }).Status);
        string after = Flushed(ext, "levels/hall.nw.map", t => t.Contains("mudblob"));

        // Assert
        Assert.Contains("flag born=1524297788.081527948\n", before);
        Assert.Contains("flag ismudobject=1\n", before);
        Assert.DoesNotContain("mudblob", before);
        Assert.DoesNotContain("flag born", after);
        Assert.Contains("mudblob v=1\n", after);
    }

    [Fact]
    public void Level_Given_a_header_in_the_seed_When_saved_Then_its_fields_are_written_back()
    {
        // Arrange
        using IExt ext = Hall();
        LoadAll(ext, "hall.nw");

        // Act
        string text = Flushed(ext, "levels/hall.nw.map");

        // Assert
        Assert.StartsWith("arch map\nname hall.nw\n", text);
        Assert.Contains("width 8\nheight 8\n", text);
        Assert.Contains("outdoor 1\nend\n", text);
        Assert.Contains("x 3.328125\ny 1.125\n", text);
    }

    // ------------------------------------------------------------------ objects.blobs

    [Fact]
    public void ObjectsBlobs_Given_rows_for_live_dead_and_unknown_handles_When_sent_Then_live_ones_are_stored_and_the_rest_ignored()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong live = places[0].Get("handle")!.AsHandle;
        ulong archetype = ArchHandle(ext, "banana");

        // Act
        ReplyFrame r = ext.Call(16, new[] { Val.List(
            Val.List(H(live), Val.Int(7), N(8.5), S("state")),
            Val.List(H(archetype), Val.Int(1), Val.Int(1), S("x")),
            Val.List(H((1UL << 32) | 999), Val.Int(1), Val.Int(1), S("x")),
            Val.List(H((5UL << 32) | 1), Val.Int(1), Val.Int(1), S("x"))) });
        Assert.Equal(0UL, ext.Call(8, new[] { S("hall.nw") }).Status);
        (List<Val> again, _) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Equal(7.0, again[0].Get("x")!.AsFloat);
        Assert.Equal(8.5, again[0].Get("y")!.AsFloat);
        Assert.Equal("state", again[0].Get("blob")!.AsString);
    }

    [Fact]
    public void ObjectsBlobs_Given_a_malformed_row_When_sent_Then_bad_args_and_no_row_is_applied()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong live = places[0].Get("handle")!.AsHandle;
        ext.Call(8, new[] { S("hall.nw") });
        (List<Val> reloaded, _) = LoadAll(ext, "hall.nw");
        ulong h = reloaded[0].Get("handle")!.AsHandle;

        // Act
        ReplyFrame shortRow = ext.Call(16, new[] { Val.List(Val.List(H(h), Val.Int(1), Val.Int(2), S("changed")), Val.List(H(h), Val.Int(1))) });
        ReplyFrame badType = ext.Call(16, new[] { Val.List(Val.List(H(h), S("1"), Val.Int(2), S("c"))) });
        ReplyFrame notList = ext.Call(16, new[] { S("rows") });
        ext.Call(8, new[] { S("hall.nw") });
        (List<Val> third, _) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.NotEqual(live, h);
        Assert.Equal((ulong)Status.BadArgs, shortRow.Status);
        Assert.Equal((ulong)Status.BadArgs, badType.Status);
        Assert.Equal((ulong)Status.BadArgs, notList.Status);
        Assert.True(third[0].Get("blob")!.IsNil);
        Assert.Equal(3.328125, third[0].Get("x")!.AsFloat);
    }

    [Fact]
    public void ObjectsBlobs_Given_nil_or_an_empty_list_When_sent_Then_nil()
    {
        // Arrange
        using IExt ext = Hall();

        // Act
        ReplyFrame nil = ext.Call(16, new[] { Val.Nil });
        ReplyFrame empty = ext.Call(16, new[] { Val.List() });

        // Assert
        Assert.Equal(0UL, nil.Status);
        Assert.Equal(0UL, empty.Status);
    }

    // ------------------------------------------------------------------ level.unload

    [Fact]
    public void LevelUnload_Given_a_loaded_level_When_unloaded_Then_its_handles_are_dead_and_no_effects_are_sent()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong h = places[0].Get("handle")!.AsHandle;

        // Act
        ReplyFrame r = ext.Call(8, new[] { S("hall.nw") });
        ReplyFrame read = ext.Call(9, new[] { H(h), Val.Int(Obj("mudname")) });

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        Assert.Empty(r.Events);
        Assert.Equal((ulong)Status.BadHandle, read.Status);
    }

    [Fact]
    public void LevelUnload_Given_a_level_not_held_or_the_wrong_arguments_When_unloaded_Then_nil_or_bad_args()
    {
        // Arrange
        using IExt ext = Hall();

        // Act
        ReplyFrame none = ext.Call(8, new[] { S("nowhere.nw") });
        ReplyFrame wrong = ext.Call(8, new[] { Val.Int(3) });
        ReplyFrame count = ext.Call(8);

        // Assert
        Assert.Equal(0UL, none.Status);
        Assert.Equal((ulong)Status.BadArgs, wrong.Status);
        Assert.Equal((ulong)Status.BadArgs, count.Status);
    }

    [Fact]
    public void LevelUnload_Given_an_unload_in_the_middle_of_a_load_When_loaded_again_Then_it_starts_afresh()
    {
        // Arrange
        using IExt ext = Hall();
        ReplyFrame first = ext.Call(7, new[] { S("many.nw"), Val.Nil }, level: "many.nw");
        Assert.True(first.Result.Get("more")!.AsBool);
        ext.Call(8, new[] { S("many.nw") });

        // Act
        ReplyFrame stale = ext.Call(7, new[] { S("many.nw"), first.Result.Get("cursor")! }, level: "many.nw");
        (List<Val> places, List<int> pages) = LoadAll(ext, "many.nw");

        // Assert
        Assert.False(stale.Result.Get("more")!.AsBool);
        Assert.Empty(stale.Effects);
        Assert.Equal(150, places.Count);
        Assert.Equal(new[] { 64, 64, 22 }, pages);
    }

    // ------------------------------------------------------------------ strings with line breaks (review of ext-objects, finding 1)

    [Theory]
    [InlineData("img")]
    [InlineData("npcparam")]
    [InlineData("mudgraalparam")]
    [InlineData("restrictaccess")]
    [InlineData("mudrealname")]
    [InlineData("mudtitle")]
    [InlineData("mudslaying")]
    [InlineData("mudrace")]
    public void Set_Given_a_text_with_a_line_break_When_set_Then_it_is_refused_and_the_saved_level_gains_no_object(string member)
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong banana = Add(ext, "lv.nw", "banana");

        // Act
        ReplyFrame viaNewline = Set(ext, banana, Obj(member), S("a\nend\narch sword\nx 9\ny 9\nend\narch map\n"));
        ReplyFrame viaReturn = Set(ext, banana, Obj(member), S("a\rb"));
        string text = Flushed(ext, "levels/lv.nw.map");

        // Assert
        Assert.NotEqual(0UL, viaNewline.Status);
        Assert.NotEqual(0UL, viaReturn.Status);
        Assert.DoesNotContain("arch sword", text);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "(?m)^arch map$"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "(?m)^arch banana$"));
    }

    [Fact]
    public void Set_Given_a_one_line_text_When_set_Then_it_is_stored()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong banana = Add(ext, "lv.nw", "banana");

        // Act
        ReplyFrame r = Set(ext, banana, Obj("mudrealname"), S("Excalibur"));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal("Excalibur", Get(ext, banana, Obj("mudrealname")).AsString);
    }

    [Fact]
    public void MudAddObject_Given_a_level_name_with_line_breaks_When_saved_Then_the_header_holds_no_lines_of_its_own()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        string level = "lv\nwidth 1\nend\narch sword";
        Add(ext, level, "banana");

        // Act
        string text = Flushed(ext, FileNames.LevelFile(level)!);

        // Assert
        Assert.DoesNotContain("\narch sword", text);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "(?m)^width "));
        Assert.DoesNotContain("\nwidth 1\n", text);
    }

    // ------------------------------------------------------------------ mudaddobject and mudaddspecialobject

    [Fact]
    public void MudAddObject_Given_a_known_archetype_When_called_Then_a_handle_and_its_npc_place_in_the_same_reply()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { N(1.5), N(2.25), S("banana") }, level: "main.nw");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(ValKind.Handle, r.Result.Kind);
        Val place = Assert.Single(r.Effects);
        Assert.Equal("npc.place", place.Get("k")!.AsString);
        Assert.Equal(r.Result.AsHandle, place.Get("handle")!.AsHandle);
        Assert.Equal("main.nw", place.Get("level")!.AsString);
        Assert.Equal(1.5, place.Get("x")!.AsFloat);
        Assert.Equal(2.25, place.Get("y")!.AsFloat);
        Assert.Equal("mud_banana.png", place.Get("image")!.AsString);
        Assert.Empty(place.Get("props")!.Pairs);
        Assert.True(place.Get("blob")!.IsNil);
        Assert.Equal("banana", Get(ext, r.Result.AsHandle, Obj("mudname")).AsString);
    }

    [Fact]
    public void MudAddObject_Given_two_calls_When_called_Then_two_handles_and_the_level_holds_both()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ulong a = Add(ext, "main.nw", "banana");
        ulong b = Add(ext, "main.nw", "sword");
        string text = Flushed(ext, "levels/main.nw.map");

        // Assert
        Assert.NotEqual(a, b);
        Assert.Contains("arch banana", text);
        Assert.Contains("arch sword", text);
    }

    [Fact]
    public void MudAddObject_Given_an_unknown_archetype_When_called_Then_nil_one_log_line_and_no_effects()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        int before = ext.Logs.Count;

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("nosuchthing") }, level: "main.nw");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        LogFrame line = Assert.Single(ext.Logs.Skip(before));
        Assert.Contains("nosuchthing", line.Text);
        Assert.Contains("not found", line.Text);
    }

    [Theory]
    [InlineData("treasure mining_iron")]
    [InlineData("Treasure shop_weapons")]
    [InlineData("treasure  x")]
    public void MudAddObject_Given_a_treasure_list_name_When_called_Then_nil_one_log_line_naming_phase_4(string name)
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        int before = ext.Logs.Count;

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S(name) }, level: "main.nw");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        LogFrame line = Assert.Single(ext.Logs.Skip(before));
        Assert.Contains("treasure", line.Text);
        Assert.Contains("phase 4", line.Text);
    }

    [Fact]
    public void MudAddObject_Given_no_level_in_the_context_When_called_Then_nil_and_one_log_line()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        int before = ext.Logs.Count;

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("banana") });

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        Assert.Single(ext.Logs.Skip(before));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void MudAddObject_Given_the_wrong_number_or_types_of_arguments_When_called_Then_bad_args(int shape)
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        Val[] args = shape switch
        {
            0 => Array.Empty<Val>(),
            2 => new[] { S("1"), N(1), S("banana") },
            _ => new[] { N(1), N(1), S("banana"), S("extra") },
        };

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, args, level: "main.nw");

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
        Assert.Empty(r.Effects);
    }

    [Fact]
    public void MudAddObject_Given_nil_arguments_When_called_Then_they_read_as_zero_and_the_empty_name_finds_nothing()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { Val.Nil, Val.Nil, Val.Nil }, level: "main.nw");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
    }

    [Fact]
    public void MudAddObject_Given_a_level_not_loaded_yet_When_called_Then_its_saved_objects_are_read_first_and_the_load_shows_them_but_not_the_new_one_again()
    {
        // Arrange
        using IExt ext = Hall();

        // Act
        ReplyFrame added = ext.Call(Manifest.OpMudAddObject, new[] { N(5), N(5), S("banana") }, level: "hall.nw");
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        string text = Flushed(ext, "levels/hall.nw.map");

        // Assert
        Assert.Single(added.Effects);
        Assert.Equal(3, places.Count);
        Assert.DoesNotContain(added.Result.AsHandle, places.Select(p => p.Get("handle")!.AsHandle));
        Assert.Equal(3, text.Split("arch banana").Length - 1); // the seed's, the one in the bag, the new one
        Assert.Contains("arch sword", text);
        Assert.Contains("x 5\ny 5\n", text);
    }

    [Fact]
    public void MudAddObject_Given_a_level_with_a_saved_file_not_loaded_When_called_Then_nothing_of_the_file_is_lost()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        Directory.CreateDirectory(Path.Combine(world.Path, "data", "levels"));
        File.WriteAllText(Path.Combine(world.Path, "data", "levels", "main.nw.map"), "arch map\nname main.nw\nend\narch sword\nmudx 2\nmudy 3\nend\n");
        using IExt ext = Start(world);

        // Act
        Add(ext, "main.nw", "banana");
        string text = Flushed(ext, "levels/main.nw.map", t => t.Contains("banana"));

        // Assert
        Assert.Contains("arch sword", text);
        Assert.Contains("x 2\ny 3\n", text);
    }

    [Fact]
    public void MudAddObject_Given_a_level_that_holds_the_most_When_called_Then_nil_and_a_log_line()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make(), new ExtensionOptions { Gk = new GkSettings { MaxObjectsPerLevel = 2 } });
        Add(ext, "main.nw", "banana");
        Add(ext, "main.nw", "banana");
        int before = ext.Logs.Count;

        // Act
        ReplyFrame third = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("banana") }, level: "main.nw");
        ReplyFrame other = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("banana") }, level: "other.nw");

        // Assert
        Assert.True(third.Result.IsNil);
        Assert.Empty(third.Effects);
        Assert.Contains("the most", Assert.Single(ext.Logs.Skip(before)).Text);
        Assert.Equal(ValKind.Handle, other.Result.Kind);
    }

    [Fact]
    public void MudAddSpecialObject_Given_an_archetype_a_key_a_value_and_a_message_When_called_Then_slaying_value_and_msg_are_set_before_the_place()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddSpecialObject, new[] { N(3), N(4), Val.Int(25), S("housedoor k5"), S("Mailbox of Bob") }, level: "main.nw");
        ulong h = r.Result.AsHandle;

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(h, Assert.Single(r.Effects).Get("handle")!.AsHandle);
        Assert.Equal("housedoor", Get(ext, h, Obj("mudname")).AsString);
        Assert.Equal("k5", Get(ext, h, Obj("mudslaying")).AsString);
        Assert.Equal("25", Get(ext, h, Obj("value")).AsString);
        Assert.Equal(new[] { "Mailbox of Bob" }, Get(ext, h, Obj("mudmessage")).Items.Select(i => i.AsString));
        Assert.Equal(23L, Get(ext, h, Obj("mudtype")).AsInt);
    }

    [Fact]
    public void MudAddSpecialObject_Given_only_an_archetype_and_empty_texts_When_called_Then_no_slaying_and_no_message()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddSpecialObject, new[] { N(3), N(4), Val.Int(0), S("  banana  "), S("") }, level: "main.nw");
        ulong h = r.Result.AsHandle;

        // Assert
        Assert.Equal("", Get(ext, h, Obj("mudslaying")).AsString);
        Assert.Empty(Get(ext, h, Obj("mudmessage")).Items);
        Assert.Equal("0", Get(ext, h, Obj("value")).AsString);
    }

    [Fact]
    public void MudAddSpecialObject_Given_an_unknown_first_word_or_wrong_arguments_When_called_Then_nil_or_bad_args()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame unknown = ext.Call(Manifest.OpMudAddSpecialObject, new[] { N(3), N(4), Val.Int(1), S("nothing k1"), S("") }, level: "main.nw");
        ReplyFrame wrong = ext.Call(Manifest.OpMudAddSpecialObject, new[] { N(3), N(4), Val.Int(1), S("banana") }, level: "main.nw");

        // Assert
        Assert.True(unknown.Result.IsNil);
        Assert.Empty(unknown.Effects);
        Assert.Equal((ulong)Status.BadArgs, wrong.Status);
    }

    // ------------------------------------------------------------------ cloneMudObject and destroymudobject

    [Fact]
    public void CloneMudObject_Given_an_object_with_state_When_called_Then_a_new_handle_at_the_new_position_with_the_state_but_not_the_script_variables()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong banana = places[0].Get("handle")!.AsHandle;
        Set(ext, banana, Obj("mudhp"), Val.Int(7));
        Set(ext, banana, Obj("mudrealname"), S("Golden banana"));
        Set(ext, banana, Obj("mudgraalparam"), S("kept"));

        // Act
        ReplyFrame r = ext.Call(13, new[] { H(banana), Val.Int(TMudObjectMembers.MethodCloneMudObject), Val.List(N(10.5), N(11.5)) });
        ulong clone = r.Result.AsHandle;

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.NotEqual(banana, clone);
        Val place = Assert.Single(r.Effects);
        Assert.Equal(clone, place.Get("handle")!.AsHandle);
        Assert.Equal(10.5, place.Get("x")!.AsFloat);
        Assert.Equal(11.5, place.Get("y")!.AsFloat);
        Assert.Equal("hall.nw", place.Get("level")!.AsString);
        Assert.Empty(place.Get("props")!.Pairs);
        Assert.True(place.Get("blob")!.IsNil);
        Assert.Equal(7L, Get(ext, clone, Obj("mudhp")).AsInt);
        Assert.Equal("Golden banana", Get(ext, clone, Obj("mudrealname")).AsString);
        Assert.Equal("kept", Get(ext, clone, Obj("mudgraalparam")).AsString);
        Assert.Equal(3L, Get(ext, clone, Obj("mudquantity")).AsInt);
        Set(ext, clone, Obj("mudhp"), Val.Int(1));
        Assert.Equal(7L, Get(ext, banana, Obj("mudhp")).AsInt);
    }

    [Fact]
    public void CloneMudObject_Given_a_container_When_called_Then_the_contents_are_copied_too()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong bag = places[2].Get("handle")!.AsHandle;

        // Act
        ReplyFrame r = ext.Call(13, new[] { H(bag), Val.Int(TMudObjectMembers.MethodCloneMudObject), Val.List(N(1), N(1)) });
        ulong clone = r.Result.AsHandle;
        string text = Flushed(ext, "levels/hall.nw.map", t => t.Split("arch bag").Length == 3);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.NotEqual(bag, clone);
        Assert.Equal(2, text.Split("arch bag").Length - 1);
        Assert.Equal(3, text.Split("arch banana").Length - 1); // the seed's and one in each bag
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void CloneMudObject_Given_the_wrong_number_of_arguments_When_called_Then_bad_args(int count)
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "banana");

        // Act
        ReplyFrame r = ext.Call(13, new[] { H(h), Val.Int(TMudObjectMembers.MethodCloneMudObject), Val.List(Enumerable.Repeat(N(1), count)) });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
    }

    [Fact]
    public void DestroyMudObject_Given_an_object_When_called_Then_npc_remove_the_handle_is_dead_and_the_file_has_no_trace()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong sword = places[1].Get("handle")!.AsHandle;

        // Act
        ReplyFrame r = ext.Call(13, new[] { H(sword), Val.Int(TMudObjectMembers.MethodDestroyMudObject), Val.List() });
        string text = Flushed(ext, "levels/hall.nw.map");
        ReplyFrame again = ext.Call(9, new[] { H(sword), Val.Int(Obj("mudname")) });

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Val remove = Assert.Single(r.Effects);
        Assert.Equal("npc.remove", remove.Get("k")!.AsString);
        Assert.Equal(sword, remove.Get("handle")!.AsHandle);
        Assert.Empty(r.Events);
        Assert.DoesNotContain("arch sword", text);
        Assert.Equal((ulong)Status.BadHandle, again.Status);
    }

    [Fact]
    public void DestroyMudObject_Given_arguments_or_a_dead_object_When_called_Then_bad_args_or_bad_handle()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "banana");

        // Act
        ReplyFrame args = ext.Call(13, new[] { H(h), Val.Int(TMudObjectMembers.MethodDestroyMudObject), Val.List(Val.Int(1)) });
        ext.Call(13, new[] { H(h), Val.Int(TMudObjectMembers.MethodDestroyMudObject), Val.List() });
        ReplyFrame dead = ext.Call(13, new[] { H(h), Val.Int(TMudObjectMembers.MethodDestroyMudObject), Val.List() });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, args.Status);
        Assert.Equal((ulong)Status.BadHandle, dead.Status);
    }

    [Fact]
    public void Invoke_Given_an_unknown_method_or_a_target_without_methods_When_called_Then_bad_args()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "banana");
        ulong arch = ArchHandle(ext, "banana");

        // Act
        ReplyFrame unknown = ext.Call(13, new[] { H(h), Val.Int(999), Val.List() });
        ReplyFrame onArch = ext.Call(13, new[] { H(arch), Val.Int(TMudObjectMembers.MethodDestroyMudObject), Val.List() });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, unknown.Status);
        Assert.Equal((ulong)Status.BadArgs, onArch.Status);
    }

    // ------------------------------------------------------------------ object.destroyed

    [Fact]
    public void ObjectDestroyed_Given_a_live_object_When_called_Then_nil_no_effects_no_events_the_handle_is_dead_and_the_object_is_not_saved_again()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong banana = places[0].Get("handle")!.AsHandle;

        // Act
        ReplyFrame r = ext.Call(14, new[] { H(banana) });
        ReplyFrame read = ext.Call(9, new[] { H(banana), Val.Int(Obj("mudname")) });
        ReplyFrame second = ext.Call(14, new[] { H(banana) });
        string text = Flushed(ext, "levels/hall.nw.map");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        Assert.Empty(r.Events);
        Assert.Equal((ulong)Status.BadHandle, read.Status);
        Assert.Equal((ulong)Status.BadHandle, second.Status);
        Assert.DoesNotContain("arch banana\nx 3.328125", text);
        Assert.Contains("arch sword", text);
    }

    [Fact]
    public void ObjectDestroyed_Given_a_nil_archetype_or_player_handle_or_wrong_arguments_When_called_Then_bad_handle_or_bad_args()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong arch = ArchHandle(ext, "banana");
        ulong player = Ok(ext.Call(5, new[] { S("bob") }, player: "bob"));

        // Act
        ReplyFrame nil = ext.Call(14, new[] { Val.Nil });
        ReplyFrame archetype = ext.Call(14, new[] { H(arch) });
        ReplyFrame ply = ext.Call(14, new[] { H(player) });
        ReplyFrame wrong = ext.Call(14, new[] { S("x") });
        ReplyFrame count = ext.Call(14);

        // Assert
        Assert.Equal((ulong)Status.BadHandle, nil.Status);
        Assert.Equal((ulong)Status.BadHandle, archetype.Status);
        Assert.Equal((ulong)Status.BadHandle, ply.Status);
        Assert.Equal((ulong)Status.BadArgs, wrong.Status);
        Assert.Equal((ulong)Status.BadArgs, count.Status);
        Assert.Equal(arch, ArchHandle(ext, "banana"));
        Assert.Equal(0UL, ext.Call(9, new[] { H(player), Val.Int(Ply("mudhp")) }).Status);
    }

    // ------------------------------------------------------------------ the members and onUpdateObject

    [Fact]
    public void Set_Given_a_writable_member_of_a_level_object_When_written_Then_the_stored_value_comes_back_and_onUpdateObject_is_fired_for_the_object()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "rat");

        // Act
        ReplyFrame r = Set(ext, h, Obj("mudhp"), Val.Int(3));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(3L, r.Result.AsInt);
        Assert.Equal(3L, Get(ext, h, Obj("mudhp")).AsInt);
        Val e = Assert.Single(r.Events);
        Assert.Equal(new[] { "name", "target", "handle", "args" }, e.Pairs.Select(p => p.Key));
        Assert.Equal("onUpdateObject", e.Get("name")!.AsString);
        Assert.Equal("object", e.Get("target")!.AsString);
        Assert.Equal(h, e.Get("handle")!.AsHandle);
        Assert.Empty(e.Get("args")!.Items);
        Assert.Empty(r.Effects);
    }

    [Fact]
    public void Set_Given_a_value_out_of_the_range_of_the_field_When_written_Then_it_is_clamped_and_the_clamped_value_is_returned()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "rat");

        // Act
        ReplyFrame big = Set(ext, h, Obj("mudhp"), Val.Int(70000));
        ReplyFrame small = Set(ext, h, Obj("mudac"), N(-1000.9));
        ReplyFrame nil = Set(ext, h, Obj("mudmaxhp"), Val.Nil);

        // Assert
        Assert.Equal(32767L, big.Result.AsInt);
        Assert.Equal(-128L, small.Result.AsInt);
        Assert.Equal(0L, nil.Result.AsInt);
    }

    [Fact]
    public void Set_Given_a_read_only_member_or_a_value_of_another_type_When_written_Then_bad_args_without_events_and_nothing_changes()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "rat");

        // Act
        ReplyFrame readOnly = Set(ext, h, Obj("mudname"), S("x"));
        ReplyFrame text = Set(ext, h, Obj("mudhp"), S("3"));
        ReplyFrame flag = Set(ext, h, Obj("nopick"), Val.Int(1));
        ReplyFrame str = Set(ext, h, Obj("mudrealname"), Val.Int(3));
        ReplyFrame unknown = Set(ext, h, 12345, Val.Int(1));
        ReplyFrame ofArch = Set(ext, h, Arch("mudname"), S("x"));

        // Assert
        Assert.All(new[] { readOnly, text, flag, str, unknown, ofArch }, r =>
        {
            Assert.Equal((ulong)Status.BadArgs, r.Status);
            Assert.Empty(r.Events);
            Assert.Empty(r.Effects);
        });
        Assert.Equal(5L, Get(ext, h, Obj("mudhp")).AsInt);
        Assert.Equal("rat", Get(ext, h, Obj("mudname")).AsString);
    }

    [Fact]
    public void Get_Given_reads_and_many_reads_When_called_Then_no_event_is_fired()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "rat");

        // Act
        ReplyFrame get = ext.Call(9, new[] { H(h), Val.Int(Obj("mudhp")) });
        ReplyFrame many = ext.Call(11, new[] { H(h), Val.List(Val.Int(Obj("mudhp")), Val.Int(Obj("mudname")), Val.Int(Obj("alive"))) });

        // Assert
        Assert.Empty(get.Events);
        Assert.Empty(many.Events);
        Assert.Equal(new[] { 5L }, many.Result.Items.Take(1).Select(v => v.AsInt));
        Assert.Equal("rat", many.Result.Items[1].AsString);
        Assert.Equal(ValKind.True, many.Result.Items[2].Kind);
    }

    [Fact]
    public void Get_Given_a_member_of_another_type_When_read_Then_bad_args()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "rat");
        ulong arch = ArchHandle(ext, "rat");

        // Act
        ReplyFrame playerMemberOnObject = ext.Call(9, new[] { H(h), Val.Int(Ply("levelcap")) });
        ReplyFrame objectMemberOnArch = ext.Call(9, new[] { H(arch), Val.Int(Obj("ismudobject")) });
        ReplyFrame many = ext.Call(11, new[] { H(h), Val.List(Val.Int(Obj("mudhp")), Val.Int(Arch("mudname"))) });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, playerMemberOnObject.Status);
        Assert.Equal((ulong)Status.BadArgs, objectMemberOnArch.Status);
        Assert.Equal((ulong)Status.BadArgs, many.Status);
    }

    [Fact]
    public void Get_Given_the_names_and_the_picture_members_When_read_Then_the_archetype_name_the_icon_and_a_picture_a_script_set()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "sword");
        ulong none = Add(ext, "main.nw", "nofaced");

        // Act
        Set(ext, h, Obj("mudrealname"), S("blade"));
        string icon = Get(ext, h, Obj("icon")).AsString;
        string img = Get(ext, h, Obj("img")).AsString;
        Set(ext, h, Obj("img"), S("mud_special.png"));
        string imgSet = Get(ext, h, Obj("img")).AsString;
        string iconSet = Get(ext, h, Obj("icon")).AsString;
        Set(ext, h, Obj("img"), S(""));

        // Assert
        Assert.Equal("sword", Get(ext, h, Obj("mudname")).AsString);
        Assert.Equal("sword", Get(ext, h, Obj("archname")).AsString);
        Assert.Equal("blade", Get(ext, h, Obj("mudrealname")).AsString);
        Assert.Equal("mud_sword.png", icon);
        Assert.Equal("mud_sword.png", img);
        Assert.Equal("mud_special.png", imgSet);
        Assert.Equal("mud_sword.png", iconSet);
        Assert.Equal("mud_sword.png", Get(ext, h, Obj("img")).AsString);
        Assert.Equal("", Get(ext, none, Obj("img")).AsString);
        Assert.Equal(ArchHandle(ext, "sword"), Get(ext, h, Obj("arch")).AsHandle);
        Assert.True(Get(ext, h, Obj("ismudobject")).AsBool);
    }

    [Fact]
    public void Get_Given_the_plain_fields_of_an_archetype_When_read_on_an_object_Then_the_values_of_the_archetype_and_the_flags_and_extra_lines_can_be_written()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "sword");

        // Act
        long type = Get(ext, h, Obj("mudtype")).AsInt;
        long damage = Get(ext, h, Obj("muddamage")).AsInt;
        long weight = Get(ext, h, Obj("mudweight")).AsInt;
        string value = Get(ext, h, Obj("value")).AsString;
        Set(ext, h, Obj("nopick"), Val.True);
        Set(ext, h, Obj("mudhpbuff"), Val.Int(40));
        Set(ext, h, Obj("mudspeed"), N(0.25));
        Set(ext, h, Obj("mudrandomvar"), Val.Int(9));
        Set(ext, h, Obj("restrictaccess"), S("guild:Staff"));
        Set(ext, h, Obj("nopeaceful"), Val.True);

        // Assert
        Assert.Equal(15L, type);
        Assert.Equal(8L, damage);
        Assert.Equal(3000L, weight);
        Assert.Equal("50", value);
        Assert.True(Get(ext, h, Obj("nopick")).AsBool);
        Assert.Equal(40L, Get(ext, h, Obj("mudhpbuff")).AsInt);
        Assert.Equal(0.25, Get(ext, h, Obj("mudspeed")).AsFloat);
        Assert.Equal(9L, Get(ext, h, Obj("mudrandomvar")).AsInt);
        Assert.Equal("guild:Staff", Get(ext, h, Obj("restrictaccess")).AsString);
        Assert.True(Get(ext, h, Obj("nopeaceful")).AsBool);
        Assert.Equal(-1L, Get(ext, h, Obj("mudgraaltype")).AsInt);
    }

    [Fact]
    public void Set_Given_members_that_are_extra_lines_When_the_level_is_saved_and_loaded_Then_they_survive()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        ulong before;
        using (IExt ext = Start(world, owns: false))
        {
            before = Add(ext, "main.nw", "sword");
            Set(ext, before, Obj("mudhpbuff"), Val.Int(40));
            Set(ext, before, Obj("img"), S("mud_special.png"));
            Set(ext, before, Obj("mudrandomvar"), Val.Int(77));
            Set(ext, before, Obj("mudtitle"), S("of Balor"));
            Set(ext, before, Obj("unpaid"), Val.True);
            Set(ext, before, Obj("mudgraaltype"), Val.Int(2));
        }

        // Act
        using IExt again = Start(world, epoch: 2);
        (List<Val> places, _) = LoadAll(again, "main.nw");
        ulong h = places.Single().Get("handle")!.AsHandle;

        // Assert
        Assert.Equal(40L, Get(again, h, Obj("mudhpbuff")).AsInt);
        Assert.Equal("mud_special.png", Get(again, h, Obj("img")).AsString);
        Assert.Equal(77L, Get(again, h, Obj("mudrandomvar")).AsInt);
        Assert.Equal("of Balor", Get(again, h, Obj("mudtitle")).AsString);
        Assert.True(Get(again, h, Obj("unpaid")).AsBool);
        Assert.Equal(2L, Get(again, h, Obj("mudgraaltype")).AsInt);
        Assert.Equal("mud_special.png", places.Single().Get("image")!.AsString);
    }

    [Fact]
    public void Manifest_Given_the_members_of_TMudObject_When_listed_Then_every_writable_member_is_not_stable_when_a_rule_could_change_it()
    {
        // Arrange
        var members = TMudObjectMembers.Table.Members;

        // Act
        var stable = members.Where(m => m.Stable).Select(m => m.Name).ToHashSet();
        var changing = new[] { "mudhp", "mudmaxhp", "mudsp", "mudmaxsp", "mudgrace", "mudmaxgrace", "mudexp", "mudfood", "mudweight", "mudcarrying", "mudquantity", "mudnrof", "mudspeed", "applied", "identified", "cursed", "damned", "unpaid", "alive", "scared", "fullname", "mudfullname" };

        // Assert
        Assert.All(changing, n => Assert.DoesNotContain(n, stable));
        Assert.All(new[] { "mudname", "archname", "ismudobject", "mudtype", "mudslaying", "mudrace", "icon", "img", "mudmessage" }, n => Assert.Contains(n, stable));
        Assert.Equal(members.Select(m => m.Id).Distinct().Count(), members.Count);
    }

    // ------------------------------------------------------------------ the classes

    [Theory]
    [InlineData("altar", "special_altar")]
    [InlineData("ghost", "mudobjects")]
    [InlineData("rat", "monsters_rat")]
    [InlineData("wolf", "mudobjects")]
    [InlineData("banana", "mudobjects")]
    public void MudAddObject_Given_archetypes_of_each_kind_When_placed_Then_the_class_follows_the_graalnpctype_and_the_classes_the_world_has(string arch, string expected)
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S(arch) }, level: "main.nw");

        // Assert
        Assert.Equal(new[] { expected }, Assert.Single(r.Effects).Get("classes")!.Items.Select(c => c.AsString));
    }

    [Fact]
    public void MudAddObject_Given_other_settings_When_placed_Then_the_prefixes_and_the_default_class_are_the_settings()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        File.WriteAllText(Path.Combine(world.Path, "scripts", "sp_altar.txt"), "x");
        File.WriteAllText(Path.Combine(world.Path, "scripts", "mon_wolf.txt"), "x");
        using IExt ext = Start(world, new ExtensionOptions { Gk = new GkSettings { SpecialPrefix = "sp_", MonsterPrefix = "mon_", DefaultClass = "things" } });

        // Act
        string[] classes = new[] { "altar", "wolf", "rat", "banana" }.Select(a =>
            ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S(a) }, level: "main.nw").Effects.Single().Get("classes")!.Items.Single().AsString).ToArray();

        // Assert
        Assert.Equal(new[] { "sp_altar", "mon_wolf", "things", "things" }, classes);
    }

    [Fact]
    public void MudAddObject_Given_the_scripts_folder_in_another_case_and_class_files_in_another_case_When_placed_Then_the_class_is_found_by_the_name_of_the_file()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        Directory.Move(Path.Combine(world.Path, "scripts"), Path.Combine(world.Path, "Scripts"));
        File.Move(Path.Combine(world.Path, "Scripts", "special_altar.txt"), Path.Combine(world.Path, "Scripts", "Special_Altar.txt"));
        using IExt ext = Start(world);

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("altar") }, level: "main.nw");

        // Assert
        Assert.Equal("Special_Altar", Assert.Single(r.Effects).Get("classes")!.Items.Single().AsString);
    }

    [Fact]
    public void MudAddObject_Given_a_world_without_a_scripts_folder_When_placed_Then_every_object_gets_the_default_class()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        Directory.Delete(Path.Combine(world.Path, "scripts"), true);
        using IExt ext = Start(world);

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("altar") }, level: "main.nw");

        // Assert
        Assert.Equal("mudobjects", Assert.Single(r.Effects).Get("classes")!.Items.Single().AsString);
    }

    // ------------------------------------------------------------------ handles, stats and the saves

    [Fact]
    public void Handles_Given_objects_and_archetypes_When_given_out_Then_every_tag_is_new_and_stats_count_them()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong arch = ArchHandle(ext, "banana");
        ulong a = Add(ext, "main.nw", "banana");
        ulong b = Add(ext, "main.nw", "banana");
        ext.Call(14, new[] { H(a) });
        ulong c = Add(ext, "main.nw", "banana");

        // Act
        ReplyFrame stats = ext.Call(15);

        // Assert
        Assert.Equal(4, new[] { arch, a, b, c }.Select(h => (uint)h).Distinct().Count());
        Assert.All(new[] { arch, a, b, c }, h => Assert.Equal(1UL, h >> 32));
        Assert.Equal(4L, stats.Result.Get("tags")!.AsInt);
        Assert.Equal(1L, stats.Result.Get("levels")!.AsInt);
        Assert.Equal(2L, stats.Result.Get("level_objects")!.AsInt);
    }

    [Fact]
    public void Handles_Given_the_tags_used_up_When_an_object_is_made_Then_internal_and_the_known_ones_still_answer()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make(), new ExtensionOptions { TagLimit = 2 });
        ulong a = Add(ext, "main.nw", "banana");
        ulong b = Add(ext, "main.nw", "banana");

        // Act
        ReplyFrame third = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("banana") }, level: "main.nw");

        // Assert
        Assert.Equal((ulong)Status.Internal, third.Status);
        Assert.Contains("tags", third.Result.AsString);
        Assert.DoesNotContain(ext.Logs, l => l.Level == 0); // not a failure of the extension: the answer the protocol gives
        Assert.Empty(third.Effects);
        Assert.Equal("banana", Get(ext, a, Obj("mudname")).AsString);
        Assert.Equal("banana", Get(ext, b, Obj("mudname")).AsString);
    }

    [Fact]
    public void Destroyed_Given_an_object_made_and_saved_When_the_host_reports_it_gone_Then_the_next_file_has_no_such_object()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong sword = places[1].Get("handle")!.AsHandle;
        ulong made = Add(ext, "hall.nw", "banana", 5, 6);
        string before = Flushed(ext, "levels/hall.nw.map");

        // Act
        ReplyFrame destroyed = ext.Call(14, new[] { H(sword) });
        ReplyFrame destroyedMade = ext.Call(14, new[] { H(made) });
        string after = Flushed(ext, "levels/hall.nw.map", t => !t.Contains("arch sword"));

        // Assert
        Assert.Equal(0UL, destroyed.Status);
        Assert.Equal(0UL, destroyedMade.Status);
        Assert.Contains("arch sword", before);
        Assert.DoesNotContain("arch sword", after);
        Assert.Equal(after.Split("arch banana").Length - 1 + 1, before.Split("arch banana").Length - 1); // the made banana is gone, the seed's stay
        Assert.Contains("arch banana", after);
    }

    [Fact]
    public void LevelLoad_Given_a_level_named_in_another_case_by_the_host_When_made_unloaded_and_loaded_Then_it_is_one_level_with_one_file_and_the_seed_is_found()
    {
        // Arrange (the host compares level names without regard to case and names the level as whoever loaded it first spelled it)
        using IExt ext = Hall();
        (List<Val> seedPlaces, _) = LoadAll(ext, "HALL.NW"); // the seed's file is hall.nw
        ulong made = Add(ext, "Hall.nw", "banana", 5, 6); // the same level, spelled otherwise
        Flushed(ext, "levels/hall.nw.map");

        // Act
        Assert.Equal(0UL, ext.Call(8, new[] { S("HALL.nw") }).Status);
        (List<Val> again, _) = LoadAll(ext, "hAll.nw");

        // Assert
        Assert.NotEmpty(seedPlaces);
        Assert.Equal(seedPlaces.Count + 1, again.Count); // the made banana came back with the seed's objects
        Assert.Equal(new[] { "hall.nw.map" }, Directory.GetFiles(ObjectWorld.Data(ext.World, "levels")).Select(Path.GetFileName).ToArray());
        Assert.NotEqual(0UL, made);
    }

    [Fact]
    public void LevelLoad_Given_a_seed_file_spelled_in_mixed_case_When_the_level_is_loaded_in_any_case_Then_the_seed_is_read()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make(("Hall.NW", ObjectWorld.SeedHall)));

        // Act
        (List<Val> places, _) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.NotEmpty(places);
    }

    [Fact]
    public void Set_Given_the_value_the_member_already_has_When_set_Then_ok_and_no_event_but_a_changed_value_fires_one()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "banana");
        Set(ext, h, Obj("mudhp"), Val.Int(7));
        Flushed(ext, "levels/main.nw.map");

        // Act
        ReplyFrame same = Set(ext, h, Obj("mudhp"), Val.Int(7));
        ReplyFrame other = Set(ext, h, Obj("mudhp"), Val.Int(8));

        // Assert
        Assert.Equal(0UL, same.Status);
        Assert.Empty(same.Events);
        Assert.Equal(0UL, other.Status);
        Assert.Single(other.Events);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("special")]
    [InlineData("clone")]
    public void Maker_Given_the_tags_used_up_When_an_object_is_made_Then_it_is_not_left_in_the_level_or_its_file(string maker)
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make(), new ExtensionOptions { TagLimit = 1 });
        ulong first = Add(ext, "main.nw", "banana");

        // Act
        ReplyFrame failed = maker switch
        {
            "add" => ext.Call(Manifest.OpMudAddObject, new[] { N(3), N(4), S("sword") }, level: "main.nw"),
            "special" => ext.Call(Manifest.OpMudAddSpecialObject, new[] { N(3), N(4), Val.Int(1), S("sword"), S("") }, level: "main.nw"),
            _ => ext.Call(13, new[] { H(first), Val.Int(TMudObjectMembers.MethodCloneMudObject), Val.List(N(1), N(1)) }),
        };
        string text = Flushed(ext, "levels/main.nw.map");
        ReplyFrame stats = ext.Call(15);

        // Assert
        Assert.Equal((ulong)Status.Internal, failed.Status);
        Assert.Empty(failed.Effects);
        Assert.Equal(1L, stats.Result.Get("level_objects")!.AsInt);
        Assert.Contains("arch banana", text);
        Assert.DoesNotContain("arch sword", text);
        Assert.Equal(1, text.Split("arch banana").Length - 1);
    }

    [Fact]
    public void Flush_Given_changed_levels_and_no_more_changes_When_flushed_Then_the_files_are_written_and_a_second_flush_leaves_them()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        Add(ext, "a.nw", "banana");
        Add(ext, "b/c.nw", "sword");

        // Act
        string a = Flushed(ext, "levels/a.nw.map");
        string bc = Flushed(ext, "levels/b%2Fc.nw.map");

        // Assert
        Assert.Contains("arch banana", a);
        Assert.Contains("arch sword", bc);
    }

    [Fact]
    public void Shutdown_Given_save_true_When_called_Then_the_levels_are_on_disk_at_the_reply_and_with_save_false_nothing_is_written()
    {
        // Arrange
        TempFolder saved = ObjectWorld.Make();
        TempFolder unsaved = ObjectWorld.Make();
        using (IExt ext = Start(saved, owns: false))
        {
            Add(ext, "a.nw", "banana");
            Assert.Equal(0UL, ext.Call(4, new[] { Val.True }).Status);
            Assert.True(File.Exists(ObjectWorld.Data(saved, "levels/a.nw.map")));
        }
        using (IExt ext = Start(unsaved, owns: false))
        {
            Add(ext, "a.nw", "banana");
            Assert.Equal(0UL, ext.Call(4, new[] { Val.False }).Status);
        }

        // Act / Assert
        Assert.False(File.Exists(ObjectWorld.Data(unsaved, "levels/a.nw.map")));
        saved.Dispose();
        unsaved.Dispose();
    }

    [Fact]
    public void Exit_Given_a_connection_that_ends_When_levels_changed_Then_they_are_saved_by_the_exit()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        using (IExt ext = Start(world, owns: false))
        {
            Add(ext, "a.nw", "banana");
        }

        // Act / Assert
        Assert.Contains("arch banana", File.ReadAllText(ObjectWorld.Data(world, "levels/a.nw.map")));
        world.Dispose();
    }

    [Fact]
    public void Level_Given_a_saved_file_that_cannot_be_read_When_loaded_or_added_to_Then_it_is_left_as_it_is()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        string folder = Path.Combine(world.Path, "data", "levels");
        Directory.CreateDirectory(Path.Combine(folder, "locked.nw.map")); // a folder where the file should be: reading it fails
        using IExt ext = Start(world);

        // Act
        (List<Val> places, _) = LoadAll(ext, "locked.nw");
        ReplyFrame added = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("banana") }, level: "locked.nw");
        Assert.Equal(0UL, ext.Call(3).Status);

        // Assert
        Assert.Empty(places);
        Assert.True(added.Result.IsNil);
        Assert.True(Directory.Exists(Path.Combine(folder, "locked.nw.map")));
    }

    // ------------------------------------------------------------------ more branches

    [Fact]
    public void ObjectsBlobs_Given_a_row_with_more_than_four_items_When_sent_Then_bad_args()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");

        // Act
        ReplyFrame r = ext.Call(16, new[] { Val.List(Val.List(places[0].Get("handle")!, Val.Int(1), Val.Int(2), S("b"), S("extra"))) });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
    }

    [Fact]
    public void MudAddObject_Given_an_empty_level_name_When_called_Then_nil_and_the_line_says_the_call_has_no_level()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        int before = ext.Logs.Count;

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("banana") }, level: "");

        // Assert
        Assert.True(r.Result.IsNil);
        Assert.Contains("no level", Assert.Single(ext.Logs.Skip(before)).Text);
    }

    [Fact]
    public void ObjectsBlobs_Given_an_empty_blob_for_an_object_with_script_variables_When_sent_Then_the_variables_of_the_file_are_gone_for_good()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        ulong banana = places[0].Get("handle")!.AsHandle;
        Assert.NotEmpty(places[0].Get("props")!.Pairs);

        // Act
        Assert.Equal(0UL, ext.Call(16, new[] { Val.List(Val.List(H(banana), N(3.328125), N(1.125), S(""))) }).Status);
        string text = Flushed(ext, "levels/hall.nw.map", t => !t.Contains("flag born"));
        Assert.Equal(0UL, ext.Call(8, new[] { S("hall.nw") }).Status);
        (List<Val> again, _) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.DoesNotContain("flag ", text.Substring(0, text.IndexOf("arch sword", StringComparison.Ordinal)));
        Assert.Empty(again[0].Get("props")!.Pairs);
        Assert.True(again[0].Get("blob")!.IsNil);
    }

    [Fact]
    public void Level_Given_a_file_with_an_empty_mudblob_line_When_loaded_Then_the_blob_is_nil()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        Directory.CreateDirectory(Path.Combine(world.Path, "data", "levels"));
        File.WriteAllText(Path.Combine(world.Path, "data", "levels", "b.nw.map"), "arch map\nname b.nw\nend\narch banana\nmudx 1\nmudy 2\nmudblob \nend\n");
        using IExt ext = Start(world);

        // Act
        (List<Val> places, _) = LoadAll(ext, "b.nw");

        // Assert
        Assert.True(Assert.Single(places).Get("blob")!.IsNil);
    }

    [Fact]
    public void Set_Given_a_change_after_the_level_was_flushed_When_flushed_again_Then_the_file_has_the_change()
    {
        // Arrange
        using IExt ext = Hall();
        (List<Val> places, _) = LoadAll(ext, "hall.nw");
        Flushed(ext, "levels/hall.nw.map");
        Thread.Sleep(100); // the writer has written it: the level is clean now

        // Act
        Assert.Equal(0UL, Set(ext, places[1].Get("handle")!.AsHandle, Obj("mudtitle"), S("of Doom")).Status);
        string text = Flushed(ext, "levels/hall.nw.map", t => t.Contains("title of Doom"));

        // Assert
        Assert.Contains("title of Doom\n", text);
    }

    [Fact]
    public void LevelLoad_Given_an_object_whose_place_would_not_fit_a_frame_When_loaded_Then_it_is_given_up_with_a_log_line_and_the_others_load()
    {
        // Arrange
        using IExt ext = StartSmallFrames(ObjectWorld.Make(("hall.nw", ObjectWorld.SeedHall)));
        (List<Val> placed, _) = LoadAll(ext, "hall.nw");
        ulong huge = placed[0].Get("handle")!.AsHandle;
        Assert.Equal(0UL, ext.Call(16, new[] { Val.List(Val.List(H(huge), Val.Int(1), Val.Int(1), S(new string('x', 65_420)))) }).Status);
        Assert.Equal(0UL, ext.Call(8, new[] { S("hall.nw") }).Status);
        int before = ext.Logs.Count;

        // Act
        (List<Val> again, _) = LoadAll(ext, "hall.nw");

        // Assert
        Assert.Equal(2, again.Count);
        Assert.Contains(ext.Logs.Skip(before), l => l.Text.Contains("too large to show"));
    }

    [Theory]
    [InlineData("treasure")]
    [InlineData("treasurechest")]
    public void MudAddObject_Given_an_archetype_whose_name_starts_with_treasure_When_called_Then_it_is_an_archetype_and_not_a_list(string name)
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S(name) }, level: "main.nw");

        // Assert
        Assert.Equal(ValKind.Handle, r.Result.Kind);
        Assert.Single(r.Effects);
    }

    [Fact]
    public void Set_Given_an_empty_title_after_a_title_When_saved_Then_the_field_is_cleared_and_not_an_empty_text()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Add(ext, "main.nw", "banana");
        Set(ext, h, Obj("mudtitle"), S("of Doom"));
        ReplyFrame cleared = Set(ext, h, Obj("mudtitle"), S(""));

        // Act
        string text = Flushed(ext, "levels/main.nw.map");

        // Assert
        Assert.Equal("", cleared.Result.AsString);
        Assert.DoesNotContain("title", text);
    }
}
