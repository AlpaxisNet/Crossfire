// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/object.c, common/friend.c, include/newclient.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/object.c:
//         Copyright (C) 2001 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     common/friend.c:
//         Copyright (C) 2001 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/newclient.h:
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

/// <summary>common/object.c: <c>freearr_x</c>, <c>freearr_y</c>, <c>maxfree</c> and <c>freedir</c>, the squares around an object in the order they are searched.</summary>
public static class FreeArrays
{
    /// <summary>object.c freearr_x.</summary>
    public static readonly int[] X =
    {
        0, 0, 1, 1, 1, 0, -1, -1, -1, 0, 1, 2, 2, 2, 2, 2, 1, 0, -1, -2, -2, -2, -2, -2, -1,
        0, 1, 2, 3, 3, 3, 3, 3, 3, 3, 2, 1, 0, -1, -2, -3, -3, -3, -3, -3, -3, -3, -2, -1,
    };

    /// <summary>object.c freearr_y.</summary>
    public static readonly int[] Y =
    {
        0, -1, -1, 0, 1, 1, 1, 0, -1, -2, -2, -2, -1, 0, 1, 2, 2, 2, 2, 2, 1, 0, -1, -2, -2,
        -3, -3, -3, -3, -2, -1, 0, 1, 2, 3, 3, 3, 3, 3, 3, 3, 2, 1, 0, -1, -2, -3, -3, -3,
    };

    /// <summary>object.c maxfree.</summary>
    public static readonly int[] MaxFree =
    {
        0, 9, 10, 13, 14, 17, 18, 21, 22, 25, 26, 27, 30, 31, 32, 33, 36, 37, 39, 39, 42, 43, 44, 45,
        48, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49, 49,
    };

    /// <summary>object.c freedir.</summary>
    public static readonly int[] FreeDir =
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 2, 2, 3, 4, 4, 4, 5, 6, 6, 6, 7, 8, 8, 8,
        1, 2, 2, 2, 2, 2, 3, 4, 4, 4, 4, 4, 5, 6, 6, 6, 6, 6, 7, 8, 8, 8, 8, 8,
    };
}

/// <summary>Raised where the C code calls <c>abort()</c> or <c>fatal()</c>: a state that Crossfire does not survive either.</summary>
public sealed class CrossfireFatalException : Exception
{
    /// <summary>Creates the exception.</summary>
    public CrossfireFatalException(string message) : base(message)
    {
    }
}

public sealed partial class CfWorld
{
    /// <summary>
    /// object.c <c>CAN_MERGE</c>: true when the two objects can be merged into one stack. Check nrof before calling it. As in C it sets
    /// FLAG_BEEN_APPLIED on an identified object so that the flags compare equal.
    /// </summary>
    public static bool CanMerge(CfObject ob1, CfObject ob2)
    {
        // A couple of quick sanity checks
        if (ob1 == ob2 || ob1.Type != ob2.Type)
        {
            return false;
        }

        if (ob1.Speed != ob2.Speed)
        {
            return false;
        }
        // Not sure why the following is the case - either the object has to be animated or have a very low speed.
        if (!ob1.QueryFlag(Define.FLAG_ANIMATE) && Math.Abs((double)ob1.Speed) > Define.MIN_ACTIVE_SPEED)
        {
            return false;
        }

        // If the objects have been identified, set the BEEN_APPLIED flag so that the flags below compare equal.
        if (ob1.QueryFlag(Define.FLAG_IDENTIFIED))
        {
            ob1.SetFlag(Define.FLAG_BEEN_APPLIED);
        }
        if (ob2.QueryFlag(Define.FLAG_IDENTIFIED))
        {
            ob2.SetFlag(Define.FLAG_BEEN_APPLIED);
        }

        // 0x400000 on flags[2] is FLAG_INV_LOCKED (being locked in the inventory does not prevent merging); 0x4 in flags[3] is CLIENT_SENT.
        if (ob1.Arch != ob2.Arch
            || ob1.Stats.Sp != ob2.Stats.Sp
            || ob1.Flags[0] != ob2.Flags[0]
            || ob1.Flags[1] != ob2.Flags[1]
            || (ob1.Flags[2] & ~0x400000U) != (ob2.Flags[2] & ~0x400000U)
            || (ob1.Flags[3] & ~0x4U) != (ob2.Flags[3] & ~0x4U)
            || ob1.Name != ob2.Name
            || ob1.Title != ob2.Title
            || ob1.Msg != ob2.Msg
            || ob1.Weight != ob2.Weight
            || ob1.Stats.Food != ob2.Stats.Food
            || !ob1.Resist.AsSpan().SequenceEqual(ob2.Resist)
            || ob1.Attacktype != ob2.Attacktype
            || ob1.Magic != ob2.Magic
            || ob1.Slaying != ob2.Slaying
            || ob1.Value != ob2.Value
            || ob1.AnimationId != ob2.AnimationId
            || ob1.ClientType != ob2.ClientType)
        {
            return false;
        }

        switch (ob1.Type)
        {
            case Define.SCROLL:
                if (ob1.Level != ob2.Level)
                {
                    return false;
                }
                break;

            case Define.RING:
                // Don't merge applied rings: they are the only objects of the same type of which more than 1 can be applied.
                // There is no break: the case falls into the POTION/AMULET check.
                if (ob1.QueryFlag(Define.FLAG_APPLIED) || ob2.QueryFlag(Define.FLAG_APPLIED))
                {
                    return false;
                }
                if (!ob1.Stats.SameAs(ob2.Stats))
                {
                    return false;
                }
                break;

            case Define.POTION:
            case Define.AMULET:
                // This compares the value of the stats and not the pointer itself.
                if (!ob1.Stats.SameAs(ob2.Stats))
                {
                    return false;
                }
                break;
        }
        // Everything passes, must be OK.
        return true;
    }

