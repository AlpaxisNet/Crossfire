// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): include/attack.h, common/item.c, common/living.c, include/spellist.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     include/attack.h:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     common/item.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     common/living.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/spellist.h:
//         Copyright (C) 1994 Mark Wedel
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

/// <summary>common/item.c: <c>materialtype</c>, one entry of the materials table (index = the number of the bit in M_...).</summary>
public sealed class MaterialType
{
    /// <summary>material.h: name.</summary>
    public string Name { get; }

    /// <summary>material.h: save[NROFATTACKS], the chance of the material to survive an attack of each type.</summary>
    public sbyte[] Save { get; }

    internal MaterialType(string name, params sbyte[] save)
    {
        Name = name;
        Save = new sbyte[Define.NROFATTACKS];
        Array.Copy(save, Save, save.Length);
    }
}

/// <summary>
/// The tables that the object code and the loader use: include/attack.h (names of the attack types), common/item.c (materials),
/// common/living.c (stat and attack names), include/spellist.h (names of the spell paths).
/// </summary>
public static class Tables
{
    /// <summary>attack.h: resist_save, the suffix of "resist_" when an object is saved (and the loader's keywords).</summary>
    public static readonly string[] ResistSave =
    {
        "physical", "magic", "fire", "electricity", "cold", "confusion", "acid",
        "drain", "weaponmagic", "ghosthit", "poison", "slow", "paralyze",
        "turn_undead", "fear", "cancellation", "deplete", "death", "chaos",
        "counterspell", "godpower", "holyword", "blind", "internal", "life_stealing",
    };

    /// <summary>attack.h: attacktype_desc, short descriptions of the attack types.</summary>
    public static readonly string[] AttacktypeDesc =
    {
        "physical", "magic", "fire", "electricity", "cold", "confusion", "acid",
        "drain", "weapon magic", "ghost hit", "poison", "slow", "paralyze",
        "turn undead", "fear", "cancellation", "deplete", "death", "chaos",
        "counterspell", "god power", "holy word", "blind", "internal", "life stealing",
    };

    /// <summary>attack.h: resist_plus, what the player sees of a resistance.</summary>
    public static readonly string[] ResistPlus =
    {
        "armour", "resist magic", "resist fire", "resist electricity", "resist cold",
        "resist confusion", "resist acid", "resist drain",
        "resist weaponmagic", "resist ghosthit", "resist poison", "resist slow",
        "resist paralyzation", "resist turn undead", "resist fear",
        "resist cancellation", "resist depletion", "resist death", "resist chaos",
        "resist counterspell", "resist god power", "resist holy word",
        "resist blindness", "resist internal", "resist life stealing",
    };

    /// <summary>attack.h: change_resist_msg, the descriptions shown when a player puts on or takes off an item.</summary>
    public static readonly string[] ChangeResistMsg =
    {
        "physical", "magic", "fire", "electricity", "cold", "confusion", "acid",
        "draining", "weapon magic", "ghosts", "poison", "slow", "paralyze",
        "turn undead", "fear", "cancellation", "depletion", "death attacks", "chaos",
        "counterspell", "god power", "holy word", "blinding attacks", "internal",
        "life stealing",
    };

    /// <summary>attack.h: resist_table, the resistances a random item may get (an attack number can be there more than once to weigh it).</summary>
    public static readonly int[] ResistTable =
    {
        Define.ATNR_PHYSICAL, Define.ATNR_MAGIC, Define.ATNR_FIRE,
        Define.ATNR_ELECTRICITY, Define.ATNR_COLD, Define.ATNR_CONFUSION, Define.ATNR_ACID, Define.ATNR_DRAIN,
        Define.ATNR_GHOSTHIT, Define.ATNR_POISON, Define.ATNR_SLOW, Define.ATNR_PARALYZE, Define.ATNR_TURN_UNDEAD,
        Define.ATNR_FEAR, Define.ATNR_DEPLETE, Define.ATNR_DEATH, Define.ATNR_HOLYWORD, Define.ATNR_BLIND, Define.ATNR_LIFE_STEALING,
    };

