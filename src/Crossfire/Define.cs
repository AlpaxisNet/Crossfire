// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): include/define.h, include/attack.h, include/material.h, include/living.h, include/object.h, include/plugin.h, include/config.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     include/define.h:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/attack.h:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/material.h:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/living.h:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/object.h:
//         Copyright (C) 2001 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     include/plugin.h:
//         Copyright (C) 2001 Yann Chachkoff (Crossfire plugin support; placed under the GPL by the author)
//     include/config.h:
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
/// The constants of Crossfire: include/define.h (object types, flags, spell paths, pickup modes, movement), include/attack.h
/// (attack numbers and types), include/material.h, include/living.h (stats), include/object.h (insert and update codes),
/// include/plugin.h (events) and include/config.h (table sizes). The names are the C names.
/// </summary>
public static partial class Define
{

    // define.h: object types (type 0 is undefined and not valid)
    /// <summary>define.h PLAYER (object types (type 0 is undefined and not valid)).</summary>
    public const int PLAYER = 1;
    /// <summary>define.h BULLET (object types (type 0 is undefined and not valid)).</summary>
    public const int BULLET = 2;
    /// <summary>define.h ROD (object types (type 0 is undefined and not valid)).</summary>
    public const int ROD = 3;
    /// <summary>define.h TREASURE (object types (type 0 is undefined and not valid)).</summary>
    public const int TREASURE = 4;
    /// <summary>define.h POTION (object types (type 0 is undefined and not valid)).</summary>
    public const int POTION = 5;
    /// <summary>define.h FOOD (object types (type 0 is undefined and not valid)).</summary>
    public const int FOOD = 6;
    /// <summary>define.h POISON (object types (type 0 is undefined and not valid)).</summary>
    public const int POISON = 7;
    /// <summary>define.h BOOK (object types (type 0 is undefined and not valid)).</summary>
    public const int BOOK = 8;
    /// <summary>define.h CLOCK (object types (type 0 is undefined and not valid)).</summary>
    public const int CLOCK = 9;
    /// <summary>define.h FBULLET (object types (type 0 is undefined and not valid)).</summary>
    public const int FBULLET = 10;
    /// <summary>define.h FBALL (object types (type 0 is undefined and not valid)).</summary>
    public const int FBALL = 11;
    /// <summary>define.h LIGHTNING (object types (type 0 is undefined and not valid)).</summary>
    public const int LIGHTNING = 12;
    /// <summary>define.h ARROW (object types (type 0 is undefined and not valid)).</summary>
    public const int ARROW = 13;
    /// <summary>define.h BOW (object types (type 0 is undefined and not valid)).</summary>
    public const int BOW = 14;
    /// <summary>define.h WEAPON (object types (type 0 is undefined and not valid)).</summary>
    public const int WEAPON = 15;
    /// <summary>define.h ARMOUR (object types (type 0 is undefined and not valid)).</summary>
    public const int ARMOUR = 16;
    /// <summary>define.h PEDESTAL (object types (type 0 is undefined and not valid)).</summary>
    public const int PEDESTAL = 17;
    /// <summary>define.h ALTAR (object types (type 0 is undefined and not valid)).</summary>
    public const int ALTAR = 18;
    /// <summary>define.h CONFUSION (object types (type 0 is undefined and not valid)).</summary>
    public const int CONFUSION = 19;
    /// <summary>define.h LOCKED_DOOR (object types (type 0 is undefined and not valid)).</summary>
    public const int LOCKED_DOOR = 20;
    /// <summary>define.h SPECIAL_KEY (object types (type 0 is undefined and not valid)).</summary>
    public const int SPECIAL_KEY = 21;
    /// <summary>define.h MAP (object types (type 0 is undefined and not valid)).</summary>
    public const int MAP = 22;
    /// <summary>define.h DOOR (object types (type 0 is undefined and not valid)).</summary>
    public const int DOOR = 23;
    /// <summary>define.h KEY (object types (type 0 is undefined and not valid)).</summary>
    public const int KEY = 24;
    /// <summary>define.h MMISSILE (object types (type 0 is undefined and not valid)).</summary>
    public const int MMISSILE = 25;
    /// <summary>define.h TIMED_GATE (object types (type 0 is undefined and not valid)).</summary>
    public const int TIMED_GATE = 26;
    /// <summary>define.h TRIGGER (object types (type 0 is undefined and not valid)).</summary>
    public const int TRIGGER = 27;
    /// <summary>define.h GRIMREAPER (object types (type 0 is undefined and not valid)).</summary>
    public const int GRIMREAPER = 28;
    /// <summary>define.h MAGIC_EAR (object types (type 0 is undefined and not valid)).</summary>
    public const int MAGIC_EAR = 29;
    /// <summary>define.h TRIGGER_BUTTON (object types (type 0 is undefined and not valid)).</summary>
    public const int TRIGGER_BUTTON = 30;
    /// <summary>define.h TRIGGER_ALTAR (object types (type 0 is undefined and not valid)).</summary>
    public const int TRIGGER_ALTAR = 31;
    /// <summary>define.h TRIGGER_PEDESTAL (object types (type 0 is undefined and not valid)).</summary>
    public const int TRIGGER_PEDESTAL = 32;
    /// <summary>define.h SHIELD (object types (type 0 is undefined and not valid)).</summary>
    public const int SHIELD = 33;
    /// <summary>define.h HELMET (object types (type 0 is undefined and not valid)).</summary>
    public const int HELMET = 34;
    /// <summary>define.h HORN (object types (type 0 is undefined and not valid)).</summary>
    public const int HORN = 35;
    /// <summary>define.h MONEY (object types (type 0 is undefined and not valid)).</summary>
    public const int MONEY = 36;
    /// <summary>define.h CLASS (object types (type 0 is undefined and not valid)). object for applying character class modifications to someone</summary>
    public const int CLASS = 37;
    /// <summary>define.h GRAVESTONE (object types (type 0 is undefined and not valid)).</summary>
    public const int GRAVESTONE = 38;
    /// <summary>define.h AMULET (object types (type 0 is undefined and not valid)).</summary>
    public const int AMULET = 39;
    /// <summary>define.h PLAYERMOVER (object types (type 0 is undefined and not valid)).</summary>
    public const int PLAYERMOVER = 40;
    /// <summary>define.h TELEPORTER (object types (type 0 is undefined and not valid)).</summary>
    public const int TELEPORTER = 41;
    /// <summary>define.h CREATOR (object types (type 0 is undefined and not valid)).</summary>
    public const int CREATOR = 42;
    /// <summary>define.h SKILL (object types (type 0 is undefined and not valid)). Skills are similar to abilites, but</summary>
    public const int SKILL = 43;
    /// <summary>define.h EXPERIENCE (object types (type 0 is undefined and not valid)). An experience 'object'. Needed for multi-exp/skills</summary>
    public const int EXPERIENCE = 44;
    /// <summary>define.h EARTHWALL (object types (type 0 is undefined and not valid)).</summary>
    public const int EARTHWALL = 45;
    /// <summary>define.h GOLEM (object types (type 0 is undefined and not valid)).</summary>
    public const int GOLEM = 46;
    /// <summary>define.h BOMB (object types (type 0 is undefined and not valid)).</summary>
    public const int BOMB = 47;
    /// <summary>define.h THROWN_OBJ (object types (type 0 is undefined and not valid)).</summary>
    public const int THROWN_OBJ = 48;
    /// <summary>define.h BLINDNESS (object types (type 0 is undefined and not valid)).</summary>
    public const int BLINDNESS = 49;
    /// <summary>define.h GOD (object types (type 0 is undefined and not valid)).</summary>
    public const int GOD = 50;
    /// <summary>define.h DETECTOR (object types (type 0 is undefined and not valid)).</summary>
    public const int DETECTOR = 51;
    /// <summary>define.h SPEEDBALL (object types (type 0 is undefined and not valid)).</summary>
    public const int SPEEDBALL = 52;
    /// <summary>define.h DEAD_OBJECT (object types (type 0 is undefined and not valid)).</summary>
    public const int DEAD_OBJECT = 53;
    /// <summary>define.h DRINK (object types (type 0 is undefined and not valid)).</summary>
    public const int DRINK = 54;
    /// <summary>define.h MARKER (object types (type 0 is undefined and not valid)). inserts an invisible, weightless</summary>
    public const int MARKER = 55;
    /// <summary>define.h HOLY_ALTAR (object types (type 0 is undefined and not valid)).</summary>
    public const int HOLY_ALTAR = 56;
    /// <summary>define.h PLAYER_CHANGER (object types (type 0 is undefined and not valid)).</summary>
    public const int PLAYER_CHANGER = 57;
    /// <summary>define.h BATTLEGROUND (object types (type 0 is undefined and not valid)). battleground, by Andreas Vogl</summary>
    public const int BATTLEGROUND = 58;
    /// <summary>define.h PEACEMAKER (object types (type 0 is undefined and not valid)). Object owned by a player which can convert</summary>
    public const int PEACEMAKER = 59;
    /// <summary>define.h GEM (object types (type 0 is undefined and not valid)).</summary>
    public const int GEM = 60;
    /// <summary>define.h FIRECHEST (object types (type 0 is undefined and not valid)).</summary>
    public const int FIRECHEST = 61;
    /// <summary>define.h FIREWALL (object types (type 0 is undefined and not valid)).</summary>
    public const int FIREWALL = 62;
    /// <summary>define.h ANVIL (object types (type 0 is undefined and not valid)).</summary>
    public const int ANVIL = 63;
    /// <summary>define.h CHECK_INV (object types (type 0 is undefined and not valid)). by b.t. thomas@nomad.astro.psu.edu</summary>
    public const int CHECK_INV = 64;
    /// <summary>define.h MOOD_FLOOR (object types (type 0 is undefined and not valid)). by b.t. thomas@nomad.astro.psu.edu</summary>
    public const int MOOD_FLOOR = 65;
    /// <summary>define.h EXIT (object types (type 0 is undefined and not valid)).</summary>
    public const int EXIT = 66;
    /// <summary>define.h ENCOUNTER (object types (type 0 is undefined and not valid)).</summary>
    public const int ENCOUNTER = 67;
    /// <summary>define.h SHOP_FLOOR (object types (type 0 is undefined and not valid)).</summary>
    public const int SHOP_FLOOR = 68;
    /// <summary>define.h SHOP_MAT (object types (type 0 is undefined and not valid)).</summary>
    public const int SHOP_MAT = 69;
    /// <summary>define.h RING (object types (type 0 is undefined and not valid)).</summary>
    public const int RING = 70;
    /// <summary>define.h FLOOR (object types (type 0 is undefined and not valid)). this is a floor tile -&gt; native layer 0</summary>
    public const int FLOOR = 71;
    /// <summary>define.h FLESH (object types (type 0 is undefined and not valid)). animal 'body parts' -b.t.</summary>
    public const int FLESH = 72;
    /// <summary>define.h INORGANIC (object types (type 0 is undefined and not valid)). metals and minerals</summary>
    public const int INORGANIC = 73;
    /// <summary>define.h LIGHTER (object types (type 0 is undefined and not valid)).</summary>
    public const int LIGHTER = 75;
    /// <summary>define.h TRAP_PART (object types (type 0 is undefined and not valid)). Needed by set traps skill -b.t.</summary>
    public const int TRAP_PART = 76;
    /// <summary>define.h WALL (object types (type 0 is undefined and not valid)). this is a wall. put it always in layer 1 if not set is_floor</summary>
    public const int WALL = 77;
    /// <summary>define.h LIGHT_SOURCE (object types (type 0 is undefined and not valid)). torches, lamps, etc.</summary>
    public const int LIGHT_SOURCE = 78;
    /// <summary>define.h MISC_OBJECT (object types (type 0 is undefined and not valid)). misc. objects are for objects without a function</summary>
    public const int MISC_OBJECT = 79;
    /// <summary>define.h MONSTER (object types (type 0 is undefined and not valid)). yes, thats a real, living creature</summary>
    public const int MONSTER = 80;
    /// <summary>define.h SPAWN_GENERATOR (object types (type 0 is undefined and not valid)). a spawn point or monster generator object</summary>
    public const int SPAWN_GENERATOR = 81;
    /// <summary>define.h SPELLBOOK (object types (type 0 is undefined and not valid)).</summary>
    public const int SPELLBOOK = 85;
    /// <summary>define.h CLOAK (object types (type 0 is undefined and not valid)).</summary>
    public const int CLOAK = 87;
    /// <summary>define.h CONE (object types (type 0 is undefined and not valid)).</summary>
    public const int CONE = 88;
    /// <summary>define.h AURA (object types (type 0 is undefined and not valid)). aura spell object</summary>
    public const int AURA = 89;
    /// <summary>define.h SPINNER (object types (type 0 is undefined and not valid)).</summary>
    public const int SPINNER = 90;
    /// <summary>define.h GATE (object types (type 0 is undefined and not valid)).</summary>
    public const int GATE = 91;
    /// <summary>define.h BUTTON (object types (type 0 is undefined and not valid)).</summary>
    public const int BUTTON = 92;
    /// <summary>define.h CF_HANDLE (object types (type 0 is undefined and not valid)).</summary>
    public const int CF_HANDLE = 93;
    /// <summary>define.h HOLE (object types (type 0 is undefined and not valid)).</summary>
    public const int HOLE = 94;
    /// <summary>define.h TRAPDOOR (object types (type 0 is undefined and not valid)).</summary>
    public const int TRAPDOOR = 95;
    /// <summary>define.h WORD_OF_RECALL (object types (type 0 is undefined and not valid)).</summary>
    public const int WORD_OF_RECALL = 96;
    /// <summary>define.h PARAIMAGE (object types (type 0 is undefined and not valid)).</summary>
    public const int PARAIMAGE = 97;
    /// <summary>define.h SIGN (object types (type 0 is undefined and not valid)).</summary>
    public const int SIGN = 98;
    /// <summary>define.h BOOTS (object types (type 0 is undefined and not valid)).</summary>
    public const int BOOTS = 99;
    /// <summary>define.h GLOVES (object types (type 0 is undefined and not valid)).</summary>
    public const int GLOVES = 100;
    /// <summary>define.h CONVERTER (object types (type 0 is undefined and not valid)).</summary>
    public const int CONVERTER = 103;
    /// <summary>define.h BRACERS (object types (type 0 is undefined and not valid)).</summary>
    public const int BRACERS = 104;
    /// <summary>define.h POISONING (object types (type 0 is undefined and not valid)).</summary>
    public const int POISONING = 105;
    /// <summary>define.h SAVEBED (object types (type 0 is undefined and not valid)).</summary>
    public const int SAVEBED = 106;
    /// <summary>define.h POISONCLOUD (object types (type 0 is undefined and not valid)).</summary>
    public const int POISONCLOUD = 107;
    /// <summary>define.h FIREHOLES (object types (type 0 is undefined and not valid)).</summary>
    public const int FIREHOLES = 108;
    /// <summary>define.h WAND (object types (type 0 is undefined and not valid)).</summary>
    public const int WAND = 109;
    /// <summary>define.h ABILITY (object types (type 0 is undefined and not valid)).</summary>
    public const int ABILITY = 110;
    /// <summary>define.h SCROLL (object types (type 0 is undefined and not valid)).</summary>
    public const int SCROLL = 111;
    /// <summary>define.h DIRECTOR (object types (type 0 is undefined and not valid)).</summary>
    public const int DIRECTOR = 112;
    /// <summary>define.h GIRDLE (object types (type 0 is undefined and not valid)).</summary>
    public const int GIRDLE = 113;
    /// <summary>define.h FORCE (object types (type 0 is undefined and not valid)).</summary>
    public const int FORCE = 114;
    /// <summary>define.h POTION_EFFECT (object types (type 0 is undefined and not valid)). a force, holding the effect of a potion</summary>
    public const int POTION_EFFECT = 115;
    /// <summary>define.h CLOSE_CON (object types (type 0 is undefined and not valid)). Eneq(@csd.uu.se): Id for close_container archetype.</summary>
    public const int CLOSE_CON = 121;
    /// <summary>define.h CONTAINER (object types (type 0 is undefined and not valid)).</summary>
    public const int CONTAINER = 122;
    /// <summary>define.h ARMOUR_IMPROVER (object types (type 0 is undefined and not valid)).</summary>
    public const int ARMOUR_IMPROVER = 123;
    /// <summary>define.h WEAPON_IMPROVER (object types (type 0 is undefined and not valid)).</summary>
    public const int WEAPON_IMPROVER = 124;
    /// <summary>define.h SKILLSCROLL (object types (type 0 is undefined and not valid)). can add a skill to player's inventory -bt.</summary>
    public const int SKILLSCROLL = 130;
    /// <summary>define.h DEEP_SWAMP (object types (type 0 is undefined and not valid)).</summary>
    public const int DEEP_SWAMP = 138;
    /// <summary>define.h IDENTIFY_ALTAR (object types (type 0 is undefined and not valid)).</summary>
    public const int IDENTIFY_ALTAR = 139;
    /// <summary>define.h CANCELLATION (object types (type 0 is undefined and not valid)).</summary>
    public const int CANCELLATION = 141;
    /// <summary>define.h MENU (object types (type 0 is undefined and not valid)). Mark Wedel (mark@pyramid.com) Shop inventories</summary>
    public const int MENU = 150;
    /// <summary>define.h BALL_LIGHTNING (object types (type 0 is undefined and not valid)). peterm:  ball lightning and color spray</summary>
    public const int BALL_LIGHTNING = 151;
    /// <summary>define.h SWARM_SPELL (object types (type 0 is undefined and not valid)).</summary>
    public const int SWARM_SPELL = 153;
    /// <summary>define.h RUNE (object types (type 0 is undefined and not valid)).</summary>
    public const int RUNE = 154;
    /// <summary>define.h POWER_CRYSTAL (object types (type 0 is undefined and not valid)).</summary>
    public const int POWER_CRYSTAL = 156;
    /// <summary>define.h CORPSE (object types (type 0 is undefined and not valid)).</summary>
    public const int CORPSE = 157;
    /// <summary>define.h DISEASE (object types (type 0 is undefined and not valid)).</summary>
    public const int DISEASE = 158;
    /// <summary>define.h SYMPTOM (object types (type 0 is undefined and not valid)).</summary>
    public const int SYMPTOM = 159;