    /// <summary>
    /// object.c <c>sum_weight</c>: calculates (recursively) the weight an object is carrying. A container with Str set lets its
    /// contents weigh only (100 - Str) percent.
    /// </summary>
    public static int SumWeight(CfObject op)
    {
        int sum = 0;
        for (CfObject? inv = op.Inv; inv != null; inv = inv.Below)
        {
            if (inv.Inv != null)
            {
                SumWeight(inv);
            }
            sum = unchecked(sum + inv.Carrying + (inv.Nrof != 0 ? inv.Weight * (int)inv.Nrof : inv.Weight));
        }
        if (op.Type == Define.CONTAINER && op.Stats.Str != 0)
        {
            sum = unchecked(sum * (100 - op.Stats.Str)) / 100;
        }
        if (op.Carrying != sum)
        {
            op.Carrying = sum;
        }
        return sum;
    }

    /// <summary>
    /// object.c <c>is_player_inv</c>: keeps going up the environments until it finds a player (an item can be buried in a character);
    /// null when there is none. An object that is its own environment is repaired (env = NULL).
    /// </summary>
    public static CfObject? IsPlayerInv(CfObject? op)
    {
        for (; op != null && op.Type != Define.PLAYER; op = op.Env)
        {
            if (op.Env == op)
            {
                op.Env = null;
            }
        }
        return op;
    }

    /// <summary>
    /// object.c <c>dump_object2</c> and <c>dump_object</c>: the text of an object as the save format has it ("arch name", the
    /// differences from the empty archetype, "end"; an object without an archetype is "Object name" and "end"), left in
    /// <see cref="ErrMsg"/> and returned. A null object gives "[NULL pointer]".
    /// </summary>
    public string DumpObject(CfObject? op)
    {
        ErrMsg = DumpText(op);
        return ErrMsg;
    }

    /// <summary>object.c <c>dump_me</c> (GROS): the same dump, returned and not kept in <see cref="ErrMsg"/>.</summary>
    public string DumpMe(CfObject? op) => DumpText(op);

    private string DumpText(CfObject? op)
    {
        if (op == null)
        {
            return "[NULL pointer]";
        }
        var sb = new System.Text.StringBuilder();
        if (op.Arch != null)
        {
            sb.Append("arch ").Append(op.Arch.Name ?? "(null)").Append('\n');
            string? cp = GetObDiff(op, EmptyArchetypeOrBlank().Clone);
            if (cp != null)
            {
                sb.Append(cp);
            }
            sb.Append("end\n");
        }
        else
        {
            sb.Append("Object ").Append(op.Name ?? "(null)").Append('\n');
            sb.Append("end\n");
        }
        return sb.ToString();
    }

    /// <summary>object.c <c>get_nearest_part</c>: the part of the multi-part object <paramref name="op"/> that is closest to <paramref name="pl"/>; the object itself when it has one part.</summary>
    public static CfObject GetNearestPart(CfObject op, CfObject pl)
    {
        if (op.More == null)
        {
            return op;
        }
        CfObject closest = op;
        int lastDist = Distance(op, pl);
        for (CfObject? tmp = op.More; tmp != null; tmp = tmp.More)
        {
            int i = Distance(tmp, pl);
            if (i < lastDist)
            {
                closest = tmp;
                lastDist = i;
            }
        }
        return closest;
    }

    /// <summary>object.c <c>find_object</c>: the object whose count (id) is <paramref name="i"/>, walking the list of used objects; null when there is none.</summary>
    public CfObject? FindObject(int i)
    {
        CfObject? op;
        for (op = Objects; op != null; op = op.Next)
        {
            if (op.Count == unchecked((uint)i))
            {
                break;
            }
        }
        return op;
    }

    /// <summary>object.c <c>find_object_name</c>: the first object whose name is <paramref name="str"/>.</summary>
    public CfObject? FindObjectName(string str)
    {
        CfObject? op;
        for (op = Objects; op != null; op = op.Next)
        {
            if (op.Name == str)
            {
                break;
            }
        }
        return op;
    }

    /// <summary>object.c <c>get_owner</c>: the owner of the object, or null when it has none or has been freed and its tag reused (the owner is then cleared).</summary>
    public static CfObject? GetOwner(CfObject op)
    {
        if (op.Owner == null)
        {
            return null;
        }
        if (!op.Owner.QueryFlag(Define.FLAG_FREED) && op.Owner.Count == op.Ownercount)
        {
            return op.Owner;
        }
        op.Owner = null;
        op.Ownercount = 0;
        return null;
    }

    /// <summary>object.c <c>clear_owner</c>.</summary>
    public static void ClearOwner(CfObject? op)
    {
        if (op == null)
        {
            return;
        }
        if (op.Owner != null && op.Ownercount == op.Owner.Count)
        {
            op.Owner.Refcount--;
        }
        op.Owner = null;
        op.Ownercount = 0;
    }

    /// <summary>object.c <c>set_owner_simple</c>.</summary>
    private static void SetOwnerSimple(CfObject op, CfObject owner)
    {
        // Objects may own objects. Loop on the owner's owner while the counts match (the endless loop of the fireball that owned a
        // poison cloud that owned the fireball came from a freed and reused object, which the counts detect).
        while (owner.Owner != null && owner != owner.Owner && owner.Ownercount == owner.Owner.Count)
        {
            owner = owner.Owner;
        }

        // If the owner still has an owner, we did not resolve to a final owner, so do not add to that.
        if (owner.Owner != null)
        {
            return;
        }

        op.Owner = owner;
        op.Ownercount = owner.Count;
        owner.Refcount++;
    }

