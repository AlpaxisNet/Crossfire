// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/living.c <c>fix_player</c>, the entry point and the part of
// it that the start path needs (the stats, the hit, spell and grace points, and what the player may carry). Wave 5D completes it.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/living.c:
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
/// common/living.c <c>fix_player</c>, the one entry point of the extension (<see cref="GkHost.FixPlayer"/> calls it when an item
/// comes into or goes out of a player). THE PART HERE: the stats come back from <c>orig_stats</c> and the applied items' stat bonuses
/// are added; <c>check_stat_bounds</c>; <c>maxhp</c>, <c>maxsp</c> and <c>maxgrace</c> from the levels (and the hit, spell and grace
/// points kept inside them); the weight limit. NOT HERE (wave 5D): the resistances, armour class, weapon class, damage, speed,
/// light, the flags the applied items give, the digestion and the regeneration, <c>add_statbonus</c>.
/// </summary>
internal static class FixPlayer
{
    /// <summary>fix_player (the part above) for a player. A player whose stats were never rolled (<see cref="Player.StatsRolled"/>) keeps its stats and life as they are: only the weight limit is set.</summary>
    public static void Fix(CfWorld world, CfObject op)
    {
        if (op.Type != Define.PLAYER || op.Contr is not Player pl)
        {
            return; // the part for monsters is not ported
        }
        if (pl.StatsRolled)
        {
            for (int i = 0; i < 7; i++)
            {
                LivingRules.SetAttr(op.Stats, i, CfWorld.GetAttrValue(pl.OrigStats, i));
            }
            CfObject? manaObj = null;
            CfObject? graceObj = null;
            for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
            {
                if (!tmp.QueryFlag(Define.FLAG_APPLIED) || tmp.Type == Define.CONTAINER || tmp.Type == Define.CLOSE_CON)
                {
                    continue;
                }
                // the meaning of stats in skill or experience objects is different: they link skills to experience; an exception is exp_wis
                if ((tmp.Type != Define.EXPERIENCE || tmp.Arch?.Name == "experience_wis") && tmp.Type != Define.POTION)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        LivingRules.SetAttr(op.Stats, i, CfWorld.GetAttrValue(op.Stats, i) + CfWorld.GetAttrValue(tmp.Stats, i));
                    }
                }
                if (tmp.Type == Define.EXPERIENCE)
                {
                    if (tmp.Stats.Pow != 0 && manaObj == null)
                    {
                        manaObj = tmp; // for spellpoint determ
                    }
                    if (tmp.Stats.Wis != 0 && graceObj == null)
                    {
                        graceObj = tmp;
                    }
                }
            }
            LivingRules.CheckStatBounds(op.Stats);
            Life(op, pl, manaObj, graceObj);
        }
        // c_object.c: the most the player can pick up, by Str
        pl.WeightLimit = LivingRules.WeightLimit[Math.Clamp((int)op.Stats.Str, 0, Define.MAX_STAT)];
    }

    // fix_player's awarding of hp, sp and grace
    private static void Life(CfObject op, Player pl, CfObject? manaObj, CfObject? graceObj)
    {
        int plLevel = op.Level;
        if (plLevel < 1)
        {
            plLevel = 1; // safety, we should always get 1 levels worth of hp!
        }
        int con = LivingRules.ConBonus[op.Stats.Con];
        int maxhp = 0;
        for (int i = 1; i <= plLevel && i <= 10; i++)
        {
            int j = pl.LevHp[i] + con / 2;
            if (i % 2 != 0 && con % 2 != 0)
            {
                if (con > 0)
                {
                    j++;
                }
                else
                {
                    j--;
                }
            }
            maxhp += j > 1 ? j : 1;
        }
        for (int i = 11; i <= op.Level; i++)
        {
            maxhp += 2;
        }
        op.Stats.Maxhp = (short)maxhp;
        if (op.Stats.Hp > op.Stats.Maxhp)
        {
            op.Stats.Hp = op.Stats.Maxhp;
        }

        // sp gain is controlled by the level of the player's relevant experience object (manaObj)
        if (manaObj == null || manaObj.Level == 0)
        {
            manaObj = op;
        }
        int pow = LivingRules.SpBonus[op.Stats.Pow];
        int maxsp = 0;
        for (int i = 1; i <= manaObj.Level && i <= 10; i++)
        {
            int j = pl.LevSp[i] + pow / 2;
            if (i % 2 != 0 && pow % 2 != 0)
            {
                if (pow > 0)
                {
                    j++;
                }
                else
                {
                    j--;
                }
            }
            maxsp += j > 1 ? j : 1;
        }
        for (int i = 11; i <= manaObj.Level; i++)
        {
            maxsp += 2;
        }
        op.Stats.Maxsp = (short)maxsp;
        // characters can get their sp supercharged via rune of transferrance
        if (op.Stats.Sp > op.Stats.Maxsp * 2)
        {
            op.Stats.Sp = (short)(op.Stats.Maxsp * 2);
        }

        // maxgrace depends on both Wis and Pow; it is kept in a float so that the divisions do not make big jumps from level to level
        if (graceObj == null || graceObj.Level == 0)
        {
            graceObj = op;
        }
        float gp = LivingRules.GraceBonus[op.Stats.Pow];
        float gw = LivingRules.GraceBonus[op.Stats.Wis];
        float spTmp = 0.0f;
        for (int i = 1; i <= graceObj.Level && i <= 10; i++)
        {
            float graceTmp = (float)(pl.LevGrace[i] + (gp + gw) / 8.0);
            // changed the starting grace for playability
            if (i < 2)
            {
                graceTmp = (float)(graceTmp + (1.0 + (gp + gw) / 4.0));
            }
            if (graceTmp < 1.0f)
            {
                graceTmp = 1.0f;
            }
            spTmp += graceTmp;
        }
        int maxgrace = (int)spTmp;
        // one grace point per level after 11
        for (int i = 11; i <= graceObj.Level; i++)
        {
            maxgrace += 2;
        }
        op.Stats.Maxgrace = (short)maxgrace;
    }
}
