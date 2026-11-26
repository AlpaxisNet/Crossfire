// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): what the player is told when he applies a sign or a book, server/apply.c
// <c>apply_sign</c> and <c>apply_book</c>, with server/skill_util.c <c>SK_level</c> (docs/mud-phase3.md section 4.4, lane 6D).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/apply.c:
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//
//     server/skill_util.c:
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
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
/// Signs and books (server/apply.c). Every line C writes with <c>new_draw_info</c> goes to <see cref="CfWorld.Host"/>'s
/// <c>DrawInfo</c>, one call per C call (the colour of C's <c>NDI_NAVY</c> is not carried). What differs from C is in
/// docs/differences.md ("Signs, books, lighters and chests").
/// </summary>
internal static class Readable
{
    /// <summary>The text of a sign that has no message.</summary>
    public const string NothingWritten = "Nothing is written on it.";

    /// <summary>The text of a sign that was read as often as it allows.</summary>
    public const string CannotReadAnymore = "You cannot read it anymore.";

    /// <summary>The text of a blind player's try to read a sign or a book.</summary>
    public const string BlindToRead = "You are unable to read while blind.";

    /// <summary>The text of a player without the skill literacy.</summary>
    public const string StrangeSymbols = "You are unable to decipher the strange symbols.";

    /// <summary>
    /// apply.c <c>apply_sign</c>: <paramref name="op"/> reads <paramref name="sign"/>. A sign with a counter (<c>stats.food</c>)
    /// can be read that many times (<c>last_eat</c> counts them). Returns true when the sign's state changed (<c>last_eat</c> grew),
    /// so that the file that holds it is written.
    /// </summary>
    public static bool ApplySign(CfWorld w, CfObject op, CfObject sign)
    {
        if (sign.Msg == null)
        {
            w.Host.DrawInfo(op, NothingWritten);
            return false;
        }

        bool counted = false;
        if (sign.Stats.Food != 0)
        {
            if (sign.LastEat >= sign.Stats.Food)
            {
                if (!sign.QueryFlag(Define.FLAG_WALK_ON) && !sign.QueryFlag(Define.FLAG_FLY_ON))
                {
                    w.Host.DrawInfo(op, CannotReadAnymore);
                }
                return false;
            }
            sign.LastEat++;
            counted = true;
        }

        /* Sign or magic mouth?  Do we need to see it, or does it talk to us? No way to know for sure. This check fails for signs with
         * FLAG_WALK_ON/FLAG_FLY_ON. Checking for FLAG_INVISIBLE instead of FLAG_WALK_ON/FLAG_FLY_ON would fail for magic mouths that
         * have been made visible. */
        if (op.QueryFlag(Define.FLAG_BLIND) && !op.QueryFlag(Define.FLAG_WIZ)
            && !sign.QueryFlag(Define.FLAG_WALK_ON) && !sign.QueryFlag(Define.FLAG_FLY_ON))
        {
            w.Host.DrawInfo(op, BlindToRead);
            return counted;
        }

        w.Host.DrawInfo(op, sign.Msg);
        return counted;
    }

    /// <summary>
    /// skill_util.c <c>SK_level</c>: the level of the skill a player has chosen (the object's own level for anything else, and when the
    /// skill has none); at least 1 (C logs a bug for a level below 1).
    /// </summary>
    public static int SkLevel(CfWorld w, CfObject op)
    {
        CfObject head = op.Head ?? op;
        int level;
        if (head.Type == Define.PLAYER && head.ChosenSkill != null && head.ChosenSkill.Level != 0)
        {
            level = head.ChosenSkill.Level;
        }
        else
        {
            level = head.Level;
        }
        if (level <= 0)
        {
            w.Log(LogLevel.Error, "BUG: SK_level(arch " + (op.Arch?.Name ?? "(null)") + ", name " + op.Name + "): level <= 0\n");
            level = 1; /* safety */
        }
        return level;
    }

    /// <summary>
    /// apply.c <c>apply_book</c> for a player: the text of the book, when the player sees, has the skill literacy and the book is not
    /// beyond his level. C then gives experience for the first reading and identifies the book; this port gives neither (no experience
    /// in phase 3: the book stays as it is, so a later phase can give it once). C's <c>apply_book</c> teaches no spell (that is
    /// <c>apply_spellbook</c>'s). <paramref name="skills"/> is the skill table that readies the skill (null: no skill can be readied).
    /// </summary>
    public static void ApplyBook(CfWorld w, SkillTable? skills, CfObject op, CfObject tmp)
    {
        if (op.QueryFlag(Define.FLAG_BLIND) && !op.QueryFlag(Define.FLAG_WIZ))
        {
            w.Host.DrawInfo(op, BlindToRead);
            return;
        }
        if (tmp.Msg == null)
        {
            w.Host.DrawInfo(op, "You open the " + tmp.Name + " and find it empty.");
            return;
        }

        /* need a literacy skill to read stuff! */
        if (skills == null || skills.ChangeSkill(w, op, Define.SK_LITERACY) == 0)
        {
            w.Host.DrawInfo(op, StrangeSymbols);
            return;
        }
        int levDiff = tmp.Level - (SkLevel(w, op) + 5);
        if (!op.QueryFlag(Define.FLAG_WIZ) && levDiff > 0)
        {
            if (levDiff < 2)
            {
                w.Host.DrawInfo(op, "This book is just barely beyond your comprehension.");
            }
            else if (levDiff < 3)
            {
                w.Host.DrawInfo(op, "This book is slightly beyond your comprehension.");
            }
            else if (levDiff < 5)
            {
                w.Host.DrawInfo(op, "This book is beyond your comprehension.");
            }
            else if (levDiff < 8)
            {
                w.Host.DrawInfo(op, "This book is quite a bit beyond your comprehension.");
            }
            else if (levDiff < 15)
            {
                w.Host.DrawInfo(op, "This book is way beyond your comprehension.");
            }
            else
            {
                w.Host.DrawInfo(op, "This book is totally beyond your comprehension.");
            }
            return;
        }

        w.Host.DrawInfo(op, "You open the " + tmp.Name + " and start reading.");
        w.Host.DrawInfo(op, tmp.Msg);
    }
}
