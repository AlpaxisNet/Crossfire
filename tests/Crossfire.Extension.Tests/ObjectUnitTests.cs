// Not ported from a Crossfire file: tests of the parts of objects in levels that need no extension process: the level text, the class
// rule, the props of a seed's script variables, the handles table and the docs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Text.RegularExpressions;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

public class ObjectUnitTests
{
    private static string RepoFile(string relative)
    {
        for (string? dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string file = Path.Combine(dir, relative);
            if (File.Exists(file))
            {
                return file;
            }
        }
        throw new FileNotFoundException(relative);
    }

    private static (CfWorld World, GkHost Host, List<string> Log) World()
    {
        var log = new List<string>();
        var host = new GkHost();
        var world = new CfWorld(host, (level, text) => log.Add(level + ": " + text.TrimEnd('\n')));
        world.InitArchetypes("Object empty_archetype\nend\n" + ObjectWorld.Arc);
        return (world, host, log);
    }

    // ------------------------------------------------------------------ RenamePositions

    [Theory]
    [InlineData("arch a\nx 3.5\ny 1.25\nend\n", "arch a\nmudx 3.5\nmudy 1.25\nend\n")]
    [InlineData("arch a\n  X 3\nY 4\nend\n", "arch a\n  mudx 3\nmudy 4\nend\n")]
    [InlineData("arch a\nxx 3\nx2 3\nend\n", "arch a\nxx 3\nx2 3\nend\n")]
    [InlineData("x 1\ny 2\n", "x 1\ny 2\n")]
    public void RenamePositions_Given_position_lines_When_renamed_Then_only_x_and_y_inside_an_object_block_change(string text, string expected)
    {
        // Arrange / Act
        string renamed = LevelFiles.RenamePositions(text);

        // Assert
        Assert.Equal(expected, renamed);
    }

    [Fact]
    public void RenamePositions_Given_a_nested_block_and_a_message_When_renamed_Then_the_nested_block_and_the_message_text_are_left_alone()
    {
        // Arrange
        string text = "arch bag\nx 1\nmsg\nx 5\nend\nendmsg\narch banana\nx 9\nend\ny 2\nend\narch sword\ny 3\nend\n";

        // Act
        string renamed = LevelFiles.RenamePositions(text);

        // Assert
        Assert.Equal("arch bag\nmudx 1\nmsg\nx 5\nend\nendmsg\narch banana\nx 9\nend\nmudy 2\nend\narch sword\nmudy 3\nend\n", renamed);
    }

    [Fact]
    public void RenamePositions_Given_lines_with_a_carriage_return_When_renamed_Then_the_line_ends_are_kept()
    {
        // Arrange / Act
        string renamed = LevelFiles.RenamePositions("arch a\r\nx 3\r\nend\r\n");

        // Assert
        Assert.Equal("arch a\r\nmudx 3\r\nend\r\n", renamed);
    }

    // ------------------------------------------------------------------ Read and Write

    [Fact]
    public void Read_Given_a_text_without_a_header_When_read_Then_the_objects_are_read_and_the_header_is_the_default_with_the_level_s_name()
    {
        // Arrange
        (CfWorld w, GkHost host, _) = World();

        // Act
        LevelRead r = LevelFiles.Read(w, host, "plain.nw", "arch banana\nx 2\ny 3\nend\n");

        // Assert
        Assert.Equal(1, r.Objects);
        Assert.Equal("plain.nw", r.Level.Header.Name);
        Assert.Equal((2.0, 3.0), (r.Level.Objects[0].X, r.Level.Objects[0].Y));
    }

    [Fact]
    public void Read_Given_an_object_without_a_position_and_odd_numbers_When_read_Then_zero_for_a_missing_or_bad_number()
    {
        // Arrange
        (CfWorld w, GkHost host, _) = World();

        // Act
        LevelRead r = LevelFiles.Read(w, host, "p.nw", "arch banana\nend\narch banana\nx abc\ny 1e400\nend\narch banana\nx -2.5\nend\n");

        // Assert
        Assert.Equal(new[] { (0.0, 0.0), (0.0, 0.0), (-2.5, 0.0) }, r.Level.Objects.Select(o => (o.X, o.Y)));
    }

    [Fact]
    public void Write_Given_a_level_When_read_back_Then_objects_positions_flags_blobs_and_contents_are_the_same_and_the_text_is_a_fixpoint()
    {
        // Arrange
        (CfWorld w, GkHost host, _) = World();
        LevelRead first = LevelFiles.Read(w, host, "hall.nw", ObjectWorld.SeedHall);
        first.Level.Objects[0].Blob = "a=1\nb=\"two\"\\";
        first.Level.Objects[0].SeedFlags.Clear();

        // Act
        string text = LevelFiles.Write(w, first.Level);
        LevelRead second = LevelFiles.Read(w, host, "hall.nw", text);

        // Assert
        Assert.Equal(3, second.Objects);
        Assert.Equal("a=1\nb=\"two\"\\", second.Level.Objects[0].Blob);
        Assert.Equal(new[] { 3.328125, 0.5, 7.0 }, second.Level.Objects.Select(o => o.X));
        Assert.Equal(first.Level.Objects[1].Ob.Msg, second.Level.Objects[1].Ob.Msg);
        Assert.NotNull(second.Level.Objects[2].Ob.Inv);
        Assert.Equal(text, LevelFiles.Write(w, second.Level));
    }

