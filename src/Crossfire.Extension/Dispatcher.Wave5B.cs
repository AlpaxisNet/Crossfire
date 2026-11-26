// Not ported from a Crossfire file: lane 5B of wave 5, the inventory functions (docs/mud-phase3.md section 4.2): the global functions
// mudaddinvobject, mudremoveinvobject, mudcountinvobject, mudfindinvobject, mudresetinvobject, mudorderinventory, findMudObject,
// mudhaveskill, mudskilllevel and the four lists of the quick menu; the methods of TMudPlayer (addinvobject and its kin) and the
// inventory subset and muddecreasequantity of TMudObject. The C they use is in Gk/Inventory.cs and Gk/ItemMatching.cs.
//
// Every id a script gives is looked up in the tree of the player it names (the request's player for a function, the receiver for a
// method): the id of another player's item, of an object of a level or of a dead handle is an unknown id (section 4.2). Every object
// that is made comes from NewItem (identified at creation). What a function changes is told to the client by the object model's
// hooks (the flush of lane 4C), and the account's file is marked dirty. What moves items is slow, or asks CheckBusy() first, and
// answers busy before anything changes (section 3, rule 10).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    /// <summary>The text a player reads when what he gets would be more than he can carry (docs/mud-phase3.md section 4.2).</summary>
    internal const string TooHeavyToHold = "That item is too heavy for you to put it in your inventory.";

    // ------------------------------------------------------------------ the arguments

    // the player of the request's context when he is logged in
    private PlayerState? ContextState() => ctxPlayer != null && players.TryGetValue(ctxPlayer, out PlayerState? ps) ? ps : null;

    // a count or an id as a whole number: nil is 0, a float is cut; null for a value that is no number
    private static long? WholeNumber(Val v) => AsNumber(v) is { } d ? (long)Math.Clamp(Math.Truncate(d), -1e15, 1e15) : null;

    // a text argument as the bytes the world uses; nil is the empty text; null for a value of another kind
    private static string? BytesText(Val v) => v.IsNil || v.Kind == ValKind.String ? FileText.ToBytesText(v.AsString) : null;

    // the truthiness of a bool argument (the host sends a bool; a number is true unless it is 0)
    private static bool Truthy(Val v) => v.Kind switch
    {
        ValKind.True => true,
        ValKind.Int => v.AsInt != 0,
        ValKind.Float => v.AsFloat != 0,
        _ => false,
    };

    private static Outcome BadArgs(string text) => Outcome.Fail(Status.BadArgs, text);

    // the handle of an item that is in a tree (nil for none): the same handle whatever the number of calls
    private Val ItemValue(CfObject? item) => item == null ? Val.Nil : Val.Handle(((IMemberEnv)this).ItemHandleOf(item));

    // the id the client knows an item by (its tag)
    private long ItemId(CfObject item) => handles.TagOf(HolderOf(item) is { } holder ? TagItem(holder, item) : ItemProxy(item));

    // ------------------------------------------------------------------ what changes a tree

    /// <summary>
    /// <c>addinvobject</c>: <paramref name="count"/> objects of the archetype named <paramref name="name"/> (<c>arch_to_object</c> by
    /// <see cref="NewItem"/>, <c>nrof</c> = count) go into the tree of the player (<c>insert_ob_in_ob</c>: they merge into a stack that
    /// can take them). Null for a count that is not 1 to <see cref="Inventory.MaxAdd"/>, an archetype the world does not have, a
    /// player whose account is not loaded, and (unless <paramref name="ignoreWeight"/>) more than he can carry: then he reads
    /// <see cref="TooHeavyToHold"/>. The result is the stack that now holds them.
    /// </summary>
    private CfObject? AddInv(PlayerState ps, long count, string name, bool ignoreWeight)
    {
        if (!ps.Loaded || ps.Contr == null || count <= 0 || count > Inventory.MaxAdd)
        {
            return null;
        }
        Archetype? at = world!.FindArchetype(name);
        if (at == null)
        {
            return null;
        }
        CfObject pl = ps.Ob;
        long weight = (long)at.Clone.Weight * count;
        if (ignoreWeight)
        {
            if ((long)pl.Carrying + weight > int.MaxValue / 2)
            {
                return null; // not a weight the object model can add up
            }
        }
        else if (Inventory.TooHeavy(pl, weight))
        {
            DrawInfoTo(pl, TooHeavyToHold);
            return null;
        }
        CfObject ob = NewItem(at);
        ob.Nrof = (uint)count;
        CfObject stack = world.InsertObInOb(ob, pl);
        ps.Dirty = true;
        return stack;
    }

    // an object leaves the player's tree for good: it is no longer his current weapon or his mark, its tag dies with it
    private void RemoveStack(PlayerState ps, CfObject item)
    {
        CfObject pl = ps.Ob;
        if (pl.CurrentWeapon == item)
        {
            pl.CurrentWeapon = null;
        }
        if (ps.Contr is { } contr && contr.Mark == item)
        {
            contr.Mark = null;
            contr.MarkCount = 0;
        }
        DropItem(ps, item);
    }

    // takes <take> of a stack (take is not more than it has): the whole stack leaves, or decrease_ob_nr cuts it
    private void TakeFrom(PlayerState ps, CfObject item, long take)
    {
        if (take >= Inventory.StackCount(item))
        {
            RemoveStack(ps, item);
        }
        else
        {
            world!.DecreaseObNr(item, (int)take);
            ps.Dirty = true;
            if (ps.Contr is { } contr)
            {
                contr.Shown.MarkDirty(handles.TagOf(HolderOf(item) is { } holder ? TagItem(holder, item) : ItemProxy(item)));
            }
        }
    }

    /// <summary>
    /// <c>removeinvobject</c>: <paramref name="count"/> objects of the archetype leave the player's top level (the stacks of that
    /// archetype in the order of the inventory), all of them or none: false when he has fewer, and then nothing changed and nothing is said.
    /// </summary>
    private bool RemoveInv(PlayerState ps, long count, string name)
    {
        if (!ps.Loaded || ps.Contr == null || count <= 0 || count > int.MaxValue || world!.FindArchetype(name) is not { } at)
        {
            return false;
        }
        List<CfObject> stacks = Inventory.PresentArch(ps.Ob, at);
        if (stacks.Sum(Inventory.StackCount) < count)
        {
            return false;
        }
        long left = count;
        foreach (CfObject stack in stacks)
        {
            if (left == 0)
            {
                break;
            }
            long take = Math.Min(left, Inventory.StackCount(stack));
            TakeFrom(ps, stack, take);
            left -= take;
        }
        ps.Dirty = true;
        return true;
    }

    // removeinvobjectbyid: the item of this id in the player's tree (the sacks too) loses count objects, or none when it has fewer
    private bool RemoveInvById(PlayerState ps, long count, long id)
    {
        if (!ps.Loaded || ps.Contr == null || count <= 0 || FindItemById(ps, id, deep: true) is not { } item || count > Inventory.StackCount(item))
        {
            return false;
        }
        TakeFrom(ps, item, count);
        return true;
    }

    // removeinvobjects: every stack of the archetype at the top level leaves; how many objects that were
    private long RemoveAllInv(PlayerState ps, string name)
    {
        if (!ps.Loaded || ps.Contr == null || world!.FindArchetype(name) is not { } at)
        {
            return 0;
        }
        long total = 0;
        foreach (CfObject stack in Inventory.PresentArch(ps.Ob, at))
        {
            total += Inventory.StackCount(stack);
            RemoveStack(ps, stack);
        }
        ps.Dirty = true;
        return total;
    }

    /// <summary>
    /// <c>mudresetinvobject</c>: the item of this id goes back to what its archetype is (every field, the extra lines such as the
    /// owner and the real name too; the count and the identified and applied state stay). It leaves the tree and enters it again, so
    /// that weights and the fixed player are right; it may then merge into an equal stack, and its handle dies with it.
    /// </summary>
    private bool ResetInv(PlayerState ps, long id)
    {
        if (!ps.Loaded || ps.Contr == null || FindItemById(ps, id, deep: true) is not { } item || item.Arch is not { } at || item.Env is not { } env)
        {
            return false;
        }
        uint nrof = item.Nrof;
        bool applied = item.QueryFlag(Define.FLAG_APPLIED);
        int carrying = item.Carrying;
        world!.RemoveOb(item);
        world.CopyObject(at.Clone, item);
        item.Carrying = carrying; // the copy takes the archetype's (empty) Carrying; the contents of a sack stay in it
        item.Nrof = nrof;
        item.SetFlag(Define.FLAG_IDENTIFIED);
        if (applied)
        {
            item.SetFlag(Define.FLAG_APPLIED);
        }
        world.InsertObInOb(item, env);
        AskForWholeWindow(ps);
        ps.Dirty = true;
        return true;
    }

    // ------------------------------------------------------------------ the global functions (the actor is the request's player)

    internal Outcome MudAddInvObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 2 || WholeNumber(args[0]) is not { } count || BytesText(args[1]) is not { } name)
        {
            return BadArgs("mudaddinvobject takes a count and an archetype name");
        }
        return Outcome.Ok(Val.Bool(ContextState() is { } ps && AddInv(ps, count, name, ignoreWeight: false) != null));
    }

    internal Outcome MudRemoveInvObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 2 || WholeNumber(args[0]) is not { } count || BytesText(args[1]) is not { } name)
        {
            return BadArgs("mudremoveinvobject takes a count and an archetype name");
        }
        return Outcome.Ok(Val.Bool(ContextState() is { } ps && RemoveInv(ps, count, name)));
    }

    internal Outcome MudCountInvObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 2 || BytesText(args[0]) is not { } name)
        {
            return BadArgs("mudcountinvobject takes an archetype name and a flag");
        }
        return Outcome.Ok(Val.Int(ContextState() is { } ps ? CountInv(ps.Ob, name, Truthy(args[1])) : 0));
    }

    internal Outcome MudFindInvObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || BytesText(args[0]) is not { } text)
        {
            return BadArgs("mudfindinvobject takes a text");
        }
        CfObject? item = ContextState() is { } ps ? Inventory.FindBest(world!, ps.Ob, text, deep: false) : null;
        return Outcome.Ok(Val.Int(item == null ? -1 : ItemId(item)));
    }

    internal Outcome MudResetInvObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || WholeNumber(args[0]) is not { } id)
        {
            return BadArgs("mudresetinvobject takes an id");
        }
        return Outcome.Ok(Val.Bool(ContextState() is { } ps && ResetInv(ps, id)));
    }

    internal Outcome MudOrderInventory(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || !(args[0].IsNil || args[0].Kind == ValKind.List))
        {
            return BadArgs("mudorderinventory takes a list of ids");
        }
        if (ContextState() is not { Loaded: true, Contr: not null } ps)
        {
            return Outcome.Ok(Val.Nil);
        }
        // the items of the top level named by an id of the player's own tree, once each, in the order given; the others follow as they were
        var first = new List<CfObject>();
        var seen = new HashSet<CfObject>(ReferenceEqualityComparer.Instance);
        foreach (Val v in args[0].Items)
        {
            if (WholeNumber(v) is { } id && FindItemById(ps, id, deep: false) is { } item && item.Env == ps.Ob && seen.Add(item))
            {
                first.Add(item);
            }
        }
        if (first.Count > 0)
        {
            Inventory.Reorder(ps.Ob, first);
            ps.Dirty = true;
            AskForWholeWindow(ps);
        }
        return Outcome.Ok(Val.Nil);
    }

    internal Outcome FindMudObject(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || WholeNumber(args[0]) is not { } id)
        {
            return BadArgs("findMudObject takes an id");
        }
        return Outcome.Ok(ContextState() is { } ps ? ItemValue(FindItemById(ps, id, deep: true)) : Val.Nil);
    }

    internal Outcome MudHaveSkill(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || BytesText(args[0]) is not { } name)
        {
            return BadArgs("mudhaveskill takes a skill name");
        }
        return Outcome.Ok(Val.Bool(ContextState() is { } ps && Inventory.SkillOf(ps.Ob, name) != null));
    }

    internal Outcome MudSkillLevel(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || BytesText(args[0]) is not { } name)
        {
            return BadArgs("mudskilllevel takes a skill name");
        }
        return Outcome.Ok(Val.Int(ContextState() is { } ps && Inventory.SkillOf(ps.Ob, name) is { } skill ? Inventory.SkillLevel(skill) : 0));
    }

    // ------------------------------------------------------------------ the lists of the quick menu (into the calling NPC)

    // caller.setprop this.<prefix><i> for each entry, and the empty one after them, which the script's loop stops at
    private void ListInto(string prefix, IReadOnlyList<string> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            effects.Add(SetCallerProp(prefix + i, entries[i]));
        }
        effects.Add(SetCallerProp(prefix + entries.Count, ""));
    }

    private static Val SetCallerProp(string name, string value) =>
        Val.Map(("k", Val.Str("caller.setprop")), ("name", Val.Str("this." + name)), ("value", Val.Str(value)));

    private Outcome ListFunction(IReadOnlyList<Val> args, string name, Func<PlayerState, List<string>> make, string prefix)
    {
        if (args.Count != 0)
        {
            return BadArgs(name + " takes no arguments");
        }
        ListInto(prefix, ContextState() is { Loaded: true } ps ? make(ps) : new List<string>());
        return Outcome.Ok(Val.Nil);
    }

    internal Outcome MudListWeapons(IReadOnlyList<Val> args) =>
        ListFunction(args, "mudlistweapons", ps => Inventory.ListItems(world!, ps.Ob, Inventory.IsWeapon), "weapon");

    internal Outcome MudListSkills(IReadOnlyList<Val> args) =>
        ListFunction(args, "mudlistskills", ps => Inventory.ListSkills(world!, ps.Ob), "skill");

    internal Outcome MudListEquipment(IReadOnlyList<Val> args) =>
        ListFunction(args, "mudlistequipment", ps => Inventory.ListItems(world!, ps.Ob, o => o.QueryFlag(Define.FLAG_APPLIED)), "equipment");

    /// <summary>
    /// <c>mudlistspells</c>: <c>this.spellpaths</c> is the names of the spell paths; no spell is listed, as this port has no spell
    /// table (the spells of a player are numbers in his file: docs/differences.md).
    /// </summary>
    internal Outcome MudListSpells(IReadOnlyList<Val> args)
    {
        if (args.Count != 0)
        {
            return BadArgs("mudlistspells takes no arguments");
        }
        effects.Add(SetCallerProp("spellpaths", Inventory.SpellPaths()));
        ListInto("spell", new List<string>());
        return Outcome.Ok(Val.Nil);
    }

    // ------------------------------------------------------------------ counting for a holder

    private long CountInv(CfObject holder, string name, bool deep) =>
        world!.FindArchetype(name) is { } at ? Inventory.CountArch(holder, at, deep) : 0;

    // ------------------------------------------------------------------ the methods

    private partial Outcome? InvokeWave5B(object target, long method, IReadOnlyList<Val> args) => target switch
    {
        PlayerState ps => InvokePlayerInventory(ps, method, args),
        LevelObject lo => InvokeObjectInventory(lo, method, args),
        _ => null,
    };

    private Outcome? InvokePlayerInventory(PlayerState ps, long method, IReadOnlyList<Val> args)
    {
        switch (method)
        {
            case TMudPlayerMembers.MethodAddInvObject:
            case TMudPlayerMembers.MethodAddInvObject2:
            {
                if (args.Count != 2 || WholeNumber(args[0]) is not { } count || BytesText(args[1]) is not { } name)
                {
                    return BadArgs("addinvobject takes a count and an archetype name");
                }
                return CheckBusy() ?? Outcome.Ok(ItemValue(AddInv(ps, count, name, ignoreWeight: method == TMudPlayerMembers.MethodAddInvObject2)));
            }
            case TMudPlayerMembers.MethodRemoveInvObject:
                return args.Count == 2 && WholeNumber(args[0]) is { } n && BytesText(args[1]) is { } arch
                    ? CheckBusy() ?? Outcome.Ok(Val.Bool(RemoveInv(ps, n, arch)))
                    : BadArgs("removeinvobject takes a count and an archetype name");
            case TMudPlayerMembers.MethodRemoveInvObjectById:
                return args.Count == 2 && WholeNumber(args[0]) is { } howMany && WholeNumber(args[1]) is { } id
                    ? CheckBusy() ?? Outcome.Ok(Val.Bool(RemoveInvById(ps, howMany, id)))
                    : BadArgs("removeinvobjectbyid takes a count and an id");
            case TMudPlayerMembers.MethodRemoveInvObjects:
                return args.Count == 1 && BytesText(args[0]) is { } all
                    ? CheckBusy() ?? Outcome.Ok(Val.Int(RemoveAllInv(ps, all)))
                    : BadArgs("removeinvobjects takes an archetype name");
            case TMudPlayerMembers.MethodFindInvObject:
                return args.Count == 2 && BytesText(args[0]) is { } text
                    ? Outcome.Ok(ItemValue(Inventory.FindBest(world!, ps.Ob, text, Truthy(args[1]))))
                    : BadArgs("findinvobject takes a text and a flag");
            case TMudPlayerMembers.MethodFindInvObjectById:
                return args.Count == 2 && WholeNumber(args[0]) is { } wanted
                    ? Outcome.Ok(ItemValue(FindItemById(ps, wanted, Truthy(args[1]))))
                    : BadArgs("findinvobjectbyid takes an id and a flag");
            case TMudPlayerMembers.MethodFindInvObjectByTitle:
                return args.Count == 2 && BytesText(args[0]) is { } title
                    ? Outcome.Ok(ItemValue(Inventory.FindByTitle(ps.Ob, title, Truthy(args[1]))))
                    : BadArgs("findinvobjectbytitle takes a title and a flag");
            case TMudPlayerMembers.MethodCountInvObject:
                return args.Count == 2 && BytesText(args[0]) is { } countName
                    ? Outcome.Ok(Val.Int(CountInv(ps.Ob, countName, Truthy(args[1]))))
                    : BadArgs("countinvobject takes an archetype name and a flag");
            default:
                return null;
        }
    }

    private Outcome? InvokeObjectInventory(LevelObject lo, long method, IReadOnlyList<Val> args)
    {
        switch (method)
        {
            case TMudObjectMembers.MethodFindInvObject:
                return args.Count == 2 && BytesText(args[0]) is { } text
                    ? Outcome.Ok(lo.Dead ? Val.Nil : ItemValue(Inventory.FindBest(world!, lo.Ob, text, Truthy(args[1]))))
                    : BadArgs("findinvobject takes a text and a flag");
            case TMudObjectMembers.MethodFindInvObjectById:
            {
                if (args.Count != 2 || WholeNumber(args[0]) is not { } id)
                {
                    return BadArgs("findinvobjectbyid takes an id and a flag");
                }
                // the id is looked for in the request's player's own tree; the object must hold what is found
                bool deep = Truthy(args[1]);
                CfObject? found = !lo.Dead && ContextState() is { } ps ? FindItemById(ps, id, deep: true) : null;
                return Outcome.Ok(found != null && Inventory.IsInside(found, lo.Ob, deep) ? ItemValue(found) : Val.Nil);
            }
            case TMudObjectMembers.MethodFindInvObjectByTitle:
                return args.Count == 2 && BytesText(args[0]) is { } title
                    ? Outcome.Ok(lo.Dead ? Val.Nil : ItemValue(Inventory.FindByTitle(lo.Ob, title, Truthy(args[1]))))
                    : BadArgs("findinvobjectbytitle takes a title and a flag");
            case TMudObjectMembers.MethodCountInvObject:
                return args.Count == 2 && BytesText(args[0]) is { } name
                    ? Outcome.Ok(Val.Int(lo.Dead ? 0 : CountInv(lo.Ob, name, Truthy(args[1]))))
                    : BadArgs("countinvobject takes an archetype name and a flag");
            case TMudObjectMembers.MethodMudDecreaseQuantity:
                return args.Count == 1 && WholeNumber(args[0]) is { } n
                    ? CheckBusy() ?? DecreaseQuantity(lo, n)
                    : BadArgs("muddecreasequantity takes a number");
            default:
                return null;
        }
    }

    /// <summary>
    /// <c>muddecreasequantity(n)</c>: <c>decrease_ob_nr</c> for n above 0, an increase for n below 0 (the stack holds more); at 0 the
    /// object is gone and its handle dies. On an item in a player's tree the player's file and the client's strings change; on an
    /// object of a level the level does (and the NPC goes, with <c>npc.remove</c>, when the object is gone).
    /// </summary>
    private Outcome DecreaseQuantity(LevelObject lo, long n)
    {
        CfObject ob = lo.Ob;
        if (lo.Dead || n == 0 || ob.QueryFlag(Define.FLAG_FREED))
        {
            return Outcome.Ok(Val.Nil);
        }
        long have = Inventory.StackCount(ob);
        long by = Math.Abs(n);
        if (n < 0 && (by > Inventory.MaxAdd || (have + by) * Math.Max(ob.Weight, 1) > int.MaxValue / 2))
        {
            return Outcome.Ok(Val.Nil); // a stack the object model could not add up
        }
        if (lo.Level != Level.Inventory)
        {
            // an object of a level: its count changes in the file of the level, the script is told as for every write
            if (n > 0 && n >= have)
            {
                DestroyObject(lo, removeNpc: true);
                return Outcome.Ok(Val.Nil);
            }
            ob.Nrof = (uint)(have - n);
            lo.Level.Dirty = true;
            events.Add(Val.Map(
                ("name", Val.Str(Manifest.EventOnUpdateObject)),
                ("target", Val.Str("object")),
                ("handle", Val.Handle(handles.HandleOf(lo))),
                ("args", Val.List())));
            return Outcome.Ok(Val.Nil);
        }
        Player? holder = HolderOf(ob);
        if (n < 0)
        {
            if (ob.Env != null)
            {
                CfWorld.AddWeight(ob.Env, ob.Weight * (int)by);
            }
            ob.Nrof = (uint)(have + by);
            if (holder != null)
            {
                world!.Host.EsrvSendItem(holder.State.Ob, ob);
                world.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, holder.State.Ob, holder.State.Ob);
            }
            AfterItemSet(lo, true);
            return Outcome.Ok(Val.Nil);
        }
        if (n < have)
        {
            world!.DecreaseObNr(ob, (int)n);
            AfterItemSet(lo, true);
            return Outcome.Ok(Val.Nil);
        }
        // the whole stack: the object is gone, here and for the files that hold it
        AfterItemSet(lo, true);
        if (holder != null)
        {
            RemoveStack(holder.State, ob);
        }
        else
        {
            ReleaseItemTags(ob);
            KillItem(ob);
            if (!ob.QueryFlag(Define.FLAG_REMOVED))
            {
                world!.RemoveOb(ob);
            }
            world!.FreeObject(ob);
        }
        return Outcome.Ok(Val.Nil);
    }
}
