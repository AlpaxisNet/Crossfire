// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/c_object.c <c>drop_object</c>, <c>drop</c>,
// <c>command_dropall</c> and <c>command_drop</c>, the part of the object commands that puts an object of the inventory on the ground
// (docs/mud-phase3.md section 3). The pick up half is in Pickup.cs.
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

/// <summary>A place on a level the extension holds where an object can be put: the level and the position in it (tiles).</summary>
internal sealed record DropPlace(Level Level, double X, double Y);

/// <summary>
/// Where a drop puts its objects: a fixed place (a script gave it), or the player's feet, found when the first object is dropped (the
/// query <c>player.pos</c>) and kept for the rest of the command. <see cref="Resolve"/> is null when there is no place the extension
/// can use (a level it does not hold, a position in no segment of a gmap, a level that is full); <see cref="Failure"/> is the host's
/// answer when the query failed (the command then answers with it, having changed nothing).
/// </summary>
internal sealed class DropSite
{
    private readonly Func<DropSite, DropPlace?>? find;
    private DropPlace? place;
    private bool found;

    public DropSite(DropPlace? place)
    {
        this.place = place;
        found = true;
    }

    public DropSite(Func<DropSite, DropPlace?> find)
    {
        this.find = find;
    }

    /// <summary>The host's failed answer to the query that asked for the place, or null.</summary>
    public Outcome? Failure { get; set; }

    /// <summary>The place, found at the first call; null when there is none.</summary>
    public DropPlace? Resolve()
    {
        if (!found)
        {
            found = true;
            place = find!(this);
        }
        return place;
    }
}

internal sealed partial class Pickup
{
    /// <summary>The text of a drop that has no place to go to (rule 8 of docs/mud-phase3.md section 3).</summary>
    public const string CannotDropHere = "You can't drop that here.";

    /// <summary>
    /// c_object.c <c>drop_object</c>: <paramref name="op"/> drops <paramref name="nrof"/> (0: all) of <paramref name="tmp"/> at
    /// <paramref name="site"/>. C's <c>insert_ob_in_map</c> at the player's square is <see cref="Dispatcher.InsertObInMap"/> (<c>npc.place</c>
    /// of a new face at the place); the player is no object of a map here, so C's re-insertion of the player is not ported. The
    /// player's name is the <c>mudowner</c> of what he drops (docs/mud-phase3.md section 3, rule 5). Returns true when the object left
    /// the inventory (a starting item that the gods take back counts); <paramref name="placed"/> is its level object when it lies on the
    /// ground now.
    /// </summary>
    public bool DropObject(CfObject op, CfObject tmp, long nrof, DropSite site, out LevelObject? placed)
    {
        CfWorld w = World;
        placed = null;
        if (tmp.QueryFlag(Define.FLAG_NO_DROP))
        {
            return false;
        }
        // the place is known before anything changes: a refused drop leaves the object and its state as they were
        DropPlace? place = site.Resolve();
        if (place == null)
        {
            if (site.Failure == null)
            {
                Say(op, CannotDropHere);
            }
            return false;
        }

        long have = tmp.Nrof != 0 ? tmp.Nrof : 1;
        if (nrof < 0)
        {
            return false;
        }
        if (nrof > have)
        {
            // get_split_ob's own text, also for an object that is not a stack (C would split one off an object of nrof 0 and fail)
            Say(op, "There are only " + have + " " + tmp.Name + "s.");
            return false;
        }
        if (nrof == have)
        {
            nrof = 0; // the whole stack: Alpaxis's reading of C's "nrof && tmp->nrof != nrof" for an object that is no stack
        }

        if (tmp.QueryFlag(Define.FLAG_APPLIED))
        {
            // apply_special(op, tmp, AP_UNAPPLY | AP_NO_MERGE) is the next wave's (apply); until then an applied object stays
            if (d.ApplySpecialUnapply == null || d.ApplySpecialUnapply(op, tmp) != 0)
            {
                return false; // can't unapply it
            }
        }

        var holder = (Player)op.Contr!;
        // we are only dropping some of the items: the new object is split off
        bool part = nrof != 0 && tmp.Nrof != nrof;
        if (part)
        {
            CfObject tmp2 = tmp;
            uint tmp2Tag = tmp2.Count;
            CfObject? split = w.GetSplitOb(tmp, (int)nrof);
            if (split == null)
            {
                Say(op, w.ErrMsg);
                return false;
            }
            tmp = split;
            // tell the client what happened to the rest of the objects; tmp2 is the original object now
            if (CfWorld.WasDestroyed(tmp2, tmp2Tag))
            {
                w.Host.EsrvDelItem(op.Contr, tmp2Tag);
            }
            else
            {
                w.Host.EsrvSendItem(op, tmp2);
                d.ShowItemNow(holder, tmp2);
            }
        }
        else
        {
            w.RemoveOb(tmp);
        }

        if (tmp.QueryFlag(Define.FLAG_STARTEQUIP))
        {
            Say(op, "You drop the " + w.QueryName(tmp) + ".");
            Say(op, "The gods who lent it to you retrieves it.");
            d.HideItemNow(holder, tmp);
            w.Host.EsrvDelItem(op.Contr, tmp.Count);
            d.FreeRemovedItem(holder, tmp);
            w.Host.FixPlayer(op);
            Changed = true;
            return true;
        }

        // the shop floor's sale (C: sell_item on SHOP_FLOOR) is not ported: Graal Kingdoms' shops are scripts
        if (!part)
        {
            d.HideItemNow(holder, tmp); // the client's string goes before the face comes
        }
        w.Host.EsrvDelItem(op.Contr, tmp.Count);
        (tmp.Extra ??= new ExtraKeys()).Set("mudowner", FileText.EscapeLine(FileText.ToBytesText(holder.Account)));
        placed = d.InsertObInMap(tmp, place);
        To = place.Level;
        Changed = true;

        // call this before we update the various windows: the weight is right now (C: fix_player, also done when the object left)
        w.Host.FixPlayer(op);
        // need to update the weight for the player
        w.Host.EsrvSendItem(op, op);
        return true;
    }

