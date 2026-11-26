// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/c_object.c <c>sack_can_hold</c>, <c>pick_up_object</c>,
// <c>pick_up</c> and <c>command_take</c>, the part of the object commands that takes an object into the player's inventory
// (docs/mud-phase3.md section 3). The drop half is in PickupDrop.cs.
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
namespace Crossfire.Extension.Gk;

/// <summary>
/// Picking up and dropping (server/c_object.c). The object model has no squares: an object on the ground is an object of a level of the
/// extension (a <see cref="LevelObject"/>, the host's NPC), so where C calls <c>remove_ob</c> or <c>insert_ob_in_map</c> for such an
/// object this class asks the dispatcher to take it out of the level (<c>npc.remove</c>) or to put it into one (<c>npc.place</c>).
/// Every function names its C original; what differs from C is in docs/differences.md ("Pick up and drop").
/// </summary>
internal sealed partial class Pickup
{
    private readonly Dispatcher d;

    public Pickup(Dispatcher dispatcher)
    {
        d = dispatcher;
    }

    private CfWorld World => d.WorldOrNull!;

    /// <summary>The level an object was taken from by the running call (a pick up), or null; the caller hands it to <see cref="Dispatcher.SubmitMove"/>.</summary>
    public Level? From { get; private set; }

    /// <summary>The level an object was put into by the running call (a drop), or null.</summary>
    public Level? To { get; private set; }

    /// <summary>True when the running call changed the player's tree.</summary>
    public bool Changed { get; private set; }

    /// <summary>Forgets what the last call did; every entry point of the dispatcher calls it first.</summary>
    public void Begin()
    {
        From = null;
        To = null;
        Changed = false;
    }

    private void Say(CfObject who, string text) => World.Host.DrawInfo(who, text);

