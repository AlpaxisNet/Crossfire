// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): include/define.h, include/skills.h, common/living.c, common/player.c, common/porting.c.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     include/define.h:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/skills.h:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     common/living.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     common/player.c:
//         Copyright (C) 2001 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     common/porting.c:
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
using System.Text;

namespace Crossfire;

public static partial class Define
{
    /// <summary>include/skills.h <c>enum skillnrs</c>: SK_STEALING.</summary>
    public const int SK_STEALING = 0;
    /// <summary>include/skills.h SK_LOCKPICKING.</summary>
    public const int SK_LOCKPICKING = 1;
    /// <summary>include/skills.h SK_HIDING.</summary>
    public const int SK_HIDING = 2;
    /// <summary>include/skills.h SK_SMITH.</summary>
    public const int SK_SMITH = 3;
    /// <summary>include/skills.h SK_BOWYER.</summary>
    public const int SK_BOWYER = 4;
    /// <summary>include/skills.h SK_JEWELER.</summary>
    public const int SK_JEWELER = 5;
    /// <summary>include/skills.h SK_ALCHEMY.</summary>
    public const int SK_ALCHEMY = 6;
    /// <summary>include/skills.h SK_THAUMATURGY.</summary>
    public const int SK_THAUMATURGY = 7;
    /// <summary>include/skills.h SK_LITERACY.</summary>
    public const int SK_LITERACY = 8;
    /// <summary>include/skills.h SK_BARGAINING: sells equip at Cha + level-based bonus (30 max).</summary>
    public const int SK_BARGAINING = 9;
    /// <summary>include/skills.h SK_JUMPING.</summary>
    public const int SK_JUMPING = 10;
    /// <summary>include/skills.h SK_DET_MAGIC.</summary>
    public const int SK_DET_MAGIC = 11;
    /// <summary>include/skills.h SK_ORATORY.</summary>
    public const int SK_ORATORY = 12;
    /// <summary>include/skills.h SK_MUSIC.</summary>
    public const int SK_MUSIC = 13;
    /// <summary>include/skills.h SK_DET_CURSE.</summary>
    public const int SK_DET_CURSE = 14;
    /// <summary>include/skills.h SK_FIND_TRAPS.</summary>
    public const int SK_FIND_TRAPS = 15;
    /// <summary>include/skills.h SK_MEDITATION.</summary>
    public const int SK_MEDITATION = 16;
    /// <summary>include/skills.h SK_BOXING.</summary>
    public const int SK_BOXING = 17;
    /// <summary>include/skills.h SK_FLAME_TOUCH.</summary>
    public const int SK_FLAME_TOUCH = 18;
    /// <summary>include/skills.h SK_KARATE.</summary>
    public const int SK_KARATE = 19;
    /// <summary>include/skills.h SK_CLIMBING.</summary>
    public const int SK_CLIMBING = 20;
    /// <summary>include/skills.h SK_WOODSMAN.</summary>
    public const int SK_WOODSMAN = 21;
    /// <summary>include/skills.h SK_INSCRIPTION.</summary>
    public const int SK_INSCRIPTION = 22;
    /// <summary>include/skills.h SK_MELEE_WEAPON.</summary>
    public const int SK_MELEE_WEAPON = 23;
    /// <summary>include/skills.h SK_MISSILE_WEAPON.</summary>
    public const int SK_MISSILE_WEAPON = 24;
    /// <summary>include/skills.h SK_THROWING.</summary>
    public const int SK_THROWING = 25;
    /// <summary>include/skills.h SK_SPELL_CASTING.</summary>
    public const int SK_SPELL_CASTING = 26;
    /// <summary>include/skills.h SK_REMOVE_TRAP.</summary>
    public const int SK_REMOVE_TRAP = 27;
    /// <summary>include/skills.h SK_SET_TRAP.</summary>
    public const int SK_SET_TRAP = 28;
    /// <summary>include/skills.h SK_USE_MAGIC_ITEM.</summary>
    public const int SK_USE_MAGIC_ITEM = 29;
    /// <summary>include/skills.h SK_PRAYING.</summary>
    public const int SK_PRAYING = 30;
    /// <summary>include/skills.h SK_CLAWING.</summary>
    public const int SK_CLAWING = 31;
    /// <summary>include/skills.h SK_LEVITATION.</summary>
    public const int SK_LEVITATION = 32;
}

public sealed partial class CfWorld
{
    /// <summary>
    /// include/define.h <c>safe_strcat(dest, orig, &amp;curlen, maxlen)</c>, on the string that the C buffer holds. Like the C
    /// function it appends <paramref name="orig"/> at <paramref name="curlen"/> cut to the room the buffer has (<c>maxlen-1</c>
    /// characters), adds the full length of <paramref name="orig"/> to <paramref name="curlen"/> and, when that is over
    /// <c>maxlen-1</c>, sets it to 1 (the C code has <c>*curlen=maxlen=1</c>, a typing slip that the port keeps: the next call
    /// then writes over the text from character 1 on). It does nothing when <paramref name="curlen"/> is <c>maxlen-1</c>.
    /// </summary>
    internal static void SafeStrcat(ref string dest, string orig, ref int curlen, int maxlen)
    {
        if (curlen == maxlen - 1)
        {
            return;
        }
        int room = Math.Max(0, maxlen - curlen - 1);
        string head = dest.Length > curlen ? dest.Substring(0, Math.Max(0, curlen)) : dest;
        dest = head + (orig.Length > room ? orig.Substring(0, room) : orig);
        curlen += orig.Length;
        if (curlen > maxlen - 1)
        {
            curlen = 1;
        }
    }

