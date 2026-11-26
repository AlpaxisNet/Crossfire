// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): include/treasure.h and common/treasure.c (the treasure lists: their
// data and the reader of the lib/treasures file).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     include/treasure.h, common/treasure.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
// ---- end of notices ----
using System.Globalization;

namespace Crossfire;

/// <summary>
/// include/treasure.h <c>treasure</c>: one element of a treasure list, which is a linked list of these (<see cref="Next"/>), with
/// the branches <see cref="NextYes"/> (after the item was made) and <see cref="NextNo"/> (after it was not).
/// </summary>
public sealed class Treasure
{
    /// <summary>C <c>item</c>: which item this link can be (null when it makes a list transition).</summary>
    public Archetype? Item;

    /// <summary>C <c>name</c>: when not null, the name of the list to use instead.</summary>
    public string? Name;

    /// <summary>C <c>next</c>.</summary>
    public Treasure? Next;

    /// <summary>C <c>next_yes</c>: used instead of <see cref="Next"/> when this item was generated.</summary>
    public Treasure? NextYes;

    /// <summary>C <c>next_no</c>: used when this item was not generated.</summary>
    public Treasure? NextNo;

    /// <summary>C <c>change_arch.name</c>: copied over the name of the generated object.</summary>
    public string? ChangeName;

    /// <summary>C <c>change_arch.title</c>.</summary>
    public string? ChangeTitle;

    /// <summary>C <c>change_arch.slaying</c>.</summary>
    public string? ChangeSlaying;

    /// <summary>C <c>chance</c> (uint8): percent chance for this item; the default is 100.</summary>
    public byte Chance = 100;

    /// <summary>C <c>magic</c> (uint8): the largest magic bonus of the item, or for a list transition the difficulty it needs.</summary>
    public byte Magic;

    /// <summary>C <c>nrof</c> (uint16): 1 to this many items are made.</summary>
    public ushort Nrof;
}

public sealed partial class TreasureList
{
    /// <summary>C <c>items</c>: the first element.</summary>
    public Treasure? Items;

    /// <summary>C <c>total_chance</c> (sint16): not 0 for a <c>treasureone</c> list, which makes one of its items.</summary>
    public short TotalChance;

    /// <summary>C <c>next</c>: the next list in the order of the file.</summary>
    public TreasureList? Next;
}

/// <summary>include/treasure.h <c>GT_*</c>: the flags of <see cref="CfWorld.CreateTreasure"/>.</summary>
public static class GT
{
    /// <summary>GT_ENVIRONMENT: the items go on the map square of the creator.</summary>
    public const int ENVIRONMENT = 0x0001;

    /// <summary>GT_INVISIBLE: only invisible items (abilities) are made.</summary>
    public const int INVISIBLE = 0x0002;

    /// <summary>GT_STARTEQUIP: the items are starting equipment (flag STARTEQUIP, or the value zero).</summary>
    public const int STARTEQUIP = 0x0004;

    /// <summary>GT_APPLY: a monster applies what it was given.</summary>
    public const int APPLY = 0x0008;

    /// <summary>GT_ONLY_GOOD: no cursed items.</summary>
    public const int ONLY_GOOD = 0x0010;

    /// <summary>GT_UPDATE_INV: the client of the player is sent each new item.</summary>
    public const int UPDATE_INV = 0x0020;
}

public sealed partial class CfWorld
{
    // the lists the treasures text made (null while no text was read: the host answers for the names then)
    private Dictionary<string, TreasureList>? treasureLists;
    private TreasureList? firstTreasurelist;

    /// <summary>
    /// Not in C: the text of the treasures file (C reads <c>lib/treasures</c> in <c>load_treasures</c>). When it is set before
    /// <see cref="InitArchetypes"/> or <see cref="LoadArchetypes"/>, the lists are read from it between the two archetype passes and
    /// <see cref="FindTreasurelist"/> answers from them; when it is null the host answers (<see cref="CrossfireHost.FindTreasureList"/>).
    /// </summary>
    public string? TreasureText { get; set; }

    /// <summary>The lists read from <see cref="TreasureText"/>, in the order of the file (empty before they were read).</summary>
    public IEnumerable<TreasureList> TreasureLists
    {
        get
        {
            for (TreasureList? tl = firstTreasurelist; tl != null; tl = tl.Next)
            {
                yield return tl;
            }
        }
    }

