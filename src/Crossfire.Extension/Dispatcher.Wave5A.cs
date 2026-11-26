// Not ported from a Crossfire file: lane 5A of wave 5, objects between a level and a player's inventory (docs/mud-phase3.md section 3;
// docs/extension.md "Pick up and drop"). The ported C is Gk/Pickup.cs and Gk/PickupDrop.cs (server/c_object.c); this file is what
// C's <c>remove_ob</c> and <c>insert_ob_in_map</c> are here (an object on the ground is a level object, shown by the host as an NPC),
// the script's entry points (<c>mudpickupobject</c>, <c>mudpickupobject2</c> and <c>muddropinvobject</c>) and the entry points of the
// commands that wave 7's <c>mudcommand</c> table calls (<see cref="CommandTake"/>, <see cref="CommandDrop"/>, <see cref="CommandDropAll"/>).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    private Pickup? pickupRules;

    /// <summary>The ported pick up and drop functions (c_object.c) of this dispatcher; each entry point of this file calls <see cref="Pickup.Begin"/> first.</summary>
    internal Pickup Pickups => pickupRules ??= new Pickup(this);

    /// <summary>
    /// C's <c>apply_special(op, tmp, AP_UNAPPLY | AP_NO_MERGE)</c>, which <c>drop_object</c> calls for an applied object (wave 6's apply
    /// code sets it): zero when the object is unapplied, else it cannot be. Until it is set an applied object is not dropped.
    /// </summary>
    internal Func<CfObject, CfObject, int>? ApplySpecialUnapply { get; set; }

    /// <summary>
    /// C's <c>put_object_in_sack(op, sack, tmp, nrof)</c>, which <c>drop</c> calls while a container is open (wave 6's container code
    /// sets it); true when the object went in. Until it is set a drop into an open container does nothing.
    /// </summary>
    internal Func<CfObject, CfObject, CfObject, long, bool>? PutObjectInSack { get; set; }

    // ------------------------------------------------------------------ the level side of a move

    /// <summary>
    /// C's <c>remove_ob</c> of an object on a map, as <c>RemoveObFromMap</c>: the object's NPC is deleted (<c>npc.remove</c>, no
    /// <c>object.destroyed</c> will come for it) and the object leaves the level's list. The C object stays as it is (a level
    /// object is "removed" in the sense of C's flag already); the caller makes it an item, or frees it.
    /// </summary>
    internal void RemoveObFromMap(LevelObject ground)
    {
        effects.Add(Val.Map(("k", Val.Str("npc.remove")), ("handle", Val.Handle(handles.HandleOf(ground)))));
        ground.Level.Objects.Remove(ground);
        ground.Level.Dirty = true;
    }

    /// <summary>
    /// C's <c>insert_ob_in_map</c> at a place: a level object is made of <paramref name="ob"/> (a removed object) and shown as a new NPC
    /// (<c>npc.place</c>). An object that has a tag as an item keeps it (its proxy becomes the level object: docs/mud-phase3.md section 3);
    /// a new object gets one. The object is never merged with another on the square. A level object has no script variables of an NPC
    /// that was deleted: no props, no blob.
    /// </summary>
    internal LevelObject InsertObInMap(CfObject ob, DropPlace place)
    {
        LevelObject lo;
        if (itemEntries.Remove(ob, out ItemEntry? entry))
        {
            looseItems.Remove(ob);
            lo = entry.Wrapper;
            lo.Level = place.Level;
        }
        else
        {
            lo = new LevelObject(place.Level, ob);
        }
        lo.X = place.X;
        lo.Y = place.Y;
        lo.Blob = null;
        lo.SeedFlags.Clear();
        lo.Placed = true;
        ob.Map = place.Level;
        place.Level.Objects.Add(lo);
        place.Level.Dirty = true;
        effects.Add(PlaceEffect(lo));
        return lo;
    }

    /// <summary>A level object kept its tag and lost part of its stack: the level is to be saved and the script gets <c>onUpdateObject</c> (docs/mud-phase3.md section 3).</summary>
    internal void GroundChanged(LevelObject ground)
    {
        ground.Level.Dirty = true;
        events.Add(Val.Map(
            ("name", Val.Str(Manifest.EventOnUpdateObject)),
            ("target", Val.Str("object")),
            ("handle", Val.Handle(handles.HandleOf(ground))),
            ("args", Val.List())));
    }

    /// <summary>A level object that left its level is going into an inventory: it is the proxy of its object from now on (the tag and the handle stay).</summary>
    internal void AdoptGround(LevelObject ground)
    {
        ground.Level = Level.Inventory;
        AdoptLevelObject(ground);
    }

    /// <summary>The object of an adopted level object merged into a stack and was freed: the level object is dead, its handle too.</summary>
    internal void GroundMerged(LevelObject ground, CfObject freed)
    {
        itemEntries.Remove(freed);
        looseItems.Remove(freed);
        ground.Dead = true;
        handles.Kill(ground);
    }

    /// <summary>The string of an item of the window, written now (the order of the effects of a drop: the smaller stack, then the face); the flush finds it written.</summary>
    internal void ShowItemNow(Player p, CfObject item)
    {
        CfObject pl = p.State.Ob;
        bool top = item.Env == pl;
        if ((top || (pl.Container != null && item.Env == pl.Container)) && Visible(item))
        {
            SetShown(p, ItemStrings.ItemPrefix + handles.TagOf(TagItem(p, item)), ItemStrings.Text(world!, item, !top));
        }
    }

    /// <summary>The string of an item that left the window is deleted now (<c>esrv_del_item</c>, before the face of a drop comes); nothing when the client holds none.</summary>
    internal void HideItemNow(Player p, CfObject item)
    {
        if (itemEntries.TryGetValue(item, out ItemEntry? entry))
        {
            DeleteShown(p, ItemStrings.ItemPrefix + TagOfItem(entry));
        }
    }

    /// <summary>An item of a player's tree is thrown away (<c>remove_ob</c> and <c>free_object</c>): its tag dies and the window is looked at again.</summary>
    internal void FreeItem(PlayerState ps, CfObject item) => DropItem(ps, item);

    /// <summary>An item that was removed from the tree already is freed: its tag dies and the window is looked at again.</summary>
    internal void FreeRemovedItem(Player p, CfObject item)
    {
        ReleaseItemTags(item);
        KillItem(item);
        p.Tracked.Remove(item);
        world!.FreeObject(item);
        AskForWholeWindow(p.State);
    }

    // ------------------------------------------------------------------ places

    /// <summary>
    /// The place of a position of a level for a drop: the level the extension holds (a gmap's segment, with the position in it), or null
    /// when it holds none of that name, the position is in no segment, or the level has the most objects it may (docs/mud-phase3.md
    /// section 3, rule 8). <paramref name="rawX"/> and <paramref name="rawY"/> are the call's numbers as they came;
    /// <paramref name="forDrop"/> false is for a place that only has to be found (the square of a take), whose level may be full.
    /// </summary>
    internal DropPlace? DropPlaceAt(string? level, double x, double y, Val rawX, Val rawY, bool forDrop = true)
    {
        if (string.IsNullOrEmpty(level) || ResolvePlace(level, RawNumber(rawX) ?? x, RawNumber(rawY) ?? y) is not { } place
            || !levels.TryGetValue(place.Level, out Level? held) || (forDrop && held.Objects.Count >= options.Gk.MaxObjectsPerLevel))
        {
            return null;
        }
        return new DropPlace(held, place.Gmap != null ? place.X : x, place.Gmap != null ? place.Y : y);
    }

    /// <summary>The place of the player's feet: the query <c>player.pos</c> (translated into the gmap's tiles for a script on a gmap), then <see cref="DropPlaceAt"/>.</summary>
    internal DropSite FeetOf(PlayerState ps) => new(site =>
    {
        Outcome pos = PlayerPosition(ctxLevel, ps.Account);
        if (pos.Status != Status.Ok)
        {
            site.Failure = pos;
            return null;
        }
        Val v = pos.Result;
        if (v.Kind != ValKind.List || v.Items.Count != 3 || v.Items[0].Kind != ValKind.String || AsNumber(v.Items[1]) is not { } x || AsNumber(v.Items[2]) is not { } y)
        {
            return null;
        }
        return DropPlaceAt(v.Items[0].AsString, x, y, v.Items[1], v.Items[2]);
    });

    private PlayerState? ActorOfRequest() => ctxPlayer != null && players.TryGetValue(ctxPlayer, out PlayerState? ps) && ps.Contr != null ? ps : null;

    /// <summary>
    /// Writes what a pick up or a drop changed, in the order of docs/mud-phase3.md section 3 rule 9: a move between a level and the
    /// account goes to <see cref="SubmitMove"/> with the source first; a change of the account alone is saved after the quiet time.
    /// </summary>
    private void FinishMove(PlayerState ps)
    {
        Pickup rules = Pickups;
        if (rules.From != null)
        {
            SubmitMove(rules.From, null, null, ps);
        }
        else if (rules.To != null)
        {
            SubmitMove(null, ps, rules.To, null);
        }
        else if (rules.Changed)
        {
            ps.Dirty = true;
        }
    }

    // ------------------------------------------------------------------ the script's entry points

    private partial Outcome? InvokeWave5A(object target, long method, IReadOnlyList<Val> args)
    {
        if (target is LevelObject lo)
        {
            switch (method)
            {
                case TMudObjectMembers.MethodMudPickUpObject:
                    return MudPickUpObject(lo, "mudpickupobject", args);
                case TMudObjectMembers.MethodMudPickUpObject2:
                    return MudPickUpObject(lo, "mudpickupobject2", args);
            }
        }
        return null;
    }

    /// <summary>
    /// <c>mudpickupobject(n)</c> and <c>mudpickupobject2(n)</c>: <c>ctx.player</c> picks up n (0: all) of the object the
    /// receiver is (docs/mud-phase3.md sections 3 and 4.1): <c>pick_up(op, alt)</c> of c_object.c. A ground object that is
    /// <c>nopick</c> or <c>unpaid</c> is refused with false and no text (rule 6). The receiver may also be an item in a container of
    /// the player's own tree: it goes into the pack, or the open container. Anything else (an item of another tree, an item already in
    /// the pack, a level the extension does not hold) is false. Methods have no <c>slow</c> flag: the call answers <c>busy</c> itself.
    /// </summary>
    private Outcome MudPickUpObject(LevelObject target, string name, IReadOnlyList<Val> args)
    {
        if (CheckBusy() is { } busy)
        {
            return busy;
        }
        if (args.Count != 1 || AsNumber(args[0]) is not { } amount)
        {
            return Outcome.Fail(Status.BadArgs, name + " takes a number");
        }
        if (ActorOfRequest() is not { } ps)
        {
            log(1, name + ": the call has no logged-in player");
            return Outcome.Ok(Val.False);
        }
        CfObject pl = ps.Ob;
        var contr = (Player)ps.Contr!;
        Pickups.Begin();
        bool ground = target.Level != Level.Inventory;
        if (amount < 0 || target.Dead)
        {
            return Outcome.Ok(Val.False);
        }
        if (ground)
        {
            if (target.Ob.QueryFlag(Define.FLAG_NO_PICK) || target.Ob.QueryFlag(Define.FLAG_UNPAID))
            {
                return Outcome.Ok(Val.False);
            }
        }
        else if (target.Ob.Env == null || target.Ob.Env == pl || !Holds(pl, target.Ob))
        {
            return Outcome.Ok(Val.False);
        }
        contr.Count = (uint)Math.Min(Math.Truncate(amount), int.MaxValue);
        bool picked;
        try
        {
            picked = Pickups.PickUp(pl, target.Ob, ground ? target : null);
        }
        finally
        {
            contr.Count = 0;
        }
        if (picked)
        {
            FinishMove(ps);
        }
        return Outcome.Ok(Val.Bool(picked));
    }

    /// <summary>
    /// <c>muddropinvobject(id, x, y, n)</c> (a slow function): <c>ctx.player</c> drops n (0: all) of the item of his
    /// tree with that id at (x, y) of the level of the request: <c>drop_object</c> of c_object.c. False, with nothing changed, for an
    /// id that is not an item of his own tree (docs/mud-phase3.md section 4.2), a negative number, an object that cannot be dropped
    /// (<c>FLAG_NO_DROP</c>), or a place on a level the extension does not hold ("You can't drop that here.").
    /// </summary>
    internal Outcome MudDropInvObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 4 || AsNumber(args[0]) is not { } id || AsNumber(args[1]) is not { } x || AsNumber(args[2]) is not { } y
            || AsNumber(args[3]) is not { } amount)
        {
            return Outcome.Fail(Status.BadArgs, "muddropinvobject takes an id, x, y and a number");
        }
        if (ActorOfRequest() is not { } ps)
        {
            log(1, "muddropinvobject: the call has no logged-in player");
            return Outcome.Ok(Val.False);
        }
        Pickups.Begin();
        if (FindItemById(ps, (long)Math.Truncate(id), deep: true) is not { } item || amount < 0)
        {
            return Outcome.Ok(Val.False);
        }
        var site = new DropSite(DropPlaceAt(ctxLevel, x, y, args[1], args[2]));
        bool dropped = Pickups.DropObject(ps.Ob, item, (long)Math.Min(Math.Truncate(amount), int.MaxValue), site, out _);
        if (dropped)
        {
            FinishMove(ps);
        }
        return Outcome.Ok(Val.Bool(dropped));
    }

    // ------------------------------------------------------------------ the commands (wave 7's mudcommand table calls these)

    /// <summary>
    /// The command <c>take</c> (c_object.c <c>command_take</c>) for <paramref name="ps"/>: the objects that match
    /// <paramref name="parameters"/> (without: the first that can be picked up) of the open container, else of the player's square.
    /// The square is the level's objects whose tile is the player's (<c>player.pos</c>, the host's answer), the last placed first.
    /// Output goes through <c>DrawInfo</c>. Answers <c>busy</c> before it changes anything, and the host's failed answer to the
    /// position query.
    /// </summary>
    internal Outcome CommandTake(PlayerState ps, string? parameters)
    {
        if (CheckBusy() is { } busy)
        {
            return busy;
        }
        Pickups.Begin();
        CfObject pl = ps.Ob;
        var below = new List<(CfObject Ob, LevelObject? Ground)>();
        if (pl.Container != null)
        {
            for (CfObject? c = pl.Container.Inv; c != null; c = c.Below)
            {
                below.Add((c, null));
            }
        }
        else
        {
            Outcome pos = PlayerPosition(ctxLevel, ps.Account);
            if (pos.Status != Status.Ok)
            {
                return pos;
            }
            Val v = pos.Result;
            if (v.Kind == ValKind.List && v.Items.Count == 3 && v.Items[0].Kind == ValKind.String && AsNumber(v.Items[1]) is { } x && AsNumber(v.Items[2]) is { } y
                && DropPlaceAt(v.Items[0].AsString, x, y, v.Items[1], v.Items[2], forDrop: false) is { } at)
            {
                double column = Math.Floor(at.X);
                double row = Math.Floor(at.Y);
                for (int i = at.Level.Objects.Count - 1; i >= 0; i--)
                {
                    LevelObject lo = at.Level.Objects[i];
                    if (lo.Placed && Math.Floor(lo.X) == column && Math.Floor(lo.Y) == row)
                    {
                        below.Add((lo.Ob, lo));
                    }
                }
            }
        }
        try
        {
            Pickups.CommandTake(pl, parameters, below);
        }
        finally
        {
            ((Player)ps.Contr!).Count = 0;
        }
        FinishMove(ps);
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>The command <c>drop</c> (c_object.c <c>command_drop</c>): the matching items go to the player's feet (<c>player.pos</c>).</summary>
    internal Outcome CommandDrop(PlayerState ps, string? parameters) => DropCommand(ps, (rules, feet) => rules.CommandDrop(ps.Ob, parameters, feet));

    /// <summary>The command <c>dropall</c> (c_object.c <c>command_dropall</c>); <paramref name="parameters"/> is null, <c>weapons</c>, <c>armor</c> or <c>misc</c>.</summary>
    internal Outcome CommandDropAll(PlayerState ps, string? parameters) => DropCommand(ps, (rules, feet) => rules.CommandDropAll(ps.Ob, parameters, feet));

    private Outcome DropCommand(PlayerState ps, Action<Pickup, DropSite> run)
    {
        if (CheckBusy() is { } busy)
        {
            return busy;
        }
        Pickups.Begin();
        DropSite feet = FeetOf(ps);
        run(Pickups, feet);
        if (feet.Failure is { } failure)
        {
            return failure; // the position query failed before anything changed
        }
        FinishMove(ps);
        return Outcome.Ok(Val.Nil);
    }
}
