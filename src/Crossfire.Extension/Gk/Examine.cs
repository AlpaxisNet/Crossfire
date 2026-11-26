// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): what the player is told when he examines an object, server/c_object.c
// (group D of docs/mud-phase3.md).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/c_object.c:
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
using System.Globalization;
using System.Text;

namespace Crossfire.Extension.Gk;

/// <summary>
/// server/c_object.c <c>command_examine</c>, <c>examine</c>, <c>examine_monster</c> and <c>long_desc</c>. Every line C writes with
/// <c>new_draw_info</c> goes to <see cref="CfWorld.Host"/>'s <c>DrawInfo</c> (the player's event <c>onRPGMessage</c>, one event per line).
/// Texts are C's bytes (the characters of the strings are bytes).
/// </summary>
internal static class Examine
{
    /// <summary>include/define.h <c>VERY_BIG_BUF</c>: the size of the buffers of the description.</summary>
    public const int VeryBigBuf = 1024;

    private static void Draw(CfWorld w, CfObject op, string text) => w.Host.DrawInfo(op, text);

    /// <summary>C's <c>strncpy(buf, text, VERY_BIG_BUF-1)</c> into a buffer that starts empty.</summary>
    private static string Cut(string text, int already = 0) => text.Length > VeryBigBuf - 1 - already ? text.Substring(0, VeryBigBuf - 1 - already) : text;

    /// <summary>
    /// c_object.c <c>command_examine</c>: without parameters the first object below the player that shows (the ground: the player of
    /// this extension stands in no level, so there is none); with them the best match of <see cref="ItemMatching.FindBestObjectMatch"/>
    /// or the line "Could not find an object that matches ...". Returns 0, as C does.
    /// </summary>
    public static int CommandExamine(CfWorld w, CfObject op, string? parameters)
    {
        if (parameters == null)
        {
            CfObject? tmp = op.Below;
            while (tmp != null && !(tmp.Invisible == 0 && tmp.Type != Define.PLAYER)) // LOOK_OBJ
            {
                tmp = tmp.Below;
            }
            if (tmp != null)
            {
                ExamineObject(w, op, tmp);
            }
        }
        else
        {
            CfObject? tmp = ItemMatching.FindBestObjectMatch(w, op, parameters);
            if (tmp != null)
            {
                ExamineObject(w, op, tmp);
            }
            else
            {
                Draw(w, op, "Could not find an object that matches " + parameters);
            }
        }
        return 0;
    }

    /// <summary>c_object.c <c>examine_monster</c>: <paramref name="op"/> is the player, <paramref name="tmp"/> the monster being examined.</summary>
    public static void ExamineMonster(CfWorld w, CfObject op, CfObject tmp)
    {
        CfObject mon = tmp.Head ?? tmp;

        if (mon.QueryFlag(Define.FLAG_UNDEAD))
        {
            Draw(w, op, "It is an undead force.");
        }
        if (mon.Level > op.Level)
        {
            Draw(w, op, "It is likely more powerful than you.");
        }
        else if (mon.Level < op.Level)
        {
            Draw(w, op, "It is likely less powerful than you.");
        }
        else
        {
            Draw(w, op, "It is probably as powerful as you.");
        }
        if ((mon.Attacktype & Define.AT_ACID) != 0)
        {
            Draw(w, op, "You seem to smell an acrid odor.");
        }

        /* Anyone know why this used to use the clone value instead of the maxhp field? This seems that it should give more accurate results. */
        // C divides by maxhp + 1 and would stop with a division by zero for maxhp -1: no line then
        switch (mon.Stats.Maxhp + 1 == 0 ? 0 : (mon.Stats.Hp + 1) * 4 / (mon.Stats.Maxhp + 1)) /* From 1-4 */
        {
            case 1:
                Draw(w, op, "It is in a bad shape.");
                break;
            case 2:
                Draw(w, op, "It is hurt.");
                break;
            case 3:
                Draw(w, op, "It is somewhat hurt.");
                break;
            case 4:
                Draw(w, op, "It is in excellent shape.");
                break;
        }
        if (CfWorld.PresentInOb(Define.POISONING, mon) != null)
        {
            Draw(w, op, "It looks very ill.");
        }
    }