    /// <summary>object.c <c>set_skill_pointers</c>.</summary>
    private static void SetSkillPointers(CfObject op, CfObject? chosenSkill, CfObject? expObj)
    {
        op.ChosenSkill = chosenSkill;
        op.ExpObj = expObj;

        // Summoned monsters cannot use skills: the chosen_skill field is needed for calc_skill_exp().
        op.ClearFlag(Define.FLAG_CAN_USE_SKILL);
        op.ClearFlag(Define.FLAG_READY_SKILL);
    }

    /// <summary>object.c <c>set_owner</c>: sets the owner and the skill and experience objects to the owner's current ones.</summary>
    public static void SetOwner(CfObject? op, CfObject? owner)
    {
        if (owner == null || op == null)
        {
            return;
        }
        SetOwnerSimple(op, owner);

        if (owner.Type == Define.PLAYER && owner.ChosenSkill != null)
        {
            SetSkillPointers(op, owner.ChosenSkill, owner.ChosenSkill.ExpObj);
        }
        else if (op.Type != Define.PLAYER)
        {
            op.ClearFlag(Define.FLAG_READY_SKILL);
        }
    }

    /// <summary>
    /// object.c <c>copy_owner</c>: sets the owner to the clone's current owner and the skill and experience objects to the clone's
    /// (the ones that were the owner's when the clone's owner was set).
    /// </summary>
    public static void CopyOwner(CfObject op, CfObject clone)
    {
        CfObject? owner = GetOwner(clone);
        if (owner == null)
        {
            // players don't have owners - they own themselves
            if (clone.Type == Define.PLAYER)
            {
                owner = clone;
            }
            else
            {
                return;
            }
        }
        SetOwnerSimple(op, owner);

        if (clone.ChosenSkill != null)
        {
            SetSkillPointers(op, clone.ChosenSkill, clone.ExpObj);
        }
        else if (op.Type != Define.PLAYER)
        {
            op.ClearFlag(Define.FLAG_READY_SKILL);
        }
    }

    /// <summary>object.c <c>reset_object</c>: resets the vital variables of an object (the strings and events are forgotten, not freed).</summary>
    public void ResetObject(CfObject op)
    {
        op.Name = null;
        op.NamePl = null;
        op.Title = null;
        op.Race = null;
        op.Slaying = null;
        op.Msg = null;
        for (int i = 0; i < 30; i++)
        {
            op.EventHook[i] = null;
            op.EventPlugin[i] = null;
            op.EventOptions[i] = null;
        }
        op.CurrentWeaponScript = null;
        ClearObject(op);
    }

    /// <summary>
    /// object.c <c>clear_object</c>: frees everything allocated by an object and clears all variables and flags to the defaults
    /// (FLAG_REMOVED is set). The face is the blank face. <see cref="CfObject.Extra"/> is cleared too.
    /// </summary>
    public void ClearObject(CfObject op)
    {
        op.Name = null;
        op.NamePl = null;
        op.Title = null;
        op.Race = null;
        op.Slaying = null;
        op.Msg = null;
        for (int tmp = 0; tmp <= Define.NUM_FLAGS / 32; tmp++)
        {
            op.Flags[tmp] = 0;
        }
        // This is more or less true
        op.SetFlag(Define.FLAG_REMOVED);
        op.Map = null;
        op.Below = null;
        op.Above = null;
        op.Owner = null;
        op.Refcount = 0;
        op.Inv = null;
        op.Env = null;
        op.Container = null;
        op.More = null;
        op.Head = null;
        op.Arch = null;
        op.OtherArch = null;
        op.Enemy = null;
        op.Contr = null;
        op.Weight = 0;
        op.Carrying = 0;
        op.WeightLimit = 0;
        op.AnimSpeed = 0;
        op.Level = 0;
        op.Value = 0;
        op.RunAway = 0;
        op.Invisible = 0;
        op.LastHeal = 0;
        op.LastSp = 0;
        op.LastGrace = 0;
        op.LastEat = 0;
        op.Nrof = 0;
        op.Ownercount = 0;
        op.Attacktype = 0;
        Array.Clear(op.Resist);
        op.Stats.Exp = 0;
        op.X = 0;
        op.Y = 0;
        op.Ox = 0;
        op.Oy = 0;
        op.Stats.Dam = 0;
        op.Stats.Wc = 0;
        op.Stats.Ac = 0;
        op.Stats.Luck = 0;
        op.Stats.Food = 0;
        op.Stats.Sp = 0;
        op.Stats.Maxsp = 0;
        op.Stats.Hp = 0;
        op.Stats.Maxhp = 0;
        op.Stats.Grace = 0;
        op.Stats.Maxgrace = 0;
        op.Expmul = 1.0;
        op.Direction = 0;
        op.GlowRadius = 0;
        op.Stats.Str = 0;
        op.Stats.Dex = 0;
        op.Stats.Con = 0;
        op.Stats.Wis = 0;
        op.Stats.Cha = 0;
        op.Stats.Int = 0;
        op.Stats.Pow = 0;
        op.Material = 0;
        op.Magic = 0;
        op.State = 0;
        op.Type = 0;
        op.Face = Faces.BlankFace;

        op.AttackedBy = null;
        op.AttackedByCount = unchecked((uint)-1);
        op.Type = 0;
        op.CastingSpeed = 0f;

        // The object should already have been removed from the speed list before this function is called
        op.Speed = 0;
        op.SpeedLeft = 0;
        op.PickUp = 0;
        op.CanApply = 0;
        op.WillApply = 0;
        op.MoveStatus = 0;
        op.MoveType = 0;
        op.PathRepelled = 0;
        op.PathAttuned = 0;
        op.PathDenied = 0;
        op.Casting = -1;
        op.Spelltype = 0;
        op.Spell = null;
        op.Randomitems = null;
        op.Spellitem = null;
        op.AnimationId = 0;
        op.Weapontype = 0;
        op.ClientType = 0;
        op.Extra = null;
    }

