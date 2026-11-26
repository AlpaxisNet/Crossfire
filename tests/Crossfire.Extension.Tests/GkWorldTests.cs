// Not ported from a Crossfire file: tests that read Graal Kingdoms' own world when the environment names it (nothing of it is part of
// this repository): the 1,233 files of its mudlevels folder and the classes of its archetypes.
//
//   CROSSFIRE_GK_ARCH       the Arch folder
//   CROSSFIRE_GK_MUDLEVELS  the mudlevels folder
//   CROSSFIRE_GK_SCRIPTS    the scripts folder (one file per class)
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Xunit.Abstractions;

namespace Crossfire.Extension.Tests;

/// <summary>Runs a test only when the named variables name existing folders.</summary>
public sealed class GkWorldFactAttribute : FactAttribute
{
    public GkWorldFactAttribute(params string[] variables)
    {
        foreach (string v in variables)
        {
            string? folder = Environment.GetEnvironmentVariable(v);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Skip = "Set " + v + " to the folder of Graal Kingdoms' world to run this test.";
                return;
            }
        }
    }

    public static string Folder(string variable) => Environment.GetEnvironmentVariable(variable) ?? "";
}

public class GkWorldTests
{
    private readonly ITestOutputHelper output;

    public GkWorldTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static (CfWorld World, GkHost Host, List<string> Errors) LoadWorld()
    {
        var errors = new List<string>();
        var host = new GkHost();
        var world = new CfWorld(host, (level, text) =>
        {
            if (level == LogLevel.Error)
            {
                errors.Add(text.TrimEnd('\n'));
            }
        });
        ArchCollection collection = ArchCollector.Collect(GkWorldFactAttribute.Folder("CROSSFIRE_GK_ARCH"));
        world.InitArchetypes(collection.Archetypes);
        errors.Clear();
        return (world, host, errors);
    }

    [GkWorldFact("CROSSFIRE_GK_ARCH", "CROSSFIRE_GK_MUDLEVELS")]
    public void LevelFiles_Given_every_file_of_the_mudlevels_folder_When_read_Then_each_parses_with_its_header_and_the_counts_are_those_of_the_specification()
    {
        // Arrange
        (CfWorld world, GkHost host, List<string> errors) = LoadWorld();
        string[] files = Directory.GetFiles(GkWorldFactAttribute.Folder("CROSSFIRE_GK_MUDLEVELS")).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        int objects = 0;
        int withObjects = 0;
        int withHeader = 0;
        int flagObjects = 0;
        int nested = 0;
        var perFileErrors = new Dictionary<string, int>();
        var errorKinds = new Dictionary<string, int>();

        // Act
        foreach (string file in files)
        {
            int before = errors.Count;
            string name = Path.GetFileName(file);
            LevelRead read = LevelFiles.Read(world, host, name, FileText.FromBytes(File.ReadAllBytes(file)));
            objects += read.Objects;
            withObjects += read.Objects > 0 ? 1 : 0;
            withHeader += read.Level.Header.Name == name ? 1 : 0;
            flagObjects += read.Level.Objects.Count(o => o.SeedFlags.Count > 0);
            nested += read.Level.Objects.Sum(o => CountNested(o.Ob));
            if (errors.Count > before)
            {
                perFileErrors[name] = errors.Count - before;
            }
        }
        foreach (string e in errors)
        {
            string kind = e.Length > 40 ? e.Substring(0, 40) : e;
            errorKinds[kind] = errorKinds.GetValueOrDefault(kind) + 1;
        }

        // Assert
        output.WriteLine("files " + files.Length + ", with objects " + withObjects + ", header names " + withHeader + ", objects " + objects + ", nested " + nested
            + ", with script variables " + flagObjects + ", errors " + errors.Count + " in " + perFileErrors.Count + " files");
        foreach (KeyValuePair<string, int> kv in errorKinds.OrderByDescending(k => k.Value).Take(15))
        {
            output.WriteLine(kv.Value + " x " + kv.Key);
        }
        Assert.Equal(1233, files.Length);
        Assert.Equal(1233, withHeader);
        // objects.md 8.1: 1,631 object blocks in 140 files, 49 of them nested: 1,582 objects of levels; every one has its archetype
        Assert.Equal(140, withObjects);
        Assert.Equal(1582, objects);
        Assert.Equal(49, nested);
        Assert.Equal(1564, flagObjects);
        Assert.Equal(new[] { "Couldn't find archetype arrow" }, errors); // a nested object of one file: its archetype is not in the dump
    }

    private static int CountNested(CfObject ob)
    {
        int n = 0;
        for (CfObject? inv = ob.Inv; inv != null; inv = inv.Below)
        {
            n += 1 + CountNested(inv);
        }
        return n;
    }

