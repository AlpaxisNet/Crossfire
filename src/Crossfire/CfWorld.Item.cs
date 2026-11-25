// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/item.c.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/item.c:
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
using System.Text;

namespace Crossfire;

/// <summary>
/// The names and descriptions of objects: common/item.c. The functions return the text that the C functions leave in their
/// static buffers (docs/differences.md: the five buffers of <c>query_name</c> are not needed for strings).
/// </summary>
public sealed partial class CfWorld
{
    private const int ItemMaxBuf = 256;
    private const int ItemVeryBigBuf = 1024;
    private const int ItemHugeBuf = 4096;

    /// <summary>common/item.c: <c>numbers</c> (static, 21 entries).</summary>
    private static readonly string[] Numbers =
    {
        "no", "", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
        "eighteen", "nineteen", "twenty",
    };

    /// <summary>common/item.c: <c>numbers_10</c> (static; "fourty" is the C spelling).</summary>
    private static readonly string[] Numbers10 =
    {
        "zero", "ten", "twenty", "thirty", "fourty", "fifty", "sixty", "seventy", "eighty", "ninety",
    };

    /// <summary>common/item.c: <c>levelnumbers</c> (static; "eighteen" and "nineteen" are the C spellings).</summary>
    private static readonly string[] LevelNumbers =
    {
        "zeroth", "first", "second", "third", "fourth", "fifth", "sixth", "seventh",
        "eighth", "ninth", "tenth", "eleventh", "twelfth", "thirteenth",
        "fourteenth", "fifteenth", "sixteenth", "seventeenth", "eighteen",
        "nineteen", "twentieth",
    };

    /// <summary>common/item.c: <c>levelnumbers_10</c> (static).</summary>
    private static readonly string[] LevelNumbers10 =
    {
        "zeroth", "tenth", "twentieth", "thirtieth", "fortieth", "fiftieth", "sixtieth",
        "seventieth", "eightieth", "ninetieth",
    };

    /// <summary>
    /// common/item.c <c>describe_resistance</c>: the resistances of the object as "(resist fire +30)" pieces, or, when
    /// <paramref name="newline"/> is set, as "resist fire 30" lines. A flesh object shows only the resistances a dragon player
    /// can gain.
    /// </summary>
    public string DescribeResistance(CfObject op, bool newline)
    {
        var buf = new StringBuilder();
        for (int tmpvar = 0; tmpvar < Define.NROFATTACKS; tmpvar++)
        {
            if (op.Resist[tmpvar] != 0 && (op.Type != Define.FLESH || AtnrIsDragonEnabled(tmpvar) == 1))
            {
                if (!newline)
                {
                    buf.Append('(').Append(Tables.ResistPlus[tmpvar]).Append(' ').Append(CStr.Plus(op.Resist[tmpvar])).Append(')');
                }
                else
                {
                    buf.Append(Tables.ResistPlus[tmpvar]).Append(' ').Append(op.Resist[tmpvar].ToString(CultureInfo.InvariantCulture)).Append('\n');
                }
            }
        }
        return buf.ToString();
    }

    /// <summary>
    /// common/item.c <c>query_weight</c>: the weight of the object (the stack, or the weight and what it carries) in kilograms
    /// as "%6.1f", or as "%4d  " when it is whole; six blanks when the weight is negative.
    /// </summary>
    public static string QueryWeight(CfObject op)
    {
        int i = unchecked(op.Nrof != 0 ? (int)(op.Nrof * (uint)op.Weight) : op.Weight + op.Carrying);
        if (op.Weight < 0)
        {
            return "      ";
        }
        if (i % 1000 != 0)
        {
            return CStr.FormatFixed(i / 1000.0, 1).PadLeft(6);
        }
        return (i / 1000).ToString(CultureInfo.InvariantCulture).PadLeft(4) + "  ";
    }

    /// <summary>
    /// common/item.c <c>get_levelnumber</c>: "first", "second" .. "twentieth", "twentyfirst" .. "ninetyninth", and "100." from 100 on.
    /// A negative number (C reads outside its table) gives the empty string.
    /// </summary>
    public static string GetLevelnumber(int i)
    {
        if (i > 99)
        {
            return i.ToString(CultureInfo.InvariantCulture) + ".";
        }
        if (i < 0)
        {
            return "";
        }
        if (i < 21)
        {
            return LevelNumbers[i];
        }
        if (i % 10 == 0)
        {
            return LevelNumbers10[i / 10];
        }
        return Numbers10[i / 10] + LevelNumbers[i % 10];
    }

