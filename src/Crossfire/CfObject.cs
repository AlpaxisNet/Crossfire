// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): include/object.h, include/treasure.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     include/object.h:
//         Copyright (C) 2001 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     include/treasure.h:
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

/// <summary>A player of Crossfire (include/player.h <c>struct pl</c>): not ported, the owner of the object model supplies it.</summary>
public interface IPlayerHandle
{
}

/// <summary>A map of Crossfire (include/map.h <c>mapdef</c>): not ported, the owner of the object model supplies it.</summary>
public interface IMapHandle
{
}

/// <summary>A spell of the casting-time code (include/spells.h <c>struct spell_struct</c>): not ported.</summary>
public interface ISpellHandle
{
}

/// <summary>include/treasure.h <c>treasurelist</c>: the list an object takes its random items from. The name is held here, the rest is in Treasure.cs.</summary>
public sealed partial class TreasureList
{
    /// <summary>treasure.h: name.</summary>
    public string Name { get; }

    /// <summary>Creates a list of that name.</summary>
    public TreasureList(string name) => Name = name;
}

/// <summary>
/// The keys of an archetype that the loader does not know (Graal Kingdoms' <c>graalnpctype</c>, <c>graalnpcparam</c>,
/// <c>hpbuff</c>, <c>flag gender=male</c> and the others): the loader of C ignores such a line, the port keeps it on the object so
/// that nothing of the archetypes is lost (docs/differences.md). A key can occur more than once on an object (<c>flag</c> does);
/// the order of the lines is kept. Keys are compared without regard to case, as the C scanner does.
/// </summary>
public sealed class ExtraKeys
{
    private readonly List<KeyValuePair<string, string>> entries = new();

    /// <summary>The number of lines kept.</summary>
    public int Count => entries.Count;

    /// <summary>The lines kept, in file order.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Entries => entries;

    /// <summary>Keeps a line.</summary>
    public void Add(string key, string value) => entries.Add(new KeyValuePair<string, string>(key, value));

    /// <summary>The value of the last line with this key, or null.</summary>
    public string? Get(string key)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (string.Equals(entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return entries[i].Value;
            }
        }
        return null;
    }

    /// <summary>Not in C: makes this key's value <paramref name="value"/>, replacing every line it had (a new line at the end when it had none).</summary>
    public void Set(string key, string value)
    {
        entries.RemoveAll(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
        entries.Add(new KeyValuePair<string, string>(key, value));
    }

    /// <summary>Not in C: removes every line with this key.</summary>
    public void Remove(string key) => entries.RemoveAll(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The values of every line with this key, in file order.</summary>
    public IEnumerable<string> GetAll(string key) =>
        entries.Where(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase)).Select(e => e.Value);

    /// <summary>True when a line has this key.</summary>
    public bool Contains(string key) => Get(key) != null;

    /// <summary>A copy (the C <c>memcpy</c> of an object copies them with it).</summary>
    public ExtraKeys Copy()
    {
        var c = new ExtraKeys();
        c.entries.AddRange(entries);
        return c;
    }
}

/// <summary>
/// include/object.h <c>struct obj</c> (typedef <c>object</c>): an object of Crossfire. Every field of the C structure is here, in the
/// order of the C declaration, with the C type (a <c>char *</c> is a string, a pointer to an object is a reference, <c>sint8</c>
/// is <see cref="sbyte"/>, <c>signed long</c> is a 32-bit <see cref="int"/>: docs/differences.md). A string that C shares through
/// shstr.c is a plain string here: equal text is the same string, as the equal pointers of C are.
/// </summary>
public sealed class CfObject
{
    // These variables are not changed by copy_object():