    // define.h: weapon types
    /// <summary>define.h WEAP_HIT (weapon types). the basic</summary>
    public const int WEAP_HIT = 0;
    /// <summary>define.h WEAP_SLASH (weapon types). slash</summary>
    public const int WEAP_SLASH = 1;
    /// <summary>define.h WEAP_PIERCE (weapon types). arrows, stiletto</summary>
    public const int WEAP_PIERCE = 2;
    /// <summary>define.h WEAP_CLEAVE (weapon types). axe</summary>
    public const int WEAP_CLEAVE = 3;
    /// <summary>define.h WEAP_SLICE (weapon types). katana</summary>
    public const int WEAP_SLICE = 4;
    /// <summary>define.h WEAP_STAB (weapon types). knife, dagger</summary>
    public const int WEAP_STAB = 5;
    /// <summary>define.h WEAP_WHIP (weapon types). whips n chains</summary>
    public const int WEAP_WHIP = 6;
    /// <summary>define.h WEAP_CRUSH (weapon types). big hammers, flails</summary>
    public const int WEAP_CRUSH = 7;
    /// <summary>define.h WEAP_BLUD (weapon types). bludgeoning, club, stick</summary>
    public const int WEAP_BLUD = 8;

    // define.h: pickup modes
    /// <summary>define.h PU_NOTHING (pickup modes).</summary>
    public const uint PU_NOTHING = 0x00000000U;
    /// <summary>define.h PU_DEBUG (pickup modes).</summary>
    public const uint PU_DEBUG = 0x10000000U;
    /// <summary>define.h PU_INHIBIT (pickup modes).</summary>
    public const uint PU_INHIBIT = 0x20000000U;
    /// <summary>define.h PU_STOP (pickup modes).</summary>
    public const uint PU_STOP = 0x40000000U;
    /// <summary>define.h PU_NEWMODE (pickup modes).</summary>
    public const uint PU_NEWMODE = 0x80000000U;
    /// <summary>define.h PU_RATIO (pickup modes).</summary>
    public const uint PU_RATIO = 0x0000000FU;
    /// <summary>define.h PU_FOOD (pickup modes).</summary>
    public const uint PU_FOOD = 0x00000010U;
    /// <summary>define.h PU_DRINK (pickup modes).</summary>
    public const uint PU_DRINK = 0x00000020U;
    /// <summary>define.h PU_VALUABLES (pickup modes).</summary>
    public const uint PU_VALUABLES = 0x00000040U;
    /// <summary>define.h PU_BOW (pickup modes).</summary>
    public const uint PU_BOW = 0x00000080U;
    /// <summary>define.h PU_ARROW (pickup modes).</summary>
    public const uint PU_ARROW = 0x00000100U;
    /// <summary>define.h PU_HELMET (pickup modes).</summary>
    public const uint PU_HELMET = 0x00000200U;
    /// <summary>define.h PU_SHIELD (pickup modes).</summary>
    public const uint PU_SHIELD = 0x00000400U;
    /// <summary>define.h PU_ARMOUR (pickup modes).</summary>
    public const uint PU_ARMOUR = 0x00000800U;
    /// <summary>define.h PU_BOOTS (pickup modes).</summary>
    public const uint PU_BOOTS = 0x00001000U;
    /// <summary>define.h PU_GLOVES (pickup modes).</summary>
    public const uint PU_GLOVES = 0x00002000U;
    /// <summary>define.h PU_CLOAK (pickup modes).</summary>
    public const uint PU_CLOAK = 0x00004000U;
    /// <summary>define.h PU_KEY (pickup modes).</summary>
    public const uint PU_KEY = 0x00008000U;
    /// <summary>define.h PU_MISSILEWEAPON (pickup modes).</summary>
    public const uint PU_MISSILEWEAPON = 0x00010000U;
    /// <summary>define.h PU_ALLWEAPON (pickup modes).</summary>
    public const uint PU_ALLWEAPON = 0x00020000U;
    /// <summary>define.h PU_MAGICAL (pickup modes).</summary>
    public const uint PU_MAGICAL = 0x00040000U;
    /// <summary>define.h PU_POTION (pickup modes).</summary>
    public const uint PU_POTION = 0x00080000U;

