// Not ported from a Crossfire file: lane 6A of wave 6, containers, sacks and keys (docs/mud-phase3.md sections 4.2, 4.4 and 5; docs/extension.md,
// "Containers"). The ported C is Gk/Container.cs (put_object_in_sack, esrv_move_object) and Gk/ContainerApply.cs (esrv_apply_container,
// find_key); this file is the script's entry point (mudmoveinvobject), the hook that lane 5A left for a drop into an open container, the apply
// of the types CONTAINER (122) and CLOSE_CON (121), and what C's squares and sockets are here (the level that holds a sack that lies on the
// ground, the window that is looked at again). The lane owns this file; its ids are in the ranges of Lane.Wave6A.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    /// <summary>The setup of lane 6A: a drop while a container is open puts the object into it (<see cref="PutObjectInSack"/>, lane 5A's hook).</summary>
    partial void InitWave6A(CfWorld w) =>
        PutObjectInSack = (op, sack, tmp, nrof) => Pickups.PutObjectInSack(op, sack, tmp, nrof);

    private partial Outcome? InvokeWave6A(object target, long method, IReadOnlyList<Val> args) => null;

    /// <summary>
    /// The apply of a container or of the object that closes one (<c>manual_apply</c> of apply.c): <c>esrv_apply_container</c> of the
    /// player for the container, and for the <c>CLOSE_CON</c> object the container it lies in. Always 1 (applied, or an error was told), as
    /// in C; an object that is no player does nothing (C's <c>apply_container</c> answers 0 for it).
    /// </summary>
    private partial int? ApplyWave6A(CfObject op, CfObject tmp, int aflag)
    {
        switch (tmp.Type)
        {
            case Define.CLOSE_CON:
                Pickups.EsrvApplyContainer(op, tmp.Env); // 0 and nothing done for an applier that is no player, as in C
                return 1;
            case Define.CONTAINER:
                Pickups.EsrvApplyContainer(op, tmp);
                return 1;
            default:
                return null;
        }
    }

    /// <summary>The level whose file holds the object at the top of a tree (a container that lies on the ground), or null when it is no level's object.</summary>
    internal Level? LevelHolding(CfObject top)
    {
        foreach (Level level in levels.Values)
        {
            if (level.Objects.Any(o => o.Ob == top))
            {
                return level;
            }
        }
        return null;
    }

    /// <summary>
    /// A container that lies on the ground is going away (its level unloads, a script destroys it): every player who has it open closes
    /// it first, as <c>esrv_apply_container</c> does, so that no player keeps a container that is freed (a drop would put his things into it
    /// and lose them). C closes a container at the walk-off, which is not ported.
    /// </summary>
    private void CloseOpenOn(CfObject sack)
    {
        foreach (PlayerState ps in players.Values.ToList())
        {
            if (ps.Ob is { Container: { } open } pl && open == sack)
            {
                Pickups.EsrvApplyContainer(pl, sack);
            }
        }
    }

    /// <summary>C's <c>esrv_send_inventory</c> and the like: the whole window of the player is looked at again at the next flush.</summary>
    internal void WholeWindow(PlayerState ps) => AskForWholeWindow(ps);

    /// <summary>
    /// <c>mudmoveinvobject(id, to, count)</c> (a slow function): <c>ctx.player</c> moves <c>count</c> (0: all) of his item <c>id</c>
    /// (<c>esrv_move_object</c> of socket/item.c): into the container <c>to</c> (an item of his tree, or the container he has open), or, for
    /// <c>to</c> 0 or -1 (Graal Kingdoms' window), or the id of the player (C's <c>pl-&gt;count</c>: the tag of his own handle), into his pack, out of the container
    /// it is in. True when it moved. False, with nothing changed, for an id that is not an item of his own tree, a place that is not a
    /// container, a negative count, and the refusals of C (not active, does not fit, too heavy, cannot be picked up), which tell him why.
    /// </summary>
    internal Outcome MudMoveInvObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 3 || AsNumber(args[0]) is not { } id || AsNumber(args[1]) is not { } to || AsNumber(args[2]) is not { } count)
        {
            return Outcome.Fail(Status.BadArgs, "mudmoveinvobject takes an id, a place and a number");
        }
        if (ActorOfRequest() is not { } ps)
        {
            log(1, "mudmoveinvobject: the call has no logged-in player");
            return Outcome.Ok(Val.False);
        }
        Pickups.Begin();
        if (count < 0 || FindItemById(ps, (long)Math.Truncate(id), deep: true) is not { } item)
        {
            return Outcome.Ok(Val.False);
        }
        CfObject pl = ps.Ob;
        long place = (long)Math.Truncate(Math.Max(Math.Min(to, int.MaxValue), int.MinValue));
        bool toPack = false;
        CfObject? env = null;
        if (place <= 0)
        {
            toPack = true;
        }
        else if (FindItemById(ps, place, deep: true) is { } sack)
        {
            env = sack;
        }
        else if (pl.Container != null && handles.TagOf(ItemProxy(pl.Container)) == place)
        {
            env = pl.Container; // the open container: a sack on the ground has no item of its own tree's id, its string says which it is
        }
        else if (place == handles.TagOf(ps))
        {
            toPack = true; // C's pl->count: the id of the player is the tag of his own handle here
        }
        long amount = (long)Math.Min(Math.Truncate(count), int.MaxValue);
        bool moved = Pickups.EsrvMoveObject(pl, item, toPack, env, amount);
        if (moved)
        {
            FinishContainerMove(ps);
        }
        return Outcome.Ok(Val.Bool(moved));
    }

    /// <summary>
    /// Writes what a move into or out of a container changed: the files of <see cref="FinishMove"/>, and for a move inside one level (a
    /// container on the ground into another there) only that level, which changed alone.
    /// </summary>
    private void FinishContainerMove(PlayerState ps)
    {
        Pickup rules = Pickups;
        if (rules.From != null && rules.To != null)
        {
            rules.From.Dirty = true;
            rules.To.Dirty = true;
            return;
        }
        FinishMove(ps);
    }
}
