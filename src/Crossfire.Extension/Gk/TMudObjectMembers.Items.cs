// Not ported from a Crossfire file: the members of lane 4C on the types TMudObject (an object that holds objects, and an item) and
// TMudPlayer: the inventory list, the money, the owner, the marked object and the current weapon (docs/mud-phase3.md section 4.2).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Gk;

internal static partial class TMudObjectMembers
{
    /// <summary>Name of the member of the inventory list on both types.</summary>
    public const string MemberItems = "muditems";

    /// <summary>The visible objects an object holds, in the order of its inventory (the invisible ones, a player's skills and forces, are not items).</summary>
    internal static IReadOnlyList<CfObject> VisibleInventory(CfObject ob)
    {
        var list = new List<CfObject>();
        for (CfObject? c = ob.Inv; c != null; c = c.Below)
        {
            if (c.Invisible == 0)
            {
                list.Add(c);
            }
        }
        return list;
    }

    internal static Val HandlesOf(IMemberEnv env, IReadOnlyList<CfObject> items) => Val.List(items.Select(i => Val.Handle(env.ItemHandleOf(i))));

    /// <summary>The value a <c>ref</c> member gives for an owner kept as an account name: the handle of the player while he is online, else the text.</summary>
    internal static Val OwnerValue(IMemberEnv env, CfObject ob)
    {
        if (env.OwnerOf(ob) is LevelObject npc)
        {
            return Val.Handle(env.HandleOf(npc));
        }
        string? text = ob.Extra?.Get("mudowner");
        if (string.IsNullOrEmpty(text))
        {
            return Val.Nil;
        }
        string account = FileText.FromBytesText(FileText.UnescapeLine(text));
        return env.PlayerHandleOf(account) is { } handle ? Val.Handle(handle) : Val.Str(account);
    }

    internal static SetResult SetOwner(IMemberEnv env, CfObject ob, Val value, Func<ulong, object?> resolve)
    {
        switch (value.Kind)
        {
            case ValKind.Nil:
                ClearOwner(env, ob);
                break;
            case ValKind.String when value.AsString.Length == 0:
                ClearOwner(env, ob);
                break;
            case ValKind.String:
                if (FieldBuilder<LevelObject>.HasLineBreak(value.AsString))
                {
                    return SetResult.Fail(FieldBuilder<LevelObject>.LineBreakRefused);
                }
                ClearOwner(env, ob);
                (ob.Extra ??= new ExtraKeys()).Set("mudowner", FileText.EscapeLine(FileText.ToBytesText(value.AsString)));
                break;
            case ValKind.Handle:
                switch (resolve(value.AsHandle))
                {
                    case PlayerState ps:
                        ClearOwner(env, ob);
                        (ob.Extra ??= new ExtraKeys()).Set("mudowner", FileText.EscapeLine(FileText.ToBytesText(ps.Account)));
                        break;
                    case LevelObject npc:
                        // the owner of an object is kept for an NPC in memory only: it has no name to save
                        ClearOwner(env, ob);
                        env.SetOwnerOf(ob, npc);
                        break;
                    default:
                        ClearOwner(env, ob); // a dead or unknown handle names nobody
                        break;
                }
                break;
            default:
                return SetResult.Fail("the member takes an object or a text");
        }
        return SetResult.Ok(OwnerValue(env, ob));
    }

    private static void ClearOwner(IMemberEnv env, CfObject ob)
    {
        ob.Extra?.Remove("mudowner");
        env.SetOwnerOf(ob, null);
    }

    static partial void ExtendItems(FieldBuilder<LevelObject> b, List<MethodDef> methods)
    {
        b.Add(MemberItems, "list:handle:" + TypeName, false, false, (env, lo) => HandlesOf(env, VisibleInventory(lo.Ob)), null,
            items: (_, lo) => VisibleInventory(lo.Ob));
        b.Add("mudmoney", "int", false, false, (env, lo) => Val.Int(MoneyOf(env, lo.Ob)), null);
        b.Add("mudowner", "ref", true, false, (env, lo) => OwnerValue(env, lo.Ob), (env, lo, v) => SetOwner(env, lo.Ob, v, env.Resolve), minMinor: 1);
    }

    internal static long MoneyOf(IMemberEnv env, CfObject ob) =>
        ob.Type is Define.PLAYER or Define.CONTAINER ? env.World.QueryMoney(ob) : 0;
}

internal static partial class TMudPlayerMembers
{
    /// <summary>The visible object the player has applied as his weapon (C: <c>current_weapon</c>), when it is in his inventory.</summary>
    internal static CfObject? WieldedObject(CfObject player)
    {
        CfObject? weapon = player.CurrentWeapon;
        return weapon != null && weapon.Env == player && weapon.QueryFlag(Define.FLAG_APPLIED) && !weapon.QueryFlag(Define.FLAG_FREED) ? weapon : null;
    }

    static partial void ExtendItems(FieldBuilder<PlayerState> b, List<MethodDef> methods)
    {
        b.Add(TMudObjectMembers.MemberItems, "list:handle:" + TMudObjectMembers.TypeName, false, false,
            (env, p) => TMudObjectMembers.HandlesOf(env, TMudObjectMembers.VisibleInventory(p.Ob)), null,
            items: (_, p) => TMudObjectMembers.VisibleInventory(p.Ob));
        b.Add("mudmoney", "int", false, false, (env, p) => Val.Int(TMudObjectMembers.MoneyOf(env, p.Ob)), null);
        b.Add("markedobject", "ref:" + TMudObjectMembers.TypeName, true, false, (env, p) => MarkedValue(env, p), SetMarked, minMinor: 1);
        b.Add("currentweapon", "handle:" + TMudObjectMembers.TypeName, false, false,
            (env, p) => WieldedObject(p.Ob) is { } w ? Val.Handle(env.ItemHandleOf(w)) : Val.Nil, null);
    }

    private static Val MarkedValue(IMemberEnv env, PlayerState p)
    {
        CfObject? mark = ItemMatching.FindMarkedObject(p.Ob);
        return mark == null ? Val.Str("") : Val.Handle(env.ItemHandleOf(mark));
    }

    /// <summary>
    /// A write of <c>markedobject</c>: an item of the player's own top level marks it, nil or "" clears the mark; a handle that is not
    /// an item of the player's own tree (another player's item, a level object, a dead handle) changes nothing.
    /// </summary>
    private static SetResult SetMarked(IMemberEnv env, PlayerState p, Val value)
    {
        Player contr = p.Contr!; // a player state exists while the player is logged in, with its controller
        switch (value.Kind)
        {
            case ValKind.Nil:
                contr.Mark = null;
                contr.MarkCount = 0;
                break;
            case ValKind.String when value.AsString.Length == 0:
                contr.Mark = null;
                contr.MarkCount = 0;
                break;
            case ValKind.Handle:
                if (env.ItemOfPlayer(p, value.AsHandle) is { } item && item.Env == p.Ob && item.Invisible == 0)
                {
                    contr.Mark = item;
                    contr.MarkCount = item.Count;
                }
                break;
            default:
                return SetResult.Fail("the member takes an item or \"\"");
        }
        return SetResult.Ok(MarkedValue(env, p));
    }
}