    // define.h: free-spot array sizes
    /// <summary>define.h SIZEOFFREE1 (free-spot array sizes).</summary>
    public const int SIZEOFFREE1 = 8;
    /// <summary>define.h SIZEOFFREE2 (free-spot array sizes).</summary>
    public const int SIZEOFFREE2 = 24;
    /// <summary>define.h SIZEOFFREE (free-spot array sizes).</summary>
    public const int SIZEOFFREE = 49;

    // define.h: spell paths
    /// <summary>define.h PATH_NULL (spell paths). 0</summary>
    public const uint PATH_NULL = 0x00000000U;
    /// <summary>define.h PATH_PROT (spell paths). 1</summary>
    public const uint PATH_PROT = 0x00000001U;
    /// <summary>define.h PATH_FIRE (spell paths). 2</summary>
    public const uint PATH_FIRE = 0x00000002U;
    /// <summary>define.h PATH_FROST (spell paths). 4</summary>
    public const uint PATH_FROST = 0x00000004U;
    /// <summary>define.h PATH_ELEC (spell paths). 8</summary>
    public const uint PATH_ELEC = 0x00000008U;
    /// <summary>define.h PATH_MISSILE (spell paths). 16</summary>
    public const uint PATH_MISSILE = 0x00000010U;
    /// <summary>define.h PATH_SELF (spell paths). 32</summary>
    public const uint PATH_SELF = 0x00000020U;
    /// <summary>define.h PATH_SUMMON (spell paths). 64</summary>
    public const uint PATH_SUMMON = 0x00000040U;
    /// <summary>define.h PATH_ABJURE (spell paths). 128</summary>
    public const uint PATH_ABJURE = 0x00000080U;
    /// <summary>define.h PATH_RESTORE (spell paths). 256</summary>
    public const uint PATH_RESTORE = 0x00000100U;
    /// <summary>define.h PATH_DETONATE (spell paths). 512</summary>
    public const uint PATH_DETONATE = 0x00000200U;
    /// <summary>define.h PATH_MIND (spell paths). 1024</summary>
    public const uint PATH_MIND = 0x00000400U;
    /// <summary>define.h PATH_CREATE (spell paths). 2048</summary>
    public const uint PATH_CREATE = 0x00000800U;
    /// <summary>define.h PATH_TELE (spell paths). 4096</summary>
    public const uint PATH_TELE = 0x00001000U;
    /// <summary>define.h PATH_INFO (spell paths). 8192</summary>
    public const uint PATH_INFO = 0x00002000U;
    /// <summary>define.h PATH_TRANSMUTE (spell paths). 16384</summary>
    public const uint PATH_TRANSMUTE = 0x00004000U;
    /// <summary>define.h PATH_TRANSFER (spell paths). 32768</summary>
    public const uint PATH_TRANSFER = 0x00008000U;
    /// <summary>define.h PATH_TURNING (spell paths). 65536</summary>
    public const uint PATH_TURNING = 0x00010000U;
    /// <summary>define.h PATH_WOUNDING (spell paths). 131072</summary>
    public const uint PATH_WOUNDING = 0x00020000U;
    /// <summary>define.h PATH_DEATH (spell paths). 262144</summary>
    public const uint PATH_DEATH = 0x00040000U;
    /// <summary>define.h PATH_LIGHT (spell paths). 524288</summary>
    public const uint PATH_LIGHT = 0x00080000U;

    // define.h: spell and skill counts
    /// <summary>define.h NRSPELLPATHS (spell and skill counts).</summary>
    public const int NRSPELLPATHS = 20;
    /// <summary>define.h NROFREALSPELLS (spell and skill counts). Number of different spells</summary>
    public const int NROFREALSPELLS = 205;
    /// <summary>define.h NROFSKILLS (spell and skill counts). match to the number of elements in skills[] array</summary>
    public const int NROFSKILLS = 33;