    /// <summary>The number of treasure elements read (C <c>nroftreasures</c>).</summary>
    public int NrOfTreasures { get; private set; }

    // sscanf(cp, "<literal> %s", word) as the C code tests it: 1 when a word was read, 0 when the literal does not match, and EOF (-1,
    // which is true in an if) when the text ends after the literal; the space of the format skips white space.
    private static int ScanWord(string cp, string literal, ref string word)
    {
        if (cp.Length == 0)
        {
            return -1;
        }
        if (!cp.StartsWith(literal, StringComparison.Ordinal))
        {
            return 0;
        }
        int i = literal.Length;
        while (i < cp.Length && IsSpace(cp[i]))
        {
            i++;
        }
        if (i >= cp.Length)
        {
            return -1;
        }
        int e = i;
        while (e < cp.Length && !IsSpace(cp[e]))
        {
            e++;
        }
        word = cp.Substring(i, e - i);
        return 1;
    }

    // sscanf(cp, "<literal> %d", value) as ScanWord does (a sign and digits; a word that is no number is a mismatch)
    private static int ScanInt(string cp, string literal, ref int value)
    {
        if (cp.Length == 0)
        {
            return -1;
        }
        if (!cp.StartsWith(literal, StringComparison.Ordinal))
        {
            return 0;
        }
        int i = literal.Length;
        while (i < cp.Length && IsSpace(cp[i]))
        {
            i++;
        }
        if (i >= cp.Length)
        {
            return -1;
        }
        int start = i;
        if (cp[i] == '+' || cp[i] == '-')
        {
            i++;
        }
        int digits = i;
        while (i < cp.Length && cp[i] >= '0' && cp[i] <= '9')
        {
            i++;
        }
        if (i == digits)
        {
            return 0;
        }
        value = int.TryParse(cp.AsSpan(start, i - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v) ? v : (cp[start] == '-' ? int.MinValue : int.MaxValue);
        return 1;
    }

    /// <summary>treasure.c <c>load_treasure</c> (static): reads one element and, through <c>more</c>, <c>yes</c> and <c>no</c>, the ones after it.</summary>
    private Treasure LoadTreasure(LoadSource fp)
    {
        var t = new Treasure();
        string variable = "";
        int value = 0;
        NrOfTreasures++;
        string? line;
        while ((line = Fgets(fp, Define.MAX_BUF)) != null)
        {
            string buf = line;
            if (buf.Length > 0 && buf[0] == '#')
            {
                continue;
            }
            int nl = buf.IndexOf('\n');
            if (nl >= 0)
            {
                buf = buf.Substring(0, nl);
            }
            int c = 0;
            while (c < buf.Length && buf[c] == ' ')
            {
                c++;
            }
            string cp = buf.Substring(c);
            if (ScanWord(cp, "arch", ref variable) != 0)
            {
                t.Item = FindArchetype(variable);
                if (t.Item == null)
                {
                    Log(LogLevel.Error, "Treasure lacks archetype: " + variable + "\n");
                }
            }
            else if (ScanWord(cp, "list", ref variable) != 0)
            {
                t.Name = variable;
            }
            else if (ScanWord(cp, "change_name", ref variable) != 0)
            {
                t.ChangeName = variable;
            }
            else if (ScanWord(cp, "change_title", ref variable) != 0)
            {
                t.ChangeTitle = variable;
            }
            else if (ScanWord(cp, "change_slaying", ref variable) != 0)
            {
                t.ChangeSlaying = variable;
            }
            else if (ScanInt(cp, "chance", ref value) != 0)
            {
                t.Chance = unchecked((byte)value);
            }
            else if (ScanInt(cp, "nrof", ref value) != 0)
            {
                t.Nrof = unchecked((ushort)value);
            }
            else if (ScanInt(cp, "magic", ref value) != 0)
            {
                t.Magic = unchecked((byte)value);
            }
            else if (cp == "yes")
            {
                t.NextYes = LoadTreasure(fp);
            }
            else if (cp == "no")
            {
                t.NextNo = LoadTreasure(fp);
            }
            else if (cp == "end")
            {
                return t;
            }
            else if (cp == "more")
            {
                t.Next = LoadTreasure(fp);
                return t;
            }
            else
            {
                Log(LogLevel.Error, "Unknown treasure-command: '" + cp + "', last entry " + (t.Name ?? "null") + "\n");
            }
        }
        Log(LogLevel.Error, "treasure lacks 'end'.\n");
        return t;
    }

    /// <summary>treasure.c <c>check_treasurelist</c> (static, TREASURE_DEBUG): looks for faults of the data and for names of lists that do not exist.</summary>
    private void CheckTreasurelist(Treasure t, TreasureList tl)
    {
        if (t.Item == null && t.Name == null)
        {
            Log(LogLevel.Error, "Treasurelist " + tl.Name + " has element with no name or archetype\n");
        }
        if (t.Chance >= 100 && t.NextYes != null && (t.Next != null || t.NextNo != null))
        {
            Log(LogLevel.Error, "Treasurelist " + tl.Name + " has element that has 100% generation, next_yes field as well as next or next_no\n");
        }
        if (t.Name != null && t.Name != "NONE")
        {
            FindTreasurelist(t.Name); // prints its own error message
        }
        if (t.Next != null)
        {
            CheckTreasurelist(t.Next, tl);
        }
        if (t.NextYes != null)
        {
            CheckTreasurelist(t.NextYes, tl);
        }
        if (t.NextNo != null)
        {
            CheckTreasurelist(t.NextNo, tl);
        }
    }

    /// <summary>
    /// treasure.c <c>load_treasures</c>: reads all the treasure lists from the text of the treasures file (the text takes the place of
    /// the file). <see cref="LoadArchetypes"/> calls it between the two archetype passes, after <see cref="CrossfireHost.LoadTreasures"/>,
    /// when <see cref="TreasureText"/> is set. A list that is read twice under one name is kept twice, and the first is the one
    /// <see cref="FindTreasurelist"/> finds, as in C.
    /// </summary>
    public void LoadTreasureLists(string text)
    {
        treasureLists = new Dictionary<string, TreasureList>(StringComparer.Ordinal);
        firstTreasurelist = null;
        NrOfTreasures = 0;
        TreasureList? previous = null;
        var fp = new LoadSource(text);
        string? buf;
        string name = "";
        while ((buf = Fgets(fp, Define.MAX_BUF)) != null)
        {
            if (buf.Length > 0 && buf[0] == '#')
            {
                continue;
            }
            // sscanf(buf, "treasureone %s\n", name) || sscanf(buf, "treasure %s\n", name)
            if (ScanWord(buf, "treasureone", ref name) != 0 || ScanWord(buf, "treasure", ref name) != 0)
            {
                var tl = new TreasureList(name);
                if (previous == null)
                {
                    firstTreasurelist = tl;
                }
                else
                {
                    previous.Next = tl;
                }
                previous = tl;
                treasureLists.TryAdd(name, tl);
                tl.Items = LoadTreasure(fp);
                // a treasureone list makes one of its items: add up the chances and check that yes and no are not used
                if (buf.StartsWith("treasureone", StringComparison.Ordinal))
                {
                    for (Treasure? t = tl.Items; t != null; t = t.Next)
                    {
                        if (t.NextYes != null || t.NextNo != null)
                        {
                            Log(LogLevel.Error, "Treasure " + tl.Name + " is one item, but on treasure " + (t.Item != null ? t.Item.Name : t.Name) + "\n");
                            Log(LogLevel.Error, "  the next_yes or next_no field is set");
                        }
                        tl.TotalChance = unchecked((short)(tl.TotalChance + t.Chance));
                    }
                }
            }
            else
            {
                Log(LogLevel.Error, "Treasure-list didn't understand: " + buf + "\n");
            }
        }
    }

    // LoadArchetypes calls it after the host's LoadTreasures: reads TreasureText and checks the lists
    private void ReadTreasuresText()
    {
        if (TreasureText == null)
        {
            treasureLists = null;
            firstTreasurelist = null;
            return;
        }
        LoadTreasureLists(TreasureText);
        for (TreasureList? tl = firstTreasurelist; tl != null; tl = tl.Next)
        {
            if (tl.Items != null)
            {
                CheckTreasurelist(tl.Items, tl);
            }
        }
    }
}
