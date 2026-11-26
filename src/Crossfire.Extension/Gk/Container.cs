// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/c_object.c <c>put_object_in_sack</c> and socket/item.c
// <c>esrv_move_object</c>, the part of the object commands that puts an object into a container or takes it out of one (docs/mud-phase3.md
// sections 3 and 4.2). The open and close of a container are in ContainerApply.cs.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/c_object.c, socket/item.c:
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
namespace Crossfire.Extension.Gk;

internal sealed partial class Pickup
{
    /// <summary>newclient.h <c>UPD_FLAGS</c> (the update of the flags of an item; the string of an item is written whole whatever the update says).</summary>
    public const int UpdFlags = 0x02;

    /// <summary>
    /// The level whose file holds <paramref name="ob"/>: the one of the level object at the top of its environments (a container that
    /// lies on the ground, or an object in it); null for an object in a player's tree or in no tree at all.
    /// </summary>
    internal Level? LevelOf(CfObject ob)
    {
        CfObject top = ob;
        while (top.Env != null)
        {
            top = top.Env;
        }
        return top.Type == Define.PLAYER ? null : d.LevelHolding(top);
    }

    /// <summary>
    /// <c>esrv_update_item</c> of a container: nothing for one that lies on the ground. It is no item of the player's window (it has no
    /// string, and the extension would count it as one of his items and take its tag away at the next flush); the window shows what is
    /// inside it while it is open.
    /// </summary>
    private void UpdateItem(int flags, CfObject op, CfObject item)
    {
        if (item.Env == null && LevelOf(item) != null)
        {
            return;
        }
        World.Host.EsrvUpdateItem(flags, op, item);
    }

    /// <summary>
    /// c_object.c <c>put_object_in_sack</c>: <paramref name="op"/> puts <paramref name="nrof"/> (0: all) of <paramref name="tmp"/> into
    /// the container <paramref name="sack"/>. A container that holds objects is not put in: its contents are moved into the open
    /// container instead. Returns true when something went in. An object in a container that lies on the ground, or a container
    /// there, makes the move cross files: <see cref="From"/> and <see cref="To"/> say which level file changed.
    /// </summary>
    public bool PutObjectInSack(CfObject op, CfObject sack, CfObject tmp, long nrof)
    {
        Level? source = LevelOf(tmp);
        Level? destination = LevelOf(sack);
        if (!PutInSack(op, sack, tmp, nrof))
        {
            return false;
        }
        Changed = true;
        if (source != null)
        {
            From = source;
        }
        if (destination != null)
        {
            To = destination;
        }
        return true;
    }

