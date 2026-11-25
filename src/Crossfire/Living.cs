// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): include/living.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     include/living.h:
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

/// <summary>
/// include/living.h <c>struct liv</c> (typedef <c>living</c>): the stats of an object. The field types are the C types
/// (<c>sint8</c> is <see cref="sbyte"/>, <c>sint16</c> is <see cref="short"/>, <c>sint32</c> is <see cref="int"/>).
/// </summary>
public sealed class Living
{
    /// <summary>living.h: Str.</summary>
    public sbyte Str;
    /// <summary>living.h: Dex.</summary>
    public sbyte Dex;
    /// <summary>living.h: Con.</summary>
    public sbyte Con;
    /// <summary>living.h: Wis.</summary>
    public sbyte Wis;
    /// <summary>living.h: Cha.</summary>
    public sbyte Cha;
    /// <summary>living.h: Int.</summary>
    public sbyte Int;
    /// <summary>living.h: Pow.</summary>
    public sbyte Pow;
    /// <summary>living.h: wc, the weapon class.</summary>
    public sbyte Wc;
    /// <summary>living.h: ac, the armour class.</summary>
    public sbyte Ac;
    /// <summary>living.h: hp, hit points.</summary>
    public short Hp;
    /// <summary>living.h: maxhp.</summary>
    public short Maxhp;
    /// <summary>living.h: sp, spell points.</summary>
    public short Sp;
    /// <summary>living.h: maxsp.</summary>
    public short Maxsp;
    /// <summary>living.h: grace.</summary>
    public short Grace;
    /// <summary>living.h: maxgrace.</summary>
    public short Maxgrace;
    /// <summary>living.h: exp, experience.</summary>
    public int Exp;
    /// <summary>living.h: food, how much food in the stomach (0 is starved).</summary>
    public short Food;
    /// <summary>living.h: dam, the damage this object does when hitting.</summary>
    public short Dam;
    /// <summary>living.h: luck.</summary>
    public sbyte Luck;

    /// <summary>The C <c>memcpy</c> of a <c>living</c>.</summary>
    public void CopyFrom(Living o)
    {
        Str = o.Str;
        Dex = o.Dex;
        Con = o.Con;
        Wis = o.Wis;
        Cha = o.Cha;
        Int = o.Int;
        Pow = o.Pow;
        Wc = o.Wc;
        Ac = o.Ac;
        Hp = o.Hp;
        Maxhp = o.Maxhp;
        Sp = o.Sp;
        Maxsp = o.Maxsp;
        Grace = o.Grace;
        Maxgrace = o.Maxgrace;
        Exp = o.Exp;
        Food = o.Food;
        Dam = o.Dam;
        Luck = o.Luck;
    }

    /// <summary>The C <c>memcmp(&amp;a, &amp;b, sizeof(living)) == 0</c> as used by CAN_MERGE (the padding of the C struct is not compared).</summary>
    public bool SameAs(Living o) =>
        Str == o.Str && Dex == o.Dex && Con == o.Con && Wis == o.Wis && Cha == o.Cha && Int == o.Int && Pow == o.Pow
        && Wc == o.Wc && Ac == o.Ac && Hp == o.Hp && Maxhp == o.Maxhp && Sp == o.Sp && Maxsp == o.Maxsp && Grace == o.Grace
        && Maxgrace == o.Maxgrace && Exp == o.Exp && Food == o.Food && Dam == o.Dam && Luck == o.Luck;

    /// <summary>Sets every stat to 0 (<c>clear_object</c> clears them one by one).</summary>
    public void Clear() => CopyFrom(new Living());
}
