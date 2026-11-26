// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): the parts of common/living.c and common/utils.c that the start path
// needs: the bonus tables, level_exp, player_lvl_adj, the "no exp" branch of add_exp, check_stat_bounds and die_roll.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/living.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     common/utils.c:
//         Copyright (C) 2001 Mark Wedel
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
/// The living.c of the start path: what a player's level and the three life values need. The rest of living.c (<c>change_abil</c>,
/// <c>add_statbonus</c>, the armour, weapon and speed part of <c>fix_player</c>, <c>add_exp</c> for gains and losses, the dragon
/// code) is wave 5D's and later phases'.
/// </summary>
internal static class LivingRules
{
    /// <summary>living.c <c>MAXLEVEL</c>.</summary>
    public const int MaxLevel = 110;

    /// <summary>living.c <c>con_bonus</c> (static).</summary>
    public static readonly int[] ConBonus =
    {
        -6, -5, -4, -3, -2, -1, -1, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18, 20,
        22, 25, 30, 40, 50,
    };

    /// <summary>living.c <c>sp_bonus</c> (static): the spell point bonus of Pow.</summary>
    public static readonly int[] SpBonus =
    {
        -10, -10, -9, -8, -7, -6, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 25,
        30, 40, 50, 70, 100,
    };

    /// <summary>living.c <c>grace_bonus</c> (static).</summary>
    public static readonly int[] GraceBonus =
    {
        -10, -10, -9, -8, -7, -6, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 25,
        30, 40, 50, 70, 100,
    };

    // living.c weight_limit[] is ItemMatching.WeightLimit (one table, lane 4C's: the flush and fix_player both read it)

    // living.c levels[MAXLEVEL+1] (static): the experience needed for each level (settings.simple_exp is off, as Crossfire's default has it)
    private static readonly uint[] Levels =
    {
        0,
        0, 1000, 2000, 4000, 8000,
        16000, 32000, 64000, 125000, 250000, // 10
        500000, 900000, 1400000, 2000000, 2600000,
        3300000, 4100000, 4900000, 5700000, 6600000, // 20
        7500000, 8400000, 9300000, 10300000, 11300000,
        12300000, 13300000, 14400000, 15500000, 16600000, // 30
        17700000, 18800000, 19900000, 21100000, 22300000,
        23500000, 24700000, 25900000, 27100000, 28300000, // 40
        29500000, 30800000, 32100000, 33400000, 34700000,
        36000000, 37300000, 38600000, 39900000, 41200000, // 50
        42600000, 44000000, 45400000, 46800000, 48200000,
        49600000, 51000000, 52400000, 53800000, 55200000, // 60
        56600000, 58000000, 59400000, 60800000, 62200000,
        63700000, 65200000, 66700000, 68200000, 69700000, // 70
        71200000, 72700000, 74200000, 75700000, 77200000,
        78700000, 80200000, 81700000, 83200000, 84700000, // 80
        86200000, 87700000, 89300000, 90900000, 92500000,
        94100000, 95700000, 97300000, 98900000, 100500000, // 90
        102100000, 103700000, 105300000, 106900000, 108500000,
        110100000, 111700000, 113300000, 114900000, 116500000, // 100
        118100000, 119700000, 121300000, 122900000, 124500000,
        126100000, 127700000, 129300000, 130900000, 785400000, // 110
    };

    /// <summary>living.c <c>level_exp</c>: the experience needed to become the given level (uint32 as C computes it).</summary>
    public static uint LevelExp(int level, double expmul)
    {
        const long bleep = 1650000;
        if (level <= 100)
        {
            return unchecked((uint)(long)(expmul * Levels[level]));
        }
        return unchecked((uint)(long)(expmul * (Levels[100] + bleep * (level - 100))));
    }

    /// <summary>living.c <c>get_attr_value</c> and <c>set_attr_value</c> for the seven stats, by their number STR to POW.</summary>
    public static void SetAttr(Living stats, int attr, int value)
    {
        switch (attr)
        {
            case Define.STR:
                stats.Str = (sbyte)value;
                break;
            case Define.DEX:
                stats.Dex = (sbyte)value;
                break;
            case Define.CON:
                stats.Con = (sbyte)value;
                break;
            case Define.WIS:
                stats.Wis = (sbyte)value;
                break;
            case Define.CHA:
                stats.Cha = (sbyte)value;
                break;
            case Define.INT:
                stats.Int = (sbyte)value;
                break;
            case Define.POW:
                stats.Pow = (sbyte)value;
                break;
        }
    }