    /// <summary>
    /// include/define.h <c>DESCRIBE_ABILITY</c> (the unsafe form that <c>describe_item</c> uses): the names of the attack types
    /// of <paramref name="variable"/> in "(name: a, b)" form, appended to <paramref name="retbuf"/>; nothing when it is 0.
    /// </summary>
    internal static void DescribeAbility(StringBuilder retbuf, uint variable, string name)
    {
        if (variable == 0)
        {
            return;
        }
        bool j = false;
        retbuf.Append('(').Append(name).Append(": ");
        for (int i = 0; i < Define.NROFATTACKS; i++)
        {
            if ((variable & (1U << i)) != 0)
            {
                if (j)
                {
                    retbuf.Append(", ");
                }
                else
                {
                    j = true;
                }
                retbuf.Append(Tables.Attacks[i]);
            }
        }
        retbuf.Append(')');
    }

    /// <summary>include/define.h <c>DESCRIBE_PATH</c>: the names of the spell paths of <paramref name="variable"/> in "(name: a, b)" form.</summary>
    internal static void DescribePath(StringBuilder retbuf, uint variable, string name)
    {
        if (variable == 0)
        {
            return;
        }
        bool j = false;
        retbuf.Append('(').Append(name).Append(": ");
        for (int i = 0; i < Define.NRSPELLPATHS; i++)
        {
            if ((variable & (1U << i)) != 0)
            {
                if (j)
                {
                    retbuf.Append(", ");
                }
                else
                {
                    j = true;
                }
                retbuf.Append(Tables.Spellpathnames[i]);
            }
        }
        retbuf.Append(')');
    }

    /// <summary>
    /// include/define.h <c>DESCRIBE_PATH_SAFE</c>: as <see cref="DescribePath"/>, but every piece goes through
    /// <see cref="SafeStrcat"/> with the length <paramref name="len"/> that the caller keeps.
    /// </summary>
    internal static void DescribePathSafe(ref string retbuf, uint variable, string name, ref int len, int maxlen)
    {
        if (variable == 0)
        {
            return;
        }
        bool j = false;
        SafeStrcat(ref retbuf, "(" + name + ": ", ref len, maxlen);
        for (int i = 0; i < Define.NRSPELLPATHS; i++)
        {
            if ((variable & (1U << i)) != 0)
            {
                if (j)
                {
                    SafeStrcat(ref retbuf, ", ", ref len, maxlen);
                }
                else
                {
                    j = true;
                }
                SafeStrcat(ref retbuf, Tables.Spellpathnames[i], ref len, maxlen);
            }
        }
        SafeStrcat(ref retbuf, ")", ref len, maxlen);
    }

    /// <summary>common/living.c <c>get_attr_value</c>: the stat <paramref name="attr"/> (STR..POW) of <paramref name="stats"/>; 0 for any other number.</summary>
    public static sbyte GetAttrValue(Living stats, int attr)
    {
        switch (attr)
        {
            case Define.STR:
                return stats.Str;
            case Define.DEX:
                return stats.Dex;
            case Define.CON:
                return stats.Con;
            case Define.WIS:
                return stats.Wis;
            case Define.CHA:
                return stats.Cha;
            case Define.INT:
                return stats.Int;
            case Define.POW:
                return stats.Pow;
        }
        return 0;
    }

    /// <summary>
    /// common/porting.c <c>isqrt</c>: the integer square root by summing odd numbers; 0 for a negative number. The sum is a
    /// 64-bit number here (the C <c>int</c> would wrap for an argument near the limit of the type and loop; docs/differences.md).
    /// </summary>
    public static int Isqrt(int n)
    {
        int result = 0;
        long prev = 1;
        long sum = 1;
        while (sum <= n)
        {
            prev += 2;
            sum += prev;
            ++result;
        }
        return result;
    }

    /// <summary>
    /// common/player.c <c>find_skill</c>: the skill object with number <paramref name="skillnr"/> in the inventory of
    /// <paramref name="op"/>; the first one that is not invisible, else the last invisible one, else null.
    /// </summary>
    public static CfObject? FindSkill(CfObject op, int skillnr)
    {
        CfObject? skill1 = null;
        for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Type == Define.SKILL && tmp.Stats.Sp == skillnr)
            {
                if (tmp.Invisible == 0)
                {
                    return tmp;
                }
                skill1 = tmp;
            }
        }
        return skill1;
    }

    /// <summary>common/player.c <c>atnr_is_dragon_enabled</c>: the attack types a dragon player (quetzal) gains resistances for.</summary>
    public static int AtnrIsDragonEnabled(int attacknr)
    {
        if (attacknr == Define.ATNR_MAGIC || attacknr == Define.ATNR_FIRE || attacknr == Define.ATNR_ELECTRICITY
            || attacknr == Define.ATNR_COLD || attacknr == Define.ATNR_ACID || attacknr == Define.ATNR_POISON)
        {
            return 1;
        }
        return 0;
    }

    /// <summary>common/player.c <c>is_dragon_pl</c>: a player whose archetype's race is "dragon".</summary>
    public static int IsDragonPl(CfObject? op)
    {
        if (op != null && op.Type == Define.PLAYER && op.Arch != null && op.Arch.Clone.Race != null
            && CStr.Strcmp(op.Arch.Clone.Race, "dragon") == 0)
        {
            return 1;
        }
        return 0;
    }
}