    // define.h: the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)
    /// <summary>define.h FLAG_ALIVE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object can fight (or be fought)</summary>
    public const int FLAG_ALIVE = 0;
    /// <summary>define.h FLAG_WIZ (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object has special privilegies</summary>
    public const int FLAG_WIZ = 1;
    /// <summary>define.h FLAG_REMOVED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object is not in any map or invenory</summary>
    public const int FLAG_REMOVED = 2;
    /// <summary>define.h FLAG_FREED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object is in the list of free objects</summary>
    public const int FLAG_FREED = 3;
    /// <summary>define.h FLAG_WAS_WIZ (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Player was once a wiz</summary>
    public const int FLAG_WAS_WIZ = 4;
    /// <summary>define.h FLAG_APPLIED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object is ready for use by living</summary>
    public const int FLAG_APPLIED = 5;
    /// <summary>define.h FLAG_UNPAID (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object hasn't been paid for yet</summary>
    public const int FLAG_UNPAID = 6;
    /// <summary>define.h FLAG_NO_PICK (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object can't be picked up</summary>
    public const int FLAG_NO_PICK = 8;
    /// <summary>define.h FLAG_WALK_ON (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Applied when it's walked upon</summary>
    public const int FLAG_WALK_ON = 9;
    /// <summary>define.h FLAG_NO_PASS (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Nothing can pass (wall() is true)</summary>
    public const int FLAG_NO_PASS = 10;
    /// <summary>define.h FLAG_ANIMATE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The object looks at archetype for faces</summary>
    public const int FLAG_ANIMATE = 11;
    /// <summary>define.h FLAG_SLOW_MOVE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Uses the stats.exp/1000 to slow down</summary>
    public const int FLAG_SLOW_MOVE = 12;
    /// <summary>define.h FLAG_FLYING (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Not affected by WALK_ON or SLOW_MOVE)</summary>
    public const int FLAG_FLYING = 13;
    /// <summary>define.h FLAG_MONSTER (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Will attack players</summary>
    public const int FLAG_MONSTER = 14;
    /// <summary>define.h FLAG_FRIENDLY (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Will help players</summary>
    public const int FLAG_FRIENDLY = 15;
    /// <summary>define.h FLAG_GENERATOR (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Will generate type ob-&gt;stats.food</summary>
    public const int FLAG_GENERATOR = 16;
    /// <summary>define.h FLAG_IS_THROWN (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object is designed to be thrown.</summary>
    public const int FLAG_IS_THROWN = 17;
    /// <summary>define.h FLAG_AUTO_APPLY (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Will be applied when created</summary>
    public const int FLAG_AUTO_APPLY = 18;
    /// <summary>define.h FLAG_TREASURE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Will generate treasure when applied</summary>
    public const int FLAG_TREASURE = 19;
    /// <summary>define.h FLAG_APPLY_ONCE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Will dissapear when applied</summary>
    public const int FLAG_APPLY_ONCE = 20;
    /// <summary>define.h FLAG_SEE_INVISIBLE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Will see invisible player</summary>
    public const int FLAG_SEE_INVISIBLE = 21;
    /// <summary>define.h FLAG_CAN_ROLL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object can be rolled</summary>
    public const int FLAG_CAN_ROLL = 22;
    /// <summary>define.h FLAG_PARALYZED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object is paralyzed</summary>
    public const int FLAG_PARALYZED = 23;
    /// <summary>define.h FLAG_IS_TURNABLE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object can change face with direction</summary>
    public const int FLAG_IS_TURNABLE = 24;
    /// <summary>define.h FLAG_WALK_OFF (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object is applied when left</summary>
    public const int FLAG_WALK_OFF = 25;
    /// <summary>define.h FLAG_FLY_ON (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). As WALK_ON, but only with FLAG_FLYING</summary>
    public const int FLAG_FLY_ON = 26;
    /// <summary>define.h FLAG_FLY_OFF (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). As WALK_OFF, but only with FLAG_FLYING</summary>
    public const int FLAG_FLY_OFF = 27;
    /// <summary>define.h FLAG_IS_USED_UP (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). When (--food&lt;0) the object will exit</summary>
    public const int FLAG_IS_USED_UP = 28;
    /// <summary>define.h FLAG_IDENTIFIED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Not implemented yet</summary>
    public const int FLAG_IDENTIFIED = 29;
    /// <summary>define.h FLAG_REFLECTING (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object reflects from walls (lightning)</summary>
    public const int FLAG_REFLECTING = 30;
    /// <summary>define.h FLAG_CHANGING (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Changes to other_arch when anim is done</summary>
    public const int FLAG_CHANGING = 31;
    /// <summary>define.h FLAG_SPLITTING (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object splits into stats.food other objs</summary>
    public const int FLAG_SPLITTING = 32;
    /// <summary>define.h FLAG_HITBACK (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object will hit back when hit</summary>
    public const int FLAG_HITBACK = 33;
    /// <summary>define.h FLAG_STARTEQUIP (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object was given to player at start</summary>
    public const int FLAG_STARTEQUIP = 34;
    /// <summary>define.h FLAG_BLOCKSVIEW (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object blocks view</summary>
    public const int FLAG_BLOCKSVIEW = 35;
    /// <summary>define.h FLAG_UNDEAD (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Monster is undead</summary>
    public const int FLAG_UNDEAD = 36;
    /// <summary>define.h FLAG_SCARED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Monster is scared (mb player in future)</summary>
    public const int FLAG_SCARED = 37;
    /// <summary>define.h FLAG_UNAGGRESSIVE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Monster doesn't attack players</summary>
    public const int FLAG_UNAGGRESSIVE = 38;
    /// <summary>define.h FLAG_REFL_MISSILE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Arrows will reflect from object</summary>
    public const int FLAG_REFL_MISSILE = 39;
    /// <summary>define.h FLAG_REFL_SPELL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Spells (some) will reflect from object</summary>
    public const int FLAG_REFL_SPELL = 40;
    /// <summary>define.h FLAG_NO_MAGIC (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Spells (some) can't pass this object</summary>
    public const int FLAG_NO_MAGIC = 41;
    /// <summary>define.h FLAG_NO_FIX_PLAYER (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). fix_player() won't be called</summary>
    public const int FLAG_NO_FIX_PLAYER = 42;
    /// <summary>define.h FLAG_TEAR_DOWN (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). at-&gt;faces[hp*animations/maxhp] at hit</summary>
    public const int FLAG_TEAR_DOWN = 44;
    /// <summary>define.h FLAG_RUN_AWAY (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object runs away from nearest player</summary>
    public const int FLAG_RUN_AWAY = 45;
    /// <summary>define.h FLAG_PASS_THRU (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Objects with can_pass_thru can pass</summary>
    public const int FLAG_PASS_THRU = 46;
    /// <summary>define.h FLAG_CAN_PASS_THRU (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Can pass thru...</summary>
    public const int FLAG_CAN_PASS_THRU = 47;
    /// <summary>define.h FLAG_PICK_UP (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Can pick up</summary>
    public const int FLAG_PICK_UP = 48;
    /// <summary>define.h FLAG_UNIQUE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Item is really unique (UNIQUE_ITEMS)</summary>
    public const int FLAG_UNIQUE = 49;
    /// <summary>define.h FLAG_NO_DROP (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Object can't be dropped</summary>
    public const int FLAG_NO_DROP = 50;
    /// <summary>define.h FLAG_NO_PRETEXT (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). No text is added before name.</summary>
    public const int FLAG_NO_PRETEXT = 51;
    /// <summary>define.h FLAG_CAST_SPELL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) can learn and cast spells</summary>
    public const int FLAG_CAST_SPELL = 52;
    /// <summary>define.h FLAG_USE_SCROLL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) can read scroll</summary>
    public const int FLAG_USE_SCROLL = 53;
    /// <summary>define.h FLAG_USE_WAND (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) can apply and use wands</summary>
    public const int FLAG_USE_WAND = 54;
    /// <summary>define.h FLAG_USE_BOW (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) can apply and fire bows</summary>
    public const int FLAG_USE_BOW = 55;
    /// <summary>define.h FLAG_USE_ARMOUR (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) can wear armour/shield/helmet</summary>
    public const int FLAG_USE_ARMOUR = 56;
    /// <summary>define.h FLAG_USE_WEAPON (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) can wield weapons</summary>
    public const int FLAG_USE_WEAPON = 57;
    /// <summary>define.h FLAG_USE_RING (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) can use rings, boots, gauntlets, etc</summary>
    public const int FLAG_USE_RING = 58;
    /// <summary>define.h FLAG_READY_WAND (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) has a wand readied... 8)</summary>
    public const int FLAG_READY_WAND = 59;
    /// <summary>define.h FLAG_READY_BOW (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). not implemented yet</summary>
    public const int FLAG_READY_BOW = 60;
    /// <summary>define.h FLAG_XRAYS (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). X-ray vision</summary>
    public const int FLAG_XRAYS = 61;
    /// <summary>define.h FLAG_NO_APPLY (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Avoids step_on/fly_on to this object</summary>
    public const int FLAG_NO_APPLY = 62;
    /// <summary>define.h FLAG_IS_FLOOR (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Can't see what's underneath this object</summary>
    public const int FLAG_IS_FLOOR = 63;
    /// <summary>define.h FLAG_LIFESAVE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Saves a players' life once, then destr.</summary>
    public const int FLAG_LIFESAVE = 64;
    /// <summary>define.h FLAG_NO_STRENGTH (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Strength-bonus not added to wc/dam</summary>
    public const int FLAG_NO_STRENGTH = 65;
    /// <summary>define.h FLAG_SLEEP (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). NPC is sleeping</summary>
    public const int FLAG_SLEEP = 66;
    /// <summary>define.h FLAG_STAND_STILL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). NPC will not (ever) move</summary>
    public const int FLAG_STAND_STILL = 67;
    /// <summary>define.h FLAG_RANDOM_MOVE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). NPC will move randomly</summary>
    public const int FLAG_RANDOM_MOVE = 68;
    /// <summary>define.h FLAG_ONLY_ATTACK (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). NPC will evaporate if there is no enemy</summary>
    public const int FLAG_ONLY_ATTACK = 69;
    /// <summary>define.h FLAG_CONFUSED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Will also be unable to cast spells</summary>
    public const int FLAG_CONFUSED = 70;
    /// <summary>define.h FLAG_STEALTH (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Will wake monsters with less range</summary>
    public const int FLAG_STEALTH = 71;
    /// <summary>define.h FLAG_WIZPASS (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The wizard can go through walls</summary>
    public const int FLAG_WIZPASS = 72;
    /// <summary>define.h FLAG_IS_LINKED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The object is linked with other objects</summary>
    public const int FLAG_IS_LINKED = 73;
    /// <summary>define.h FLAG_CURSED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The object is cursed</summary>
    public const int FLAG_CURSED = 74;
    /// <summary>define.h FLAG_DAMNED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The object is _very_ cursed</summary>
    public const int FLAG_DAMNED = 75;
    /// <summary>define.h FLAG_SEE_ANYWHERE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The object will be visible behind walls</summary>
    public const int FLAG_SEE_ANYWHERE = 76;
    /// <summary>define.h FLAG_KNOWN_MAGICAL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The object is known to be magical</summary>
    public const int FLAG_KNOWN_MAGICAL = 77;
    /// <summary>define.h FLAG_KNOWN_CURSED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The object is known to be cursed</summary>
    public const int FLAG_KNOWN_CURSED = 78;
    /// <summary>define.h FLAG_CAN_USE_SKILL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The monster can use skills</summary>
    public const int FLAG_CAN_USE_SKILL = 79;
    /// <summary>define.h FLAG_BEEN_APPLIED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). The object has been applied</summary>
    public const int FLAG_BEEN_APPLIED = 80;
    /// <summary>define.h FLAG_READY_ROD (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) has a rod readied... 8)</summary>
    public const int FLAG_READY_ROD = 81;
    /// <summary>define.h FLAG_USE_ROD (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) can apply and use rods</summary>
    public const int FLAG_USE_ROD = 82;
    /// <summary>define.h FLAG_READY_HORN (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) has a horn readied</summary>
    public const int FLAG_READY_HORN = 83;
    /// <summary>define.h FLAG_USE_HORN (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster) can apply and use horns</summary>
    public const int FLAG_USE_HORN = 84;
    /// <summary>define.h FLAG_MAKE_INVIS (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Item) gives invisibility when applied</summary>
    public const int FLAG_MAKE_INVIS = 85;
    /// <summary>define.h FLAG_INV_LOCKED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Item will not be dropped from inventory</summary>
    public const int FLAG_INV_LOCKED = 86;
    /// <summary>define.h FLAG_IS_WOODED (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Item is wooded terrain</summary>
    public const int FLAG_IS_WOODED = 87;
    /// <summary>define.h FLAG_IS_HILLY (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Item is hilly/mountain terrain</summary>
    public const int FLAG_IS_HILLY = 88;
    /// <summary>define.h FLAG_READY_SKILL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster or Player) has a skill readied</summary>
    public const int FLAG_READY_SKILL = 89;
    /// <summary>define.h FLAG_READY_WEAPON (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). (Monster or Player) has a weapon readied</summary>
    public const int FLAG_READY_WEAPON = 90;
    /// <summary>define.h FLAG_NO_SKILL_IDENT (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). If set, item cannot be identified w/ a skill</summary>
    public const int FLAG_NO_SKILL_IDENT = 91;
    /// <summary>define.h FLAG_BLIND (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). If set, object cannot see (visually)</summary>
    public const int FLAG_BLIND = 92;
    /// <summary>define.h FLAG_SEE_IN_DARK (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). if set ob not effected by darkness</summary>
    public const int FLAG_SEE_IN_DARK = 93;
    /// <summary>define.h FLAG_IS_CAULDRON (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). container can make alchemical stuff</summary>
    public const int FLAG_IS_CAULDRON = 94;
    /// <summary>define.h FLAG_DUST (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). item is a 'powder', effects throwing</summary>
    public const int FLAG_DUST = 95;
    /// <summary>define.h FLAG_NO_STEAL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Item can't be stolen</summary>
    public const int FLAG_NO_STEAL = 96;
    /// <summary>define.h FLAG_ONE_HIT (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Monster can only hit once before going</summary>
    public const int FLAG_ONE_HIT = 97;
    /// <summary>define.h FLAG_CLIENT_SENT (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). THIS IS A DEBUG FLAG ONLY.  We use it to</summary>
    public const int FLAG_CLIENT_SENT = 98;
    /// <summary>define.h FLAG_BERSERK (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). monster will attack closest living</summary>
    public const int FLAG_BERSERK = 99;
    /// <summary>define.h FLAG_NEUTRAL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). monster is from type neutral</summary>
    public const int FLAG_NEUTRAL = 100;
    /// <summary>define.h FLAG_NO_ATTACK (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). monster don't attack</summary>
    public const int FLAG_NO_ATTACK = 101;
    /// <summary>define.h FLAG_NO_DAMAGE (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). monster can't be damaged</summary>
    public const int FLAG_NO_DAMAGE = 102;
    /// <summary>define.h FLAG_OBJ_ORIGINAL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). NEVER SET THIS.  Item was loaded by</summary>
    public const int FLAG_OBJ_ORIGINAL = 103;
    /// <summary>define.h FLAG_OBJ_SAVE_ON_OVL (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). this object should be saved on</summary>
    public const int FLAG_OBJ_SAVE_ON_OVL = 104;
    /// <summary>define.h NUM_FLAGS (the flags (flags[0] is 0 to 31, flags[1] 32 to 63, flags[2] 64 to 95, flags[3] 96 to 127)). Should always be equal to the last</summary>
    public const int NUM_FLAGS = 104;

