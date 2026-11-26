// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/player.c <c>roll_stat</c>, <c>roll_stats</c> and
// <c>give_initial_items</c>, the start of a new character.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/player.c:
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
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

/// <summary>The start of a new character: the rolling of the stats and the starting items (server/player.c).</summary>
internal static class Start
{
    // include/spells.h spellnrs: the spells the first spellbook of a character may hold (give_initial_items' start_spells and
    // start_prayers): SP_BULLET, SP_S_FIREBALL, SP_BURNING_HANDS, SP_S_LIGHTNING, SP_M_MISSILE, SP_ICESTORM, SP_S_SNOWSTORM and
    // SP_TURN_UNDEAD, SP_HOLY_WORD, SP_MINOR_HEAL, SP_CAUSE_LIGHT
    private static readonly int[] StartSpells = { 0, 1, 4, 5, 7, 17, 168 };
    private static readonly int[] StartPrayers = { 19, 31, 32, 129 };

    /// <summary>player.c <c>roll_stat</c>: four rolls of 1 to 6, the sum of the best three.</summary>
    public static int RollStat(CfWorld world)
    {
        var a = new int[4];
        for (int i = 0; i < 4; i++)
        {
            a[i] = world.Random() % 6 + 1;
        }
        int j = 0;
        int k = 7;
        for (int i = 0; i < 4; i++)
        {
            if (a[i] < k)
            {
                k = a[i];
                j = i;
            }
        }
        k = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i != j)
            {
                k += a[i];
            }
        }
        return k;
    }

    /// <summary>
    /// player.c <c>roll_stats</c> of a player with a controller: rolls the seven stats until their sum is 82 to 116, puts them in order
    /// from the highest to the lowest (config.h has <c>USE_SWAP_STATS</c> and <c>SORT_ROLLED_STATS</c>), keeps them as
    /// <c>orig_stats</c>, sets level 0 and no experience, lets the level rise to 1 (<c>add_exp(op, 0)</c>, which rolls the life,
    /// spell and grace points of level 1 and fixes the player) and sets the life values to their largest.
    /// </summary>
    public static void RollStats(CfWorld world, CfObject op, Player pl)
    {
        int sum;
        var sorted = new int[7];
        do
        {
            sorted[0] = RollStat(world); // Str
            sorted[1] = RollStat(world); // Dex
            sorted[2] = RollStat(world); // Int
            sorted[3] = RollStat(world); // Con
            sorted[4] = RollStat(world); // Wis
            sorted[5] = RollStat(world); // Pow
            sorted[6] = RollStat(world); // Cha
            sum = sorted.Sum();
        }
        while (sum < 82 || sum > 116);

        // sort the stats so that rerolling is easier: a quick and dirty bubble sort, from the highest to the lowest
        int n = 0;
        do
        {
            if (sorted[n] < sorted[n + 1])
            {
                (sorted[n], sorted[n + 1]) = (sorted[n + 1], sorted[n]);
                n = 0;
            }
            else
            {
                n++;
            }
        }
        while (n < 6);
        op.Stats.Str = (sbyte)sorted[0];
        op.Stats.Dex = (sbyte)sorted[1];
        op.Stats.Con = (sbyte)sorted[2];
        op.Stats.Int = (sbyte)sorted[3];
        op.Stats.Wis = (sbyte)sorted[4];
        op.Stats.Pow = (sbyte)sorted[5];
        op.Stats.Cha = (sbyte)sorted[6];

        pl.EnsureStats();
        pl.OrigStats.Str = op.Stats.Str;
        pl.OrigStats.Dex = op.Stats.Dex;
        pl.OrigStats.Int = op.Stats.Int;
        pl.OrigStats.Con = op.Stats.Con;
        pl.OrigStats.Wis = op.Stats.Wis;
        pl.OrigStats.Pow = op.Stats.Pow;
        pl.OrigStats.Cha = op.Stats.Cha;
        pl.StoreStats(); // from here on the stats are rolled: fix_player takes them back from orig_stats
        op.Stats.Hp = -10000;
        op.Level = 0;
        op.Stats.Exp = 0;
        op.Stats.Sp = 0;
        op.Stats.Grace = 0;
        op.Stats.Ac = 0;
        LivingRules.AddExpNone(world, op);
        op.Stats.Sp = op.Stats.Maxsp;
        op.Stats.Hp = op.Stats.Maxhp;
        op.Stats.Grace = 0;
        pl.OrigStats.CopyFrom(op.Stats);
        pl.StoreStats();
    }

    /// <summary>
    /// player.c <c>give_initial_items</c>: makes the starting items of <paramref name="items"/> (a class's <c>randomitems</c>) into
    /// the player (<c>GT_STARTEQUIP | GT_ONLY_GOOD</c>) and fixes them: forces are applied unless neutral; armour and weapons the
    /// race cannot use go again; a spellbook gets one of the first spells (each only once among the books); what needs to be identified is
    /// identified and neither cursed nor damned; an ability gives the player a known spell and goes. C's test <c>pl-&gt;randomitems != NULL</c>
    /// is a test of <paramref name="items"/> here (the class's list, not the player archetype's own).
    /// </summary>
    public static void GiveInitialItems(CfWorld world, CfObject pl, TreasureList? items)
    {
        var startSpells = new List<int>(StartSpells);
        var startPrayers = new List<int>(StartPrayers);
        CfObject? next;

        if (items != null)
        {
            world.CreateTreasure(items, pl, GT.STARTEQUIP | GT.ONLY_GOOD, 1, 0);
        }

        for (CfObject? op = pl.Inv; op != null; op = next)
        {
            next = op.Below;

            // forces get applied per default, unless they have the flag "neutral" set
            if (op.Type == Define.FORCE && !op.QueryFlag(Define.FLAG_NEUTRAL))
            {
                op.SetFlag(Define.FLAG_APPLIED);
            }

            // we never give weapons/armour if these cannot be used by this player due to race restrictions
            if (pl.Type == Define.PLAYER)
            {
                if ((!pl.QueryFlag(Define.FLAG_USE_ARMOUR)
                        && (op.Type == Define.ARMOUR || op.Type == Define.BOOTS || op.Type == Define.CLOAK || op.Type == Define.HELMET
                            || op.Type == Define.SHIELD || op.Type == Define.GLOVES || op.Type == Define.BRACERS || op.Type == Define.GIRDLE))
                    || (!pl.QueryFlag(Define.FLAG_USE_WEAPON) && op.Type == Define.WEAPON))
                {
                    world.RemoveOb(op);
                    world.FreeObject(op);
                    continue;
                }
            }

            if (op.Type == Define.SPELLBOOK)
            {
                // fix spells for first level spells
                List<int> pool = op.Arch?.Name == "cleric_book" ? startPrayers : startSpells;
                if (pool.Count <= 0)
                {
                    world.RemoveOb(op);
                    world.FreeObject(op);
                    continue;
                }
                int idx = world.Random() % pool.Count;
                op.Stats.Sp = (short)pool[idx];
                // this makes sure the character does not get duplicate spells
                pool[idx] = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
            }

            // give starting characters identified, uncursed, and undamned items; just don't identify gold or silver, or it won't be merged properly
            if (CfWorld.NeedIdentify(op) != 0)
            {
                op.SetFlag(Define.FLAG_IDENTIFIED);
                op.ClearFlag(Define.FLAG_CURSED);
                op.ClearFlag(Define.FLAG_DAMNED);
            }

            if (op.Type == Define.ABILITY)
            {
                if (pl.Contr is Player contr)
                {
                    contr.KnownSpells.Add(op.Stats.Sp);
                }
                world.RemoveOb(op);
                world.FreeObject(op);
            }
        }
    }
}
