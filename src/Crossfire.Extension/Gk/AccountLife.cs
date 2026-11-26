// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/login.c <c>check_login</c>, the part after the player's file is
// read (the sum of the weight, <c>init_player_exp</c>, <c>link_player_skills</c>, the check of the range and <c>fix_player</c>), and
// server/c_range.c <c>legal_range</c>, which that part calls.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/login.c:
//         Copyright (C) 2001 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//
//     server/c_range.c:
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
/// What C does to a player whose file was just read (login.c <c>check_login</c>, docs/mud-phase3.md section 4.7), for a player of the
/// extension: the weight carried is added up, the experience objects and the skills are linked, the readied range is checked and
/// <c>fix_player</c> runs, so that the stats, the life and the weight the strings show are those of the items the file holds. Not
/// ported: the password, the file's header lines (the extension keeps its own: docs/differences.md), the map and the savebed, the
/// dragon's name, "Welcome Back!", the death of a player saved with no life, and <c>esrv_new_player</c> / <c>esrv_send_inventory</c>
/// (the first flush of the player's window sends the strings).
/// </summary>
internal static class AccountLife
{
    // include/player.h rangetype
    public const int RangeNone = 0;
    public const int RangeBow = 1;
    public const int RangeMagic = 2;
    public const int RangeWand = 3;
    public const int RangeRod = 4;
    public const int RangeScroll = 5;
    public const int RangeHorn = 6;
    public const int RangeSkill = 7;

    /// <summary>
    /// login.c <c>check_login</c> after <c>load_object</c>: <c>sum_weight</c>, <c>init_player_exp</c>, <c>link_player_skills</c>, the
    /// shoot type made legal and <c>fix_player</c>. <paramref name="skills"/> is null, or has no experience category, in a world without
    /// them (nothing is linked then: C's <c>init_player_exp</c> would set the player's experience to the sum of no objects, which is 0;
    /// Crossfire's own world always has the categories).
    /// </summary>
    public static void CheckLogin(CfWorld world, SkillTable? skills, CfObject op)
    {
        if (op.Type != Define.PLAYER || op.Contr is not Player pl)
        {
            return;
        }
        op.Carrying = CfWorld.SumWeight(op);
        if (skills is { NrOfExpCat: > 0 })
        {
            skills.InitPlayerExp(world, op);
            skills.LinkPlayerSkills(world, op);
        }
        if (!LegalRange(op, pl.ShootType))
        {
            pl.ShootType = RangeNone;
        }
        FixPlayer.Fix(world, op);
    }

    /// <summary>
    /// c_range.c <c>legal_range</c>: true when the player has what the range <paramref name="r"/> needs (an applied bow, wand, rod or
    /// horn, a known spell, a readied skill). Not kept: <c>known_spell</c> and <c>chosen_item_spell</c> of the controller, which only
    /// the casting of wands and rods reads (phase 4), and <c>chosen_spell</c> (the extension keeps no chosen spell).
    /// </summary>
    public static bool LegalRange(CfObject op, int r)
    {
        switch (r)
        {
            case RangeNone:
                return true;
            case RangeBow:
                return HasApplied(op, Define.BOW);
            case RangeMagic:
                return op.Contr is Player pl && pl.KnownSpells.Count != 0;
            case RangeWand:
                return HasApplied(op, Define.WAND);
            case RangeRod:
                return HasApplied(op, Define.ROD);
            case RangeHorn:
                return HasApplied(op, Define.HORN);
            case RangeScroll:
                return false;
            case RangeSkill:
                return op.ChosenSkill != null;
            default:
                return false;
        }
    }

    private static bool HasApplied(CfObject op, int type)
    {
        for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Type == type && tmp.QueryFlag(Define.FLAG_APPLIED))
            {
                return true;
            }
        }
        return false;
    }
}