    /// <summary>
    /// c_object.c <c>sack_can_hold</c>: true when <paramref name="op"/> can be put into the container <paramref name="sack"/>;
    /// <paramref name="pl"/> (null for none) is told why not. <paramref name="nrof"/> is the number of objects to put in.
    /// </summary>
    public bool SackCanHold(CfObject? pl, CfObject sack, CfObject op, long nrof)
    {
        string buf = "";
        CfWorld w = World;
        if (!sack.QueryFlag(Define.FLAG_APPLIED))
        {
            buf = "The " + w.QueryName(sack) + " is not active.";
        }
        if (sack == op)
        {
            buf = "You can't put the " + w.QueryName(sack) + " into itself.";
        }
        if (sack.Race != null && (sack.Race != op.Race || op.Type == Define.CONTAINER
            || (sack.Stats.Food != 0 && sack.Stats.Food != op.Type)))
        {
            buf = "You can put only " + sack.Race + " into the " + w.QueryName(sack) + ".";
        }
        if (op.Type == Define.SPECIAL_KEY && sack.Slaying != null && op.Slaying != null)
        {
            buf = "You don't want put the key into " + w.QueryName(sack) + ".";
        }
        if (sack.WeightLimit != 0 && sack.Carrying + (nrof != 0 ? nrof : 1)
            * (op.Weight + (op.Type == Define.CONTAINER ? op.Carrying * op.Stats.Str : 0)) * (100 - sack.Stats.Str) / 100 > sack.WeightLimit)
        {
            buf = "That won't fit in the " + w.QueryName(sack) + "!";
        }
        if (buf.Length > 0)
        {
            if (pl != null)
            {
                Say(pl, buf);
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// c_object.c <c>pick_up_object</c>: <paramref name="pl"/> picks <paramref name="nrof"/> (0: all) of <paramref name="tmp"/> up into
    /// <paramref name="op"/> (the player, or a container of his). <paramref name="ground"/> is the level object when <paramref name="tmp"/>
    /// lies on the ground (the object is then in no environment: C's <c>remove_ob</c> of a map object is
    /// <see cref="Dispatcher.RemoveObFromMap"/>); null when it is in a container. Returns true when something was picked up. Not ported:
    /// the <c>FLAG_WAS_WIZ</c> branches (no object here was made by a wizard).
    /// </summary>
    public bool PickUpObject(CfObject pl, CfObject op, CfObject tmp, int nrof, LevelObject? ground)
    {
        CfWorld w = World;
        CfObject? env = tmp.Env;
        long tmpNrof = tmp.Nrof != 0 ? tmp.Nrof : 1;

        // a flying player may only take things out of a container that is in his inventory
        if (pl.QueryFlag(Define.FLAG_FLYING) && !pl.QueryFlag(Define.FLAG_WIZ) && CfWorld.IsPlayerInv(tmp) != pl)
        {
            Say(pl, "You are levitating, you can't reach the ground!");
            return false;
        }
        if (tmp.QueryFlag(Define.FLAG_NO_DROP))
        {
            return false;
        }

        if (nrof > tmpNrof || nrof == 0)
        {
            nrof = (int)tmpNrof;
        }
        // how much weight this object will add to the player
        long weight = (long)tmp.Weight * nrof;
        if (tmp.Inv != null)
        {
            weight += (long)tmp.Carrying * (100 - tmp.Stats.Str) / 100;
        }
        long effectiveWeightLimit = ItemMatching.WeightLimitOf(pl.Stats.Str);
        if ((long)pl.Weight + pl.Carrying + weight > effectiveWeightLimit)
        {
            // Alpaxis's text (docs/mud-phase3.md section 4.2); C says "That item is too heavy for you to pick up."
            Say(pl, "That item is too heavy for you to put it in your inventory.");
            return false;
        }

        LevelObject? whole = null; // the level object that becomes the item itself (a whole stack, not a part of it)
        if (nrof != tmpNrof)
        {
            CfObject tmp2 = tmp;
            uint tmp2Tag = tmp2.Count;
            CfObject? part = w.GetSplitOb(tmp, nrof);
            if (part == null)
            {
                Say(pl, w.ErrMsg);
                return false;
            }
            tmp = part;
            // tell the client what happened to the rest of the objects; a level object tells the host through its event
            if (ground != null)
            {
                d.GroundChanged(ground);
                From = ground.Level;
            }
            else if (CfWorld.WasDestroyed(tmp2, tmp2Tag))
            {
                w.Host.EsrvDelItem(pl.Contr, tmp2Tag);
            }
            else
            {
                w.Host.EsrvSendItem(pl, tmp2);
            }
        }
        else if (ground != null)
        {
            // the object lies on the ground: its NPC goes (the C object is "removed" already, as every object of a level is)
            d.RemoveObFromMap(ground);
            From = ground.Level;
            whole = ground;
        }
        else if (!tmp.QueryFlag(Define.FLAG_REMOVED))
        {
            // in a container: the client is told it left
            if (tmp.Env != null)
            {
                w.Host.EsrvDelItem(pl.Contr, tmp.Count);
            }
            w.RemoveOb(tmp);
        }
        if (tmp.QueryFlag(Define.FLAG_UNPAID))
        {
            Say(pl, w.QueryName(tmp) + " will cost you " + w.QueryCostString(tmp, pl, Define.F_BUY) + ".");
        }
        else
        {
            Say(pl, "You pick up the " + w.QueryName(tmp) + ".");
        }

        // the object that lay on the ground is the item now: its handle and tag stay, the NPC is gone
        if (whole != null)
        {
            d.AdoptGround(whole);
        }
        CfObject inserted = w.InsertObInOb(tmp, op);
        if (whole != null && inserted != tmp)
        {
            d.GroundMerged(whole, tmp); // merged into a stack: the level object is freed and its handle dead
        }
        tmp = inserted;
        Changed = true;

        // all the stuff below deals with the client, and is only usable by players
        if (pl.Type != Define.PLAYER)
        {
            return true;
        }
        w.Host.EsrvSendItem(pl, tmp);
        // these are needed to update the weight of the container the object went into, and of the player, if different
        w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, pl, op);
        if (op != pl)
        {
            w.Host.EsrvSendItem(pl, pl);
        }
        // update the container the object was in
        if (env != null && env != pl && env != op)
        {
            w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, pl, env);
        }
        return true;
    }

    /// <summary>
    /// c_object.c <c>pick_up</c>: <paramref name="op"/> picks up <paramref name="alt"/> (the object of the ground when
    /// <paramref name="ground"/> is its level object, else an object in a container); the number comes from the player's
    /// <c>count</c>. The object goes into the open container, else into a container that fits (the one for its race first), else
    /// into the player. Not ported: the case of no object (C's <c>op-&gt;below</c>: a square the object model does not have) and
    /// <c>stop_item</c> (it only acts on arrows and thrown objects).
    /// </summary>
    public bool PickUp(CfObject op, CfObject alt, LevelObject? ground)
    {
        if (!CfWorld.CanPick(op, alt))
        {
            Say(op, "You can't pick up the " + alt.Name + ".");
            return false;
        }
        CfObject tmp = alt;
        var contr = op.Contr as Player;
        long count = op.Type == Define.PLAYER && contr != null ? (contr.Count == 0 ? tmp.Nrof : contr.Count) : tmp.Nrof;

        CfObject? target;
        if (op.Container != null)
        {
            // the container is open, so use it
            target = op.Container;
            if (target != tmp.Env && !SackCanHold(op, target, tmp, count))
            {
                return false;
            }
        }
        else
        {
            // non container pickup: a container of the race of the object first, then any that holds it
            for (target = op.Inv; target != null; target = target.Below)
            {
                if (target.Type == Define.CONTAINER && target.QueryFlag(Define.FLAG_APPLIED) && target.Race != null && target.Race == tmp.Race
                    && SackCanHold(null, target, tmp, count))
                {
                    break;
                }
            }
            if (target == null)
            {
                for (target = op.Inv; target != null; target = target.Below)
                {
                    if (target.Type == Define.CONTAINER && target.QueryFlag(Define.FLAG_APPLIED) && SackCanHold(null, target, tmp, count))
                    {
                        break;
                    }
                }
            }
            target ??= op; // no free container
        }
        if (tmp.Env == target)
        {
            target = op;
        }

        // startequip items are not allowed to be put into containers
        if (op.Type == Define.PLAYER && target.Type == Define.CONTAINER && tmp.QueryFlag(Define.FLAG_STARTEQUIP))
        {
            Say(op, "This object cannot be put into containers!");
            return false;
        }

        bool picked = PickUpObject(op, target, tmp, (int)Math.Min(count, int.MaxValue), ground);
        if (op.Type == Define.PLAYER && contr != null)
        {
            contr.Count = 0;
        }
        return picked;
    }

    /// <summary>
    /// c_object.c <c>command_take</c>: the player takes the objects of <paramref name="below"/> that match <paramref name="parameters"/>
    /// (all of them that match; without parameters the first one that can be picked up). <paramref name="below"/> is C's
    /// <c>op-&gt;below</c> chain: the objects of the player's square, top first, each with its level object, or the contents of the
    /// open container (level object null).
    /// </summary>
    public void CommandTake(CfObject op, string? parameters, IReadOnlyList<(CfObject Ob, LevelObject? Ground)> below)
    {
        CfWorld w = World;
        if (below.Count == 0)
        {
            Say(op, "Nothing to take!");
            return;
        }
        if (parameters != null && parameters.Length == 0)
        {
            parameters = null; // makes processing easier
        }
        int i = 0;
        for (; i < below.Count; i++)
        {
            (CfObject tmp, LevelObject? ground) = below[i];
            if (tmp.Invisible != 0 || tmp.QueryFlag(Define.FLAG_FREED))
            {
                continue;
            }
            if (parameters != null && ItemMatching.ItemMatchedString(w, op, tmp, parameters) != 0)
            {
                PickUp(op, tmp, ground);
            }
            else if (parameters == null && CfWorld.CanPick(op, tmp))
            {
                PickUp(op, tmp, ground);
                break;
            }
        }
        if (parameters == null && i >= below.Count)
        {
            // nothing was taken: say what the first visible object is (C walks <c>tmp->next</c> here, and only its first step is meaningful)
            foreach ((CfObject tmp, _) in below)
            {
                if (tmp.Invisible == 0)
                {
                    Say(op, "You can't pick up a " + (string.IsNullOrEmpty(tmp.Name) ? "null" : tmp.Name) + ".");
                    return;
                }
            }
            Say(op, "There is nothing to pick up.");
        }
    }
}
