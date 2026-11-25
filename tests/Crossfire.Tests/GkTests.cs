using Xunit.Abstractions;

namespace Crossfire.Tests;

/// <summary>Runs only when the archetypes of Graal Kingdoms are on this machine (they are not part of the repository).</summary>
public sealed class GkFactAttribute : FactAttribute
{
    public const string ArchFolder = "/home/v/Desktop/UGD/Playerworlds/Graal Kingdoms/Gk Dev [2018-06-28]/GK Dev/Arch";

    public GkFactAttribute()
    {
        if (!Directory.Exists(ArchFolder))
        {
            Skip = "The archetypes of Graal Kingdoms are not on this machine: " + ArchFolder;
        }
    }
}

/// <summary>Not ported from a Crossfire file: the measure of the .arc reader on Graal Kingdoms' own archetypes.</summary>
public class GkTests
{
    private readonly ITestOutputHelper output;

    public GkTests(ITestOutputHelper output) => this.output = output;

    [GkFact]
    public void Collect_Given_the_archetypes_of_graal_kingdoms_When_loaded_Then_every_object_block_is_an_archetype_and_the_unknown_keys_are_kept()
    {
        // Arrange
        string[] files = Directory.GetFiles(GkFactAttribute.ArchFolder, "*.arc", SearchOption.AllDirectories);
        int objectLines = 0;
        foreach (string f in files)
        {
            foreach (string line in CStr.Latin1.GetString(File.ReadAllBytes(f)).Split('\n'))
            {
                if (line.StartsWith("Object ", StringComparison.OrdinalIgnoreCase))
                {
                    objectLines++;
                }
            }
        }
        var log = new List<string>();
        var w = new CfWorld(null, (level, text) => log.Add(level + ": " + text));

        // Act
        ArchCollection collection = ArchCollector.Collect(GkFactAttribute.ArchFolder);
        w.InitArchetypes(collection.Archetypes);

        // Assert
        int blocks = 0;
        int parts = 0;
        int withExtra = 0;
        int nameless = 0;
        var keys = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var names = new HashSet<string>();
        Archetype? prevAt = null;
        for (Archetype? at = w.FirstArchetype; at != null; prevAt = at, at = at.More == null ? at.Next : at.More)
        {
            blocks++;
            if (at.Head != null)
            {
                parts++;
            }
            names.Add(at.Name ?? "");
            if (at.Name == null)
            {
                nameless++;
                output.WriteLine("block without an Object name after " + prevAt?.Name + ": clone name " + at.Clone.Name + ", type " + at.Clone.Type + ", extra " + (at.Clone.Extra == null ? "-" : string.Join("|", at.Clone.Extra.Entries.Select(e => e.Key + "=" + e.Value))));
            }
            AddKeys(at.Clone, keys, ref withExtra);
        }
        var fromLines = new List<string>();
        foreach (string f in files)
        {
            foreach (string line in CStr.Latin1.GetString(File.ReadAllBytes(f)).Split('\n'))
            {
                if (line.StartsWith("Object ", StringComparison.OrdinalIgnoreCase))
                {
                    fromLines.Add(CfWorld.Yval(line));
                }
            }
        }
        var loaded = new List<string>();
        for (Archetype? at = w.FirstArchetype; at != null; at = at.More == null ? at.Next : at.More)
        {
            if (at.Name != null)
            {
                loaded.Add(at.Name);
            }
        }
        var lost = fromLines.GroupBy(x => x).Select(g => (g.Key, g.Count())).Where(t => loaded.Count(l => l == t.Key) != t.Item2).Select(t => t.Key + " (Object lines " + t.Item2 + ", loaded " + loaded.Count(l => l == t.Key) + ")").ToList();
        foreach (string n in lost)
        {
            output.WriteLine("Object line without its own archetype: " + n);
        }
        int errors = w.LogCounts[(int)LogLevel.Error];
        output.WriteLine("files " + collection.Files.Count + ", 'Object' lines " + objectLines + ", archetypes in the list " + blocks + " (parts " + parts + "), distinct names " + names.Count);
        output.WriteLine("objects with unknown keys " + withExtra + ", distinct unknown keys " + keys.Count + ", lines of unknown keys " + keys.Values.Sum());
        output.WriteLine("errors logged " + errors);
        foreach (KeyValuePair<string, int> kv in keys.OrderByDescending(k => k.Value).Take(40))
        {
            output.WriteLine("  " + kv.Key + " " + kv.Value);
        }
        foreach (string e in log.Where(l => l.StartsWith("Error")).GroupBy(l => string.Join(" ", l.Split(' ').Take(4))).Select(g => g.Count() + " x " + g.Key + " (e.g. " + g.First().Trim() + ")"))
        {
            output.WriteLine("  " + e.Trim());
        }

        // What the 1,602 files of the backup of 2018-06-28 hold: 3,297 Object lines (three of them in lower case); Monster/Human/thugs.arc has
        // no end after thug1, so that Object line is overwritten by the next one; Furniture/dice.arc has three stray end lines and
        // Monster/Human/thugs.arc one: four blocks without a name. 3,297 - 1 + 4 = 3,300 archetypes, 288 of them parts (More).
        Assert.Equal(1602, collection.Files.Count);
        Assert.Equal(3297, objectLines);
        Assert.Equal(3300, blocks);
        Assert.Equal(288, parts);
        Assert.Equal(4, nameless);
        Assert.Equal(new[] { "thug1 (Object lines 2, loaded 1)" }, lost);
        Assert.Equal(1351, withExtra);
        Assert.Equal(23, keys.Count);
        Assert.Equal(1325, keys["graalnpctype"]);
        Assert.Equal(700, keys["graalnpcparam"]);
        Assert.Equal(218, keys["flag"]);
        Assert.Equal(95, errors);
    }

    private static void AddKeys(CfObject o, SortedDictionary<string, int> keys, ref int withExtra)
    {
        if (o.Extra != null)
        {
            withExtra++;
            foreach (KeyValuePair<string, string> e in o.Extra.Entries)
            {
                string k = e.Key.ToLowerInvariant();
                keys[k] = keys.GetValueOrDefault(k) + 1;
            }
        }
        for (CfObject? i = o.Inv; i != null; i = i.Below)
        {
            AddKeys(i, keys, ref withExtra);
        }
    }
}