    /// <summary>object.h: contr, the player that controls this object.</summary>
    public IPlayerHandle? Contr;
    /// <summary>object.h: next, in the free/used list.</summary>
    public CfObject? Next;
    /// <summary>object.h: prev, in the free/used list.</summary>
    public CfObject? Prev;
    /// <summary>object.h: active_next, in the list of active objects.</summary>
    public CfObject? ActiveNext;
    /// <summary>object.h: active_prev.</summary>
    public CfObject? ActivePrev;
    /// <summary>object.h: below, the object stacked below this one in the same environment.</summary>
    public CfObject? Below;
    /// <summary>object.h: above.</summary>
    public CfObject? Above;
    /// <summary>object.h: inv, the first object of the inventory.</summary>
    public CfObject? Inv;
    /// <summary>object.h: container, the container being used (the player only).</summary>
    public CfObject? Container;
    /// <summary>object.h: env, the object that is the environment (typically the container the object is in).</summary>
    public CfObject? Env;
    /// <summary>object.h: more, the rest of a large body of objects.</summary>
    public CfObject? More;
    /// <summary>object.h: head, the main object of a large body.</summary>
    public CfObject? Head;
    /// <summary>object.h: map, the map in which this object is present.</summary>
    public IMapHandle? Map;

    /// <summary>object.h: count (tag_t), which number of object created this is.</summary>
    public uint Count;
    /// <summary>object.h: refcount, how many objects point to this object.</summary>
    public ushort Refcount;

    // These get an extra add_refcount() after having been copied by memcpy():

    /// <summary>object.h: name.</summary>
    public string? Name;
    /// <summary>object.h: name_pl, the plural name.</summary>
    public string? NamePl;
    /// <summary>object.h: title ("of foo").</summary>
    public string? Title;
    /// <summary>object.h: race (human, goblin, dragon...).</summary>
    public string? Race;
    /// <summary>object.h: slaying, which race to do double damage to.</summary>
    public string? Slaying;
    /// <summary>object.h: msg, the text of a book, sign or magic mouth; for an exit the file name.</summary>
    public string? Msg;

    // These variables are copied by memcpy() in copy_object():

    /// <summary>object.h: x.</summary>
    public short X;
    /// <summary>object.h: y.</summary>
    public short Y;
    /// <summary>object.h: ox, where it was last inserted.</summary>
    public short Ox;
    /// <summary>object.h: oy.</summary>
    public short Oy;
    /// <summary>object.h: speed.</summary>
    public float Speed;
    /// <summary>object.h: speed_left.</summary>
    public float SpeedLeft;
    /// <summary>object.h: casting_speed.</summary>
    public float CastingSpeed;
    /// <summary>object.h: nrof (uint32), how many of the objects.</summary>
    public uint Nrof;
    /// <summary>object.h: face.</summary>
    public NewFace? Face;
    /// <summary>object.h: direction (sint8), the object is moving that way.</summary>
    public sbyte Direction;
    /// <summary>object.h: facing (sint8), the object is oriented that way.</summary>
    public sbyte Facing;

    /// <summary>object.h: type (uint8), see define.h.</summary>
    public byte Type;
    /// <summary>object.h: client_type (uint16).</summary>
    public ushort ClientType;

    /// <summary>object.h: resist[NROFATTACKS], resistance adjustments for attacks.</summary>
    public short[] Resist = new short[Define.NROFATTACKS];
    /// <summary>object.h: attacktype (uint32), bit mask of the attacks this object does.</summary>
    public uint Attacktype;
    /// <summary>object.h: path_attuned.</summary>
    public uint PathAttuned;
    /// <summary>object.h: path_repelled.</summary>
    public uint PathRepelled;
    /// <summary>object.h: path_denied.</summary>
    public uint PathDenied;
    /// <summary>object.h: material (uint16), what materials this object consists of.</summary>
    public ushort Material;
    /// <summary>object.h: magic (sint8), any magical bonus.</summary>
    public sbyte Magic;
    /// <summary>object.h: thrownthaco (sint8), how precise the throw is.</summary>
    public sbyte Thrownthaco;
    /// <summary>object.h: state (uint8), how the object was last drawn (animation).</summary>
    public byte State;
    /// <summary>object.h: value (sint32), how much money it is worth.</summary>
    public int Value;
    /// <summary>object.h: level (signed short).</summary>
    public short Level;
    /// <summary>object.h: last_heal (sint32).</summary>
    public int LastHeal;
    /// <summary>object.h: last_sp (sint32).</summary>
    public int LastSp;
    /// <summary>object.h: last_grace (signed short).</summary>
    public short LastGrace;
    /// <summary>object.h: last_eat (signed short), how long since the object last ate.</summary>
    public short LastEat;
    /// <summary>object.h: invisible (signed short), how much longer the object will be invisible.</summary>
    public short Invisible;
    /// <summary>object.h: pick_up (unsigned char).</summary>
    public byte PickUp;

