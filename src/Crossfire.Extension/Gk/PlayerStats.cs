// Not ported from a Crossfire file: the part of Crossfire's player controller (include/player.h, <c>struct pl</c>) that the start path
// needs: <c>orig_stats</c>, <c>levhp</c>, <c>levsp</c>, <c>levgrace</c> and <c>known_spells</c>, and how they are kept in the account file.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;

namespace Crossfire.Extension.Gk;

internal sealed partial class Player
{
    /// <summary>The key of the player's lines in the account file that holds <see cref="OrigStats"/>: Str Dex Con Wis Cha Int Pow.</summary>
    public const string OrigStatsKey = "mudorigstats";

    /// <summary>The key that holds <see cref="LevHp"/> (the values of levels 1 to 10).</summary>
    public const string LevHpKey = "mudlevhp";

    /// <summary>The key that holds <see cref="LevSp"/>.</summary>
    public const string LevSpKey = "mudlevsp";

    /// <summary>The key that holds <see cref="LevGrace"/>.</summary>
    public const string LevGraceKey = "mudlevgrace";

    private bool statsRead;

    /// <summary>
    /// Not in C: the objects the last <c>setmudclass</c> put into the tree (the class and what its list gave that is not a stack that
    /// merged into another): choosing another class takes them out again, with the items that have the flag <c>startequip</c>.
    /// Only in memory: a character is chosen in one session.
    /// </summary>
    public List<CfObject> ClassGiven { get; } = new();

    /// <summary>C <c>orig_stats</c>: the stats as they were rolled (<c>fix_player</c> starts from them).</summary>
    public Living OrigStats { get; } = new();

    /// <summary>C <c>levhp</c> (sint16 [11]): the hit points the player got at each level, from 1 to 10 (index 0 is not used).</summary>
    public short[] LevHp { get; } = new short[11];

    /// <summary>C <c>levsp</c>: the spell points of each level.</summary>
    public short[] LevSp { get; } = new short[11];

    /// <summary>C <c>levgrace</c>: the grace of each level.</summary>
    public short[] LevGrace { get; } = new short[11];

    /// <summary>C <c>known_spells</c> and <c>nrofknownspells</c>: the spells the player knows (a starting ability gives one).</summary>
    public List<short> KnownSpells { get; } = new();

    /// <summary>
    /// True when the player's stats were rolled (<c>roll_stats</c>), here or in the file the account was read from. Not in C: C always
    /// has the values, an account of this extension that never rolled has none, and <c>fix_player</c> then leaves its stats and life
    /// as the account holds them.
    /// </summary>
    public bool StatsRolled
    {
        get
        {
            EnsureStats();
            return rolled;
        }
    }

    private bool rolled;

    /// <summary>Reads <see cref="OrigStats"/>, <see cref="LevHp"/>, <see cref="LevSp"/> and <see cref="LevGrace"/> from the lines of the player's object, once.</summary>
    public void EnsureStats()
    {
        if (statsRead)
        {
            return;
        }
        statsRead = true;
        ExtraKeys? extra = State.Ob.Extra;
        if (extra?.Get(OrigStatsKey) is { } orig && Numbers(orig, 7) is { } o)
        {
            OrigStats.Str = (sbyte)o[0];
            OrigStats.Dex = (sbyte)o[1];
            OrigStats.Con = (sbyte)o[2];
            OrigStats.Wis = (sbyte)o[3];
            OrigStats.Cha = (sbyte)o[4];
            OrigStats.Int = (sbyte)o[5];
            OrigStats.Pow = (sbyte)o[6];
            rolled = true;
        }
        Read(extra?.Get(LevHpKey), LevHp);
        Read(extra?.Get(LevSpKey), LevSp);
        Read(extra?.Get(LevGraceKey), LevGrace);
    }

    private static void Read(string? text, short[] into)
    {
        if (text != null && Numbers(text, 10) is { } n)
        {
            for (int i = 1; i <= 10; i++)
            {
                into[i] = (short)n[i - 1];
            }
        }
    }

    private static int[]? Numbers(string text, int count)
    {
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != count)
        {
            return null;
        }
        var n = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n[i]))
            {
                return null;
            }
        }
        return n;
    }

    /// <summary>Writes the four values into the lines of the player's object, which is what the account file keeps; the account is then saved with them.</summary>
    public void StoreStats()
    {
        ExtraKeys extra = State.Ob.Extra ??= new ExtraKeys();
        extra.Set(OrigStatsKey, string.Join(' ', new[] { OrigStats.Str, OrigStats.Dex, OrigStats.Con, OrigStats.Wis, OrigStats.Cha, OrigStats.Int, OrigStats.Pow }.Select(v => v.ToString(CultureInfo.InvariantCulture))));
        extra.Set(LevHpKey, string.Join(' ', LevHp.Skip(1).Select(v => v.ToString(CultureInfo.InvariantCulture))));
        extra.Set(LevSpKey, string.Join(' ', LevSp.Skip(1).Select(v => v.ToString(CultureInfo.InvariantCulture))));
        extra.Set(LevGraceKey, string.Join(' ', LevGrace.Skip(1).Select(v => v.ToString(CultureInfo.InvariantCulture))));
        rolled = true;
        statsRead = true;
        State.Dirty = true;
    }

    /// <summary>Forgets what was read (the player's object was replaced: the next use reads the new object's lines).</summary>
    public void ForgetStats()
    {
        statsRead = false;
        rolled = false;
        OrigStats.Clear();
        Array.Clear(LevHp);
        Array.Clear(LevSp);
        Array.Clear(LevGrace);
        KnownSpells.Clear();
        ClassGiven.Clear();
    }
}