    /// <summary>
    /// object.c <c>copy_object</c>: copies the second object's contents (everything from <c>name</c> on: not the links, the count
    /// or the refcount) into the first <paramref name="op"/>, keeping its FLAG_FREED and FLAG_REMOVED. Like C it gives a speed under
    /// 0 a random <c>speed_left</c> (not in the editor) and updates the active list.
    /// </summary>
    /// <param name="op2">The object copied from (the archetype's clone, for example).</param>
    /// <param name="op">The object copied into.</param>
    public void CopyObject(CfObject op2, CfObject op)
    {
        bool isFreed = op.QueryFlag(Define.FLAG_FREED);
        bool isRemoved = op.QueryFlag(Define.FLAG_REMOVED);

        // memcpy of everything from offsetof(object, name)
        op.Name = op2.Name;
        op.NamePl = op2.NamePl;
        op.Title = op2.Title;
        op.Race = op2.Race;
        op.Slaying = op2.Slaying;
        op.Msg = op2.Msg;
        op.X = op2.X;
        op.Y = op2.Y;
        op.Ox = op2.Ox;
        op.Oy = op2.Oy;
        op.Speed = op2.Speed;
        op.SpeedLeft = op2.SpeedLeft;
        op.CastingSpeed = op2.CastingSpeed;
        op.Nrof = op2.Nrof;
        op.Face = op2.Face;
        op.Direction = op2.Direction;
        op.Facing = op2.Facing;
        op.Type = op2.Type;
        op.ClientType = op2.ClientType;
        Array.Copy(op2.Resist, op.Resist, Define.NROFATTACKS);
        op.Attacktype = op2.Attacktype;
        op.PathAttuned = op2.PathAttuned;
        op.PathRepelled = op2.PathRepelled;
        op.PathDenied = op2.PathDenied;
        op.Material = op2.Material;
        op.Magic = op2.Magic;
        op.Thrownthaco = op2.Thrownthaco;
        op.State = op2.State;
        op.Value = op2.Value;
        op.Level = op2.Level;
        op.LastHeal = op2.LastHeal;
        op.LastSp = op2.LastSp;
        op.LastGrace = op2.LastGrace;
        op.LastEat = op2.LastEat;
        op.Invisible = op2.Invisible;
        op.PickUp = op2.PickUp;
        op.Owner = op2.Owner;
        op.Enemy = op2.Enemy;
        op.AttackedBy = op2.AttackedBy;
        op.AttackedByCount = op2.AttackedByCount;
        op.Arch = op2.Arch;
        op.OtherArch = op2.OtherArch;
        op.Weight = op2.Weight;
        op.Carrying = op2.Carrying;
        Array.Copy(op2.Flags, op.Flags, 4);
        op.Ownercount = op2.Ownercount;
        op.Randomitems = op2.Randomitems;
        op.RunAway = op2.RunAway;
        op.ChosenSkill = op2.ChosenSkill;
        op.ExpObj = op2.ExpObj;
        op.Hide = op2.Hide;
        op.GlowRadius = op2.GlowRadius;
        op.MoveStatus = op2.MoveStatus;
        op.MoveType = op2.MoveType;
        op.WeightLimit = op2.WeightLimit;
        op.CanApply = op2.CanApply;
        op.WillApply = op2.WillApply;
        op.AnimationId = op2.AnimationId;
        op.AnimSpeed = op2.AnimSpeed;
        op.LastAnim = op2.LastAnim;
        op.Stats.CopyFrom(op2.Stats);
        op.Elevation = op2.Elevation;
        op.Casting = op2.Casting;
        op.SpellState = op2.SpellState;
        op.StartHolding = op2.StartHolding;
        op.Spell = op2.Spell;
        op.Spelltype = op2.Spelltype;
        op.Spellarg = op2.Spellarg;
        op.Spellitem = op2.Spellitem;
        op.Expmul = op2.Expmul;
        Array.Copy(op2.EventHook, op.EventHook, 30);
        Array.Copy(op2.EventPlugin, op.EventPlugin, 30);
        Array.Copy(op2.EventOptions, op.EventOptions, 30);
        op.CurrentWeaponScript = op2.CurrentWeaponScript;
        op.CurrentWeapon = op2.CurrentWeapon;
        op.Weapontype = op2.Weapontype;
        op.Extra = op2.Extra?.Copy();

        if (isFreed)
        {
            op.SetFlag(Define.FLAG_FREED);
        }
        if (isRemoved)
        {
            op.SetFlag(Define.FLAG_REMOVED);
        }
        if (op2.Speed < 0 && !Editor)
        {
            op.SpeedLeft = (float)(op2.SpeedLeft - Random() % 200 / 100.0);
        }
        UpdateObSpeed(op);
    }

    /// <summary>
    /// object.c <c>get_object</c>: makes an object (C takes one from the list of unused objects, or expands it), initialised by
    /// clear_object with FLAG_REMOVED set, a new count, and put first in the list of used objects.
    /// </summary>
    public CfObject GetObject()
    {
        var op = new CfObject();
        op.SetFlag(Define.FLAG_FREED);
        op.Count = unchecked((uint)++ObCount);
        op.Name = null;
        op.NamePl = null;
        op.Title = null;
        op.Race = null;
        op.Slaying = null;
        op.Msg = null;
        op.Next = Objects;
        op.Prev = null;
        op.ActiveNext = null;
        op.ActivePrev = null;
        if (Objects != null)
        {
            Objects.Prev = op;
        }
        Objects = op;
        ClearObject(op);
        op.SetFlag(Define.FLAG_REMOVED);
        UsedObjects++;
        return op;
    }