    /// <summary>
    /// c_object.c <c>long_desc</c>: the name of the object and, for the types that have one, what <see cref="CfWorld.DescribeItem"/>
    /// says of it; "" for null.
    /// </summary>
    public static string LongDesc(CfWorld w, CfObject? tmp)
    {
        var buf = new StringBuilder();

        if (tmp == null)
        {
            return "";
        }
        switch (tmp.Type)
        {
            case Define.RING:
            case Define.SKILL:
            case Define.WEAPON:
            case Define.ARMOUR:
            case Define.BRACERS:
            case Define.HELMET:
            case Define.SHIELD:
            case Define.BOOTS:
            case Define.GLOVES:
            case Define.AMULET:
            case Define.GIRDLE:
            case Define.BOW:
            case Define.ARROW:
            case Define.CLOAK:
            case Define.FOOD:
            case Define.DRINK:
            case Define.FLESH:
                string cp = w.DescribeItem(tmp);
                if (cp.Length != 0)
                {
                    buf.Append(Cut(w.QueryName(tmp)));
                    if (buf.Length < VeryBigBuf - 5)
                    {
                        buf.Append(' ');
                        buf.Append(Cut(cp, buf.Length));
                    }
                }
                break;
        }
        if (buf.Length == 0)
        {
            buf.Append(Cut(w.QueryName(tmp)));
        }
        return buf.ToString();
    }