    // define.h: editable flags of an archetype
    /// <summary>define.h E_MONSTER (editable flags of an archetype).</summary>
    public const uint E_MONSTER = 0x00000001U;
    /// <summary>define.h E_EXIT (editable flags of an archetype).</summary>
    public const uint E_EXIT = 0x00000002U;
    /// <summary>define.h E_TREASURE (editable flags of an archetype).</summary>
    public const uint E_TREASURE = 0x00000004U;
    /// <summary>define.h E_BACKGROUND (editable flags of an archetype).</summary>
    public const uint E_BACKGROUND = 0x00000008U;
    /// <summary>define.h E_DOOR (editable flags of an archetype).</summary>
    public const uint E_DOOR = 0x00000010U;
    /// <summary>define.h E_SPECIAL (editable flags of an archetype).</summary>
    public const uint E_SPECIAL = 0x00000020U;
    /// <summary>define.h E_SHOP (editable flags of an archetype).</summary>
    public const uint E_SHOP = 0x00000040U;
    /// <summary>define.h E_NORMAL (editable flags of an archetype).</summary>
    public const uint E_NORMAL = 0x00000080U;
    /// <summary>define.h E_FALSE_WALL (editable flags of an archetype).</summary>
    public const uint E_FALSE_WALL = 0x00000100U;
    /// <summary>define.h E_WALL (editable flags of an archetype).</summary>
    public const uint E_WALL = 0x00000200U;
    /// <summary>define.h E_EQUIPMENT (editable flags of an archetype).</summary>
    public const uint E_EQUIPMENT = 0x00000400U;
    /// <summary>define.h E_OTHER (editable flags of an archetype).</summary>
    public const uint E_OTHER = 0x00000800U;
    /// <summary>define.h E_ARTIFACT (editable flags of an archetype).</summary>
    public const uint E_ARTIFACT = 0x00001000U;

    // define.h: lighting limits
    /// <summary>define.h MAX_LIGHT_RADII (lighting limits). max radii for 'light' object, really</summary>
    public const int MAX_LIGHT_RADII = 4;
    /// <summary>define.h MAX_DARKNESS (lighting limits). maximum map darkness, there is no</summary>
    public const int MAX_DARKNESS = 5;

