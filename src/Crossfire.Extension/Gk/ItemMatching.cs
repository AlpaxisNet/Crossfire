// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): the player's name matching and marks, group A of docs/mud-phase3.md.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/arch.c (item_matched_string, find_best_weapon_used_match):
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     server/c_object.c (find_best_object_match, find_marked_object, command_mark):
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     socket/item.c (MarkItem):
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     server/player.c (fix_weight):
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     common/living.c (weight_limit):
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
namespace Crossfire.Extension.Gk;

/// <summary>
/// The C functions of the player's controller that phase 3 needs (docs/porting.md). They take the world and the player's object;
/// the controller (<see cref="Player"/>) holds <c>count</c>, <c>mark</c> and <c>mark_count</c>. Each function names its C original.
/// </summary>
internal static class ItemMatching
{
    /// <summary>common/living.c <c>weight_limit[MAX_STAT + 1]</c>: the most a character can carry, in grams, by strength.</summary>
    public static readonly int[] WeightLimit =
    {
        200000, // 0
        250000, 300000, 350000, 400000, 500000, // 5
        600000, 700000, 800000, 900000, 1000000, // 10
        1100000, 1200000, 1300000, 1400000, 1500000, // 15
        1650000, 1800000, 1950000, 2100000, 2250000, // 20
        2400000, 2550000, 2700000, 2850000, 3000000, // 25
        3250000, 3500000, 3750000, 4000000, 4500000, // 30
    };

    /// <summary>
    /// <c>weight_limit[str]</c> as the callers in c_object.c and socket/request.c use it: a strength above MAX_STAT takes the last entry
    /// (c_object.c <c>pick_up_object</c>); a negative strength, which C would index out of the table, takes the first.
    /// </summary>
    public static int WeightLimitOf(int str) => WeightLimit[Math.Clamp(str, 0, Define.MAX_STAT)];

    // C: strcasecmp in the C locale: ASCII letters only (the names are the bytes of the file, as text)
    private static int ToLower(char c) => c >= 'A' && c <= 'Z' ? c + ('a' - 'A') : c;