    /// <summary>
    /// common/item.c <c>get_number</c>: the word of a number up to 20 ("no" for 0, the empty string for 1), the digits above.
    /// A negative number (C reads outside its table) gives the empty string.
    /// </summary>
    public static string GetNumber(int i)
    {
        if (i <= 20)
        {
            return i < 0 ? "" : Numbers[i];
        }
        return i.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// common/item.c <c>ring_desc</c>: the abilities of an identified ring, amulet or skill tool as "(Str+1)(speed +1)..." pieces,
    /// "of adornment" for a ring with none, and the empty string when the object is not identified.
    /// </summary>
    public string RingDesc(CfObject op)
    {
        var sb = new StringBuilder();
        if (!op.QueryFlag(Define.FLAG_IDENTIFIED))
        {
            return "";
        }

        for (int attr = 0; attr < 7; attr++)
        {
            int val = GetAttrValue(op.Stats, attr);
            if (val != 0)
            {
                sb.Append('(').Append(Tables.ShortStatName[attr]).Append(CStr.Plus(val)).Append(')');
            }
        }
        if (op.Stats.Exp != 0)
        {
            sb.Append("(speed ").Append(CStr.Plus(op.Stats.Exp)).Append(')');
        }
        if (op.Stats.Wc != 0)
        {
            sb.Append("(wc").Append(CStr.Plus(op.Stats.Wc)).Append(')');
        }
        if (op.Stats.Dam != 0)
        {
            sb.Append("(dam").Append(CStr.Plus(op.Stats.Dam)).Append(')');
        }
        if (op.Stats.Ac != 0)
        {
            sb.Append("(ac").Append(CStr.Plus(op.Stats.Ac)).Append(')');
        }

        sb.Append(DescribeResistance(op, false));

        if (op.Stats.Food != 0)
        {
            sb.Append("(sustenance").Append(CStr.Plus(op.Stats.Food)).Append(')');
        }
        if (op.Stats.Grace != 0)
        {
            sb.Append("(grace").Append(CStr.Plus(op.Stats.Grace)).Append(')');
        }
        if (op.Stats.Sp != 0 && op.Type != Define.SKILL)
        {
            sb.Append("(magic").Append(CStr.Plus(op.Stats.Sp)).Append(')');
        }
        if (op.Stats.Hp != 0)
        {
            sb.Append("(regeneration").Append(CStr.Plus(op.Stats.Hp)).Append(')');
        }
        if (op.Stats.Luck != 0)
        {
            sb.Append("(luck").Append(CStr.Plus(op.Stats.Luck)).Append(')');
        }
        if (op.QueryFlag(Define.FLAG_LIFESAVE))
        {
            sb.Append("(lifesaving)");
        }
        if (op.QueryFlag(Define.FLAG_REFL_SPELL))
        {
            sb.Append("(reflect spells)");
        }
        if (op.QueryFlag(Define.FLAG_REFL_MISSILE))
        {
            sb.Append("(reflect missiles)");
        }
        if (op.QueryFlag(Define.FLAG_STEALTH))
        {
            sb.Append("(stealth)");
        }
        /* Shorten some of the names, so they appear better in the windows */
        string buf = sb.ToString();
        int len = buf.Length;
        DescribePathSafe(ref buf, op.PathAttuned, "Attuned", ref len, ItemVeryBigBuf);
        DescribePathSafe(ref buf, op.PathRepelled, "Repelled", ref len, ItemVeryBigBuf);
        DescribePathSafe(ref buf, op.PathDenied, "Denied", ref len, ItemVeryBigBuf);
        if (buf.Length == 0 && op.Type != Define.SKILL)
        {
            buf = "of adornment";
        }
        return buf;
    }

    private string SpellNameOf(int index) => Host.SpellName(index) ?? "";

    private string PluralName(CfObject op, string where)
    {
        if (op.NamePl == null)
        {
            Log(LogLevel.Error, where + ": object " + op.Name + " has no plural name\n");
            return "";
        }
        return op.NamePl;
    }

    /// <summary>
    /// common/item.c <c>query_short_name</c>: the name of the object, in the plural for a stack, with its title when it is
    /// identified, the spell of a scroll, wand, rod or book, the abilities of a ring (<c>FULL_RING_DESCRIPTION</c> is defined in
    /// config.h) or the magic bonus; without the state of the object (worn, cursed, unpaid). "(null)" for an object without a name.
    /// </summary>
    public string QueryShortName(CfObject op)
    {
        if (op.Name == null)
        {
            return "(null)";
        }
        if (op.Nrof == 0 && op.Weight == 0 && op.Title == null && IsMagical(op) == 0)
        {
            return op.Name; /* To speed things up (or make things slower?) */
        }

        string buf = "";
        int len = 0;
        if (op.Nrof == 1)
        {
            SafeStrcat(ref buf, op.Name, ref len, ItemHugeBuf);
        }
        else
        {
            SafeStrcat(ref buf, PluralName(op, "query_short_name"), ref len, ItemHugeBuf);
        }

        if (op.Title != null && op.QueryFlag(Define.FLAG_IDENTIFIED))
        {
            SafeStrcat(ref buf, " ", ref len, ItemHugeBuf);
            SafeStrcat(ref buf, op.Title, ref len, ItemHugeBuf);
        }

        switch (op.Type)
        {
            case Define.SPELLBOOK:
                if (op.QueryFlag(Define.FLAG_IDENTIFIED) || op.QueryFlag(Define.FLAG_BEEN_APPLIED))
                {
                    if (op.Title == null)
                    {
                        SafeStrcat(ref buf, " of ", ref len, ItemHugeBuf);
                        if (op.Slaying != null)
                        {
                            SafeStrcat(ref buf, op.Slaying, ref len, ItemHugeBuf);
                        }
                        else
                        {
                            SafeStrcat(ref buf, SpellNameOf(op.Stats.Sp), ref len, ItemHugeBuf);
                        }
                        if (op.Type != Define.SPELLBOOK)
                        {
                            SafeStrcat(ref buf, " (lvl " + op.Level.ToString(CultureInfo.InvariantCulture) + ")", ref len, ItemHugeBuf);
                        }
                    }
                }
                break;
            case Define.SCROLL:
            case Define.WAND:
            case Define.ROD:
                if (op.QueryFlag(Define.FLAG_IDENTIFIED) || op.QueryFlag(Define.FLAG_BEEN_APPLIED))
                {
                    if (op.Title == null)
                    {
                        SafeStrcat(ref buf, " of ", ref len, ItemHugeBuf);
                        SafeStrcat(ref buf, SpellNameOf(op.Stats.Sp), ref len, ItemHugeBuf);
                        if (op.Type != Define.SPELLBOOK)
                        {
                            SafeStrcat(ref buf, " (lvl " + op.Level.ToString(CultureInfo.InvariantCulture) + ")", ref len, ItemHugeBuf);
                        }
                    }
                }
                break;
            case Define.SKILL:
            case Define.AMULET:
            case Define.RING:
                /* FULL_RING_DESCRIPTION */
                if (op.Title == null)
                {
                    /* If ring has a title, full description isn't so useful */
                    string s = RingDesc(op);
                    if (s.Length > 0)
                    {
                        SafeStrcat(ref buf, " ", ref len, ItemHugeBuf);
                        SafeStrcat(ref buf, s, ref len, ItemHugeBuf);
                    }
                }
                break;
            default:
                if (op.Magic != 0 && ((op.QueryFlag(Define.FLAG_BEEN_APPLIED) && NeedIdentify(op) != 0) || op.QueryFlag(Define.FLAG_IDENTIFIED)))
                {
                    SafeStrcat(ref buf, " " + CStr.Plus(op.Magic), ref len, ItemHugeBuf);
                }
                break;
        }
        return buf;
    }

    /// <summary>
    /// common/item.c <c>query_name</c>: <see cref="QueryShortName"/> and the state of the object: " *" (inventory locked), " (open)",
    /// " (damned)" or " (cursed)" when known, " (magic)", " (readied)", " (wielded)", " (worn)", " (active)" or " (applied)", and
    /// " (unpaid)", in that order.
    /// </summary>
    public string QueryName(CfObject op)
    {
        string buf = "";
        int len = 0;

        SafeStrcat(ref buf, QueryShortName(op), ref len, ItemHugeBuf);

        if (op.QueryFlag(Define.FLAG_INV_LOCKED))
        {
            SafeStrcat(ref buf, " *", ref len, ItemHugeBuf);
        }
        if (op.Type == Define.CONTAINER && ((op.Env != null && op.Env.Container == op) || (op.Env == null && op.QueryFlag(Define.FLAG_APPLIED))))
        {
            SafeStrcat(ref buf, " (open)", ref len, ItemHugeBuf);
        }

        if (op.QueryFlag(Define.FLAG_KNOWN_CURSED))
        {
            if (op.QueryFlag(Define.FLAG_DAMNED))
            {
                SafeStrcat(ref buf, " (damned)", ref len, ItemHugeBuf);
            }
            else if (op.QueryFlag(Define.FLAG_CURSED))
            {
                SafeStrcat(ref buf, " (cursed)", ref len, ItemHugeBuf);
            }
        }
        /* Basically, if the object is known magical (detect magic spell on it),
         * and it isn't identified,  print out the fact that
         * it is magical. */
        if (op.QueryFlag(Define.FLAG_KNOWN_MAGICAL) && !op.QueryFlag(Define.FLAG_IDENTIFIED))
        {
            SafeStrcat(ref buf, " (magic)", ref len, ItemHugeBuf);
        }
        if (op.QueryFlag(Define.FLAG_APPLIED))
        {
            switch (op.Type)
            {
                case Define.BOW:
                case Define.WAND:
                case Define.ROD:
                case Define.HORN:
                    SafeStrcat(ref buf, " (readied)", ref len, ItemHugeBuf);
                    break;
                case Define.WEAPON:
                    SafeStrcat(ref buf, " (wielded)", ref len, ItemHugeBuf);
                    break;
                case Define.ARMOUR:
                case Define.HELMET:
                case Define.SHIELD:
                case Define.RING:
                case Define.BOOTS:
                case Define.GLOVES:
                case Define.AMULET:
                case Define.GIRDLE:
                case Define.BRACERS:
                case Define.CLOAK:
                    SafeStrcat(ref buf, " (worn)", ref len, ItemHugeBuf);
                    break;
                case Define.CONTAINER:
                    SafeStrcat(ref buf, " (active)", ref len, ItemHugeBuf);
                    break;
                case Define.SKILL:
                default:
                    SafeStrcat(ref buf, " (applied)", ref len, ItemHugeBuf);
                    break;
            }
        }
        if (op.QueryFlag(Define.FLAG_UNPAID))
        {
            SafeStrcat(ref buf, " (unpaid)", ref len, ItemHugeBuf);
        }

        return buf;
    }

    /// <summary>
    /// common/item.c <c>query_base_name</c>: like <see cref="QueryShortName"/>, in the plural when <paramref name="plural"/> is
    /// set, in a buffer of 256 characters (used for sorting inventories and for the client). An object without a name (or, for the
    /// plural, without a plural name) is "(null)"; one that needs no extras gives its singular name even when the plural was asked for.
    /// </summary>
    public string QueryBaseName(CfObject op, int plural)
    {
        if ((plural == 0 && op.Name == null) || (plural != 0 && op.NamePl == null))
        {
            return "(null)";
        }

        if (op.Nrof == 0 && op.Weight == 0 && op.Title == null && IsMagical(op) == 0)
        {
            return op.Name ?? "(null)"; /* To speed things up (or make things slower?) */
        }

        string buf = plural == 0 ? op.Name! : op.NamePl!;
        int len = buf.Length;

        if (op.Title != null && op.QueryFlag(Define.FLAG_IDENTIFIED))
        {
            SafeStrcat(ref buf, " ", ref len, ItemMaxBuf);
            SafeStrcat(ref buf, op.Title, ref len, ItemMaxBuf);
        }

        switch (op.Type)
        {
            case Define.SPELLBOOK:
                if (op.QueryFlag(Define.FLAG_IDENTIFIED) || op.QueryFlag(Define.FLAG_BEEN_APPLIED))
                {
                    if (op.Title == null)
                    {
                        SafeStrcat(ref buf, " of ", ref len, ItemMaxBuf);
                        if (op.Slaying != null)
                        {
                            SafeStrcat(ref buf, op.Slaying, ref len, ItemMaxBuf);
                        }
                        else
                        {
                            SafeStrcat(ref buf, SpellNameOf(op.Stats.Sp), ref len, ItemMaxBuf);
                        }
                        if (op.Type != Define.SPELLBOOK)
                        {
                            buf += " (lvl " + op.Level.ToString(CultureInfo.InvariantCulture) + ")";
                        }
                    }
                }
                break;
            case Define.SCROLL:
            case Define.WAND:
            case Define.ROD:
                if (op.QueryFlag(Define.FLAG_IDENTIFIED) || op.QueryFlag(Define.FLAG_BEEN_APPLIED))
                {
                    if (op.Title == null)
                    {
                        SafeStrcat(ref buf, " of ", ref len, ItemMaxBuf);
                        SafeStrcat(ref buf, SpellNameOf(op.Stats.Sp), ref len, ItemMaxBuf);
                        if (op.Type != Define.SPELLBOOK)
                        {
                            buf += " (lvl " + op.Level.ToString(CultureInfo.InvariantCulture) + ")";
                        }
                    }
                }
                break;
            case Define.SKILL:
            case Define.AMULET:
            case Define.RING:
                /* FULL_RING_DESCRIPTION */
                if (op.Title == null)
                {
                    /* If ring has a title, full description isn't so useful */
                    string s = RingDesc(op);
                    if (s.Length > 0)
                    {
                        SafeStrcat(ref buf, " ", ref len, ItemMaxBuf);
                        SafeStrcat(ref buf, s, ref len, ItemMaxBuf);
                    }
                }
                break;
            default:
                if (op.Magic != 0 && ((op.QueryFlag(Define.FLAG_BEEN_APPLIED) && NeedIdentify(op) != 0) || op.QueryFlag(Define.FLAG_IDENTIFIED)))
                {
                    buf += " " + CStr.Plus(op.Magic);
                }
                break;
        }
        return buf;
    }

    /// <summary>
    /// common/item.c <c>describe_monster</c> (static): what the abilities of a monster or player look like: its speed, the
    /// flags that tell what it uses and does, the spell abilities of its treasure, what a player regenerates, the attacks of a
    /// dragon player and the spell paths.
    /// </summary>
    private string DescribeMonster(CfObject op)
    {
        var retbuf = new StringBuilder();

        /* Note that the resolution this provides for players really isn't
         * very good.  Any player with a speed greater than .67 will
         * fall into the 'lightning fast movement' category.
         */
        if (Math.Abs((double)op.Speed) > Define.MIN_ACTIVE_SPEED)
        {
            switch (CStr.ToInt(Math.Abs(op.Speed) * 15))
            {
                case 0:
                    retbuf.Append("(very slow movement)");
                    break;
                case 1:
                    retbuf.Append("(slow movement)");
                    break;
                case 2:
                    retbuf.Append("(normal movement)");
                    break;
                case 3:
                case 4:
                    retbuf.Append("(fast movement)");
                    break;
                case 5:
                case 6:
                    retbuf.Append("(very fast movement)");
                    break;
                case 7:
                case 8:
                case 9:
                case 10:
                    retbuf.Append("(extremely fast movement)");
                    break;
                default:
                    retbuf.Append("(lightning fast movement)");
                    break;
            }
        }
        if (op.QueryFlag(Define.FLAG_UNDEAD))
        {
            retbuf.Append("(undead)");
        }
        if (op.QueryFlag(Define.FLAG_CAN_PASS_THRU))
        {
            retbuf.Append("(pass through doors)");
        }
        if (op.QueryFlag(Define.FLAG_SEE_INVISIBLE))
        {
            retbuf.Append("(see invisible)");
        }
        if (op.QueryFlag(Define.FLAG_USE_WEAPON))
        {
            retbuf.Append("(wield weapon)");
        }
        if (op.QueryFlag(Define.FLAG_USE_BOW))
        {
            retbuf.Append("(archer)");
        }
        if (op.QueryFlag(Define.FLAG_USE_ARMOUR))
        {
            retbuf.Append("(wear armour)");
        }
        if (op.QueryFlag(Define.FLAG_USE_RING))
        {
            retbuf.Append("(wear ring)");
        }
        if (op.QueryFlag(Define.FLAG_USE_SCROLL))
        {
            retbuf.Append("(read scroll)");
        }
        if (op.QueryFlag(Define.FLAG_USE_WAND))
        {
            retbuf.Append("(fire wand)");
        }
        if (op.QueryFlag(Define.FLAG_USE_ROD))
        {
            retbuf.Append("(use rod)");
        }
        if (op.QueryFlag(Define.FLAG_USE_HORN))
        {
            retbuf.Append("(use horn)");
        }
        if (op.QueryFlag(Define.FLAG_CAN_USE_SKILL))
        {
            retbuf.Append("(skill user)");
        }
        if (op.QueryFlag(Define.FLAG_CAST_SPELL))
        {
            retbuf.Append("(spellcaster)");
        }
        if (op.QueryFlag(Define.FLAG_FRIENDLY))
        {
            retbuf.Append("(friendly)");
        }
        if (op.QueryFlag(Define.FLAG_UNAGGRESSIVE))
        {
            retbuf.Append("(unaggressive)");
        }
        if (op.QueryFlag(Define.FLAG_HITBACK))
        {
            retbuf.Append("(hitback)");
        }
        if (op.Randomitems != null)
        {
            bool first = true;
            foreach (string name in Host.TreasureAbilityNames(op.Randomitems))
            {
                if (first)
                {
                    first = false;
                    retbuf.Append("(Spell abilities:)");
                }
                retbuf.Append('(').Append(name).Append(')');
            }
        }
        if (op.Type == Define.PLAYER && op.Contr != null)
        {
            int v = Host.PlayerDigestion(op.Contr);
            if (v != 0)
            {
                retbuf.Append("(sustenance").Append(CStr.Plus(v)).Append(')');
            }
            v = Host.PlayerGenGrace(op.Contr);
            if (v != 0)
            {
                retbuf.Append("(grace").Append(CStr.Plus(v)).Append(')');
            }
            v = Host.PlayerGenSp(op.Contr);
            if (v != 0)
            {
                retbuf.Append("(magic").Append(CStr.Plus(v)).Append(')');
            }
            v = Host.PlayerGenHp(op.Contr);
            if (v != 0)
            {
                retbuf.Append("(regeneration").Append(CStr.Plus(v)).Append(')');
            }
        }

        /* describe attacktypes */
        if (IsDragonPl(op) != 0)
        {
            /* for dragon players display the attacktypes from clawing skill */
            CfObject? tmp;
            for (tmp = op.Inv; tmp != null; tmp = tmp.Below)
            {
                if (tmp.Type == Define.SKILL && tmp.Name == "clawing")
                {
                    break;
                }
            }

            if (tmp != null && tmp.Attacktype != 0)
            {
                DescribeAbility(retbuf, tmp.Attacktype, "Claws");
            }
            else
            {
                DescribeAbility(retbuf, op.Attacktype, "Attacks");
            }
        }
        DescribePath(retbuf, op.PathAttuned, "Attuned");
        DescribePath(retbuf, op.PathRepelled, "Repelled");
        DescribePath(retbuf, op.PathDenied, "Denied");
        return retbuf.ToString();
    }

    /// <summary>
    /// common/item.c <c>describe_item</c>: the description of an object: for a monster or player its abilities
    /// (<c>describe_monster</c>), for a ring, amulet or skill tool its abilities when it has a title, and for food, weapons,
    /// armour and the like what the object does to its user, as far as the object is identified (or applied).
    /// </summary>
    public string DescribeItem(CfObject op)
    {
        var retbuf = new StringBuilder();
        int identified;

        if (op.QueryFlag(Define.FLAG_MONSTER) || op.Type == Define.PLAYER)
        {
            return DescribeMonster(op);
        }
        /* figure this out once, instead of making multiple calls to need_identify. */
        if (NeedIdentify(op) == 0 || op.QueryFlag(Define.FLAG_IDENTIFIED))
        {
            identified = 1;
        }
        else
        {
            identified = 0;
        }

        switch (op.Type)
        {
            case Define.BOW:
            case Define.ARROW:
            case Define.WAND:
            case Define.ROD:
            case Define.HORN:
            case Define.WEAPON:
            case Define.ARMOUR:
            case Define.HELMET:
            case Define.SHIELD:
            case Define.BOOTS:
            case Define.GLOVES:
            case Define.GIRDLE:
            case Define.BRACERS:
            case Define.CLOAK:
                break; /* We have more information to do below this switch */

            case Define.FOOD:
            case Define.FLESH:
            case Define.DRINK:
                if (identified != 0 || op.QueryFlag(Define.FLAG_BEEN_APPLIED))
                {
                    retbuf.Append("(food+").Append(op.Stats.Food.ToString(CultureInfo.InvariantCulture)).Append(')');

                    if (op.Type == Define.FLESH && op.LastEat > 0 && AtnrIsDragonEnabled(op.LastEat) != 0)
                    {
                        retbuf.Append('(').Append(Tables.ChangeResistMsg[op.LastEat]).Append(" metabolism)");
                    }

                    if (!op.QueryFlag(Define.FLAG_CURSED))
                    {
                        if (op.Stats.Hp != 0)
                        {
                            retbuf.Append("(heals)");
                        }
                        if (op.Stats.Sp != 0)
                        {
                            retbuf.Append("(spellpoint regen)");
                        }
                    }
                    else
                    {
                        if (op.Stats.Hp != 0)
                        {
                            retbuf.Append("(damages)");
                        }
                        if (op.Stats.Sp != 0)
                        {
                            retbuf.Append("(spellpoint depletion)");
                        }
                    }
                }
                break;

            case Define.SKILL:
            case Define.RING:
            case Define.AMULET:
                /* FULL_RING_DESCRIPTION */
                if (op.Title != null)
                {
                    retbuf.Append(RingDesc(op));
                }
                return retbuf.ToString();

            default:
                return retbuf.ToString();
        }

        /* Down here, we more further describe equipment type items.
         * only describe them if they have been identified or the like.
         */
        if (identified != 0 || op.QueryFlag(Define.FLAG_BEEN_APPLIED))
        {
            for (int attr = 0; attr < 7; attr++)
            {
                int val = GetAttrValue(op.Stats, attr);
                if (val != 0)
                {
                    retbuf.Append('(').Append(Tables.ShortStatName[attr]).Append(CStr.Plus(val)).Append(')');
                }
            }

            if (op.Stats.Exp != 0)
            {
                retbuf.Append("(speed ").Append(CStr.Plus(op.Stats.Exp)).Append(')');
            }

            switch (op.Type)
            {
                case Define.BOW:
                case Define.ARROW:
                case Define.GIRDLE:
                case Define.HELMET:
                case Define.SHIELD:
                case Define.BOOTS:
                case Define.GLOVES:
                case Define.WEAPON:
                case Define.SKILL:
                case Define.RING:
                case Define.AMULET:
                case Define.ARMOUR:
                case Define.BRACERS:
                case Define.FORCE:
                case Define.CLOAK:
                    if (op.Stats.Wc != 0)
                    {
                        retbuf.Append("(wc").Append(CStr.Plus(op.Stats.Wc)).Append(')');
                    }
                    if (op.Stats.Dam != 0)
                    {
                        retbuf.Append("(dam").Append(CStr.Plus(op.Stats.Dam)).Append(')');
                    }
                    if (op.Stats.Ac != 0)
                    {
                        retbuf.Append("(ac").Append(CStr.Plus(op.Stats.Ac)).Append(')');
                    }
                    if (op.Type == Define.WEAPON && op.Level > 0)
                    {
                        retbuf.Append("(improved ").Append(op.LastEat.ToString(CultureInfo.InvariantCulture)).Append('/')
                            .Append(op.Level.ToString(CultureInfo.InvariantCulture)).Append(')');
                    }
                    break;

                default:
                    break;
            }
            if (op.QueryFlag(Define.FLAG_XRAYS))
            {
                retbuf.Append("(xray-vision)");
            }
            if (op.QueryFlag(Define.FLAG_FLYING))
            {
                retbuf.Append("(levitate)");
            }
            if (op.QueryFlag(Define.FLAG_SEE_IN_DARK))
            {
                retbuf.Append("(infravision)");
            }
        } /* End if identified or applied */

        /* This blocks only deals with fully identified object.
         * it is intentional that this is not an 'else' from a above -
         * in this way, information is added.
         */
        if (identified != 0)
        {
            int moreInfo = 0;

            switch (op.Type)
            {
                case Define.ROD: /* These use stats.sp for spell selection and stats.food */
                case Define.HORN: /* and stats.hp for spell-point regeneration... */
                case Define.BOW:
                case Define.ARROW:
                case Define.WAND:
                case Define.FOOD:
                case Define.FLESH:
                case Define.DRINK:
                    moreInfo = 0;
                    break;

                /* Armor type objects */
                case Define.ARMOUR:
                case Define.HELMET:
                case Define.SHIELD:
                case Define.BOOTS:
                case Define.GLOVES:
                case Define.GIRDLE:
                case Define.BRACERS:
                case Define.CLOAK:
                    if (op.LastSp != 0)
                    {
                        retbuf.Append("(Max speed ").Append(CStr.FormatFixed(op.LastSp / 10.0, 2)).Append(')');
                    }
                    /* Do this in all cases - otherwise it gets confusing - does that
                     * item have no penality, or is it not fully identified for example.
                     */
                    retbuf.Append("(Spell regen penalty ").Append(op.LastHeal.ToString(CultureInfo.InvariantCulture)).Append(')');
                    moreInfo = 1;
                    break;

                case Define.WEAPON:
                    {
                        /* Calculate it the same way fix_player does so the results
                         * make sense.
                         */
                        int i = unchecked(op.LastSp * 2 - op.Magic) / 2;
                        if (i < 0)
                        {
                            i = 0;
                        }

                        retbuf.Append("(weapon speed ").Append(i.ToString(CultureInfo.InvariantCulture)).Append(')');
                        moreInfo = 1;
                        break;
                    }
            }
            if (moreInfo != 0)
            {
                if (op.Stats.Food != 0)
                {
                    retbuf.Append("(sustenance").Append(CStr.Plus(op.Stats.Food)).Append(')');
                }
                if (op.Stats.Grace != 0)
                {
                    retbuf.Append("(grace").Append(CStr.Plus(op.Stats.Grace)).Append(')');
                }
                if (op.Stats.Sp != 0)
                {
                    retbuf.Append("(magic").Append(CStr.Plus(op.Stats.Sp)).Append(')');
                }
                if (op.Stats.Hp != 0)
                {
                    retbuf.Append("(regeneration").Append(CStr.Plus(op.Stats.Hp)).Append(')');
                }
            }

            if (op.Stats.Luck != 0)
            {
                retbuf.Append("(luck").Append(CStr.Plus(op.Stats.Luck)).Append(')');
            }
            if (op.QueryFlag(Define.FLAG_LIFESAVE))
            {
                retbuf.Append("(lifesaving)");
            }
            if (op.QueryFlag(Define.FLAG_REFL_SPELL))
            {
                retbuf.Append("(reflect spells)");
            }
            if (op.QueryFlag(Define.FLAG_REFL_MISSILE))
            {
                retbuf.Append("(reflect missiles)");
            }
            if (op.QueryFlag(Define.FLAG_STEALTH))
            {
                retbuf.Append("(stealth)");
            }
            if (op.Slaying != null)
            {
                retbuf.Append("(slay ").Append(op.Slaying).Append(')');
            }
            DescribeAbility(retbuf, op.Attacktype, "Attacks");
            /* resistance on flesh is only visible for quetzals */
            if (op.Type != Define.FLESH || op.QueryFlag(Define.FLAG_SEE_INVISIBLE))
            {
                retbuf.Append(DescribeResistance(op, false));
            }
            DescribePath(retbuf, op.PathAttuned, "Attuned");
            DescribePath(retbuf, op.PathRepelled, "Repelled");
            DescribePath(retbuf, op.PathDenied, "Denied");
        }

        return retbuf.ToString();
    }

    /// <summary>
    /// common/item.c <c>is_magical</c>: true (1) when the item is magical: it changes abilities, gives a resistance, has a magic
    /// bonus, a title (an artifact) or is of a type that is always magical. A living creature is not.
    /// </summary>
    public static int IsMagical(CfObject op)
    {
        int i;

        /* living creatures are considered non magical */
        if (op.QueryFlag(Define.FLAG_ALIVE))
        {
            return 0;
        }

        /* This is a test for it being an artifact, as artifacts have titles */
        if (op.Title != null)
        {
            return 1;
        }

        /* Handle rings and amulets specially.  If they change any of these
         * values, it means they are magical.
         */
        if ((op.Type == Define.AMULET || op.Type == Define.RING)
            && (op.Stats.Ac != 0 || op.Stats.Food != 0 || op.Stats.Exp != 0 || op.Stats.Dam != 0
                || op.Stats.Wc != 0 || op.Stats.Sp != 0 || op.Stats.Hp != 0 || op.Stats.Luck != 0))
        {
            return 1;
        }

        /* Check for stealty, speed, flying, or just plain magic in the boots */
        if (op.Type == Define.BOOTS
            && (op.QueryFlag(Define.FLAG_STEALTH) || op.QueryFlag(Define.FLAG_FLYING) || op.Stats.Exp != 0))
        {
            return 1;
        }

        /* Take care of amulet/shield that reflects spells/missiles */
        if ((op.Type == Define.AMULET || op.Type == Define.SHIELD)
            && (op.QueryFlag(Define.FLAG_REFL_SPELL) || op.QueryFlag(Define.FLAG_REFL_MISSILE)))
        {
            return 1;
        }

        /* Take care of helmet of xrays */
        if (op.Type == Define.HELMET && op.QueryFlag(Define.FLAG_XRAYS))
        {
            return 1;
        }

        /* Potions & rods are always magical.  Wands/staves are also magical,
         * assuming they still have any charges left.
         */
        if (op.Type == Define.POTION || op.Type == Define.ROD || (op.Type == Define.WAND && op.Stats.Food != 0))
        {
            return 1;
        }

        /* if something gives a protection, either positive or negative, its magical */
        for (i = Define.ATNR_PHYSICAL + 1; i < Define.NROFATTACKS; i++)
        {
            if (op.Resist[i] != 0)
            {
                return 1;
            }
        }

        /* Physical protection is expected on some item types, so they should
         * not be considered magical.
         */
        if (op.Resist[Define.ATNR_PHYSICAL] != 0 && op.Type != Define.HELMET && op.Type != Define.SHIELD
            && op.Type != Define.BOOTS && op.Type != Define.GLOVES && op.Type != Define.ARMOUR)
        {
            return 1;
        }

        /* power crystal, spellbooks, and scrolls are always magical.  */
        if (op.Magic != 0 || op.Type == Define.POWER_CRYSTAL || op.Type == Define.SPELLBOOK
            || op.Type == Define.SCROLL || op.Type == Define.GIRDLE)
        {
            return 1;
        }

        /* Check to see if it increases/decreases any stats */
        for (i = 0; i < 7; i++)
        {
            if (GetAttrValue(op.Stats, i) != 0)
            {
                return 1;
            }
        }

        /* If it doesn't fall into any of the above categories, must
         * be non magical.
         */
        return 0;
    }

    /// <summary>
    /// common/item.c <c>need_identify</c>: true (1) when the item is of a type that has to be identified (the list of
    /// the types in the C function).
    /// </summary>
    public static int NeedIdentify(CfObject op)
    {
        switch (op.Type)
        {
            case Define.RING:
            case Define.WAND:
            case Define.ROD:
            case Define.HORN:
            case Define.SCROLL:
            case Define.SKILL:
            case Define.SKILLSCROLL:
            case Define.SPELLBOOK:
            case Define.FOOD:
            case Define.POTION:
            case Define.BOW:
            case Define.ARROW:
            case Define.WEAPON:
            case Define.ARMOUR:
            case Define.SHIELD:
            case Define.HELMET:
            case Define.AMULET:
            case Define.BOOTS:
            case Define.GLOVES:
            case Define.BRACERS:
            case Define.GIRDLE:
            case Define.CONTAINER:
            case Define.DRINK:
            case Define.FLESH:
            case Define.INORGANIC:
            case Define.CLOSE_CON:
            case Define.CLOAK:
            case Define.GEM:
            case Define.POWER_CRYSTAL:
            case Define.POISON:
            case Define.BOOK:
                return 1;
        }
        return 0;
    }

    /// <summary>
    /// common/item.c <c>look_up_spell_name</c>: the number of the spell whose name is <paramref name="spname"/> (the host's
    /// spell table, <see cref="CrossfireHost.SpellName"/>), or -1.
    /// </summary>
    public int LookUpSpellName(string spname)
    {
        for (int i = 0; i < Define.NROFREALSPELLS; i++)
        {
            string? name = Host.SpellName(i);
            if (name != null && CStr.Strcmp(spname, name) == 0)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// common/item.c <c>identify</c>: marks the object identified (and no longer known as magical), known as cursed when it is,
    /// gives a potion the name of its archetype and a spellbook its spell (or the name "Spell forumla for ..." when the spell is
    /// unknown), then tells the map (<c>update_object</c>, <see cref="CrossfireHost.UpdateObject"/>) or the player that carries
    /// it (<c>esrv_send_item</c>, <see cref="CrossfireHost.EsrvSendItem"/>).
    /// </summary>
    public void Identify(CfObject op)
    {
        op.SetFlag(Define.FLAG_IDENTIFIED);
        op.ClearFlag(Define.FLAG_KNOWN_MAGICAL);
        op.ClearFlag(Define.FLAG_NO_SKILL_IDENT);

        /* We want autojoining of equal objects: */
        if (op.QueryFlag(Define.FLAG_CURSED) || op.QueryFlag(Define.FLAG_DAMNED))
        {
            op.SetFlag(Define.FLAG_KNOWN_CURSED);
        }

        if (op.Type == Define.POTION && op.Arch != null)
        {
            op.Name = op.Arch.Clone.Name;
        }
        else if (op.Type == Define.SPELLBOOK && op.Slaying != null)
        {
            op.Stats.Sp = unchecked((short)LookUpSpellName(op.Slaying));
            if (op.Stats.Sp < 0)
            {
                op.Stats.Sp = -1;
                op.Name = "Spell forumla for " + op.Slaying;
            }
            else
            {
                /* clear op->slaying since we no longer need it */
                op.Slaying = null;
            }
        }

        if (op.Map != null)
        {
            /* The shop identifies items before they hit the ground */
            Host.UpdateObject(op, Define.UP_OBJ_FACE);
        }
        else
        {
            CfObject? pl = IsPlayerInv(op.Env);
            if (pl != null)
            {
                /* A lot of the values can change from an update - might as well send it all. */
                Host.EsrvSendItem(pl, op);
            }
        }
    }
}