    // define.h: price kinds
    /// <summary>define.h F_BUY (price kinds).</summary>
    public const int F_BUY = 0;
    /// <summary>define.h F_SELL (price kinds).</summary>
    public const int F_SELL = 1;
    /// <summary>define.h F_TRUE (price kinds). True value of item, unadjusted</summary>
    public const int F_TRUE = 2;

    // define.h: attack movement (move_type) and the bit masks of it
    /// <summary>define.h DISTATT (attack movement (move_type) and the bit masks of it). move toward a player if far, but mantain some space,</summary>
    public const int DISTATT = 1;
    /// <summary>define.h RUNATT (attack movement (move_type) and the bit masks of it). run but attack if player catches up to object</summary>
    public const int RUNATT = 2;
    /// <summary>define.h HITRUN (attack movement (move_type) and the bit masks of it). run to then hit player then run away cyclicly</summary>
    public const int HITRUN = 3;
    /// <summary>define.h WAITATT (attack movement (move_type) and the bit masks of it). wait for player to approach then hit, move if hit</summary>
    public const int WAITATT = 4;
    /// <summary>define.h RUSH (attack movement (move_type) and the bit masks of it). Rush toward player blindly, similiar to dumb monster</summary>
    public const int RUSH = 5;
    /// <summary>define.h ALLRUN (attack movement (move_type) and the bit masks of it). always run never attack good for sim. of weak player</summary>
    public const int ALLRUN = 6;
    /// <summary>define.h DISTHIT (attack movement (move_type) and the bit masks of it). attack from a distance if hit as recommended by Frank</summary>
    public const int DISTHIT = 7;
    /// <summary>define.h WAIT2 (attack movement (move_type) and the bit masks of it). monster does not try to move towards player if far</summary>
    public const int WAIT2 = 8;
    /// <summary>define.h PETMOVE (attack movement (move_type) and the bit masks of it). if the upper four bits of move_type / attack_movement</summary>
    public const int PETMOVE = 16;
    /// <summary>define.h CIRCLE1 (attack movement (move_type) and the bit masks of it). if the upper four bits of move_type / attack_movement</summary>
    public const int CIRCLE1 = 32;
    /// <summary>define.h CIRCLE2 (attack movement (move_type) and the bit masks of it). same as above but a larger circle is used</summary>
    public const int CIRCLE2 = 48;
    /// <summary>define.h PACEH (attack movement (move_type) and the bit masks of it). The Monster will pace back and forth until attacked</summary>
    public const int PACEH = 64;
    /// <summary>define.h PACEH2 (attack movement (move_type) and the bit masks of it). the monster will pace as above but the length of the</summary>
    public const int PACEH2 = 80;
    /// <summary>define.h RANDO (attack movement (move_type) and the bit masks of it). the monster will go in a random direction until</summary>
    public const int RANDO = 96;
    /// <summary>define.h RANDO2 (attack movement (move_type) and the bit masks of it). constantly move in a different random direction</summary>
    public const int RANDO2 = 112;
    /// <summary>define.h PACEV (attack movement (move_type) and the bit masks of it). The Monster will pace back and forth until attacked</summary>
    public const int PACEV = 128;
    /// <summary>define.h PACEV2 (attack movement (move_type) and the bit masks of it). the monster will pace as above but the length of the</summary>
    public const int PACEV2 = 144;
    /// <summary>define.h LO4 (attack movement (move_type) and the bit masks of it). bitmasks for upper and lower 4 bits from 8 bit fields</summary>
    public const int LO4 = 15;
    /// <summary>define.h HI4 (attack movement (move_type) and the bit masks of it).</summary>
    public const int HI4 = 240;

    // define.h: player states
    /// <summary>define.h ST_PLAYING (player states).</summary>
    public const int ST_PLAYING = 0;
    /// <summary>define.h ST_PLAY_AGAIN (player states).</summary>
    public const int ST_PLAY_AGAIN = 1;
    /// <summary>define.h ST_ROLL_STAT (player states).</summary>
    public const int ST_ROLL_STAT = 2;
    /// <summary>define.h ST_CHANGE_CLASS (player states).</summary>
    public const int ST_CHANGE_CLASS = 3;
    /// <summary>define.h ST_CONFIRM_QUIT (player states).</summary>
    public const int ST_CONFIRM_QUIT = 4;
    /// <summary>define.h ST_CONFIGURE (player states).</summary>
    public const int ST_CONFIGURE = 5;
    /// <summary>define.h ST_GET_NAME (player states).</summary>
    public const int ST_GET_NAME = 6;
    /// <summary>define.h ST_GET_PASSWORD (player states).</summary>
    public const int ST_GET_PASSWORD = 7;
    /// <summary>define.h ST_CONFIRM_PASSWORD (player states).</summary>
    public const int ST_CONFIRM_PASSWORD = 8;
    /// <summary>define.h ST_GET_PARTY_PASSWORD (player states).</summary>
    public const int ST_GET_PARTY_PASSWORD = 10;

    // define.h: luck and random preferences
    /// <summary>define.h PREFER_HIGH (luck and random preferences).</summary>
    public const int PREFER_HIGH = 1;
    /// <summary>define.h PREFER_LOW (luck and random preferences).</summary>
    public const int PREFER_LOW = 0;

    // define.h: limits and sizes
    /// <summary>define.h MAX_STAT (limits and sizes). The maximum legal value of any stat</summary>
    public const int MAX_STAT = 30;
    /// <summary>define.h MIN_STAT (limits and sizes). The minimum legal value of any stat</summary>
    public const int MIN_STAT = 1;
    /// <summary>define.h MAX_BUF (limits and sizes). Used for all kinds of things</summary>
    public const int MAX_BUF = 256;
    /// <summary>define.h VERY_BIG_BUF (limits and sizes).</summary>
    public const int VERY_BIG_BUF = 1024;
    /// <summary>define.h HUGE_BUF (limits and sizes). Used for messages - some can be quite long</summary>
    public const int HUGE_BUF = 4096;
    /// <summary>define.h SOCKET_BUFLEN (limits and sizes). Max length a packet could be</summary>
    public const int SOCKET_BUFLEN = 4096;
    /// <summary>define.h FONTSIZE (limits and sizes). Max chars in font</summary>
    public const int FONTSIZE = 3000;
    /// <summary>define.h MAX_ANIMATIONS (limits and sizes).</summary>
    public const int MAX_ANIMATIONS = 256;
    /// <summary>define.h MAX_NAME (limits and sizes).</summary>
    public const int MAX_NAME = 16;
    /// <summary>define.h BIG_NAME (limits and sizes).</summary>
    public const int BIG_NAME = 32;
    /// <summary>define.h MAX_EXT_TITLE (limits and sizes).</summary>
    public const int MAX_EXT_TITLE = 98;
    /// <summary>define.h OUT_OF_MEMORY (limits and sizes).</summary>
    public const int OUT_OF_MEMORY = 0;
    /// <summary>define.h MAP_ERROR (limits and sizes).</summary>
    public const int MAP_ERROR = 1;
    /// <summary>define.h ARCHTABLE_TOO_SMALL (limits and sizes).</summary>
    public const int ARCHTABLE_TOO_SMALL = 2;
    /// <summary>define.h TOO_MANY_ERRORS (limits and sizes).</summary>
    public const int TOO_MANY_ERRORS = 3;

    // object.h: insert flags
    /// <summary>object.h INS_NO_MERGE (insert flags).</summary>
    public const int INS_NO_MERGE = 0x0001;
    /// <summary>object.h INS_ABOVE_FLOOR_ONLY (insert flags).</summary>
    public const int INS_ABOVE_FLOOR_ONLY = 0x0002;
    /// <summary>object.h INS_NO_WALK_ON (insert flags).</summary>
    public const int INS_NO_WALK_ON = 0x0004;
    /// <summary>object.h INS_ON_TOP (insert flags).</summary>
    public const int INS_ON_TOP = 0x0008;

    // object.h: update_object actions
    /// <summary>object.h UP_OBJ_INSERT (update_object actions).</summary>
    public const int UP_OBJ_INSERT = 1;
    /// <summary>object.h UP_OBJ_REMOVE (update_object actions).</summary>
    public const int UP_OBJ_REMOVE = 2;
    /// <summary>object.h UP_OBJ_CHANGE (update_object actions).</summary>
    public const int UP_OBJ_CHANGE = 3;
    /// <summary>object.h UP_OBJ_FACE (update_object actions). Only thing that changed was the face</summary>
    public const int UP_OBJ_FACE = 4;