    /// <summary>The C <c>strcasecmp</c> of two names (a null name is empty): 0 when they are equal.</summary>
    public static int StrCaseCmp(string? a, string? b)
    {
        a ??= "";
        b ??= "";
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            int d = ToLower(a[i]) - ToLower(b[i]);
            if (d != 0)
            {
                return d;
            }
        }
        return a.Length - b.Length;
    }

    private static int StrNCaseCmp(string a, string b, int n)
    {
        n = Math.Min(n, Math.Min(a.Length, b.Length) + 1);
        for (int i = 0; i < n; i++)
        {
            int ca = i < a.Length ? ToLower(a[i]) : 0;
            int cb = i < b.Length ? ToLower(b[i]) : 0;
            if (ca != cb)
            {
                return ca - cb;
            }
            if (ca == 0)
            {
                return 0;
            }
        }
        return 0;
    }

    /// <summary>
    /// arch.c <c>item_matched_string</c>: a comma separated list of names, with the words <c>all</c>, <c>unpaid</c>, <c>cursed</c> and
    /// a count in front of a name; the best value of the parts that match <paramref name="op"/>, or 0. It writes the player's
    /// <c>count</c> (what C writes in <c>pl-&gt;contr-&gt;count</c>), also for the match 6 which C gives when the count is more
    /// than 1 and the plural name differs (as the C code has it).
    /// </summary>
    public static int ItemMatchedString(CfWorld world, CfObject pl, CfObject op, string name)
    {
        var contr = pl.Contr as Player;
        int retval = 0;

        // strtok(local_name, ","): the pieces between commas, the empty ones skipped
        foreach (string piece in name.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string cp = piece.TrimStart(' ');
            int count;

            // All is a very generic match - low match value
            if (cp == "all")
            {
                return 1;
            }

            // unpaid is a little more specific
            if (cp == "unpaid" && op.QueryFlag(Define.FLAG_UNPAID))
            {
                return 2;
            }
            if (cp == "cursed" && op.QueryFlag(Define.FLAG_KNOWN_CURSED) && (op.QueryFlag(Define.FLAG_CURSED) || op.QueryFlag(Define.FLAG_DAMNED)))
            {
                return 2;
            }

            // Allow for things like '100 arrows'
            string? rest = cp;
            if ((count = CStr.Atoi(cp)) != 0)
            {
                int space = cp.IndexOf(' ');
                rest = space < 0 ? null : cp.Substring(space).TrimStart(' ');
            }
            else
            {
                count = pl.Type == Define.PLAYER && contr != null ? (int)contr.Count : 0;
            }

            if (rest == null || rest.Length == 0 || count < 0)
            {
                return 0;
            }
            cp = rest;

            // base name matched - not bad
            if (StrCaseCmp(cp, op.Name) == 0 && count == 0)
            {
                return 4;
            }
            else if (count > 1)
            {
                // Need to plurify name for proper match (C tests the result of strcasecmp itself: a plural name that differs matches)
                if (StrCaseCmp(cp, op.NamePl) != 0)
                {
                    if (contr != null)
                    {
                        contr.Count = (uint)count; // May not do anything
                    }
                    return 6;
                }
            }
            else if (count == 1)
            {
                if (StrCaseCmp(op.Name, cp) == 0)
                {
                    if (contr != null)
                    {
                        contr.Count = (uint)count; // May not do anything
                    }
                    return 6;
                }
            }
            string b0 = world.QueryBaseName(op, 0);
            string b1 = world.QueryBaseName(op, 1);
            if (StrCaseCmp(cp, world.QueryName(op)) == 0)
            {
                retval = 20;
            }
            else if (StrCaseCmp(cp, world.QueryShortName(op)) == 0)
            {
                retval = 18;
            }
            else if (StrCaseCmp(cp, b0) == 0)
            {
                retval = 16;
            }
            else if (StrCaseCmp(cp, b1) == 0)
            {
                retval = 16;
            }
            else if (StrNCaseCmp(cp, b0, Math.Min(cp.Length, b0.Length)) == 0)
            {
                retval = 14;
            }
            else if (StrNCaseCmp(cp, b1, Math.Min(cp.Length, b1.Length)) == 0)
            {
                retval = 14;
            }
            if (retval != 0)
            {
                if (pl.Type == Define.PLAYER && contr != null)
                {
                    contr.Count = (uint)count;
                }
                return retval;
            }
        }
        return 0;
    }

    /// <summary>arch.c <c>find_best_weapon_used_match</c>: the best match of the player's applied weapons (the loader's <c>current_weapon</c>).</summary>
    public static CfObject? FindBestWeaponUsedMatch(CfWorld world, CfObject pl, string parameters)
    {
        CfObject? best = null;
        int matchVal = 0;

        for (CfObject? tmp = pl.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Invisible != 0)
            {
                continue;
            }
            int tmpmatch;
            if ((tmpmatch = ItemMatchedString(world, pl, tmp, parameters)) > matchVal)
            {
                if (tmp.QueryFlag(Define.FLAG_APPLIED) && tmp.Type == Define.WEAPON)
                {
                    matchVal = tmpmatch;
                    best = tmp;
                }
            }
        }
        return best;
    }

    /// <summary>c_object.c <c>find_best_object_match</c>: the visible object of the player's inventory that matches best (the first of equal values).</summary>
    public static CfObject? FindBestObjectMatch(CfWorld world, CfObject pl, string parameters)
    {
        CfObject? best = null;
        int matchVal = 0;

        for (CfObject? tmp = pl.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Invisible != 0)
            {
                continue;
            }
            int tmpmatch;
            if ((tmpmatch = ItemMatchedString(world, pl, tmp, parameters)) > matchVal)
            {
                matchVal = tmpmatch;
                best = tmp;
            }
        }
        return best;
    }

    /// <summary>
    /// c_object.c <c>find_marked_object</c>: the object the player marked, when it is still in the inventory (and has the tag it had:
    /// <c>mark_count</c>); a mark that no longer holds is cleared.
    /// </summary>
    public static CfObject? FindMarkedObject(CfObject? op)
    {
        if (op == null || op.Contr is not Player contr)
        {
            return null;
        }
        if (contr.Mark == null)
        {
            return null;
        }
        // This may seem like overkill, but we need to make sure that they player hasn't dropped the item. We use count on the off
        // chance that an item got reincarnated at some point.
        for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Invisible != 0)
            {
                continue;
            }
            if (tmp == contr.Mark)
            {
                if (tmp.Count == contr.MarkCount)
                {
                    return tmp;
                }
                contr.Mark = null;
                contr.MarkCount = 0;
                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// c_object.c <c>command_mark</c> (op is a player, <paramref name="parameters"/> null for none): without parameters it says what
    /// is marked; with them it marks the best match. 1 when there is nothing to mark, else 0 (as C). The messages go through
    /// <see cref="CrossfireHost.DrawInfo"/>.
    /// </summary>
    public static int CommandMark(CfWorld world, CfObject op, string? parameters)
    {
        if (op.Contr is not Player contr)
        {
            return 1;
        }
        if (parameters == null)
        {
            CfObject? mark = FindMarkedObject(op);
            if (mark == null)
            {
                world.Host.DrawInfo(op, "You have no marked object.");
            }
            else
            {
                world.Host.DrawInfo(op, FileText.FromBytesText(world.QueryName(mark)) + " is marked.");
            }
        }
        else
        {
            CfObject? mark1 = FindBestObjectMatch(world, op, parameters);
            if (mark1 == null)
            {
                world.Host.DrawInfo(op, "Could not find an object that matches " + parameters);
                return 1;
            }
            contr.Mark = mark1;
            contr.MarkCount = mark1.Count;
            world.Host.DrawInfo(op, "Marked item " + FileText.FromBytesText(world.QueryName(mark1)));
            return 0;
        }
        return 0;
    }

    /// <summary>
    /// socket/item.c <c>MarkItem</c> with the object already found (the client's tag is looked up by the caller in the player's own
    /// tree): marks it and says so.
    /// </summary>
    public static void MarkItem(CfWorld world, CfObject pl, CfObject? op)
    {
        if (pl.Contr is not Player contr)
        {
            return;
        }
        if (op == null)
        {
            world.Host.DrawInfo(pl, "Could not find object to mark");
            return;
        }
        contr.Mark = op;
        contr.MarkCount = op.Count;
        world.Host.DrawInfo(pl, "Marked item " + FileText.FromBytesText(world.QueryName(op)));
    }

    /// <summary>
    /// player.c <c>fix_weight</c>: for each player whose carried weight differs from the sum of his inventory, the weight is set
    /// right, the stats are fixed (<see cref="CrossfireHost.FixPlayer"/>) and the client told (<c>esrv_update_item(UPD_WEIGHT)</c>).
    /// Returns the players that were fixed (C logs them at the debug level).
    /// </summary>
    public static List<CfObject> FixWeight(CfWorld world, IEnumerable<Player> players)
    {
        var fixedPlayers = new List<CfObject>();
        foreach (Player pl in players)
        {
            int old = pl.State.Ob.Carrying;
            int sum = CfWorld.SumWeight(pl.State.Ob);
            if (old == sum)
            {
                continue;
            }
            world.Host.FixPlayer(pl.State.Ob);
            world.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, pl.State.Ob, pl.State.Ob);
            fixedPlayers.Add(pl.State.Ob);
        }
        return fixedPlayers;
    }
}
