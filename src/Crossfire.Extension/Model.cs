// Not ported from a Crossfire file: what the Crossfire extension holds besides the archetypes: levels with their objects, and players.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Text;

namespace Crossfire.Extension;

/// <summary>
/// An object in a level (the host shows it as an NPC): the Crossfire object, the position the host last gave (tiles, floats: the
/// host's position is the truth), the host's script state as text (stored, never read) and the script variables that the file
/// the object came from had, until the host gives its own state.
/// </summary>
internal sealed class LevelObject
{
    public LevelObject(Level level, CfObject ob)
    {
        Level = level;
        Ob = ob;
    }

    public Level Level { get; }

    public CfObject Ob { get; }

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>The host's script state (objects.blobs), null when it never gave one.</summary>
    public string? Blob { get; set; }

    /// <summary>The <c>flag</c> lines of the file the object was read from (<c>name=value</c>, as written): its script variables until a blob replaces them.</summary>
    public List<string> SeedFlags { get; } = new();

    /// <summary>True once an <c>npc.place</c> for it was sent.</summary>
    public bool Placed { get; set; }

    /// <summary>True once the object is destroyed or its level unloaded.</summary>
    public bool Dead { get; set; }
}

/// <summary>A level the extension holds the objects of (the map of Crossfire's <c>load_objects</c> and <c>save_objects</c>).</summary>
internal sealed class Level : IMapHandle
{
    public Level(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public MapHeader Header { get; set; } = new();

    /// <summary>The objects in the order they were made or read (also the order of the file and of <c>level.load</c>).</summary>
    public List<LevelObject> Objects { get; } = new();

    /// <summary>True when something changed since the level was read or last saved.</summary>
    public bool Dirty { get; set; }

    /// <summary>The objects the pages of the running <c>level.load</c> go through (null when none runs).</summary>
    public List<LevelObject>? Snapshot { get; set; }

    /// <summary>Where the objects came from: "saved", "seed" or "new".</summary>
    public string Source { get; set; } = "new";
}

/// <summary>A player who is logged in: the Crossfire object of the player, and the client strings the extension set for it.</summary>
internal sealed class PlayerState
{
    public PlayerState(string account, CfObject ob)
    {
        Account = account;
        Ob = ob;
    }

    /// <summary>The account name as the host gave it (compared as bytes).</summary>
    public string Account { get; }

    public CfObject Ob { get; }

    /// <summary>The player's controller (C <c>contr</c>), set while the player is logged in.</summary>
    public Gk.Player? Contr => Ob.Contr as Gk.Player;

    public bool Dirty { get; set; }

    /// <summary>The client strings (the whole flag name, such as <c>clientr.mud_hp</c>) set for this player, in the order they were first set, with their value.</summary>
    public Dictionary<string, string> ClientStrings { get; } = new(StringComparer.Ordinal);

    public List<string> ClientOrder { get; } = new();
}

/// <summary>What the extension does to objects that C's loader hands over.</summary>
internal static class Inventories
{
    /// <summary>
    /// Puts an object's inventory (and the inventories inside it) in the order of the file it was read from. C's <c>insert_ob_in_ob</c>
    /// puts each object read at the head of the inventory and <c>save_object</c> writes the inventory from the head, so a save and a
    /// load turn the order round: an inventory would be reversed at every login. Reversing it once after the load makes a round trip
    /// keep the order (docs/differences.md).
    /// </summary>
    public static void RestoreFileOrder(CfObject ob)
    {
        var items = new List<CfObject>();
        for (CfObject? c = ob.Inv; c != null; c = c.Below)
        {
            items.Add(c);
        }
        items.Reverse();
        for (int i = 0; i < items.Count; i++)
        {
            items[i].Above = i > 0 ? items[i - 1] : null;
            items[i].Below = i < items.Count - 1 ? items[i + 1] : null;
            RestoreFileOrder(items[i]);
        }
        ob.Inv = items.Count > 0 ? items[0] : null;
    }
}

/// <summary>The text of a file with one char per byte (<see cref="CStr.Latin1"/>), as the port reads and writes it.</summary>
internal static class FileText
{
    /// <summary>A .NET string as the bytes of its UTF-8 text, one char per byte.</summary>
    public static string ToBytesText(string s) => CStr.Latin1.GetString(Encoding.UTF8.GetBytes(s));

    /// <summary>The string whose UTF-8 bytes are the chars of <paramref name="bytesText"/> (ill-formed sequences become U+FFFD).</summary>
    public static string FromBytesText(string bytesText) => Encoding.UTF8.GetString(CStr.Latin1.GetBytes(bytesText));

    public static byte[] ToBytes(string bytesText) => CStr.Latin1.GetBytes(bytesText);

    public static string FromBytes(byte[] bytes) => CStr.Latin1.GetString(bytes);

    /// <summary>
    /// A text on one line: <c>\</c> as <c>\\</c>, a newline as <c>\n</c>, a carriage return as <c>\r</c> and any other control
    /// character (below 0x20 or 0x7f) as <c>\xNN</c>; the bytes of non-ASCII text stay as they are.
    /// </summary>
    public static string EscapeLine(string bytesText)
    {
        var sb = new StringBuilder(bytesText.Length);
        foreach (char c in bytesText)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                default:
                    if (c < 0x20 || c == 0x7f)
                    {
                        sb.Append("\\x").Append(((int)c).ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>The reverse of <see cref="EscapeLine"/> (a backslash that starts no escape stays as it is).</summary>
    public static string UnescapeLine(string line)
    {
        var sb = new StringBuilder(line.Length);
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c != '\\' || i + 1 >= line.Length)
            {
                sb.Append(c);
                continue;
            }
            char n = line[i + 1];
            if (n == '\\')
            {
                sb.Append('\\');
                i++;
            }
            else if (n == 'n')
            {
                sb.Append('\n');
                i++;
            }
            else if (n == 'r')
            {
                sb.Append('\r');
                i++;
            }
            else if (n == 'x' && i + 3 < line.Length && Uri.IsHexDigit(line[i + 2]) && Uri.IsHexDigit(line[i + 3]))
            {
                sb.Append((char)Convert.ToInt32(line.Substring(i + 2, 2), 16));
                i += 3;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
