// Not ported from a Crossfire file: the parts of Crossfire's player controller (include/player.h, <c>struct pl</c>) that the rest of
// <c>fix_player</c> sets and reads: the regeneration and digestion bonuses, the braced flag, the weapon speed and the shoot type.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal sealed partial class Player
{
    /// <summary>C <c>digestion</c>: the bonus or penalty of the applied items to the digestion; <c>fix_player</c> sets it.</summary>
    public int Digestion { get; set; }

    /// <summary>C <c>gen_hp</c>: the regeneration of hit points the applied items give.</summary>
    public int GenHp { get; set; }

    /// <summary>C <c>gen_sp</c>: the regeneration of spell points.</summary>
    public int GenSp { get; set; }

    /// <summary>C <c>gen_grace</c>: the regeneration of grace.</summary>
    public int GenGrace { get; set; }

    /// <summary>C <c>gen_sp_armour</c> (10 when nothing is worn): the penalty to the regeneration of spell points that the armour gives.</summary>
    public int GenSpArmour { get; set; } = 10;

    /// <summary>C <c>encumbrance</c> (config.h <c>SPELL_ENCUMBRANCE</c>): three times the weight of the weapon plus the weight of the armour, in kilograms.</summary>
    public int Encumbrance { get; set; }

    /// <summary>C <c>braced</c> (the command <c>brace</c>): a braced player has better armour and weapon class and does not move.</summary>
    public bool Braced { get; set; }

    /// <summary>C <c>search_str[0]</c> (config.h <c>SEARCH_ITEMS</c>): true when the player searches for items on the ground, which costs speed.</summary>
    public bool Searching { get; set; }

    /// <summary>C <c>weapon_sp</c>: the speed that counts while the player fights (<c>fix_player</c> sets it).</summary>
    public float WeaponSp { get; set; }

    /// <summary>C <c>shoottype</c> (<c>rangetype</c>): which range attack the player uses; <c>check_skill_to_apply</c> sets it.</summary>
    public int ShootType { get; set; }

    /// <summary>C <c>do_los</c>: <c>change_abil</c> asks for the line of sight to be worked out again. Nothing in this extension reads it.</summary>
    public bool DoLos { get; set; }
}