    // living.h: the stats
    /// <summary>living.h STR (the stats).</summary>
    public const int STR = 0;
    /// <summary>living.h DEX (the stats).</summary>
    public const int DEX = 1;
    /// <summary>living.h CON (the stats).</summary>
    public const int CON = 2;
    /// <summary>living.h WIS (the stats).</summary>
    public const int WIS = 3;
    /// <summary>living.h CHA (the stats).</summary>
    public const int CHA = 4;
    /// <summary>living.h INT (the stats).</summary>
    public const int INT = 5;
    /// <summary>living.h POW (the stats).</summary>
    public const int POW = 6;

    // attack.h: counts
    /// <summary>attack.h NROFATTACKS (counts).</summary>
    public const int NROFATTACKS = 25;
    /// <summary>attack.h NROFATTACKMESS (counts).</summary>
    public const int NROFATTACKMESS = 20;
    /// <summary>attack.h MAXATTACKMESS (counts).</summary>
    public const int MAXATTACKMESS = 20;

    // attack.h: default resistances
    /// <summary>attack.h RESIST_IMMUNE (default resistances).</summary>
    public const int RESIST_IMMUNE = 100;
    /// <summary>attack.h RESIST_PROT (default resistances).</summary>
    public const int RESIST_PROT = 30;
    /// <summary>attack.h RESIST_VULN (default resistances).</summary>
    public const int RESIST_VULN = -100;

    // attack.h: attack messages
    /// <summary>attack.h ATM_ARROW (attack messages).</summary>
    public const int ATM_ARROW = 0;
    /// <summary>attack.h ATM_DRAIN (attack messages).</summary>
    public const int ATM_DRAIN = 1;
    /// <summary>attack.h ATM_ELEC (attack messages).</summary>
    public const int ATM_ELEC = 2;
    /// <summary>attack.h ATM_COLD (attack messages).</summary>
    public const int ATM_COLD = 3;
    /// <summary>attack.h ATM_FIRE (attack messages).</summary>
    public const int ATM_FIRE = 4;
    /// <summary>attack.h ATM_BASIC (attack messages).</summary>
    public const int ATM_BASIC = 5;
    /// <summary>attack.h ATM_KARATE (attack messages).</summary>
    public const int ATM_KARATE = 6;
    /// <summary>attack.h ATM_CLAW (attack messages).</summary>
    public const int ATM_CLAW = 7;
    /// <summary>attack.h ATM_PUNCH (attack messages).</summary>
    public const int ATM_PUNCH = 8;
    /// <summary>attack.h ATM_SLASH (attack messages).</summary>
    public const int ATM_SLASH = 9;
    /// <summary>attack.h ATM_PIERCE (attack messages).</summary>
    public const int ATM_PIERCE = 10;
    /// <summary>attack.h ATM_CLEAVE (attack messages).</summary>
    public const int ATM_CLEAVE = 11;
    /// <summary>attack.h ATM_SLICE (attack messages).</summary>
    public const int ATM_SLICE = 12;
    /// <summary>attack.h ATM_STAB (attack messages).</summary>
    public const int ATM_STAB = 13;
    /// <summary>attack.h ATM_WHIP (attack messages).</summary>
    public const int ATM_WHIP = 14;
    /// <summary>attack.h ATM_CRUSH (attack messages).</summary>
    public const int ATM_CRUSH = 15;
    /// <summary>attack.h ATM_BLUD (attack messages).</summary>
    public const int ATM_BLUD = 16;
    /// <summary>attack.h ATM_DOOR (attack messages).</summary>
    public const int ATM_DOOR = 17;
    /// <summary>attack.h ATM_SUFFER (attack messages).</summary>
    public const int ATM_SUFFER = 18;

    // attack.h: attack numbers (index into resist[])
    /// <summary>attack.h ATNR_PHYSICAL (attack numbers (index into resist[])).</summary>
    public const int ATNR_PHYSICAL = 0;
    /// <summary>attack.h ATNR_MAGIC (attack numbers (index into resist[])).</summary>
    public const int ATNR_MAGIC = 1;
    /// <summary>attack.h ATNR_FIRE (attack numbers (index into resist[])).</summary>
    public const int ATNR_FIRE = 2;
    /// <summary>attack.h ATNR_ELECTRICITY (attack numbers (index into resist[])).</summary>
    public const int ATNR_ELECTRICITY = 3;
    /// <summary>attack.h ATNR_COLD (attack numbers (index into resist[])).</summary>
    public const int ATNR_COLD = 4;
    /// <summary>attack.h ATNR_CONFUSION (attack numbers (index into resist[])).</summary>
    public const int ATNR_CONFUSION = 5;
    /// <summary>attack.h ATNR_ACID (attack numbers (index into resist[])).</summary>
    public const int ATNR_ACID = 6;
    /// <summary>attack.h ATNR_DRAIN (attack numbers (index into resist[])).</summary>
    public const int ATNR_DRAIN = 7;
    /// <summary>attack.h ATNR_WEAPONMAGIC (attack numbers (index into resist[])).</summary>
    public const int ATNR_WEAPONMAGIC = 8;
    /// <summary>attack.h ATNR_GHOSTHIT (attack numbers (index into resist[])).</summary>
    public const int ATNR_GHOSTHIT = 9;
    /// <summary>attack.h ATNR_POISON (attack numbers (index into resist[])).</summary>
    public const int ATNR_POISON = 10;
    /// <summary>attack.h ATNR_SLOW (attack numbers (index into resist[])).</summary>
    public const int ATNR_SLOW = 11;
    /// <summary>attack.h ATNR_PARALYZE (attack numbers (index into resist[])).</summary>
    public const int ATNR_PARALYZE = 12;
    /// <summary>attack.h ATNR_TURN_UNDEAD (attack numbers (index into resist[])).</summary>
    public const int ATNR_TURN_UNDEAD = 13;
    /// <summary>attack.h ATNR_FEAR (attack numbers (index into resist[])).</summary>
    public const int ATNR_FEAR = 14;
    /// <summary>attack.h ATNR_CANCELLATION (attack numbers (index into resist[])).</summary>
    public const int ATNR_CANCELLATION = 15;
    /// <summary>attack.h ATNR_DEPLETE (attack numbers (index into resist[])).</summary>
    public const int ATNR_DEPLETE = 16;
    /// <summary>attack.h ATNR_DEATH (attack numbers (index into resist[])).</summary>
    public const int ATNR_DEATH = 17;
    /// <summary>attack.h ATNR_CHAOS (attack numbers (index into resist[])).</summary>
    public const int ATNR_CHAOS = 18;
    /// <summary>attack.h ATNR_COUNTERSPELL (attack numbers (index into resist[])).</summary>
    public const int ATNR_COUNTERSPELL = 19;
    /// <summary>attack.h ATNR_GODPOWER (attack numbers (index into resist[])).</summary>
    public const int ATNR_GODPOWER = 20;
    /// <summary>attack.h ATNR_HOLYWORD (attack numbers (index into resist[])).</summary>
    public const int ATNR_HOLYWORD = 21;
    /// <summary>attack.h ATNR_BLIND (attack numbers (index into resist[])).</summary>
    public const int ATNR_BLIND = 22;
    /// <summary>attack.h ATNR_INTERNAL (attack numbers (index into resist[])).</summary>
    public const int ATNR_INTERNAL = 23;
    /// <summary>attack.h ATNR_LIFE_STEALING (attack numbers (index into resist[])).</summary>
    public const int ATNR_LIFE_STEALING = 24;