    /// <summary>
    /// object.c <c>update_turn_face</c>: an object with FLAG_IS_TURNABLE gets the face of its direction (SET_ANIMATION) and the map
    /// is told (<see cref="CrossfireHost.UpdateObject"/>).
    /// </summary>
    public void UpdateTurnFace(CfObject op)
    {
        if (!op.QueryFlag(Define.FLAG_IS_TURNABLE) || op.Arch == null)
        {
            return;
        }
        SetAnimation(op, op.Direction);
        Host.UpdateObject(op, Define.UP_OBJ_FACE);
    }

    /// <summary>global.h <c>SET_ANIMATION(ob, newanim)</c>.</summary>
    public void SetAnimation(CfObject ob, int newanim) =>
        ob.Face = Faces[Animations[ob.AnimationId].Faces[newanim]];

    /// <summary>
    /// object.c <c>update_ob_speed</c>: puts the object on the list of active objects when its speed is over MIN_ACTIVE_SPEED, takes it
    /// off when it is not. Does nothing while the archetypes are read (<see cref="ArchInit"/>).
    /// </summary>
    public void UpdateObSpeed(CfObject op)
    {
        // No reason putting the archetypes objects on the speed list, since they never really need to be updated.
        if (op.QueryFlag(Define.FLAG_FREED) && op.Speed != 0)
        {
            Log(LogLevel.Error, "Object " + op.Name + " is freed but has speed.\n");
            op.Speed = 0;
        }
        if (ArchInit)
        {
            return;
        }
        if (Math.Abs((double)op.Speed) > Define.MIN_ACTIVE_SPEED)
        {
            // If already on active list, don't do anything
            if (op.ActiveNext != null || op.ActivePrev != null || op == ActiveObjects)
            {
                return;
            }

            // process_events() expects us to insert the object at the beginning of the list.
            op.ActiveNext = ActiveObjects;
            if (op.ActiveNext != null)
            {
                op.ActiveNext.ActivePrev = op;
            }
            ActiveObjects = op;
        }
        else
        {
            // If not on the active list, nothing needs to be done
            if (op.ActiveNext == null && op.ActivePrev == null && op != ActiveObjects)
            {
                return;
            }

            if (op.ActivePrev == null)
            {
                ActiveObjects = op.ActiveNext;
                if (op.ActiveNext != null)
                {
                    op.ActiveNext.ActivePrev = null;
                }
            }
            else
            {
                op.ActivePrev.ActiveNext = op.ActiveNext;
                if (op.ActiveNext != null)
                {
                    op.ActiveNext.ActivePrev = op.ActivePrev;
                }
            }
            op.ActiveNext = null;
            op.ActivePrev = null;
        }
    }

    /// <summary>
    /// object.c <c>free_object</c>: frees an object that has been removed with <see cref="RemoveOb"/>: its parts, its inventory (put
    /// on the map when the object is on a map in memory and not in a wall, except what has STARTEQUIP, NO_DROP or is a rune), its
    /// place in the active and used lists. Where C puts it in the list of free objects the port lets it go.
    /// </summary>
    public void FreeObject(CfObject ob)
    {
        if (!ob.QueryFlag(Define.FLAG_REMOVED))
        {
            Log(LogLevel.Debug, "Free object called with non removed object\n");
            DumpObject(ob);
        }
        if (ob.QueryFlag(Define.FLAG_FRIENDLY))
        {
            Log(LogLevel.Monster, "Warning: tried to free friendly object.\n");
            RemoveFriendlyObject(ob);
        }
        if (ob.QueryFlag(Define.FLAG_FREED))
        {
            DumpObject(ob);
            Log(LogLevel.Error, "Trying to free freed object.\n" + ErrMsg + "\n");
            return;
        }
        if (ob.More != null)
        {
            FreeObject(ob.More);
            ob.More = null;
        }
        if (ob.Inv != null)
        {
            CfObject? op;
            CfObject? tmp;
            if (ob.Map == null || !Host.MapInMemory(ob.Map) || Host.Wall(ob.Map, ob.X, ob.Y))
            {
                op = ob.Inv;
                while (op != null)
                {
                    tmp = op.Below;
                    RemoveOb(op);
                    FreeObject(op);
                    op = tmp;
                }
            }
            else
            {
                op = ob.Inv;
                while (op != null)
                {
                    tmp = op.Below;
                    RemoveOb(op);
                    if (op.QueryFlag(Define.FLAG_STARTEQUIP) || op.QueryFlag(Define.FLAG_NO_DROP) || op.Type == Define.RUNE)
                    {
                        FreeObject(op);
                    }
                    else
                    {
                        op.X = ob.X;
                        op.Y = ob.Y;
                        Host.InsertObInMap(op, ob.Map, null, 0); // Insert in same map as the environment
                    }
                    op = tmp;
                }
            }
        }
        // Remove object from the active list
        ob.Speed = 0;
        UpdateObSpeed(ob);

        ob.SetFlag(Define.FLAG_FREED);
        ob.Count = 0;
        // First free the object from the used objects list:
        if (ob.Prev == null)
        {
            Objects = ob.Next;
            if (Objects != null)
            {
                Objects.Prev = null;
            }
        }
        else
        {
            ob.Prev.Next = ob.Next;
            if (ob.Next != null)
            {
                ob.Next.Prev = ob.Prev;
            }
        }

        ob.Name = null;
        ob.NamePl = null;
        ob.Title = null;
        ob.Race = null;
        ob.Slaying = null;
        ob.Msg = null;

        ob.Prev = null;
        ob.Next = null;
        UsedObjects--;
    }

    /// <summary>object.c <c>sub_weight</c>: subtracts a number from the weight carried by an object and (outwards) by its environments.</summary>
    public static void SubWeight(CfObject? op, int weight)
    {
        while (op != null)
        {
            if (op.Type == Define.CONTAINER)
            {
                weight = unchecked(weight * (100 - op.Stats.Str)) / 100;
            }
            op.Carrying = unchecked(op.Carrying - weight);
            op = op.Env;
        }
    }