    [Fact]
    public void Write_Given_a_blob_with_a_line_that_looks_like_a_block_end_When_written_Then_it_stays_inside_one_line()
    {
        // Arrange
        (CfWorld w, GkHost host, _) = World();
        LevelRead r = LevelFiles.Read(w, host, "a.nw", "arch banana\nx 1\ny 1\nend\n");
        r.Level.Objects[0].Blob = "end\narch sword\nendmsg\n";

        // Act
        string text = LevelFiles.Write(w, r.Level);
        LevelRead back = LevelFiles.Read(w, host, "a.nw", text);

        // Assert
        Assert.Equal(1, back.Objects);
        Assert.Equal("end\narch sword\nendmsg\n", back.Level.Objects[0].Blob);
        Assert.Single(text.Split('\n'), l => l.StartsWith("mudblob "));
    }

    [Fact]
    public void Write_Given_an_object_with_the_archetypes_position_When_written_Then_no_integer_position_line_but_the_floats()
    {
        // Arrange
        (CfWorld w, GkHost host, _) = World();
        LevelRead r = LevelFiles.Read(w, host, "a.nw", "arch banana\nx 4\ny 5\nend\n");
        r.Level.Objects[0].Ob.X = 77; // whatever the integer holds, it is not what is written

        // Act
        string block = LevelFiles.ObjectBlock(w, r.Level.Objects[0]);

        // Assert
        Assert.Equal("arch banana\nx 4\ny 5\nend\n", block);
        Assert.Equal(77, r.Level.Objects[0].Ob.X);
    }

    [Fact]
    public void Read_Given_flag_lines_the_archetype_has_too_When_read_Then_the_archetype_s_own_are_not_script_variables_and_the_file_s_are()
    {
        // Arrange
        var log = new List<string>();
        var host = new GkHost();
        var w = new CfWorld(host, (l, t) => log.Add(t));
        w.InitArchetypes("Object empty_archetype\nend\nObject banana\nname banana\nflag kind=fruit\nflag n=1\nend\n");

        // Act
        LevelRead r = LevelFiles.Read(w, host, "a.nw", "arch banana\nflag n=1\nflag own=2\nend\n");

        // Assert
        Assert.Equal(new[] { "n=1", "own=2" }, r.Level.Objects[0].SeedFlags); // kind=fruit is the archetype's; n=1 is written in the file as well
        Assert.DoesNotContain(log, l => l.Contains("Unrecognized"));
    }

    [Fact]
    public void Read_Given_a_block_of_an_unknown_archetype_When_read_Then_it_is_counted_as_an_error_and_discarded()
    {
        // Arrange
        (CfWorld w, GkHost host, _) = World();

        // Act
        LevelRead r = LevelFiles.Read(w, host, "a.nw", "arch notthere\nx 1\nend\narch banana\nend\n");

        // Assert
        Assert.Equal(1, r.Objects);
        Assert.True(r.Errors >= 1);
    }

    [Fact]
    public void RestoreFileOrder_Given_a_container_with_contents_When_saved_loaded_and_saved_again_Then_the_order_of_the_contents_is_the_same()
    {
        // Arrange
        (CfWorld w, GkHost host, _) = World();
        string text = "arch bag\nx 1\ny 1\narch sword\nend\narch banana\nnrof 2\nend\narch bag\nend\nend\n";

        // Act
        LevelRead first = LevelFiles.Read(w, host, "a.nw", text);
        string once = LevelFiles.Write(w, first.Level);
        string twice = LevelFiles.Write(w, LevelFiles.Read(w, host, "a.nw", once).Level);

        // Assert
        Assert.Equal(new[] { "sword", "banana", "bag" }, Contents(first.Level.Objects[0].Ob));
        Assert.Equal(once, twice);
        Assert.Contains("arch sword\nend\narch banana\nnrof 2\nend\narch bag\nend\nend\n", once);
    }

    private static IEnumerable<string> Contents(CfObject ob)
    {
        for (CfObject? c = ob.Inv; c != null; c = c.Below)
        {
            yield return c.Name!;
        }
    }

    // ------------------------------------------------------------------ the classes and the props