    // attack.h: attack types (bit masks of attacktype)
    /// <summary>attack.h AT_PHYSICAL (attack types (bit masks of attacktype)). 1</summary>
    public const uint AT_PHYSICAL = 0x00000001U;
    /// <summary>attack.h AT_MAGIC (attack types (bit masks of attacktype)). 2</summary>
    public const uint AT_MAGIC = 0x00000002U;
    /// <summary>attack.h AT_FIRE (attack types (bit masks of attacktype)). 4</summary>
    public const uint AT_FIRE = 0x00000004U;
    /// <summary>attack.h AT_ELECTRICITY (attack types (bit masks of attacktype)). 8</summary>
    public const uint AT_ELECTRICITY = 0x00000008U;
    /// <summary>attack.h AT_COLD (attack types (bit masks of attacktype)). 16</summary>
    public const uint AT_COLD = 0x00000010U;
    /// <summary>attack.h AT_CONFUSION (attack types (bit masks of attacktype)). 32 The spell will use this one</summary>
    public const uint AT_CONFUSION = 0x00000020U;
    /// <summary>attack.h AT_ACID (attack types (bit masks of attacktype)). 64 Things might corrode when hit</summary>
    public const uint AT_ACID = 0x00000040U;
    /// <summary>attack.h AT_DRAIN (attack types (bit masks of attacktype)). 128</summary>
    public const uint AT_DRAIN = 0x00000080U;
    /// <summary>attack.h AT_WEAPONMAGIC (attack types (bit masks of attacktype)). 256 Very special, use with care</summary>
    public const uint AT_WEAPONMAGIC = 0x00000100U;
    /// <summary>attack.h AT_GHOSTHIT (attack types (bit masks of attacktype)). 512 Attacker dissolves</summary>
    public const uint AT_GHOSTHIT = 0x00000200U;
    /// <summary>attack.h AT_POISON (attack types (bit masks of attacktype)). 1024</summary>
    public const uint AT_POISON = 0x00000400U;
    /// <summary>attack.h AT_SLOW (attack types (bit masks of attacktype)). 2048</summary>
    public const uint AT_SLOW = 0x00000800U;
    /// <summary>attack.h AT_PARALYZE (attack types (bit masks of attacktype)). 4096</summary>
    public const uint AT_PARALYZE = 0x00001000U;
    /// <summary>attack.h AT_TURN_UNDEAD (attack types (bit masks of attacktype)). 8192</summary>
    public const uint AT_TURN_UNDEAD = 0x00002000U;
    /// <summary>attack.h AT_FEAR (attack types (bit masks of attacktype)). 16384</summary>
    public const uint AT_FEAR = 0x00004000U;
    /// <summary>attack.h AT_CANCELLATION (attack types (bit masks of attacktype)). 32768 ylitalo@student.docs.uu.se</summary>
    public const uint AT_CANCELLATION = 0x00008000U;
    /// <summary>attack.h AT_DEPLETE (attack types (bit masks of attacktype)). 65536 vick@bern.docs.uu.se</summary>
    public const uint AT_DEPLETE = 0x00010000U;
    /// <summary>attack.h AT_DEATH (attack types (bit masks of attacktype)). 131072 peterm@soda.berkeley.edu</summary>
    public const uint AT_DEATH = 0x00020000U;
    /// <summary>attack.h AT_CHAOS (attack types (bit masks of attacktype)). 262144 peterm@soda.berkeley.edu</summary>
    public const uint AT_CHAOS = 0x00040000U;
    /// <summary>attack.h AT_COUNTERSPELL (attack types (bit masks of attacktype)). 524288 peterm@soda.berkeley.edu</summary>
    public const uint AT_COUNTERSPELL = 0x00080000U;
    /// <summary>attack.h AT_GODPOWER (attack types (bit masks of attacktype)). 1048576  peterm@soda.berkeley.edu</summary>
    public const uint AT_GODPOWER = 0x00100000U;
    /// <summary>attack.h AT_HOLYWORD (attack types (bit masks of attacktype)). 2097152 race selective attack thomas@astro.psu.edu</summary>
    public const uint AT_HOLYWORD = 0x00200000U;
    /// <summary>attack.h AT_BLIND (attack types (bit masks of attacktype)). 4194304 thomas@astro.psu.edu</summary>
    public const uint AT_BLIND = 0x00400000U;
    /// <summary>attack.h AT_INTERNAL (attack types (bit masks of attacktype)). Only used for internal calculations</summary>
    public const uint AT_INTERNAL = 0x00800000U;
    /// <summary>attack.h AT_LIFE_STEALING (attack types (bit masks of attacktype)). 16777216 dnh@hawthorn.csse.monash.edu.au</summary>
    public const uint AT_LIFE_STEALING = 0x01000000U;

    // material.h: materials
    /// <summary>material.h NROFMATERIALS (materials).</summary>
    public const int NROFMATERIALS = 13;
    /// <summary>material.h M_PAPER (materials).</summary>
    public const int M_PAPER = 1;
    /// <summary>material.h M_IRON (materials).</summary>
    public const int M_IRON = 2;
    /// <summary>material.h M_GLASS (materials).</summary>
    public const int M_GLASS = 4;
    /// <summary>material.h M_LEATHER (materials).</summary>
    public const int M_LEATHER = 8;
    /// <summary>material.h M_WOOD (materials).</summary>
    public const int M_WOOD = 16;
    /// <summary>material.h M_ORGANIC (materials).</summary>
    public const int M_ORGANIC = 32;
    /// <summary>material.h M_STONE (materials).</summary>
    public const int M_STONE = 64;
    /// <summary>material.h M_CLOTH (materials).</summary>
    public const int M_CLOTH = 128;
    /// <summary>material.h M_ADAMANT (materials).</summary>
    public const int M_ADAMANT = 256;
    /// <summary>material.h M_LIQUID (materials).</summary>
    public const int M_LIQUID = 512;
    /// <summary>material.h M_SOFT_METAL (materials).</summary>
    public const int M_SOFT_METAL = 1024;
    /// <summary>material.h M_BONE (materials).</summary>
    public const int M_BONE = 2048;
    /// <summary>material.h M_ICE (materials).</summary>
    public const int M_ICE = 4096;

    // plugin.h: events
    /// <summary>plugin.h EVENT_NONE (events). No event. This exists only to reserve the "0".</summary>
    public const int EVENT_NONE = 0;
    /// <summary>plugin.h EVENT_APPLY (events). Object applied-unapplied.</summary>
    public const int EVENT_APPLY = 1;
    /// <summary>plugin.h EVENT_ATTACK (events). Monster attacked or Scripted Weapon used.</summary>
    public const int EVENT_ATTACK = 2;
    /// <summary>plugin.h EVENT_DEATH (events). Player or monster dead.</summary>
    public const int EVENT_DEATH = 3;
    /// <summary>plugin.h EVENT_DROP (events). Object dropped on the floor.</summary>
    public const int EVENT_DROP = 4;
    /// <summary>plugin.h EVENT_PICKUP (events). Object picked up.</summary>
    public const int EVENT_PICKUP = 5;
    /// <summary>plugin.h EVENT_SAY (events). Someone speaks.</summary>
    public const int EVENT_SAY = 6;
    /// <summary>plugin.h EVENT_STOP (events). Thrown object stopped.</summary>
    public const int EVENT_STOP = 7;
    /// <summary>plugin.h EVENT_TIME (events). Triggered each time the object can react/move.</summary>
    public const int EVENT_TIME = 8;
    /// <summary>plugin.h EVENT_THROW (events). Object is thrown.</summary>
    public const int EVENT_THROW = 9;
    /// <summary>plugin.h EVENT_TRIGGER (events). Button pushed, lever pulled, etc.</summary>
    public const int EVENT_TRIGGER = 10;
    /// <summary>plugin.h EVENT_CLOSE (events). Container closed.</summary>
    public const int EVENT_CLOSE = 11;
    /// <summary>plugin.h EVENT_TIMER (events). Timer connected triggered it.</summary>
    public const int EVENT_TIMER = 12;
    /// <summary>plugin.h EVENT_BORN (events). A new character has been created.</summary>
    public const int EVENT_BORN = 13;
    /// <summary>plugin.h EVENT_CLOCK (events). Global time event.</summary>
    public const int EVENT_CLOCK = 14;
    /// <summary>plugin.h EVENT_CRASH (events). Triggered when the server crashes. Not recursive</summary>
    public const int EVENT_CRASH = 15;
    /// <summary>plugin.h EVENT_GDEATH (events). Global Death event</summary>
    public const int EVENT_GDEATH = 16;
    /// <summary>plugin.h EVENT_GKILL (events). Triggered when anything got killed by anyone.</summary>
    public const int EVENT_GKILL = 17;
    /// <summary>plugin.h EVENT_LOGIN (events). Player login.</summary>
    public const int EVENT_LOGIN = 18;
    /// <summary>plugin.h EVENT_LOGOUT (events). Player logout.</summary>
    public const int EVENT_LOGOUT = 19;
    /// <summary>plugin.h EVENT_MAPENTER (events). A player entered a map.</summary>
    public const int EVENT_MAPENTER = 20;
    /// <summary>plugin.h EVENT_MAPLEAVE (events). A player left a map.</summary>
    public const int EVENT_MAPLEAVE = 21;
    /// <summary>plugin.h EVENT_MAPRESET (events). A map is resetting.</summary>
    public const int EVENT_MAPRESET = 22;
    /// <summary>plugin.h EVENT_REMOVE (events). A Player character has been removed.</summary>
    public const int EVENT_REMOVE = 23;
    /// <summary>plugin.h EVENT_SHOUT (events). A player 'shout' something.</summary>
    public const int EVENT_SHOUT = 24;
    /// <summary>plugin.h EVENT_TELL (events). A player 'tell' something.</summary>
    public const int EVENT_TELL = 25;
    /// <summary>plugin.h NR_EVENTS (events).</summary>
    public const int NR_EVENTS = 26;

    // config.h: table sizes
    /// <summary>config.h STARTMAX (table sizes). How big array of objects to start with</summary>
    public const int STARTMAX = 500;
    /// <summary>config.h OBJ_EXPAND (table sizes). How big steps to use when expanding array</summary>
    public const int OBJ_EXPAND = 100;
    /// <summary>config.h ARCHTABLE (table sizes). Used when hashing archetypes</summary>
    public const int ARCHTABLE = 5003;
    /// <summary>config.h MAXSTRING (table sizes).</summary>
    public const int MAXSTRING = 20;
}