    /// <summary>
    /// c_object.c <c>examine</c>: what the player <paramref name="op"/> is told of <paramref name="tmp"/>: "That is" and its long
    /// description, what the type adds (a spell book, a book, a container, a wand), the material, the weight, the price, the lines of a
    /// monster, the object's message, and a blank line. An object that is not identified shows no magic (<c>describe_item</c>).
    /// </summary>
    public static void ExamineObject(CfWorld w, CfObject op, CfObject? tmp)
    {
        string buf;

        if (tmp == null || tmp.Type == Define.CLOSE_CON)
        {
            return;
        }

        /* Eneq(csd.uu.se): If NO_PRETEXT is defined we should only print the name. */
        if (tmp.QueryFlag(Define.FLAG_NO_PRETEXT))
        {
            buf = Cut(LongDesc(w, tmp));
        }
        else
        {
            /* Only quetzals can see the resistances on flesh. To realize this, we temporarily flag the flesh with SEE_INVISIBLE */
            if (op.Type == Define.PLAYER && tmp.Type == Define.FLESH && CfWorld.IsDragonPl(op) != 0)
            {
                tmp.SetFlag(Define.FLAG_SEE_INVISIBLE);
            }

            buf = "That is ";
            buf += Cut(LongDesc(w, tmp), buf.Length);
            if (op.Type == Define.PLAYER && tmp.Type == Define.FLESH)
            {
                tmp.ClearFlag(Define.FLAG_SEE_INVISIBLE);
            }
        }

        Draw(w, op, buf);
        buf = "";
        switch (tmp.Type)
        {
            case Define.SPELLBOOK:
                if (tmp.QueryFlag(Define.FLAG_IDENTIFIED) && tmp.Stats.Sp >= 0 && tmp.Stats.Sp <= Define.NROFREALSPELLS)
                {
                    string spellName = w.Host.SpellName(tmp.Stats.Sp) ?? "";
                    string level = w.Host.SpellLevel(tmp.Stats.Sp).ToString(CultureInfo.InvariantCulture);
                    buf = tmp.Arch != null && tmp.Arch.Name == "cleric_book"
                        ? spellName + " is a " + level + " level prayer."
                        : spellName + " is a " + level + " level spell.";
                }
                break;

            case Define.BOOK:
                if (tmp.Msg != null)
                {
                    buf = "Something is written in it.";
                }
                break;

            case Define.CONTAINER:
                if (tmp.Race != null)
                {
                    if (tmp.WeightLimit != 0 && tmp.Stats.Str < 100)
                    {
                        buf = "It can hold only " + tmp.Race + " and its weight limit is " + WeightLimitKg(tmp) + " kg.";
                    }
                    else
                    {
                        buf = "It can hold only " + tmp.Race + ".";
                    }
                }
                else if (tmp.WeightLimit != 0 && tmp.Stats.Str < 100)
                {
                    buf = "Its weight limit is " + WeightLimitKg(tmp) + " kg.";
                }
                break;

            case Define.WAND:
                if (tmp.QueryFlag(Define.FLAG_IDENTIFIED))
                {
                    buf = "It has " + tmp.Stats.Food.ToString(CultureInfo.InvariantCulture) + " charges left.";
                }
                break;
        }

        if (buf.Length != 0)
        {
            Draw(w, op, buf);
        }

        if (tmp.Material != 0 && tmp.Msg == null)
        {
            var made = new StringBuilder("It is made of: ");
            for (int i = 0; i < Define.NROFMATERIALS; i++)
            {
                if ((tmp.Material & (1 << i)) != 0)
                {
                    made.Append(Tables.Material[i].Name);
                    made.Append(' ');
                }
            }
            Draw(w, op, made.ToString());
        }

        if (tmp.Weight != 0)
        {
            double kg = unchecked(tmp.Nrof != 0 ? (uint)(tmp.Weight * (int)tmp.Nrof) : (uint)tmp.Weight) / 1000.0;
            buf = (tmp.Nrof > 1 ? "They weigh " : "It weighs ") + CStr.FormatFixed(kg, 3) + " kg.";
            Draw(w, op, buf);
        }

        if (tmp.Value != 0 && !tmp.QueryFlag(Define.FLAG_STARTEQUIP))
        {
            if (tmp.QueryFlag(Define.FLAG_UNPAID))
            {
                buf = (tmp.Nrof > 1 ? "They" : "It") + " would cost you " + w.QueryCostString(tmp, op, Define.F_BUY) + ".";
            }
            else
            {
                buf = "You would get " + w.QueryCostString(tmp, op, Define.F_SELL) + " for the " + (tmp.Nrof > 1 ? "them" : "it") + ".";
            }
            Draw(w, op, buf);
        }

        if (tmp.QueryFlag(Define.FLAG_MONSTER))
        {
            ExamineMonster(w, op, tmp);
        }

        /* Does the object have a message?  Don't show message for all object types - especially if the first entry is a match */
        if (tmp.Msg != null && tmp.Type != Define.EXIT && tmp.Type != Define.BOOK && tmp.Type != Define.CORPSE
            && !tmp.QueryFlag(Define.FLAG_WALK_ON) && !MatchMessage(tmp.Msg))
        {
            /* This is just a hack so when identifying the items, we print out the extra message */
            if (CfWorld.NeedIdentify(tmp) != 0 && tmp.QueryFlag(Define.FLAG_IDENTIFIED))
            {
                Draw(w, op, "The object has a story:");
            }

            Draw(w, op, tmp.Msg);
        }
        Draw(w, op, " "); /* Blank line */
    }

    /// <summary><c>strncasecmp(msg, "@match", 7) == 0</c>: seven characters are compared, the seventh of the pattern being its end, so only "@match" itself (in any case) is a match.</summary>
    private static bool MatchMessage(string msg) => msg.Length == 6 && msg.Equals("@match", StringComparison.OrdinalIgnoreCase);

    private static string WeightLimitKg(CfObject tmp) => CStr.FormatFixed(tmp.WeightLimit / (10.0 * (100 - tmp.Stats.Str)), 1);
}