    /// <summary>object.c <c>add_weight</c>: adds weight to an object and updates how much its environments carry.</summary>
    public static void AddWeight(CfObject? op, int weight)
    {
        while (op != null)
        {
            if (op.Type == Define.CONTAINER)
            {
                weight = unchecked(weight * (100 - op.Stats.Str)) / 100;
            }
            op.Carrying = unchecked(op.Carrying + weight);
            op = op.Env;
        }
    }

    /// <summary>
    /// object.c <c>remove_ob</c>: removes the object from the list it is in. An object in an inventory leaves it (the weight of its
    /// environments goes down, the player's stats are fixed) and keeps the coordinates and map of its environment but no environment;
    /// an object on a map is left to <see cref="CrossfireHost.RemoveObFromMap"/>. Removing an object that is removed already is the
    /// error that makes Crossfire abort.
    /// </summary>
    public void RemoveOb(CfObject op)
    {
        if (op.QueryFlag(Define.FLAG_REMOVED))
        {
            DumpObject(op);
            Log(LogLevel.Error, "Trying to remove removed object.\n" + ErrMsg + "\n");
            throw new CrossfireFatalException("Trying to remove removed object.");
        }
        if (op.More != null)
        {
            RemoveOb(op.More);
        }

        op.SetFlag(Define.FLAG_REMOVED);

        // In this case, the object to be removed is in someone's inventory.
        if (op.Env != null)
        {
            if (op.Nrof != 0)
            {
                SubWeight(op.Env, unchecked(op.Weight * (int)op.Nrof));
            }
            else
            {
                SubWeight(op.Env, unchecked(op.Weight + op.Carrying));
            }

            // NO_FIX_PLAYER is set when a great many changes are being made to the player's inventory.
            CfObject? otmp = IsPlayerInv(op.Env);
            if (otmp != null && otmp.Contr != null && !otmp.QueryFlag(Define.FLAG_NO_FIX_PLAYER))
            {
                Host.FixPlayer(otmp);
            }

            if (op.Above != null)
            {
                op.Above.Below = op.Below;
            }
            else
            {
                op.Env.Inv = op.Below;
            }

            if (op.Below != null)
            {
                op.Below.Above = op.Above;
            }

            // Values are set up so that it could be inserted into the map, but that is up to the caller.
            op.X = op.Env.X;
            op.Y = op.Env.Y;
            op.Ox = op.X;
            op.Oy = op.Y;
            op.Map = op.Env.Map;
            op.Above = null;
            op.Below = null;
            op.Env = null;
            return;
        }

        // If we get here, we are removing it from a map
        if (op.Map == null)
        {
            return;
        }
        Host.RemoveObFromMap(op);
    }

    /// <summary>
    /// object.c <c>merge_ob</c>: goes through all objects below and including <paramref name="top"/> (calculated from op when null)
    /// and merges op into the first that matches; returns that object, null when there was no merge.
    /// </summary>
    public CfObject? MergeOb(CfObject op, CfObject? top)
    {
        if (op.Nrof == 0)
        {
            return null;
        }
        if (top == null)
        {
            for (top = op; top != null && top.Above != null; top = top.Above)
            {
            }
        }
        for (; top != null; top = top.Below)
        {
            if (top == op)
            {
                continue;
            }
            if (CanMerge(op, top))
            {
                top.Nrof += op.Nrof;
                op.Weight = 0; // Don't want any adjustments now
                RemoveOb(op);
                FreeObject(op);
                return top;
            }
        }
        return null;
    }

    /// <summary>
    /// object.c <c>get_split_ob</c>: splits <paramref name="nr"/> objects off a stack. The new object (a copy) holds nr and the
    /// original the rest, or is removed and freed when nothing is left. On failure null, the reason in <see cref="ErrMsg"/>.
    /// </summary>
    public CfObject? GetSplitOb(CfObject origOb, int nr)
    {
        bool isRemoved = origOb.QueryFlag(Define.FLAG_REMOVED);

        if (origOb.Nrof < unchecked((uint)nr))
        {
            ErrMsg = "There are only " + (origOb.Nrof != 0 ? origOb.Nrof : 1) + " " + origOb.Name + "s.";
            return null;
        }
        CfObject newob = GetObject();
        CopyObject(origOb, newob);
        origOb.Nrof = unchecked(origOb.Nrof - (uint)nr);
        if (origOb.Nrof < 1)
        {
            if (!isRemoved)
            {
                RemoveOb(origOb);
            }
            FreeObject(origOb);
        }
        else if (!isRemoved)
        {
            if (origOb.Env != null)
            {
                SubWeight(origOb.Env, unchecked(origOb.Weight * nr));
            }
            if (origOb.Env == null && (origOb.Map == null || !Host.MapInMemory(origOb.Map)))
            {
                ErrMsg = "Tried to split object whose map is not in memory.";
                Log(LogLevel.Debug, "Error, Tried to split object whose map is not in memory.\n");
                return null;
            }
        }
        newob.Nrof = unchecked((uint)nr);
        return newob;
    }

