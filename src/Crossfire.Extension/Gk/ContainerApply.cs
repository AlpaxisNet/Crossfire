// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/apply.c <c>apply_container</c> and <c>esrv_apply_container</c>, the open
// and close of a container, and server/player.c <c>find_key</c>, the key of a locked one (docs/mud-phase3.md section 4.4 and 5). The put into
// a container and the move out of one are in Container.cs.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/apply.c:
//         Copyright (C) 2001 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     server/player.c:
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

/// <summary>player.h <c>usekeytype</c>: where a player's keys are looked for.</summary>
internal enum UseKeys
{
    /// <summary><c>key_inventory</c>: only in the top level of his inventory.</summary>
    KeyInventory,

    /// <summary><c>keyrings</c>: also in his active containers whose race is "keys".</summary>
    Keyrings,

    /// <summary><c>containers</c>: also in any of his active containers.</summary>
    Containers,
}

internal sealed partial class Pickup
{
    /// <summary>
    /// The <c>usekeys</c> of every player: C's default (the field is zero when the player is made), which only the top level of his
    /// inventory satisfies. The command that changes it is not ported (docs/differences.md); the tests set it to reach the other branches.
    /// </summary>
    public UseKeys UseKeys { get; set; } = UseKeys.KeyInventory;

    /// <summary>
    /// server/player.c <c>find_key</c>: the key of <paramref name="door"/> (a locked container or a door) among the objects of
    /// <paramref name="container"/>, searching the containers inside it too. <paramref name="pl"/> is the player. Returns the key, or null.
    /// A key inside a container is only used when the player's <c>usekeys</c> allows it.
    /// </summary>
    public CfObject? FindKey(CfObject pl, CfObject container, CfObject door)
    {
        CfObject? tmp;
        // should not happen, but sanity checking is never bad
        if (container.Inv == null)
        {
            return null;
        }
        // first, let's try to find a key in the top level inventory
        for (tmp = container.Inv; tmp != null; tmp = tmp.Below)
        {
            if (door.Type == Define.DOOR && tmp.Type == Define.KEY)
            {
                break;
            }
            // for sanity, we should really check the door's type, but other things (like containers) can be locked with special keys
            if (tmp.Slaying != null && tmp.Type == Define.SPECIAL_KEY && tmp.Slaying == door.Slaying)
            {
                break;
            }
        }
        // no key found: search the inventories now; if we find and use a key in one, return at that time
        if (tmp == null)
        {
            for (tmp = container.Inv; tmp != null; tmp = tmp.Below)
            {
                // no reason to search empty containers
                if (tmp.Type == Define.CONTAINER && tmp.Inv != null && FindKey(pl, tmp, door) is { } key)
                {
                    return key;
                }
            }
            return null;
        }
        // we get down here if we have found a key; now if it is in a container, see if we actually want to use it
        if (pl != container)
        {
            // only let players use keys in containers
            if (pl.Contr == null)
            {
                return null;
            }
            if (UseKeys == UseKeys.KeyInventory || !container.QueryFlag(Define.FLAG_APPLIED)
                || (UseKeys == UseKeys.Keyrings && (container.Race == null || container.Race != "keys")))
            {
                Say(pl, "The " + World.QueryName(tmp) + " in your " + World.QueryName(container) + " vibrates as you approach the door");
                return null;
            }
        }
        return tmp;
    }