    private bool PutInSack(CfObject op, CfObject sack, CfObject tmp, long nrof)
    {
        CfWorld w = World;
        if (sack == tmp)
        {
            return false; // can't put an object in itself
        }
        if (sack.Type != Define.CONTAINER)
        {
            Say(op, "The " + w.QueryName(sack) + " is not a container.");
            return false;
        }
        if (tmp.QueryFlag(Define.FLAG_STARTEQUIP))
        {
            Say(op, "You cannot put the " + w.QueryName(tmp) + " in the container.");
            return false;
        }
        if (tmp.Type == Define.CONTAINER && tmp.Inv != null)
        {
            // Eneq: an object to be dropped that is a container with something in it: its contents move into the active container instead.
            // C reads op->container here, which is null when the move came from esrv_move_object without an open container; it is
            // the target sack then (differences.md).
            CfObject sack2 = tmp;
            CfObject active = op.Container ?? sack;
            Say(op, "You move the items from " + w.QueryName(tmp) + " into " + w.QueryName(active) + ".");
            bool moved = false;
            CfObject? next;
            for (CfObject? tmp2 = tmp.Inv; tmp2 != null; tmp2 = next)
            {
                next = tmp2.Below;
                if (SackCanHold(op, active, tmp2, tmp2.Nrof))
                {
                    moved |= PutInSack(op, sack, tmp2, 0);
                }
                else
                {
                    Say(op, "Your " + w.QueryName(active) + " fills up.");
                    break;
                }
            }
            UpdateItem(CfWorld.Define_UPD_WEIGHT, op, sack2);
            return moved;
        }

        // a number that is the whole stack is no part of it (the same reading as Pickup.DropObject's, for an object that is no stack)
        long have = tmp.Nrof != 0 ? tmp.Nrof : 1;
        if (nrof == have)
        {
            nrof = 0;
        }
        if (!SackCanHold(op, sack, tmp, nrof != 0 ? nrof : tmp.Nrof))
        {
            return false;
        }

        if (tmp.QueryFlag(Define.FLAG_APPLIED))
        {
            // apply_special(op, tmp, AP_UNAPPLY | AP_NO_MERGE) is the apply code's (wave 6B); without it an applied object stays
            if (d.ApplySpecialUnapply == null || d.ApplySpecialUnapply(op, tmp) != 0)
            {
                return false;
            }
        }

        // we want to put some portion of the item into the container
        if (nrof != 0 && tmp.Nrof != nrof)
        {
            CfObject tmp2 = tmp;
            uint tmp2Tag = tmp2.Count;
            CfObject? part = w.GetSplitOb(tmp, (int)Math.Min(nrof, int.MaxValue));
            if (part == null)
            {
                Say(op, w.ErrMsg);
                return false;
            }
            tmp = part;
            // tell a client what happened to the other objects
            if (CfWorld.WasDestroyed(tmp2, tmp2Tag))
            {
                w.Host.EsrvDelItem(op.Contr, tmp2Tag);
            }
            else
            {
                w.Host.EsrvSendItem(op, tmp2);
            }
        }
        else
        {
            w.RemoveOb(tmp);
        }

        string buf = "You put the " + w.QueryName(tmp) + " in " + w.QueryName(sack) + ".";
        uint tmpTag = tmp.Count;
        CfObject inserted = w.InsertObInOb(tmp, sack);
        Say(op, buf);
        w.Host.FixPlayer(op); // this is overkill, fix_player() is called somewhere in object.c

        // if an object merged (and thus, different object), we need to delete the original
        if (inserted != tmp)
        {
            w.Host.EsrvDelItem(op.Contr, tmpTag);
        }
        w.Host.EsrvSendItem(op, inserted);
        // update the sack's and the player's weight
        UpdateItem(CfWorld.Define_UPD_WEIGHT, op, sack);
        w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, op, op);
        return true;
    }

    /// <summary>
    /// socket/item.c <c>esrv_move_object</c>: the player's move of <paramref name="op"/> (an object of his tree, found by the caller, so that
    /// the ids of the client are his own tree's only) to a new place. <paramref name="toPack"/> is C's <c>to == pl-&gt;count</c>, and the window
    /// of Graal Kingdoms' client for what C calls the ground (docs/mud-phase3.md section 4.2): the object goes into his pack, out of the
    /// container it is in; otherwise <paramref name="env"/> is the container it is put into (null: an unknown place, nothing happens).
    /// Returns true when the object moved.
    /// </summary>
    public bool EsrvMoveObject(CfObject pl, CfObject op, bool toPack, CfObject? env, long nrof)
    {
        if (toPack)
        {
            // return if the player has already picked it up
            if (op.Env == pl)
            {
                return false;
            }
            Level? source = LevelOf(op);
            Level? open = pl.Container != null ? LevelOf(pl.Container) : null;
            var contr = (Player)pl.Contr!;
            contr.Count = (uint)Math.Min(nrof, uint.MaxValue);
            // pick_up puts it into the open container (that lies on the ground, perhaps) when that is not where it is
            bool picked;
            try
            {
                picked = PickUp(pl, op, null);
            }
            finally
            {
                contr.Count = 0;
            }
            if (picked)
            {
                if (source != null)
                {
                    From = source;
                }
                else if (open != null)
                {
                    To = open;
                }
            }
            return picked;
        }
        // not dropped or picked up, so it is put into a sack. put_object_in_sack presumes that the necessary sanity checking has been
        // done (it can be picked up and fits in a sack)
        if (env != null && env.Type == Define.CONTAINER && CfWorld.CanPick(pl, op) && SackCanHold(pl, env, op, nrof))
        {
            return PutObjectInSack(pl, env, op, nrof);
        }
        return false;
    }
}
