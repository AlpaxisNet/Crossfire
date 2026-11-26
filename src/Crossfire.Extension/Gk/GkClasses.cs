// Not ported from a Crossfire file: the Graal Kingdoms adaptation of how a level object looks to the host: the classes it joins, its
// image and its properties (docs/extension.md, "The GK adaptation"; objects.md 3.1 and 3.3 of the specification of the library).
// Names of Graal Kingdoms' classes only: no file of its world is here.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Gk;

/// <summary>The settings of the Graal Kingdoms adaptation (the prefixes and the default class are the library's options in the original).</summary>
internal sealed class GkSettings
{
    /// <summary>The class of an archetype with <c>graalnpctype</c> 2 is this prefix and the archetype's name (when that class exists).</summary>
    public string SpecialPrefix { get; init; } = "special_";

    /// <summary>The class of an archetype with <c>graalnpctype</c> 1 (a monster) is this prefix and the archetype's name (when that class exists).</summary>
    public string MonsterPrefix { get; init; } = "monsters_";

    /// <summary>The class of every other object, and of one whose special or monster class does not exist.</summary>
    public string DefaultClass { get; init; } = "mudobjects";

    /// <summary>The folder of the world that holds the scripts, one file <c>&lt;class&gt;.txt</c> per class.</summary>
    public string ScriptsFolder { get; init; } = "scripts";

    /// <summary>The most objects one level holds when a script makes them (objects.md 5.1, a guess that protects the server from a loop); objects read from a file are not counted against it.</summary>
    public int MaxObjectsPerLevel { get; init; } = 5000;
}

/// <summary>
/// The names of the script classes of the world, read once at <c>session.init</c> from the world's scripts folder, and the rule that
/// chooses an object's classes (objects.md 3.1): <c>special_&lt;arch&gt;</c> for an archetype with <c>graalnpctype</c> 2 when that class
/// exists, <c>monsters_&lt;arch&gt;</c> for <c>graalnpctype</c> 1 when it exists, else <c>mudobjects</c>.
/// </summary>
internal sealed class GkClasses
{
    private readonly Dictionary<string, string> names;
    private readonly GkSettings settings;

    private GkClasses(Dictionary<string, string> names, GkSettings settings)
    {
        this.names = names;
        this.settings = settings;
    }

    /// <summary>A set of class names given by hand (tests).</summary>
    public static GkClasses Of(IEnumerable<string> classNames, GkSettings? settings = null)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string n in classNames)
        {
            d[n] = n;
        }
        return new GkClasses(d, settings ?? new GkSettings());
    }

    /// <summary>
    /// Reads the class names of a world: the files <c>*.txt</c> of its scripts folder (the folder is found without regard to the case
    /// of its name), each without the suffix. A world without that folder has no classes.
    /// </summary>
    public static GkClasses Read(string worldDir, GkSettings settings)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? folder = null;
        try
        {
            folder = Directory.EnumerateDirectories(worldDir)
                .FirstOrDefault(p => string.Equals(Path.GetFileName(p), settings.ScriptsFolder, StringComparison.OrdinalIgnoreCase));
            if (folder != null)
            {
                foreach (string file in Directory.EnumerateFiles(folder, "*.txt"))
                {
                    string n = Path.GetFileNameWithoutExtension(file);
                    d.TryAdd(n, n);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // an unreadable folder has no classes
        }
        return new GkClasses(d, settings);
    }

    /// <summary>How many classes the world has.</summary>
    public int Count => names.Count;

    /// <summary>True when the world has a class of that name (names compare without regard to case).</summary>
    public bool Exists(string name) => names.ContainsKey(name);

    /// <summary>The classes an object joins when the host shows it (one name; the host skips a class it does not have).</summary>
    public List<string> For(CfObject ob)
    {
        long type = ob.Extra?.Get("graalnpctype") is { } t ? CStr.Atoi(t) : -1;
        string arch = ob.Arch?.Name ?? "";
        if (type == 2 && names.TryGetValue(settings.SpecialPrefix + arch, out string? special))
        {
            return new List<string> { special };
        }
        if (type == 1 && names.TryGetValue(settings.MonsterPrefix + arch, out string? monster))
        {
            return new List<string> { monster };
        }
        return new List<string> { settings.DefaultClass };
    }
}

/// <summary>What an <c>npc.place</c> says about an object: the look of a level object to the host (objects.md 3.3).</summary>
internal static class GkLook
{
    /// <summary>
    /// The NPC's image: the object's picture (its <c>img</c>, the icon of its face), so that the NPC shows something before the
    /// object's own script runs <c>showobject()</c>; "" for an object without a face.
    /// </summary>
    public static string Image(CfObject ob) => ImageOf(ob);

    internal static string ImageOf(CfObject ob) => ob.Extra?.Get("mudimg") is { Length: > 0 } img ? FileText.FromBytesText(img) : TMudArchMembers.Icon(ob.Face?.Name);

    /// <summary>
    /// The script variables of an object that came from a file and has no state from the host yet: each <c>flag name=value</c> line
    /// is the property <c>this.name</c> (a number as a number, other text as text); a name that is no identifier is left out.
    /// </summary>
    public static Val Props(LevelObject lo)
    {
        if (lo.Blob != null)
        {
            return Val.Map();
        }
        var pairs = new List<(string, Val)>();
        foreach (string flag in lo.SeedFlags)
        {
            int eq = flag.IndexOf('=');
            string name = eq < 0 ? flag.Trim() : flag.Substring(0, eq).Trim();
            string value = eq < 0 ? "1" : flag.Substring(eq + 1).Trim();
            if (!IsIdentifier(name))
            {
                continue;
            }
            string key = "this." + name;
            pairs.RemoveAll(p => p.Item1 == key);
            pairs.Add((key, Typed(value)));
        }
        return Val.Map(pairs.ToArray());
    }

    private static bool IsIdentifier(string s)
    {
        if (s.Length == 0 || !(char.IsAsciiLetter(s[0]) || s[0] == '_'))
        {
            return false;
        }
        return s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    }

    /// <summary>A flag's value as a script value: an integer or a decimal number as a number, anything else as the text it is.</summary>
    internal static Val Typed(string value)
    {
        if (long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long n))
        {
            return Val.Int(n);
        }
        if (value.Length > 0 && (char.IsAsciiDigit(value[0]) || value[0] == '-' || value[0] == '.') && value.Any(char.IsAsciiDigit)
            && double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out double d) && double.IsFinite(d))
        {
            return Val.Float(d);
        }
        return Val.Str(FileText.FromBytesText(value));
    }
}
