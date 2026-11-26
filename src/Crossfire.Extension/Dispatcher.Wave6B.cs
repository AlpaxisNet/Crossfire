// Not ported from a Crossfire file: lane 6B of wave 6, the apply core and wearing (docs/mud-phase3.md section 4.4; docs/extension.md
// "Apply"). The ported C is Gk/Apply.cs and Gk/ApplyCommand.cs (server/apply.c and c_object.c); this file is the script's side of it: the
// function <c>mudapplyinvobject</c>, the methods <c>applyinvobject</c>, <c>apply</c>, <c>mudapplyobject</c>, <c>defaultApply</c> and
// <c>getwieldedobject</c>, the events (<c>onApplyItem</c> on the player, then <c>onApply</c> on the item's proxy with the invoke of
// <c>defaultApply</c> as its fallback; the hooks after a change of state) and the setup of the hooks other lanes left for this one.
//
// An item event is a 1.1 feature: on a link of 1.0 no event is sent and an apply runs <c>defaultApply</c> at once. Everything that
// changes items answers <c>busy</c> before it changes anything (docs/mud-phase3.md section 3, rule 10).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    private Apply? applyRules;

    /// <summary>The ported apply functions (apply.c, c_object.c) of this dispatcher.</summary>
    internal Apply Applies => applyRules ??= new Apply(this);

    /// <summary>The world's skill table (null before <c>session.init</c>): what <c>check_skill_to_apply</c> and <c>link_player_skill</c> use.</summary>
    internal SkillTable? SkillTable => gk?.Skills;

    /// <summary>
    /// Sets the hooks the earlier lanes left for the apply code: <c>drop_object</c> of lane 5A unapplies an applied object through
    /// <c>apply_special(op, tmp, AP_UNAPPLY | AP_NO_MERGE)</c>, and <c>change_skill</c> of lane 5D readies a skill through
    /// <c>apply_special</c> (<c>AP_APPLY</c> or <c>AP_UNAPPLY</c>).
    /// </summary>
    partial void InitWave6B(CfWorld w)
    {
        ApplySpecialUnapply = (who, op) => Applies.ApplySpecial(who, op, Apply.ApUnapply | Apply.ApNoMerge);
        if (gk?.Skills is { } skills)
        {
            skills.ApplySpecial = (who, item, flag) => Applies.ApplySpecial(who, item, flag);
        }
    }

    // ------------------------------------------------------------------ the events

    /// <summary>
    /// The place of C's plugin hook in <c>manual_apply</c>: the script's <c>onApply</c> as an item event on the proxy of
    /// <paramref name="item"/> (an item of the applier's tree), or as an object event on the level NPC of an object that lies on a level
    /// (the manifest declares the name for both targets), with the invoke of <c>defaultApply</c> as its fallback (the host runs it when no
    /// class joined to the proxy or the NPC has the function; the class's own function usually calls it). True when the event was sent:
    /// the script decides, and the caller does nothing more. False (the caller applies at once) on a link of 1.0, which has neither, for an
    /// object that is neither in the applier's tree nor placed on a level, and for the commands <c>apply -a</c> and <c>apply -u</c> (the
    /// fallback carries no flag).
    /// </summary>
    internal bool SendApplyEvent(CfObject pl, CfObject item, int aflag)
    {
        if (!Speaks11 || aflag != 0 || pl.Contr is not Player p)
        {
            return false;
        }
        var who = new[] { Val.Handle(handles.HandleOf(p.State)) };
        if (Holds(pl, item))
        {
            LevelObject proxy = ProxyOf(item);
            EmitItemEvent(Manifest.EventOnApply, proxy, TMudObjectMembers.TypeName, new[] { classes.MudTypeClass(item.Type) }, who,
                InvokeFallback("defaultApply", proxy, who));
            return true;
        }
        if (GroundOf(item) is { Placed: true, Dead: false } ground)
        {
            EmitObjectEvent(Manifest.EventOnApply, ground, who, InvokeFallback("defaultApply", ground, who));
            return true;
        }
        return false;
    }

    private LevelObject ProxyOf(CfObject item) => HolderOf(item) is { } holder ? TagItem(holder, item) : ItemProxy(item);

    /// <summary>
    /// The hook after an item was applied or taken off: <c>onWeaponWielded</c> and its kin, player events with the item as a typed
    /// argument, sent after the flag <c>applied</c> is set or cleared (the handlers call <c>getwieldedobject</c>). Nothing for a type
    /// without a hook (a bow, a rod, a horn, a skill, a ring's kind is armour), for an object that is no logged-in player, and on a
    /// link of 1.0 (no typed arguments).
    /// </summary>
    internal void SendApplyHook(CfObject who, CfObject item, bool applied)
    {
        if (!Speaks11 || who.Contr is not Player p || HookName(item.Type, applied) is not { } name)
        {
            return;
        }
        EmitPlayerEvent(name, p.Account, new[] { ItemValue(item) });
    }

    // the name of the hook of a type, or null
    private static string? HookName(int type, bool applied)
    {
        if (Apply.IsWeaponLike(type))
        {
            return applied ? Manifest.EventOnWeaponWielded : Manifest.EventOnWeaponUnWielded;
        }
        if (Apply.IsClothing(type))
        {
            return applied ? Manifest.EventOnClothWorn : Manifest.EventOnClothUnWorn;
        }
        switch (type)
        {
            case Define.WAND:
                return applied ? Manifest.EventOnWandReadied : Manifest.EventOnWandUnReadied;
            case Define.ARMOUR:
            case Define.HELMET:
            case Define.SHIELD:
            case Define.RING:
            case Define.BOOTS:
            case Define.GLOVES:
            case Define.AMULET:
            case Define.GIRDLE:
            case Define.BRACERS:
            case Define.CLOAK:
                return applied ? Manifest.EventOnArmorWorn : Manifest.EventOnArmorUnWorn;
            default:
                return null;
        }
    }

    // ------------------------------------------------------------------ the entry points

    /// <summary>
    /// <c>mudapplyinvobject(id)</c> (a slow function): <c>ctx.player</c> applies the item of his tree with that id (<c>player_apply</c>).
    /// False for an id that is not an item of his own tree (docs/mud-phase3.md section 4.2) or when the apply did not take place.
    /// </summary>
    internal Outcome MudApplyInvObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || WholeNumber(args[0]) is not { } id)
        {
            return BadArgs("mudapplyinvobject takes an id");
        }
        return ContextPlayerOr(ps => ApplyInvObject(ps, id));
    }

    /// <summary>The apply of the item of <paramref name="ps"/>'s own tree with that id (what <c>mudapplyinvobject</c> and <c>applyinvobject</c> do).</summary>
    private Outcome ApplyInvObject(PlayerState ps, long id) =>
        FindItemById(ps, id, deep: true) is { } item ? ApplyFor(ps, item) : Outcome.Ok(Val.False);

    /// <summary>
    /// <c>player_apply</c> for <paramref name="ps"/> and an object that is in his tree or on the ground: <c>onApplyItem</c> first (always),
    /// then the apply (which sends <c>onApply</c>). True when <c>manual_apply</c> answered 1.
    /// </summary>
    private Outcome ApplyFor(PlayerState ps, CfObject item, LevelObject? ground = null)
    {
        if (ps.Contr == null)
        {
            return Outcome.Ok(Val.False);
        }
        if (Speaks11)
        {
            // an object on the ground is shown by its own level object: it has its handle already, and no proxy
            Val shown = ground != null ? Val.Handle(handles.HandleOf(ground)) : ItemValue(item);
            EmitPlayerEvent(Manifest.EventOnApplyItem, ps.Account, new[] { shown });
        }
        int answer = Applies.PlayerApply(ps.Ob, item, 0, quiet: false);
        ps.Dirty = true;
        return Outcome.Ok(Val.Bool(answer == 1));
    }

    // the object a script object stands for when it applies: an item of the actor's own tree, or an object that lies on a level; else null
    private CfObject? ApplicableOf(LevelObject lo, PlayerState actor)
    {
        if (lo.Dead)
        {
            return null;
        }
        if (lo.Level == Level.Inventory)
        {
            return lo.Ob.Env != null && Holds(actor.Ob, lo.Ob) ? lo.Ob : null;
        }
        return lo.Placed && lo.Ob.Env == null ? lo.Ob : null;
    }

    private partial Outcome? InvokeWave6B(object target, long method, IReadOnlyList<Val> args)
    {
        if (!Lane.Wave6B.OwnsMember(method))
        {
            return null;
        }
        switch (target)
        {
            case LevelObject lo:
                switch (method)
                {
                    case TMudObjectMembers.MethodApply:
                    case TMudObjectMembers.MethodMudApplyObject:
                        return args.Count != 0 ? BadArgs("apply takes no arguments") : CheckBusy() ?? ContextPlayerOr(ps =>
                            ApplicableOf(lo, ps) is { } item ? ApplyFor(ps, item, lo.Level == Level.Inventory ? null : lo) : Outcome.Ok(Val.False));
                    case TMudObjectMembers.MethodDefaultApply:
                        return CheckBusy() ?? DefaultApplyMethod(lo, args);
                }
                return null;
            case PlayerState ps:
                switch (method)
                {
                    case TMudPlayerMembers.MethodApplyInvObject:
                        return args.Count == 1 && WholeNumber(args[0]) is { } id
                            ? CheckBusy() ?? ApplyInvObject(ps, id)
                            : BadArgs("applyinvobject takes an id");
                    case TMudPlayerMembers.MethodGetWieldedObject:
                        return args.Count == 0
                            ? Outcome.Ok(ItemValue(TMudPlayerMembers.WieldedObject(ps.Ob)))
                            : BadArgs("getwieldedobject takes no arguments");
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// <c>defaultApply(pl)</c>: the switch of <c>manual_apply</c> without the script's hook, for the player the argument is (the
    /// request's player when it names none): wield, wear, ready, or what another lane does for the type. The answer is what
    /// <c>manual_apply</c> returns (0, 1 or 2), and the lines <c>player_apply</c> says for 0 and 2 are said.
    /// </summary>
    private Outcome DefaultApplyMethod(LevelObject lo, IReadOnlyList<Val> args)
    {
        if (args.Count > 1 || (args.Count == 1 && !args[0].IsNil && args[0].Kind != ValKind.Handle))
        {
            return BadArgs("defaultApply takes a player");
        }
        PlayerState? actor = args.Count == 1 && args[0].Kind == ValKind.Handle ? handles.Resolve(args[0].AsHandle) as PlayerState : ContextState();
        if (actor?.Contr == null || ApplicableOf(lo, actor) is not { } item)
        {
            return Outcome.Ok(Val.Int(1)); // nothing to apply: the object is gone, or it is not the player's to apply (nothing is said)
        }
        int answer = Applies.DefaultApply(actor.Ob, item, 0);
        actor.Dirty = true;
        return Outcome.Ok(Val.Int(answer));
    }
}