    /// <summary>
    /// c_object.c <c>drop</c>: <paramref name="op"/> drops <paramref name="tmp"/> (the number is the player's <c>count</c>), or puts it
    /// into the open container (<see cref="Dispatcher.PutObjectInSack"/>, wave 6's). An invisible object is skipped (one inside a
    /// container that is no player's is thrown away).
    /// </summary>
    public bool Drop(CfObject op, CfObject? tmp, DropSite site)
    {
        CfWorld w = World;
        var contr = (Player)op.Contr!;
        if (tmp != null && tmp.Invisible != 0)
        {
            // if the following is the case, it must be in a container
            if (tmp.Env != null && tmp.Env.Type != Define.PLAYER)
            {
                d.FreeItem(contr.State, tmp);
                return false;
            }
            while (tmp != null && tmp.Invisible != 0)
            {
                tmp = tmp.Below;
            }
        }
        if (tmp == null)
        {
            Say(op, "You don't have anything to drop.");
            return false;
        }
        if (tmp.QueryFlag(Define.FLAG_INV_LOCKED))
        {
            Say(op, "This item is locked");
            return false;
        }
        if (tmp.QueryFlag(Define.FLAG_NO_DROP))
        {
            return false;
        }

        if (contr.LastUsed == tmp && contr.LastUsedId == tmp.Count)
        {
            CfObject? n = tmp.Below ?? tmp.Above;
            contr.LastUsed = n;
            contr.LastUsedId = n?.Count ?? 0;
        }

        bool dropped;
        if (op.Container != null)
        {
            dropped = d.PutObjectInSack != null && d.PutObjectInSack(op, op.Container, tmp, contr.Count);
        }
        else
        {
            dropped = DropObject(op, tmp, contr.Count, site, out _);
        }
        contr.Count = 0;
        return dropped;
    }

    /// <summary>
    /// c_object.c <c>command_dropall</c>: drops everything that is not locked: without parameters all but money, food, keys and gems
    /// (and the open container); with <c>weapons</c>, <c>armor</c> (<c>armour</c>) or <c>misc</c> the objects of those kinds.
    /// </summary>
    public void CommandDropAll(CfObject op, string? parameters, DropSite site)
    {
        if (op.Inv == null)
        {
            Say(op, "Nothing to drop!");
            return;
        }
        CfObject? curinv = op.Inv;
        // care must be taken that the next item is not money: drop() does unknown things to it when dropping in a shop
        CfObject? Next(CfObject cur)
        {
            CfObject? next = cur.Below;
            while (next != null && next.Type == Define.MONEY)
            {
                next = next.Below;
            }
            return next;
        }
        while (curinv != null)
        {
            CfObject? nextinv = Next(curinv);
            bool locked = curinv.QueryFlag(Define.FLAG_INV_LOCKED);
            int type = curinv.Type;
            bool dropIt;
            switch (parameters)
            {
                case null:
                    dropIt = !locked && type != Define.MONEY && type != Define.FOOD && type != Define.KEY && type != Define.SPECIAL_KEY
                        && type != Define.GEM && curinv.Invisible == 0 && (type != Define.CONTAINER || op.Container != curinv);
                    break;
                case "weapons":
                    dropIt = !locked && (type == Define.WEAPON || type == Define.BOW || type == Define.ARROW);
                    break;
                case "armor":
                case "armour":
                    dropIt = !locked && (type == Define.ARMOUR || type == Define.SHIELD || type == Define.HELMET);
                    break;
                case "misc":
                    dropIt = !locked && !curinv.QueryFlag(Define.FLAG_APPLIED) && type is Define.HORN or Define.BOOK or Define.SPELLBOOK
                        or Define.GIRDLE or Define.AMULET or Define.RING or Define.CLOAK or Define.BOOTS or Define.GLOVES or Define.BRACERS
                        or Define.SCROLL or Define.ARMOUR_IMPROVER or Define.WEAPON_IMPROVER or Define.WAND or Define.ROD or Define.POTION;
                    break;
                default:
                    dropIt = false; // C does nothing for another word
                    break;
            }
            if (dropIt)
            {
                Drop(op, curinv, site);
            }
            curinv = nextinv;
        }
    }

    /// <summary>
    /// c_object.c <c>command_drop</c>: drops the objects of the inventory that match <paramref name="parameters"/> (a comma separated
    /// list, with counts and the words of <c>item_matched_string</c>).
    /// </summary>
    public void CommandDrop(CfObject op, string? parameters, DropSite site)
    {
        var contr = (Player)op.Contr!;
        if (parameters == null || parameters.Length == 0)
        {
            Say(op, "Drop what?");
            return;
        }
        bool didOne = false;
        CfObject? next;
        for (CfObject? tmp = op.Inv; tmp != null; tmp = next)
        {
            next = tmp.Below;
            if (tmp.QueryFlag(Define.FLAG_NO_DROP) || tmp.Invisible != 0)
            {
                continue;
            }
            if (ItemMatching.ItemMatchedString(World, op, tmp, parameters) != 0)
            {
                Drop(op, tmp, site);
                didOne = true;
            }
        }
        if (!didOne)
        {
            Say(op, "Nothing to drop.");
        }
        contr.Count = 0;
    }
}