    /// <summary>living.c <c>check_stat_bounds</c>: every stat is within 1 to 30.</summary>
    public static void CheckStatBounds(Living stats)
    {
        for (int i = 0; i < 7; i++)
        {
            int v = CfWorld.GetAttrValue(stats, i);
            if (v > Define.MAX_STAT)
            {
                SetAttr(stats, i, Define.MAX_STAT);
            }
            else if (v < Define.MIN_STAT)
            {
                SetAttr(stats, i, Define.MIN_STAT);
            }
        }
    }

    /// <summary>utils.c <c>die_roll</c>: the sum of <paramref name="num"/> rolls of a die of <paramref name="size"/> sides, which a lucky player's luck bends once.</summary>
    public static int DieRoll(CfWorld world, int num, int size, CfObject op, int goodbad)
    {
        int diff = size;
        int min = 1;
        int luck = 0;
        int total = 0;
        int gotlucky = 0;
        int baseValue = diff > 2 ? 20 : 50; // d2 and d3 are corner cases
        if (size < 2 || diff < 1)
        {
            world.Log(LogLevel.Error, "Calling die_roll with num=" + num + " size=" + size + "\n");
            return num; // avoids a float exception
        }
        if (op.Type == Define.PLAYER)
        {
            luck = op.Stats.Luck;
        }
        for (int i = 0; i < num; i++)
        {
            if (world.Random() % baseValue < Math.Min(10, Math.Abs(luck)) && gotlucky == 0)
            {
                // we have a winner
                gotlucky++;
                luck = luck > 0 ? 1 : -1;
                diff -= luck;
                if (diff < 1)
                {
                    return num; // check again
                }
                if (goodbad != 0)
                {
                    min += luck;
                }
                total += Math.Max(1, Math.Min(size, world.Random() % diff + min));
            }
            else
            {
                total += world.Random() % size + 1;
            }
        }
        return total;
    }

    /// <summary>
    /// living.c <c>player_lvl_adj</c>: the level of the experience object <paramref name="op"/> (the player's own when null) follows its
    /// experience; a player who gains a level up to 10 gets the life, spell and grace points of that level, and its stats are fixed.
    /// What C does besides is left out: the dragon's new abilities (<c>dragon_level_gain</c>) and the "You are now level" messages.
    /// </summary>
    public static void PlayerLvlAdj(CfWorld world, CfObject? who, CfObject? op)
    {
        op ??= who;
        if (op == null)
        {
            return;
        }
        if (op.Level < MaxLevel && (uint)op.Stats.Exp >= LevelExp(op.Level + 1, op.Expmul))
        {
            op.Level++;
            if (who != null && who.Level < 11 && op.Type != Define.EXPERIENCE && who.Contr is Player pl)
            {
                pl.EnsureStats();
                pl.LevHp[who.Level] = (short)(DieRoll(world, 2, 4, who, Define.PREFER_HIGH) + 1);
                pl.LevSp[who.Level] = (short)DieRoll(world, 2, 3, who, Define.PREFER_HIGH);
                pl.LevGrace[who.Level] = (short)(DieRoll(world, 2, 2, who, Define.PREFER_HIGH) - 1);
                pl.StoreStats();
            }
            if (who != null)
            {
                FixPlayer.Fix(world, who);
            }
            PlayerLvlAdj(world, who, op); // to increase more levels
        }
        else if (op.Level > 1 && (uint)op.Stats.Exp < LevelExp(op.Level, op.Expmul))
        {
            op.Level--;
            if (who != null)
            {
                FixPlayer.Fix(world, who);
            }
            PlayerLvlAdj(world, who, op); // to decrease more levels
        }
    }

    /// <summary>
    /// living.c <c>add_exp(op, 0)</c> for a player: no experience is added and the levels are looked at again (the last branch of the
    /// function: the readied skill's experience object, then the player). The gain and loss branches are not ported.
    /// </summary>
    public static void AddExpNone(CfWorld world, CfObject op)
    {
        if (op.Type != Define.PLAYER)
        {
            return;
        }
        if (op.ChosenSkill != null && op.ChosenSkill.ExpObj != null)
        {
            PlayerLvlAdj(world, op, op.ChosenSkill.ExpObj);
        }
        PlayerLvlAdj(world, op, null);
        // reset the player exp_obj to NULL
        if (op.ExpObj != null)
        {
            op.ExpObj = null;
        }
    }
}
