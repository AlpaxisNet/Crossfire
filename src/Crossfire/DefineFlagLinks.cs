// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/loader.l, include/define.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/loader.l:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/define.h:
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
namespace Crossfire;

public static partial class Define
{
    /// <summary>include/define.h MIN_ACTIVE_SPEED: the speed under which an object is not on the active list (a C double).</summary>
    public const double MIN_ACTIVE_SPEED = 0.00001;

    /// <summary>
    /// common/loader.l flag_links: every flag with the loader keyword that sets it (null: no keyword), one entry per flag number in
    /// the order of the C table.
    /// </summary>
    public static readonly (int Flag, string? Keyword)[] FlagLinks =
    {
        (Define.FLAG_ALIVE, "alive"),
        (Define.FLAG_WIZ, "wiz"),
        (-1, null),
        (-1, null),
        (Define.FLAG_WAS_WIZ, "was_wiz"),
        (Define.FLAG_APPLIED, "applied"),
        (Define.FLAG_UNPAID, "unpaid"),
        (-1, null),
        (Define.FLAG_NO_PICK, "no_pick"),
        (Define.FLAG_WALK_ON, "walk_on"),
        (Define.FLAG_NO_PASS, "no_pass"),
        (Define.FLAG_ANIMATE, "is_animated"),
        (Define.FLAG_SLOW_MOVE, null),
        (Define.FLAG_FLYING, "flying"),
        (Define.FLAG_MONSTER, "monster"),
        (Define.FLAG_FRIENDLY, "friendly"),
        (Define.FLAG_GENERATOR, "generator"),
        (Define.FLAG_IS_THROWN, "is_thrown"),
        (Define.FLAG_AUTO_APPLY, "auto_apply"),
        (Define.FLAG_TREASURE, "treasure"),
        (Define.FLAG_APPLY_ONCE, "apply_once"),
        (Define.FLAG_SEE_INVISIBLE, "see_invisible"),
        (Define.FLAG_CAN_ROLL, "can_roll"),
        (-1, null),
        (Define.FLAG_IS_TURNABLE, "is_turnable"),
        (Define.FLAG_WALK_OFF, "walk_off"),
        (Define.FLAG_FLY_ON, "fly_on"),
        (Define.FLAG_FLY_OFF, "fly_off"),
        (Define.FLAG_IS_USED_UP, "is_used_up"),
        (Define.FLAG_IDENTIFIED, "identified"),
        (Define.FLAG_REFLECTING, "reflecting"),
        (Define.FLAG_CHANGING, "changing"),
        (Define.FLAG_SPLITTING, "splitting"),
        (Define.FLAG_HITBACK, "hitback"),
        (Define.FLAG_STARTEQUIP, "startequip"),
        (Define.FLAG_BLOCKSVIEW, "blocksview"),
        (Define.FLAG_UNDEAD, "undead"),
        (Define.FLAG_SCARED, "scared"),
        (Define.FLAG_UNAGGRESSIVE, "unaggressive"),
        (Define.FLAG_REFL_MISSILE, "reflect_missile"),
        (Define.FLAG_REFL_SPELL, "reflect_spell"),
        (Define.FLAG_NO_MAGIC, "no_magic"),
        (Define.FLAG_NO_FIX_PLAYER, "no_fix_player"),
        (-1, null),
        (Define.FLAG_TEAR_DOWN, "tear_down"),
        (Define.FLAG_RUN_AWAY, "run_away"),
        (Define.FLAG_PASS_THRU, "pass_thru"),
        (Define.FLAG_CAN_PASS_THRU, "can_pass_thru"),
        (Define.FLAG_PICK_UP, "pick_up"),
        (Define.FLAG_UNIQUE, "unique"),
        (Define.FLAG_NO_DROP, "no_drop"),
        (Define.FLAG_NO_PRETEXT, "no_pretext"),
        (Define.FLAG_CAST_SPELL, "can_cast_spell"),
        (Define.FLAG_USE_SCROLL, "can_use_scroll"),
        (Define.FLAG_USE_WAND, "can_use_wand"),
        (Define.FLAG_USE_BOW, "can_use_bow"),
        (Define.FLAG_USE_ARMOUR, "can_use_armour"),
        (Define.FLAG_USE_WEAPON, "can_use_weapon"),
        (Define.FLAG_USE_RING, "can_use_ring"),
        (Define.FLAG_READY_WAND, "has_ready_wand"),
        (Define.FLAG_READY_BOW, "has_ready_bow"),
        (Define.FLAG_XRAYS, "xrays"),
        (-1, null),
        (Define.FLAG_IS_FLOOR, "is_floor"),
        (Define.FLAG_LIFESAVE, "lifesave"),
        (Define.FLAG_NO_STRENGTH, "no_strength"),
        (Define.FLAG_SLEEP, "sleep"),
        (Define.FLAG_STAND_STILL, "stand_still"),
        (Define.FLAG_RANDOM_MOVE, "random_movement"),
        (Define.FLAG_ONLY_ATTACK, "only_attack"),
        (Define.FLAG_CONFUSED, "confused"),
        (Define.FLAG_STEALTH, "stealth"),
        (-1, null),
        (-1, null),
        (Define.FLAG_CURSED, "cursed"),
        (Define.FLAG_DAMNED, "damned"),
        (Define.FLAG_SEE_ANYWHERE, "see_anywhere"),
        (Define.FLAG_KNOWN_MAGICAL, "known_magical"),
        (Define.FLAG_KNOWN_CURSED, "known_cursed"),
        (Define.FLAG_CAN_USE_SKILL, "can_use_skill"),
        (Define.FLAG_BEEN_APPLIED, "been_applied"),
        (Define.FLAG_READY_ROD, "has_ready_rod"),
        (Define.FLAG_USE_ROD, "can_use_rod"),
        (Define.FLAG_READY_HORN, "has_ready_horn"),
        (Define.FLAG_USE_HORN, "can_use_horn"),
        (Define.FLAG_MAKE_INVIS, "make_invisible"),
        (Define.FLAG_INV_LOCKED, "inv_locked"),
        (Define.FLAG_IS_WOODED, "is_wooded"),
        (Define.FLAG_IS_HILLY, "is_hilly"),
        (Define.FLAG_READY_SKILL, "has_ready_skill"),
        (Define.FLAG_READY_WEAPON, "has_ready_weapon"),
        (Define.FLAG_NO_SKILL_IDENT, "no_skill_ident"),
        (Define.FLAG_BLIND, "is_blind"),
        (Define.FLAG_SEE_IN_DARK, "can_see_in_dark"),
        (Define.FLAG_IS_CAULDRON, "is_cauldron"),
        (Define.FLAG_DUST, "is_dust"),
        (Define.FLAG_NO_STEAL, "no_steal"),
        (Define.FLAG_ONE_HIT, "one_hit"),
        (-1, null),
        (Define.FLAG_BERSERK, "berserk"),
        (Define.FLAG_NEUTRAL, "neutral"),
        (Define.FLAG_NO_ATTACK, "no_attack"),
        (Define.FLAG_NO_DAMAGE, "no_damage"),
        (-1, null),
        (-1, null),
    };
}