    [Fact]
    public void For_Given_an_instance_whose_own_graalnpctype_differs_from_the_archetype_s_When_asked_Then_the_instance_decides()
    {
        // Arrange
        (CfWorld w, _, _) = World();
        GkClasses classes = GkClasses.Of(new[] { "special_banana", "monsters_banana" });
        CfObject banana = w.ArchToObject(w.FindArchetype("banana"))!;

        // Act
        string plain = classes.For(banana).Single();
        banana.Extra = new ExtraKeys();
        banana.Extra.Set("graalnpctype", "2");
        string special = classes.For(banana).Single();
        banana.Extra.Set("graalnpctype", "1");
        string monster = classes.For(banana).Single();
        banana.Extra.Set("graalnpctype", "7");
        string other = classes.For(banana).Single();

        // Assert
        Assert.Equal(new[] { "mudobjects", "special_banana", "monsters_banana", "mudobjects" }, new[] { plain, special, monster, other });
    }

    [Fact]
    public void Exists_Given_names_in_another_case_When_asked_Then_found_and_the_spelling_of_the_class_is_the_one_given_to_Of()
    {
        // Arrange
        GkClasses classes = GkClasses.Of(new[] { "Special_Altar" });

        // Act / Assert
        Assert.True(classes.Exists("special_altar"));
        Assert.False(classes.Exists("special_alt"));
        Assert.Equal(1, classes.Count);
    }

    [Theory]
    [InlineData("12", "Int", "12")]
    [InlineData("-3", "Int", "-3")]
    [InlineData("1524297788.081527948", "Float", "1524297788.081528")]
    [InlineData("0.5", "Float", "0.5")]
    [InlineData("1e3", "Float", "1000")]
    [InlineData("abc", "String", "abc")]
    [InlineData("\"a\",\"b\"", "String", "\"a\",\"b\"")]
    [InlineData("", "String", "")]
    [InlineData("1,2", "String", "1,2")]
    [InlineData("-", "String", "-")]
    [InlineData("99999999999999999999", "Float", "1E+20")]
    public void Typed_Given_the_value_of_a_flag_When_converted_Then_a_number_or_the_text(string value, string kind, string text)
    {
        // Arrange / Act
        Val v = GkLook.Typed(value);

        // Assert
        Assert.Equal(kind, v.Kind.ToString());
        Assert.Equal(text, kind switch { "Int" => v.AsInt.ToString(), "Float" => v.AsFloat.ToString("R", System.Globalization.CultureInfo.InvariantCulture), _ => v.AsString });
    }

    [Fact]
    public void Props_Given_flags_with_odd_names_and_a_repeated_one_When_made_Then_identifiers_only_and_the_last_value_of_a_repeat()
    {
        // Arrange
        (CfWorld w, GkHost host, _) = World();
        var lo = new LevelObject(new Level("a"), w.ArchToObject(w.FindArchetype("banana"))!);
        lo.SeedFlags.AddRange(new[] { "a=1", "b c=2", "=3", "9x=4", "_ok=5", "a=7", "flagonly", "d=x=y" });

        // Act
        Val props = GkLook.Props(lo);

        // Assert
        Assert.Equal(new[] { "this._ok", "this.a", "this.flagonly", "this.d" }.OrderBy(k => k), props.Pairs.Select(p => p.Key).OrderBy(k => k));
        Assert.Equal(7L, props.Get("this.a")!.AsInt);
        Assert.Equal(1L, props.Get("this.flagonly")!.AsInt);
        Assert.Equal("x=y", props.Get("this.d")!.AsString);
    }

    [Fact]
    public void Props_Given_a_blob_When_made_Then_no_props()
    {
        // Arrange
        (CfWorld w, _, _) = World();
        var lo = new LevelObject(new Level("a"), w.ArchToObject(w.FindArchetype("banana"))!) { Blob = "x" };
        lo.SeedFlags.Add("a=1");

        // Act / Assert
        Assert.Empty(GkLook.Props(lo).Pairs);
    }

    [Fact]
    public void Read_Given_the_classes_of_a_world_folder_When_read_Then_the_txt_files_of_the_scripts_folder()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts", "sub"));
        File.WriteAllText(Path.Combine(world.Path, "scripts", "sub", "deep.txt"), "x");
        File.WriteAllText(Path.Combine(world.Path, "scripts", "notaclass.dat"), "x");

        // Act
        GkClasses classes = GkClasses.Read(world.Path, new GkSettings());