    /// <summary>
    /// object.c <c>decrease_ob_nr</c>: decreases the amount of an object by <paramref name="i"/>; when it reaches 0 the object is
    /// removed and freed. Returns the object when something is left, null when not. The client is told through the host.
    /// </summary>
    public CfObject? DecreaseObNr(CfObject op, int i)
    {
        CfObject? tmp;

        if (i == 0) // objects with op->nrof require this check
        {
            return op;
        }

        if (unchecked((uint)i) > op.Nrof)
        {
            i = (int)op.Nrof;
        }

        if (op.QueryFlag(Define.FLAG_REMOVED))
        {
            op.Nrof = unchecked(op.Nrof - (uint)i);
        }
        else if (op.Env != null)
        {
            // is this object in the player's inventory, or a sub container therein?
            tmp = IsPlayerInv(op.Env);
            // nope. Is this a container the player has opened? If so, set tmp to that player.
            if (tmp == null)
            {
                tmp = Host.FindPlayerWithContainer(op.Env);
            }

            if (unchecked((uint)i) < op.Nrof)
            {
                SubWeight(op.Env, unchecked(op.Weight * i));
                op.Nrof = unchecked(op.Nrof - (uint)i);
                if (tmp != null)
                {
                    Host.EsrvSendItem(tmp, op);
                    Host.EsrvUpdateItem(Define_UPD_WEIGHT, tmp, tmp);
                }
            }
            else
            {
                RemoveOb(op);
                op.Nrof = 0;
                if (tmp != null)
                {
                    Host.EsrvDelItem(tmp.Contr, op.Count);
                    Host.EsrvUpdateItem(Define_UPD_WEIGHT, tmp, tmp);
                }
            }
        }
        else
        {
            CfObject? above = op.Above;

            if (unchecked((uint)i) < op.Nrof)
            {
                op.Nrof = unchecked(op.Nrof - (uint)i);
            }
            else
            {
                RemoveOb(op);
                op.Nrof = 0;
            }
            // Since we just removed op, op->above is null
            for (tmp = above; tmp != null; tmp = tmp.Above)
            {
                if (tmp.Type == Define.PLAYER)
                {
                    if (op.Nrof != 0)
                    {
                        Host.EsrvSendItem(tmp, op);
                    }
                    else
                    {
                        Host.EsrvDelItem(tmp.Contr, op.Count);
                    }
                }
            }
        }

        if (op.Nrof != 0)
        {
            return op;
        }
        FreeObject(op);
        return null;
    }

    /// <summary>newclient.h UPD_WEIGHT: the item update flag for the weight (the other UPD_ flags belong to the client).</summary>
    public const int Define_UPD_WEIGHT = 0x04;

    /// <summary>
    /// object.c <c>insert_ob_in_ob</c>: inserts the object in the inventory of <paramref name="where"/> (the head of a multipart
    /// object). An object with nrof is merged into a stack there that CAN_MERGE accepts; the result can then be another object.
    /// The object goes first in the inventory (the client has no idea of ordering). Returns the object that is in the inventory.
    /// </summary>
    public CfObject InsertObInOb(CfObject op, CfObject? where)
    {
        CfObject? tmp;
        CfObject? otmp;

        if (!op.QueryFlag(Define.FLAG_REMOVED))
        {
            DumpObject(op);
            Log(LogLevel.Error, "Trying to insert (ob) inserted object.\n" + ErrMsg + "\n");
            return op;
        }
        if (where == null)
        {
            DumpObject(op);
            Log(LogLevel.Error, "Trying to put object in NULL.\n" + ErrMsg + "\n");
            return op;
        }
        if (where.Head != null)
        {
            Log(LogLevel.Debug, "Warning: Tried to insert object wrong part of multipart object.\n");
            where = where.Head;
        }
        if (op.More != null)
        {
            Log(LogLevel.Error, "Tried to insert multipart object " + op.Name + " (" + op.Count + ")\n");
            return op;
        }
        op.ClearFlag(Define.FLAG_OBJ_ORIGINAL);
        op.ClearFlag(Define.FLAG_REMOVED);
        if (op.Nrof != 0)
        {
            for (tmp = where.Inv; tmp != null; tmp = tmp.Below)
            {
                if (CanMerge(tmp, op))
                {
                    // return the original object and remove inserted object (client needs the original object)
                    tmp.Nrof += op.Nrof;
                    // Weight handling gets pretty funky. Since we are adding to tmp->nrof, we need to increase the weight.
                    AddWeight(where, unchecked(op.Weight * (int)op.Nrof));
                    op.SetFlag(Define.FLAG_REMOVED);
                    FreeObject(op); // free the inserted object
                    op = tmp;
                    RemoveOb(op); // and fix old object's links
                    op.ClearFlag(Define.FLAG_REMOVED);
                    break;
                }
            }

            // I assume combined objects have no inventory. We add the weight - this object could have just been removed (if it was
            // possible to merge). Calling remove_ob will subtract the weight, so we need to add it in again, since we actually do
            // the linking below.
            AddWeight(where, unchecked(op.Weight * (int)op.Nrof));
        }
        else
        {
            AddWeight(where, unchecked(op.Weight + op.Carrying));
        }

        otmp = IsPlayerInv(where);
        if (otmp != null && otmp.Contr != null)
        {
            if (!otmp.QueryFlag(Define.FLAG_NO_FIX_PLAYER))
            {
                Host.FixPlayer(otmp);
            }
        }

        op.Map = null;
        op.Env = where;
        op.Above = null;
        op.Below = null;
        op.X = 0;
        op.Y = 0;
        op.Ox = 0;
        op.Oy = 0;

        // reset the light list and los of the players on the map
        if (op.GlowRadius > 0 && where.Map != null)
        {
            if (Host.MapDarkness(where.Map) != 0)
            {
                Host.UpdateAllLos(where.Map, where.X, where.Y);
            }
        }

        // Client has no idea of ordering so lets not bother ordering it here. It sure simplifies this function...
        if (where.Inv == null)
        {
            where.Inv = op;
        }
        else
        {
            op.Below = where.Inv;
            op.Below.Above = op;
            where.Inv = op;
        }
        return op;
    }

