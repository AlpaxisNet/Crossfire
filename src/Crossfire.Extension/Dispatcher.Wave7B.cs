// Not ported from a Crossfire file: the methods and functions of lane 7B of wave 7, the account completion and trade (docs/extension.md,
// "Methods of the lanes"): what a login does to the tree it read (fix_player, the experience, the equipment hooks), the examine of an
// item of another player (<c>mudexamineinvobject2</c>, <c>examineinvobject</c>) and the gift of an item to another player
// (<c>mudtransferobject</c>). The lane owns this file; its method ids are in the ranges of Lane.Wave7B.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    private partial Outcome? InvokeWave7B(object target, long method, IReadOnlyList<Val> args)
    {
        if (!Lane.Wave7B.OwnsMember(method) || target is not PlayerState ps)
        {
            return null;
        }
        switch (method)
        {
            case TMudPlayerMembers.MethodExamineInvObject:
                return args.Count == 1 && WholeNumber(args[0]) is { } id ? ExamineOwnInvObject(ps, id) : BadArgs("examineinvobject takes an id");
        }
        return null;
    }

    // ------------------------------------------------------------------ what a login does to the tree it read

    /// <summary>
    /// The first part of what a login or a load of an account does (login.c <c>check_login</c>, <see cref="AccountLife.CheckLogin"/>): for
    /// a player that was read from a file the experience and skills are linked and <c>fix_player</c> runs, so that the strings that
    /// follow show the life, the stats and the weight of the items the file holds. A player that was made new is left as it is (he has
    /// no items; the start path fixes him when his class is chosen).
    /// </summary>
    private void FixLoadedAccount(PlayerState ps, bool fromFile)
    {
        if (fromFile && ps.Loaded)
        {
            WithWorldLog(() => { AccountLife.CheckLogin(world!, gk?.Skills, ps.Ob); return 0; });
        }
    }

    /// <summary>
    /// The equipment hooks of a loaded account: for each applied item of the player's tree, in the order of the tree, the player event
    /// of the apply that C's login would have made (<see cref="SendApplyHook"/>: <c>onWeaponWielded</c>, <c>onArmorWorn</c>,
    /// <c>onClothWorn</c>, <c>onWandReadied</c>); nothing for an item of a type without a hook, and none on a link of 1.0. Once for a
    /// load: not for a second <c>player.login</c> of a player that is still logged in.
    /// </summary>
    private void SendLoginHooks(PlayerState ps)
    {
        if (!Speaks11 || !ps.Loaded)
        {
            return;
        }
        for (CfObject? c = ps.Ob.Inv; c != null; c = c.Below)
        {
            if (c.QueryFlag(Define.FLAG_APPLIED))
            {
                SendApplyHook(ps.Ob, c, applied: true);
            }
        }
    }

    // ------------------------------------------------------------------ the other player

    /// <summary>
    /// The query <c>player.at</c> (1.1, docs/mud-protocol.md P8): the player at <paramref name="index"/> of <c>level.players</c> of the
    /// asker's level, when he is logged in here with his account loaded (<paramref name="other"/> is null for no player, a player the
    /// extension does not hold or one whose account is unloaded). The result is the failure to answer with when the host cannot be asked
    /// (a link of 1.0, a host that fails), null otherwise.
    /// </summary>
    private Outcome? PlayerAt(string asker, long index, out PlayerState? other)
    {
        other = null;
        if (index < 0)
        {
            return null; // GK's getplayerindex answers -1 for a player that is not there
        }
        QueryAnswer answer = Ask("player.at", Val.Str(asker), Val.Int(Math.Min(index, int.MaxValue)));
        if (!answer.IsOk)
        {
            return Failed(answer);
        }
        if (answer.Result.Kind == ValKind.String && players.TryGetValue(answer.Result.AsString, out PlayerState? found) && found.Loaded && found.Contr != null)
        {
            other = found;
        }
        return null;
    }

    // ------------------------------------------------------------------ examine

    /// <summary>
    /// <c>mudexamineinvobject2(index, id)</c>: <c>ctx.player</c> reads the examine text of the item <c>id</c> of the player at
    /// <c>index</c> of <c>level.players</c> (GK's trade window: the buyer looks at what the seller offers). The item is looked for in
    /// that player's own tree only. False, with nothing said, for a player that is not there or not logged in here and for an id that is
    /// no item of his tree; a failed query fails the call.
    /// </summary>
    internal Outcome MudExamineInvObject2(IReadOnlyList<Val> args)
    {
        if (args.Count != 2 || WholeNumber(args[0]) is not { } index || WholeNumber(args[1]) is not { } id)
        {
            return BadArgs("mudexamineinvobject2 takes a player index and an id");
        }
        if (ActorOfRequest() is not { } asker)
        {
            log(1, "mudexamineinvobject2: the call has no logged-in player");
            return Outcome.Ok(Val.False);
        }
        if (PlayerAt(asker.Account, index, out PlayerState? owner) is { } failed)
        {
            return failed;
        }
        if (owner == null || FindItemById(owner, id, deep: true) is not { } item)
        {
            return Outcome.Ok(Val.False);
        }
        ExamineBy(asker, item);
        return Outcome.Ok(Val.True);
    }

    /// <summary>
    /// <c>player.examineinvobject(id)</c> (GK's transmogrification table): the player examines the item <c>id</c> of his own tree
    /// (<c>mudexamineinvobject</c> for a player handle). For a player that is not the one of the request's context the id is not looked
    /// for: ids resolve in the tree of <c>ctx.player</c> only (docs/mud-phase3.md section 4.2).
    /// </summary>
    private Outcome ExamineOwnInvObject(PlayerState ps, long id)
    {
        if (ps.Account != ctxPlayer || ActorOfRequest() != ps || FindItemById(ps, id, deep: true) is not { } item)
        {
            return Outcome.Ok(Val.False);
        }
        ExamineBy(ps, item);
        return Outcome.Ok(Val.True);
    }

    // ------------------------------------------------------------------ trade

    /// <summary>
    /// <c>mudtransferobject(id, 0, index, n)</c> (a slow function): <c>ctx.player</c> gives <c>n</c> (0: all) of his item <c>id</c> to the
    /// player at <c>index</c> of <c>level.players</c>. The item goes out of his tree (a part of a stack is split off, an applied item is
    /// taken off first, an open container is closed) and into the other's; both accounts are written, the giver's first
    /// (<see cref="SubmitMove"/>), and the strings of both windows follow at the flush. The second argument must be 0 (what GK's trade
    /// window passes: docs/differences.md says what other values would mean, which nothing shows). True when it moved. False, with
    /// nothing changed, for an id that is not an item of his own tree, more than he has, an item that cannot be dropped, a receiver
    /// that is not there, not logged in here, himself, or who cannot carry it (C's weight test of <c>pick_up_object</c>: he reads
    /// the text a pick up gives); a failed query fails the call.
    /// </summary>
    internal Outcome MudTransferObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 4 || WholeNumber(args[0]) is not { } id || WholeNumber(args[1]) is not { } mode
            || WholeNumber(args[2]) is not { } index || WholeNumber(args[3]) is not { } count)
        {
            return BadArgs("mudtransferobject takes an id, 0, a player index and a number");
        }
        if (ActorOfRequest() is not { } giver || !giver.Loaded)
        {
            log(1, "mudtransferobject: the call has no logged-in player");
            return Outcome.Ok(Val.False);
        }
        Pickups.Begin();
        if (mode != 0 || count < 0 || FindItemById(giver, id, deep: true) is not { } item)
        {
            return Outcome.Ok(Val.False);
        }
        long have = Inventory.StackCount(item);
        if (count > have || item.QueryFlag(Define.FLAG_NO_DROP))
        {
            return Outcome.Ok(Val.False);
        }
        if (PlayerAt(giver.Account, index, out PlayerState? receiver) is { } failed)
        {
            return failed;
        }
        if (receiver == null || receiver == giver)
        {
            return Outcome.Ok(Val.False);
        }
        CfWorld w = world!;
        CfObject src = giver.Ob;
        CfObject dst = receiver.Ob;
        long moving = count == 0 ? have : count;
        if (Trade.TooHeavyFor(dst, item, moving))
        {
            DrawInfoTo(dst, TooHeavyToHold); // C's pick_up_object says it to the one who picks up
            return Outcome.Ok(Val.False);
        }

        // what he has open and what he wears is put away first, as a drop does
        if (src.Container is { } open && (open == item || IsInside(open, item)))
        {
            Pickups.EsrvApplyContainer(src, open);
        }
        if (item.QueryFlag(Define.FLAG_APPLIED) && (ApplySpecialUnapply == null || ApplySpecialUnapply(src, item) != 0))
        {
            return Outcome.Ok(Val.False); // cannot be taken off
        }

        CfObject piece;
        if (moving != have)
        {
            CfObject? split = w.GetSplitOb(item, (int)moving);
            if (split == null)
            {
                DrawInfoTo(src, w.ErrMsg);
                return Outcome.Ok(Val.False);
            }
            piece = split;
        }
        else
        {
            if (src.CurrentWeapon == item)
            {
                src.CurrentWeapon = null;
            }
            if (giver.Contr is { } contr && contr.Mark == item)
            {
                contr.Mark = null;
                contr.MarkCount = 0;
            }
            ReleaseItemTags(item); // the receiver's window knows it by tags of its own
            KillItem(item);
            giver.Contr?.Tracked.Remove(item);
            w.RemoveOb(item);
            piece = item;
        }
        w.InsertObInOb(piece, dst);

        w.Host.FixPlayer(src);
        w.Host.FixPlayer(dst);
        w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, src, src);
        w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, dst, dst);
        AskForWholeWindow(giver);
        AskForWholeWindow(receiver);
        giver.Dirty = true;
        receiver.Dirty = true;
        SubmitMove(null, giver, null, receiver); // the giver's file first: a crash between the two writes loses the item and never copies it
        return Outcome.Ok(Val.True);
    }

    // true when <paramref name="ob"/> is inside <paramref name="outer"/> (at any depth)
    private static bool IsInside(CfObject ob, CfObject outer)
    {
        for (CfObject? c = ob.Env; c != null; c = c.Env)
        {
            if (c == outer)
            {
                return true;
            }
        }
        return false;
    }
}