    [GkWorldFact("CROSSFIRE_GK_ARCH", "CROSSFIRE_GK_MUDLEVELS")]
    public void LevelFiles_Given_every_level_of_the_mudlevels_folder_When_written_and_read_again_Then_the_objects_positions_and_script_variables_are_the_same()
    {
        // Arrange
        (CfWorld world, GkHost host, _) = LoadWorld();
        string[] files = Directory.GetFiles(GkWorldFactAttribute.Folder("CROSSFIRE_GK_MUDLEVELS")).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        int compared = 0;

        // Act / Assert
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            LevelRead first = LevelFiles.Read(world, host, name, FileText.FromBytes(File.ReadAllBytes(file)));
            if (first.Objects == 0)
            {
                continue;
            }
            string text = LevelFiles.Write(world, first.Level);
            LevelRead second = LevelFiles.Read(world, host, name, text);
            Assert.Equal(first.Objects, second.Objects);
            for (int i = 0; i < first.Objects; i++)
            {
                LevelObject a = first.Level.Objects[i];
                LevelObject b = second.Level.Objects[i];
                Assert.True(a.Ob.Arch!.Name == b.Ob.Arch!.Name && a.X == b.X && a.Y == b.Y, name + " object " + i);
                Assert.Equal(a.SeedFlags, b.SeedFlags);
                // C's saver writes a float with %f (6 decimals): the seed's speed_left -0.100000023 comes back as -0.1, which the archetype has too
                string da = string.Join("\n", world.DumpObject(a.Ob).Split('\n').Where(l => !l.StartsWith("speed_left")));
                string db = string.Join("\n", world.DumpObject(b.Ob).Split('\n').Where(l => !l.StartsWith("speed_left")));
                Assert.Equal(da, db);
            }
            // the second text is a fixpoint (the first one differs from it only by the floats %f rounded)
            string again = LevelFiles.Write(world, second.Level);
            Assert.Equal(again, LevelFiles.Write(world, LevelFiles.Read(world, host, name, again).Level));
            compared++;
        }
        output.WriteLine("levels compared: " + compared);
        Assert.True(compared > 100);
    }

    [GkWorldFact("CROSSFIRE_GK_ARCH", "CROSSFIRE_GK_SCRIPTS")]
    public void GkClasses_Given_the_archetypes_and_scripts_of_graal_kingdoms_When_the_classes_are_chosen_Then_they_follow_the_rule_of_objects_md_3_1()
    {
        // Arrange
        (CfWorld world, _, _) = LoadWorld();
        var settings = new GkSettings();
        GkClasses classes = GkClasses.Read(Path.GetDirectoryName(GkWorldFactAttribute.Folder("CROSSFIRE_GK_SCRIPTS"))!, settings);
        var scripts = Directory.GetFiles(GkWorldFactAttribute.Folder("CROSSFIRE_GK_SCRIPTS"), "*.txt").Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int special = 0;
        int monsters = 0;
        int plain = 0;
        int type2NoClass = 0;
        int type1NoClass = 0;

        // Act / Assert
        for (Archetype? at = world.FirstArchetype; at != null; at = at.Next)
        {
            if (at.Head != null)
            {
                continue;
            }
            CfObject ob = world.ArchToObject(at)!;
            long type = ob.Extra?.Get("graalnpctype") is { } t ? CStr.Atoi(t) : -1;
            string expected = type == 2 && scripts.Contains("special_" + at.Name) ? "special_" + at.Name
                : type == 1 && scripts.Contains("monsters_" + at.Name) ? "monsters_" + at.Name
                : "mudobjects";
            string got = Assert.Single(classes.For(ob));
            Assert.Equal(expected, got, ignoreCase: true);
            special += got.StartsWith("special_", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            monsters += got.StartsWith("monsters_", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            plain += got == "mudobjects" ? 1 : 0;
            type2NoClass += type == 2 && got == "mudobjects" ? 1 : 0;
            type1NoClass += type == 1 && got == "mudobjects" ? 1 : 0;
        }
        output.WriteLine("classes in the world " + classes.Count + "; special " + special + ", monsters " + monsters + ", mudobjects " + plain
            + " (graalnpctype 2 without a class " + type2NoClass + ", graalnpctype 1 without one " + type1NoClass + ")");
        Assert.True(classes.Exists("mudobjects"));
        Assert.Equal(2015, classes.Count);
        Assert.Equal(307, special); // objects.md 3.1: 307 of the archetypes that have a special_ script have graalnpctype 2
        Assert.Equal(208, monsters);
        Assert.Equal(23, type2NoClass);
        Assert.Equal(35, type1NoClass);
    }
}