    /// <summary>attack.h: num_resist_table.</summary>
    public const int NumResistTable = 19;

    /// <summary>living.c: attacks, the names of the attack types in descriptions.</summary>
    public static readonly string[] Attacks =
    {
        "physical", "magical", "fire", "electricity", "cold", "confusion",
        "acid", "drain", "weaponmagic", "ghosthit", "poison", "slow",
        "paralyze", "turn undead", "fear", "cancellation", "depletion", "death",
        "chaos", "counterspell", "god power", "holy power", "blinding", "",
        "life stealing",
    };

    /// <summary>living.c: statname.</summary>
    public static readonly string[] Statname =
    {
        "strength", "dexterity", "constitution", "wisdom", "charisma", "intelligence", "power",
    };

    /// <summary>living.c: short_stat_name.</summary>
    public static readonly string[] ShortStatName = { "Str", "Dex", "Con", "Wis", "Cha", "Int", "Pow" };

    /// <summary>living.c: cha_bonus (a C <c>float</c> table): the price factor of a charisma of 0 to MAX_STAT.</summary>
    public static readonly float[] ChaBonus =
    {
        10.0f, 10.0f, 9.0f, 8.0f, 7.0f, 6.0f,
        5.0f, 4.5f, 4.0f, 3.5f, 3.0f, 2.9f, 2.8f, 2.7f, 2.6f, 2.5f,
        2.4f, 2.3f, 2.2f, 2.1f, 2.0f, 1.95f, 1.90f, 1.85f, 1.80f, 1.75f,
        1.70f, 1.65f, 1.60f, 1.55f, 1.50f,
    };

    /// <summary>spellist.h: spellpathnames.</summary>
    public static readonly string[] Spellpathnames =
    {
        "Protection", "Fire", "Frost", "Electricity", "Missiles", "Self", "Summoning", "Abjuration", "Restoration",
        "Detonation", "Mind", "Creation", "Teleportation", "Information", "Transmutation", "Transferrence", "Turning",
        "Wounding", "Death", "Light",
    };

    /// <summary>item.c: material[NROFMATERIALS]. The columns of the C table are the attack numbers 0 to 23; the last is 0.</summary>
    public static readonly MaterialType[] Material =
    {
        new("paper", 15, 10, 17, 9, 5, 7, 13, 0, 20, 15, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0),
        new("metal", 2, 12, 3, 12, 2, 10, 7, 0, 20, 15, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0),
        new("glass", 14, 11, 8, 3, 10, 5, 1, 0, 20, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        new("leather", 5, 10, 10, 3, 3, 10, 10, 0, 20, 15, 0, 0, 0, 0, 0, 12, 0, 0, 0, 0, 0, 0, 0, 0),
        new("wood", 10, 11, 13, 2, 2, 10, 9, 0, 20, 15, 0, 0, 0, 0, 0, 12, 0, 0, 0, 0, 0, 0, 0, 0),
        new("organics", 3, 12, 9, 11, 3, 10, 9, 0, 20, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        new("stone", 2, 5, 2, 2, 2, 2, 1, 0, 20, 15, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0),
        new("cloth", 14, 11, 13, 4, 4, 5, 10, 0, 20, 15, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0),
        new("adamant", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        new("liquid", 0, 8, 9, 6, 17, 0, 15, 0, 20, 15, 12, 0, 0, 0, 0, 11, 0, 0, 0, 0, 0, 0, 0, 0),
        new("soft metal", 6, 12, 6, 14, 2, 10, 1, 0, 20, 15, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0),
        new("bone", 10, 9, 4, 5, 3, 10, 10, 0, 20, 15, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0),
        new("ice", 14, 11, 16, 5, 0, 5, 6, 0, 20, 15, 0, 0, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, 0, 0),
    };
}