        // Assert
        Assert.Equal(ObjectWorld.Classes.Length, classes.Count);
        Assert.True(classes.Exists("monsters_rat"));
        Assert.False(classes.Exists("deep"));
        Assert.False(classes.Exists("notaclass"));
        world.Dispose();
    }

    // ------------------------------------------------------------------ the handles

    [Fact]
    public void HandleTable_Given_objects_When_given_out_and_killed_Then_tags_are_never_reused_and_a_dead_tag_resolves_to_nothing()
    {
        // Arrange
        var table = new HandleTable(5, 100);
        var a = new object();
        var b = new object();

        // Act
        ulong ha = table.HandleOf(a);
        table.Kill(a);
        ulong hb = table.HandleOf(b);

        // Assert
        Assert.Equal((5UL << 32) | 1, ha);
        Assert.Equal((5UL << 32) | 2, hb);
        Assert.Null(table.Resolve(ha));
        Assert.Same(b, table.Resolve(hb));
        Assert.Equal(2, table.Issued);
        Assert.False(table.Has(a));
        Assert.Null(table.Resolve(0));
        Assert.Null(table.Resolve((4UL << 32) | 2));
        Assert.Null(table.Resolve((5UL << 32) | 3));
    }

    [Fact]
    public void HandleTable_Given_the_limit_When_reached_Then_the_next_object_throws_and_known_ones_still_have_their_tag()
    {
        // Arrange
        var table = new HandleTable(1, 1);
        var a = new object();
        table.HandleOf(a);

        // Act / Assert
        Assert.Throws<TagsUsedUpException>(() => table.HandleOf(new object()));
        Assert.Equal(1u, table.TagOf(a));
    }

    // ------------------------------------------------------------------ the docs

    private static string Section(string doc, string heading)
    {
        int a = doc.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(a >= 0, heading);
        int b = Regex.Match(doc.Substring(a + heading.Length), @"^#{2,4} ", RegexOptions.Multiline) is { Success: true } m ? a + heading.Length + m.Index : doc.Length;
        return doc.Substring(a, b - a);
    }

    private static List<(string Name, int Id, string Type, string Access, string Stable)> Rows(string section) =>
        Regex.Matches(section, @"^\| `(?<name>[A-Za-z]+)` \| (?<id>\d+) \| (?<type>[A-Za-z:]+) \| (?<access>rw?) \| (?<stable>yes|no) \| .+ \|$", RegexOptions.Multiline)
            .Select(m => (m.Groups["name"].Value, int.Parse(m.Groups["id"].Value), m.Groups["type"].Value, m.Groups["access"].Value, m.Groups["stable"].Value)).ToList();

    [Fact]
    public void Members_Given_the_tables_of_TMudObject_and_TMudPlayer_When_compared_with_docs_extension_md_Then_ids_names_types_access_and_stability_are_the_same()
    {
        // Arrange
        string doc = File.ReadAllText(RepoFile(Path.Combine("docs", "extension.md")));

        // Act
        var objectRows = Rows(Section(doc, "#### Members of TMudObject"));
        var playerRows = Rows(Section(doc, "#### Members of TMudPlayer"));

        // Assert
        Assert.Equal(TMudObjectMembers.Table.Members.Select(m => (m.Name, m.Id, m.TypeName, m.Writable ? "rw" : "r", m.Stable ? "yes" : "no")), objectRows);
        Assert.Equal(TMudPlayerMembers.Table.Members.Select(m => (m.Name, m.Id, m.TypeName, m.Writable ? "rw" : "r", m.Stable ? "yes" : "no")), playerRows);
        Assert.Equal(200, objectRows[0].Id);
        Assert.Equal(500, playerRows[0].Id);
    }

    [Fact]
    public void Docs_Given_the_functions_methods_and_events_When_compared_with_the_manifest_Then_each_is_listed_with_its_id()
    {
        // Arrange
        string doc = File.ReadAllText(RepoFile(Path.Combine("docs", "extension.md")));
        Val manifest = Manifest.Build();

        // Act / Assert
        foreach (Val f in manifest.Get("functions")!.Items)
        {
            Assert.Contains("| `" + f.Get("name")!.AsString + "` | " + f.Get("id")!.AsInt + " |", doc);
        }
        foreach (MethodDef m in TMudObjectMembers.Table.Methods)
        {
            Assert.Contains("| `" + m.Name + "` | " + m.Id + " |", doc);
        }
        foreach (Val e in manifest.Get("events")!.Items)
        {
            Assert.Contains("| `" + e.Get("name")!.AsString + "` | " + e.Get("target")!.AsString + " | " + e.Get("mode")!.AsString + " |", doc);
        }
    }

    [Fact]
    public void Manifest_Given_the_types_When_the_ids_are_compared_Then_members_and_methods_never_share_an_id_across_the_whole_manifest()
    {
        // Arrange
        Val manifest = Manifest.Build();

        // Act
        var ids = manifest.Get("types")!.Items.SelectMany(t => t.Get("members")!.Items.Concat(t.Get("methods")!.Items)).Select(m => m.Get("id")!.AsInt).ToList();

        // Assert
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(manifest.Get("types")!.Items.Where(t => t.Get("host")!.AsString != "object"), t => Assert.True(t.Get("members")!.Items.Count > 40));
    }
}