    /// <summary>
    /// apply.c <c>esrv_apply_container</c>: the player <paramref name="op"/> opens or closes <paramref name="sack"/> (a container of his
    /// inventory or one that lies on the ground, which is a level's object). A container has three states: closed (not applied),
    /// readied (applied but not open: objects that match are put into it) and open (applied, and <c>op-&gt;container</c> points to it).
    /// Returns 1 when something was applied, 0 for an error or no apply. Not ported: the <c>EVENT_CLOSE</c> of the plugins (this
    /// world has no plugins) and <c>esrv_send_inventory</c>, which is the flush's whole look at the window (the next flush writes the
    /// strings of the open container and <c>mud_container</c>).
    /// </summary>
    public int EsrvApplyContainer(CfObject op, CfObject? sack)
    {
        CfWorld w = World;
        CfObject? tmp = op.Container;

        if (op.Type != Define.PLAYER)
        {
            return 0; // this might change
        }
        if (sack == null || sack.Type != Define.CONTAINER)
        {
            w.Log(LogLevel.Error, "esrv_apply_container: " + (sack?.Name ?? "NULL") + " is not container!\n");
            return 0;
        }

        // If we have a currently open container, then it needs to be closed in all cases if we are opening this one up. We then fall
        // through if appropriate for opening the new container.
        if (op.Container != null && sack.QueryFlag(Define.FLAG_APPLIED))
        {
            CfObject open = op.Container;
            if (open.Env != op)
            {
                // if the container is on the ground
                open.ClearFlag(Define.FLAG_WALK_OFF);
                open.ClearFlag(Define.FLAG_FLY_OFF);
            }
            Say(op, "You close " + w.QueryName(open) + ".");
            open.ClearFlag(Define.FLAG_APPLIED);
            op.Container = null;
            UpdateItem(UpdFlags, op, open);
            Touched(op, open);
            if (tmp == sack)
            {
                return 1;
            }
        }

        // If the player is trying to open it (which he must be doing if we got here), and it is locked, check to see if the player has
        // the equipment to open it.
        if (sack.Slaying != null)
        {
            // it's locked
            CfObject? key = FindKey(op, op, sack);
            if (key != null)
            {
                Say(op, "You unlock " + w.QueryName(sack) + " with " + w.QueryName(key) + ".");
            }
            else
            {
                Say(op, "You don't have the key to unlock " + w.QueryName(sack) + ".");
                return 0;
            }
        }

        // By the time we get here, we have made sure any other container has been closed and, if this is a locked container, that the
        // player has the key to open it. The sack is either on the ground, or part of the player's inventory. If on the ground, we
        // assume that the player is opening it, since if it was being closed, that would have been taken care of above.
        if (sack.Env != op)
        {
            // the player is trying to open a sack that belongs to someone else (or is in one): a misbehaving client or script
            if (sack.Env != null)
            {
                Say(op, "You can't open " + w.QueryName(sack));
                return 0;
            }
            // set these so when the player walks off, we can unapply the sack
            sack.SetFlag(Define.FLAG_WALK_OFF);
            sack.SetFlag(Define.FLAG_FLY_OFF);

            sack.ClearFlag(Define.FLAG_APPLIED);
            Say(op, "You open " + w.QueryName(sack) + ".");
            sack.SetFlag(Define.FLAG_APPLIED);
            op.Container = sack;
            UpdateItem(UpdFlags, op, sack);
            Touched(op, sack);
        }
        else
        {
            // the sack is in the player's inventory
            if (sack.QueryFlag(Define.FLAG_APPLIED))
            {
                // a readied sack becoming open
                sack.ClearFlag(Define.FLAG_APPLIED);
                Say(op, "You open " + w.QueryName(sack) + ".");
                sack.SetFlag(Define.FLAG_APPLIED);
                op.Container = sack;
                UpdateItem(UpdFlags, op, sack);
                Touched(op, sack);
            }
            else
            {
                sack.ClearFlag(Define.FLAG_APPLIED);
                Say(op, "You readied " + w.QueryName(sack) + ".");
                sack.SetFlag(Define.FLAG_APPLIED);
                UpdateItem(UpdFlags, op, sack);
                Touched(op, sack);
            }
        }
        return 1;
    }

    /// <summary>
    /// A container's state changed: the window is looked at again at the next flush (<c>esrv_send_inventory</c> and <c>esrv_update_item</c>
    /// of C), and the file that holds it is to be saved: the account's for a container of the player's tree, the level's for one that
    /// lies on the ground (the applied flag is part of what a file holds).
    /// </summary>
    private void Touched(CfObject op, CfObject sack)
    {
        if (op.Contr is not Player p)
        {
            return;
        }
        d.WholeWindow(p.State);
        if (LevelOf(sack) is { } level)
        {
            level.Dirty = true;
        }
        else
        {
            p.State.Dirty = true;
        }
    }
}
