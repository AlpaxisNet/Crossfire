// Not ported from a Crossfire file: the part of Crossfire's player controller (include/player.h, <c>struct pl</c>) that phase 3 needs,
// as the type the port's <c>CfObject.Contr</c> holds for a logged-in player. The fields keep the C names (in PascalCase); the
// functions that read and write them are the ports of lanes 4C and 4D (arch.c <c>item_matched_string</c>, <c>find_marked_object</c>,
// socket/item.c <c>MarkItem</c> and the others, port-map group A).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

/// <summary>
/// What the extension remembers of the item strings it has told the host to set for a player (docs/mud-phase3.md section 5): the
/// whole flag name (<c>clientr.mud_item12</c>) with the text the client holds, and which items changed since the last flush. The
/// ported C's <c>esrv_send_item</c>, <c>esrv_update_item</c> and <c>esrv_del_item</c> only mark here; the one flush at the end of
/// each request (<see cref="Dispatcher"/>'s flush hook) writes the effects. Lane 4C fills it; this version holds the data and the
/// marks and writes nothing. Worker only.
/// </summary>
internal sealed class ShownTable
{
    private readonly Player owner;
    private readonly HashSet<uint> dirtySet = new();
    private readonly List<uint> dirty = new();
    private readonly HashSet<uint> removedSet = new();
    private readonly List<uint> removed = new();

    public ShownTable(Player owner)
    {
        this.owner = owner;
    }

    /// <summary>The strings the client holds as far as the extension knows: the whole flag name and its text.</summary>
    public Dictionary<string, string> Sent { get; } = new(StringComparer.Ordinal);

    /// <summary>The tags of the items to send at the next flush, in the order they were first marked.</summary>
    public IReadOnlyList<uint> Dirty => dirty;

    /// <summary>The tags of the items to delete at the next flush, in the order they were first marked.</summary>
    public IReadOnlyList<uint> Removed => removed;

    /// <summary>True when something is marked.</summary>
    public bool HasMarks => dirty.Count > 0 || removed.Count > 0;

    /// <summary>Marks an item as changed (a mark again before the flush changes nothing); a mark undoes an earlier removal.</summary>
    public void MarkDirty(uint tag)
    {
        if (removedSet.Remove(tag))
        {
            removed.Remove(tag);
        }
        if (dirtySet.Add(tag))
        {
            dirty.Add(tag);
        }
        owner.Touch();
    }

    /// <summary>Marks an item as gone; a removal drops an earlier mark as changed.</summary>
    public void MarkRemoved(uint tag)
    {
        if (dirtySet.Remove(tag))
        {
            dirty.Remove(tag);
        }
        if (removedSet.Add(tag))
        {
            removed.Add(tag);
        }
        owner.Touch();
    }

    /// <summary>Empties the marks (the flush has taken them).</summary>
    public void ClearMarks()
    {
        dirtySet.Clear();
        dirty.Clear();
        removedSet.Clear();
        removed.Clear();
    }

    /// <summary>
    /// The effects of a request were lost (its REPLY was replaced by an error): the client holds what it held before, so nothing
    /// is known as sent, and the player is flushed again at the next request.
    /// </summary>
    public void Forget()
    {
        Sent.Clear();
        owner.NeedsResend = true;
        owner.Touch();
    }
}

/// <summary>
/// The player controller of a logged-in player (C: <c>struct pl</c>), made at <c>player.login</c> and set as the <c>Contr</c> of the
/// player's object; <c>player.logout</c> takes it away again. The fields are those that phase 3's functions read or write (port-map
/// group A); the types are those of player.h.
/// </summary>
internal sealed partial class Player : IPlayerHandle
{
    private readonly Action<Player> touched;

    public Player(PlayerState state, Action<Player> touched)
    {
        State = state;
        this.touched = touched;
        Shown = new ShownTable(this);
    }

    /// <summary>The extension's record of the player (the account, the object, the client strings).</summary>
    public PlayerState State { get; }

    public string Account => State.Account;

    /// <summary>C <c>count</c>: the number a command was typed with, which <c>item_matched_string</c> writes.</summary>
    public uint Count { get; set; }

    /// <summary>C <c>count_left</c>.</summary>
    public uint CountLeft { get; set; }

    /// <summary>C <c>mark</c>: the object the player marked.</summary>
    public CfObject? Mark { get; set; }

    /// <summary>C <c>mark_count</c>: the tag the marked object had (a freed object whose tag was reused does not match).</summary>
    public uint MarkCount { get; set; }

    /// <summary>C <c>mode</c>: the pickup mode.</summary>
    public uint Mode { get; set; }

    /// <summary>C <c>last_used</c>.</summary>
    public CfObject? LastUsed { get; set; }

    /// <summary>C <c>last_used_id</c>.</summary>
    public uint LastUsedId { get; set; }

    /// <summary>What the player may carry, as <c>weight_limit[]</c> gives it for the strength (living.c); lane 4C sets it.</summary>
    public long WeightLimit { get; set; }

    /// <summary>The item strings the client holds and the marks for the next flush.</summary>
    public ShownTable Shown { get; }

    /// <summary>The objects of the player's tree that have a tag as items: the sweep of the flush kills the tag of one that leaves the tree.</summary>
    public HashSet<CfObject> Tracked { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>True when the tree of the player changed (an object entered, left or was freed): the next flush looks at the whole window.</summary>
    public bool Reconcile { get; internal set; }

    /// <summary>True while the player is in the dispatcher's list of controllers to flush at the end of the request.</summary>
    public bool NeedsFlush { get; internal set; }

    /// <summary>True after the effects of a request were lost: the flush sends everything again.</summary>
    public bool NeedsResend { get; internal set; }

    /// <summary>Asks for a flush at the end of the running request (once, however often it is called).</summary>
    public void Touch()
    {
        if (!NeedsFlush)
        {
            NeedsFlush = true;
            touched(this);
        }
    }
}