    /// <summary>object.h: owner, the object that controls this one (read it with get_owner).</summary>
    public CfObject? Owner;
    /// <summary>object.h: enemy, monster or player to follow even if not closest.</summary>
    public CfObject? Enemy;
    /// <summary>object.h: attacked_by, this object starts to attack us.</summary>
    public CfObject? AttackedBy;
    /// <summary>object.h: attacked_by_count (tag_t), the tag of the attacker.</summary>
    public uint AttackedByCount;

    /// <summary>object.h: arch, the archetype.</summary>
    public Archetype? Arch;
    /// <summary>object.h: other_arch, used for various things.</summary>
    public Archetype? OtherArch;
    /// <summary>object.h: weight (signed long).</summary>
    public int Weight;
    /// <summary>object.h: carrying (signed long), how much weight this object contains.</summary>
    public int Carrying;
    /// <summary>object.h: flags[4].</summary>
    public uint[] Flags = new uint[4];
    /// <summary>object.h: ownercount (tag_t), what count the owner had in case it has been freed.</summary>
    public uint Ownercount;
    /// <summary>object.h: randomitems, the items to be generated.</summary>
    public TreasureList? Randomitems;
    /// <summary>object.h: run_away (unsigned short), a monster runs away if its hp go below this percentage.</summary>
    public ushort RunAway;

    /// <summary>object.h: chosen_skill, the skill chosen to use.</summary>
    public CfObject? ChosenSkill;
    /// <summary>object.h: exp_obj, the experience object (category) of this object.</summary>
    public CfObject? ExpObj;
    /// <summary>object.h: hide (uint32), the object is hidden, not invisible.</summary>
    public uint Hide;

    /// <summary>object.h: glow_radius (signed short).</summary>
    public short GlowRadius;

    /// <summary>object.h: move_status (signed long), what stage in attack mode.</summary>
    public int MoveStatus;
    /// <summary>object.h: move_type (unsigned short), what kind of attack movement.</summary>
    public ushort MoveType;

    /// <summary>object.h: weight_limit (signed long).</summary>
    public int WeightLimit;
    /// <summary>object.h: can_apply (unsigned char).</summary>
    public byte CanApply;
    /// <summary>object.h: will_apply (unsigned char).</summary>
    public byte WillApply;
    /// <summary>object.h: animation_id (uint16), an index into the animation array.</summary>
    public ushort AnimationId;
    /// <summary>object.h: anim_speed (uint8), ticks between animation frames.</summary>
    public byte AnimSpeed;
    /// <summary>object.h: last_anim (uint8).</summary>
    public byte LastAnim;
    /// <summary>object.h: stats (living).</summary>
    public Living Stats = new();
    /// <summary>object.h: elevation (sint32), elevation of this terrain (not used).</summary>
    public int Elevation;

    // CASTING_TIME is defined in config.h of this commit.