    /// <summary>object.c <c>present_in_ob</c>: the first object in the inventory of <paramref name="op"/> of this type, or null.</summary>
    public static CfObject? PresentInOb(byte type, CfObject op)
    {
        for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Type == type)
            {
                return tmp;
            }
        }
        return null;
    }

    /// <summary>object.c <c>present_arch_in_ob</c>: the first object in the inventory of <paramref name="op"/> made from this archetype, or null.</summary>
    public static CfObject? PresentArchInOb(Archetype at, CfObject op)
    {
        for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Arch == at)
            {
                return tmp;
            }
        }
        return null;
    }

    /// <summary>object.c <c>set_cheat</c>: sets the cheat flag (WAS_WIZ) in the object and (recursively) in its inventory.</summary>
    public static void SetCheat(CfObject op)
    {
        op.SetFlag(Define.FLAG_WAS_WIZ);
        if (op.Inv != null)
        {
            for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
            {
                SetCheat(tmp);
            }
        }
    }

    /// <summary>object.c <c>distance</c>: the square of the distance between two objects.</summary>
    public static int Distance(CfObject ob1, CfObject ob2) =>
        (ob1.X - ob2.X) * (ob1.X - ob2.X) + (ob1.Y - ob2.Y) * (ob1.Y - ob2.Y);

    /// <summary>object.c <c>find_dir_2</c>: the direction in which an object whose coordinates minus another's are (x, y) must go to reach it.</summary>
    public static int FindDir2(int x, int y)
    {
        int q;
        if (y == 0)
        {
            q = -300 * x;
        }
        else
        {
            q = x * 100 / y;
        }
        if (y > 0)
        {
            if (q < -242)
            {
                return 3;
            }
            if (q < -41)
            {
                return 2;
            }
            if (q < 41)
            {
                return 1;
            }
            if (q < 242)
            {
                return 8;
            }
            return 7;
        }
        if (q < -242)
        {
            return 7;
        }
        if (q < -41)
        {
            return 6;
        }
        if (q < 41)
        {
            return 5;
        }
        if (q < 242)
        {
            return 4;
        }
        return 3;
    }

    /// <summary>object.c <c>absdir</c>: a direction number between 1 and 8 (takes care of overflow in earlier calculations).</summary>
    public static int Absdir(int d)
    {
        while (d < 1)
        {
            d += 8;
        }
        while (d > 8)
        {
            d -= 8;
        }
        return d;
    }

    /// <summary>object.c <c>dirdiff</c>: how many 45-degree differences there are between two absolute directions.</summary>
    public static int Dirdiff(int dir1, int dir2)
    {
        int d = Math.Abs(dir1 - dir2);
        if (d > 4)
        {
            d = 8 - d;
        }
        return d;
    }

    /// <summary>object.c <c>can_pick</c>: true when the picker can pick the item up (weight over 0, not NO_PICK, not alive, not invisible, and a player or the item weighs under a third of the picker).</summary>
    public static bool CanPick(CfObject who, CfObject item) =>
        item.Weight > 0 && !item.QueryFlag(Define.FLAG_NO_PICK) && !item.QueryFlag(Define.FLAG_ALIVE) && item.Invisible == 0
        && (who.Type == Define.PLAYER || item.Weight < who.Weight / 3);

    /// <summary>
    /// object.c <c>ObjectCreateClone</c>: a copy of an object with its other parts and its inventory (coordinates relative to the
    /// head). The result is the head of the new body.
    /// </summary>
    public CfObject? ObjectCreateClone(CfObject? asrc)
    {
        CfObject? dst = null;
        CfObject? prev = null;

        if (asrc == null)
        {
            return null;
        }
        CfObject src = asrc;
        if (src.Head != null)
        {
            src = src.Head;
        }

        for (CfObject? part = src; part != null; part = part.More)
        {
            CfObject tmp = GetObject();
            CopyObject(part, tmp);
            tmp.X = unchecked((short)(tmp.X - src.X));
            tmp.Y = unchecked((short)(tmp.Y - src.Y));
            if (part.Head == null)
            {
                dst = tmp;
                tmp.Head = null;
            }
            else
            {
                tmp.Head = dst;
            }
            tmp.More = null;
            if (prev != null)
            {
                prev.More = tmp;
            }
            prev = tmp;
        }
        // copy inventory
        for (CfObject? item = src.Inv; item != null; item = item.Below)
        {
            InsertObInOb(ObjectCreateClone(item)!, dst);
        }

        return dst;
    }

    /// <summary>object.c <c>was_destroyed</c>: true when the object has been freed (its count changed, or FLAG_FREED).</summary>
    public static bool WasDestroyed(CfObject op, uint oldTag) => op.Count != oldTag || op.QueryFlag(Define.FLAG_FREED);

    /// <summary>object.c <c>count_used</c>: the number of objects on the list of used objects.</summary>
    public int CountUsed()
    {
        int i = 0;
        for (CfObject? tmp = Objects; tmp != null; tmp = tmp.Next)
        {
            i++;
        }
        return i;
    }

    /// <summary>object.c <c>count_active</c>: the number of objects on the list of active objects.</summary>
    public int CountActive()
    {
        int i = 0;
        for (CfObject? tmp = ActiveObjects; tmp != null; tmp = tmp.ActiveNext)
        {
            i++;
        }
        return i;
    }

    /// <summary>
    /// object.c <c>load_object_str</c> (GROS): makes an object from a string with the text of its save format. C writes the string
    /// to a temporary file and reads it back; the port reads the string. Multipart objects are not supported, as in C. The object is
    /// not removed (FLAG_REMOVED is cleared).
    /// </summary>
    public CfObject LoadObjectStr(string obstr)
    {
        CfObject op = GetObject();
        LoadObject(new LoadSource(obstr), op, Define.LO_NEWFILE, 0);
        Log(LogLevel.Debug, " load str completed, object=" + op.Name + "\n");
        op.ClearFlag(Define.FLAG_REMOVED);
        return op;
    }
}