    /// <summary>object.h: casting (signed short), time left before the spell goes off.</summary>
    public short Casting;
    /// <summary>object.h: spell_state (unsigned short).</summary>
    public ushort SpellState;
    /// <summary>object.h: start_holding (unsigned short).</summary>
    public ushort StartHolding;
    /// <summary>object.h: spell.</summary>
    public ISpellHandle? Spell;
    /// <summary>object.h: spelltype (unsigned int).</summary>
    public uint Spelltype;
    /// <summary>object.h: spellarg.</summary>
    public string? Spellarg;

    /// <summary>object.h: spellitem.</summary>
    public CfObject? Spellitem;

    /// <summary>object.h: expmul, the needed experience is calc_exp * expmul.</summary>
    public double Expmul;
    /// <summary>object.h: event_hook[30] (GROS plugin events: index by EVENT_...).</summary>
    public string?[] EventHook = new string?[30];
    /// <summary>object.h: event_plugin[30].</summary>
    public string?[] EventPlugin = new string?[30];
    /// <summary>object.h: event_options[30].</summary>
    public string?[] EventOptions = new string?[30];
    /// <summary>object.h: current_weapon_script, the script of the weapon in use.</summary>
    public string? CurrentWeaponScript;
    /// <summary>object.h: current_weapon, the weapon currently used.</summary>
    public CfObject? CurrentWeapon;
    /// <summary>object.h: weapontype (uint32).</summary>
    public uint Weapontype;

    /// <summary>
    /// Not in C: the lines of the archetype text that the loader of C does not know and ignores (see <see cref="ExtraKeys"/>).
    /// Copied with the object, cleared by clear_object.
    /// </summary>
    public ExtraKeys? Extra;

    /// <summary>include/define.h SET_FLAG.</summary>
    public void SetFlag(int p) => Flags[p / 32] |= 1U << (p % 32);

    /// <summary>include/define.h CLEAR_FLAG.</summary>
    public void ClearFlag(int p) => Flags[p / 32] &= ~(1U << (p % 32));

    /// <summary>include/define.h QUERY_FLAG.</summary>
    public bool QueryFlag(int p) => (Flags[p / 32] & (1U << (p % 32))) != 0;

    /// <summary>The loader's SET_OR_CLEAR_FLAG.</summary>
    public void SetOrClearFlag(int p, int val)
    {
        if (val != 0)
        {
            SetFlag(p);
        }
        else
        {
            ClearFlag(p);
        }
    }

    /// <inheritdoc />
    public override string ToString() => (Name ?? "(null)") + " #" + Count;
}

/// <summary>
/// include/object.h <c>struct archt</c> (typedef <c>archetype</c>): the rules to generate and manipulate objects that point to
/// archetypes; all it holds is the object to copy and the pointers.
/// </summary>
public sealed class Archetype
{
    /// <summary>object.h: name, the definite name like "generate_kobold".</summary>
    public string? Name;
    /// <summary>object.h: next, in the linked list.</summary>
    public Archetype? Next;
    /// <summary>object.h: head, the main part of a linked object.</summary>
    public Archetype? Head;
    /// <summary>object.h: more, the next part of a linked object.</summary>
    public Archetype? More;
    /// <summary>object.h: clone, the object from which to do copy_object().</summary>
    public CfObject Clone = new();
    /// <summary>object.h: editable (uint32), editable flags (E_...).</summary>
    public uint Editable;
    /// <summary>object.h: tail_x (sint8), where the lower right most portion of the object is in comparison to the head.</summary>
    public sbyte TailX;
    /// <summary>object.h: tail_y (sint8).</summary>
    public sbyte TailY;

    /// <inheritdoc />
    public override string ToString() => Name ?? "(null)";
}

/// <summary>object.h <c>objectlink</c>: used to link several objects together (the list of friendly objects).</summary>
public sealed class ObjectLink
{
    /// <summary>object.h: ob.</summary>
    public CfObject? Ob;
    /// <summary>object.h: next.</summary>
    public ObjectLink? Next;
    /// <summary>object.h: id.</summary>
    public int Id;
}
