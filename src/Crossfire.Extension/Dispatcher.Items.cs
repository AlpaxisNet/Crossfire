// Not ported from a Crossfire file: lane 4C of phase 3, the items of a player's tree in the Crossfire extension: their tags and the
// tags' lifecycle, the ids a client may use, the item strings with the one flush at the end of a request, and the C functions of the
// client's side (esrv_send_item, esrv_update_item, esrv_del_item, new_draw_info) as the host of the object model answers them
// (docs/mud-phase3.md sections 3, 4.2 and 5; docs/extension.md "Items of a player").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Runtime.CompilerServices;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    /// <summary>The most item strings one player has at a time (protocol 4.2: a request's effects are at most 4,096); the rest are not shown.</summary>
    public const int MaxShownItems = 3800;

    /// <summary>What the extension knows of an object that has a tag as an item: the proxy object (its key in the handle table) and the C tag it had.</summary>
    private sealed class ItemEntry
    {
        public ItemEntry(LevelObject wrapper, uint count)
        {
            Wrapper = wrapper;
            Count = count;
        }

        public LevelObject Wrapper { get; }

        public uint Count { get; }
    }

    private readonly Dictionary<CfObject, ItemEntry> itemEntries = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<uint, ItemEntry> itemsByCount = new();
    private readonly ConditionalWeakTable<CfObject, object> itemOwners = new();
    private bool shownCapLogged;

    /// <summary>How many objects have a live tag as an item (the test of the lifecycle reads it: 1,000 logins leave it where it was).</summary>
    internal int LiveItemTags => itemEntries.Count;

    /// <summary>How many tags of this epoch are alive (every kind of object).</summary>
    internal int LiveTags => handles.Live;

    // ------------------------------------------------------------------ tags

    /// <summary>
    /// The proxy object of an item, made at the first call; <see cref="handles"/> gives it a tag. The proxy is a <see cref="LevelObject"/>
    /// in <see cref="Level.Inventory"/>, so that the members of <c>TMudObject</c> read it as they read an object of a level. The
    /// lane that puts an object into a level (pick up and drop) gives the same proxy a level (<see cref="LevelObject.Level"/>) and
    /// keeps its tag: it is the one key the object has (docs/mud-phase3.md section 3).
    /// </summary>
    internal LevelObject ItemProxy(CfObject ob)
    {
        if (!itemEntries.TryGetValue(ob, out ItemEntry? entry))
        {
            var proxy = new LevelObject(Level.Inventory, ob) { Placed = false };
            entry = new ItemEntry(proxy, ob.Count);
            itemEntries[ob] = entry;
            itemsByCount[ob.Count] = entry;
        }
        handles.TagOf(entry.Wrapper); // may throw TagsUsedUpException: the request answers internal
        return entry.Wrapper;
    }

    /// <summary>Registers a level object's proxy as the proxy of its object too (it goes into an inventory with the tag it has).</summary>
    internal void AdoptLevelObject(LevelObject lo)
    {
        var entry = new ItemEntry(lo, lo.Ob.Count);
        itemEntries[lo.Ob] = entry;
        itemsByCount[lo.Ob.Count] = entry;
    }

    /// <summary>The player whose tree holds an object (inventory, nested sacks), or null.</summary>
    private static Player? HolderOf(CfObject ob) => CfWorld.IsPlayerInv(ob.Env)?.Contr as Player;

    /// <summary>The proxy of an item that is in a player's tree, tagged and remembered by that player (so that the sweep sees it leave).</summary>
    private LevelObject TagItem(Player holder, CfObject ob)
    {
        LevelObject proxy = ItemProxy(ob);
        holder.Tracked.Add(ob);
        return proxy;
    }

    ulong IMemberEnv.ItemHandleOf(CfObject item)
    {
        LevelObject proxy = HolderOf(item) is { } holder ? TagItem(holder, item) : ItemProxy(item);
        return handles.HandleOf(proxy);
    }

    object? IMemberEnv.Resolve(ulong handle) => handles.Resolve(handle);

    ulong? IMemberEnv.PlayerHandleOf(string account) => players.TryGetValue(account, out PlayerState? ps) ? handles.HandleOf(ps) : null;

    object? IMemberEnv.OwnerOf(CfObject item) => itemOwners.TryGetValue(item, out object? owner) ? owner : null;

    void IMemberEnv.SetOwnerOf(CfObject item, object? owner)
    {
        itemOwners.Remove(item);
        if (owner != null)
        {
            itemOwners.Add(item, owner);
        }
    }

    private ulong HandleOfTag(uint tag) => ((ulong)host.Epoch << 32) | tag;

    /// <summary>The tag of an item's proxy, when it has one.</summary>
    private uint TagOfItem(ItemEntry entry) => handles.TagOf(entry.Wrapper);

    private void KillItem(CfObject ob)
    {
        if (itemEntries.Remove(ob, out ItemEntry? entry))
        {
            itemsByCount.Remove(entry.Count);
            handles.Kill(entry.Wrapper);
            entry.Wrapper.Dead = true;
        }
    }

    /// <summary>Kills the tags of everything inside <paramref name="ob"/> (it is freed: nothing of its inventory is reachable any more).</summary>
    private void ReleaseItemTags(CfObject ob)
    {
        if (itemEntries.Count == 0)
        {
            return;
        }
        for (CfObject? c = ob.Inv; c != null; c = c.Below)
        {
            ReleaseItemTags(c);
            KillItem(c);
        }
    }

    // ------------------------------------------------------------------ ids from a client

    /// <summary>
    /// The item an id of the client names, looked for in <paramref name="player"/>'s own tree only: the top level and the open
    /// container, and with <paramref name="deep"/> the sacks too (docs/mud-phase3.md section 4.2). An id of another player's item, of a
    /// level object, of a dead tag or of an object that is not an item is null, as an unknown id is.
    /// </summary>
    internal CfObject? FindItemById(PlayerState player, long id, bool deep)
    {
        if (id <= 0 || id > uint.MaxValue)
        {
            return null;
        }
        return ItemOfPlayer(player, HandleOfTag((uint)id), deep);
    }

    CfObject? IMemberEnv.ItemOfPlayer(PlayerState player, ulong handle) => ItemOfPlayer(player, handle, deep: false);

    private CfObject? ItemOfPlayer(PlayerState player, ulong handle, bool deep)
    {
        if (handles.Resolve(handle) is not LevelObject proxy || !itemEntries.TryGetValue(proxy.Ob, out ItemEntry? entry) || entry.Wrapper != proxy)
        {
            return null;
        }
        CfObject ob = proxy.Ob;
        if (ob.QueryFlag(Define.FLAG_FREED) || ob.Env == null)
        {
            return null;
        }
        if (ob.Env == player.Ob || (player.Ob.Container != null && ob.Env == player.Ob.Container))
        {
            return ob;
        }
        if (deep)
        {
            for (CfObject? c = ob.Env; c != null; c = c.Env)
            {
                if (c == player.Ob)
                {
                    return ob;
                }
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ what the C functions of the client's side do here

    /// <summary><c>esrv_send_item</c> and <c>esrv_update_item</c>: the item changed; its string is written at the end of the request.</summary>
    internal void ItemSent(CfObject player, CfObject op)
    {
        if (player.Contr is not Player p)
        {
            return;
        }
        if (op == player)
        {
            p.Touch(); // the player's weight: written with the strings of the flush
            return;
        }
        if (op.Invisible != 0 || op.Type == Define.PLAYER)
        {
            return; // LOOK_OBJ: not shown
        }
        p.Shown.MarkDirty(handles.TagOf(TagItem(p, op)));
    }

    /// <summary><c>esrv_del_item</c>: the item with the C tag <paramref name="count"/> is gone from the player's window.</summary>
    internal void ItemDeleted(IPlayerHandle? contr, uint count)
    {
        if (contr is not Player p)
        {
            return;
        }
        if (itemsByCount.TryGetValue(count, out ItemEntry? entry))
        {
            p.Shown.MarkRemoved(TagOfItem(entry));
        }
        p.Reconcile = true; // the tree changed: the flush looks at all of it, and the sweep frees the tags of what left
        p.Touch();
    }

    /// <summary><c>fix_player</c>, as the object model calls it when an object enters or leaves a player's tree: the tree changed.</summary>
    internal void PlayerFixed(CfObject player)
    {
        if (player.Contr is Player p)
        {
            p.Reconcile = true;
            p.Touch();
        }
    }

    /// <summary>
    /// <c>new_draw_info</c>: a line of text for a player. On a 1.1 link it is the player event <c>onRPGMessage</c> (sync, one argument)
    /// with the <c>client.text</c> effect as its fallback, which the host runs only when the player's class does not define the
    /// function; on a 1.0 link, which has no fallback, it is the effect alone. Nothing for an object that is not a logged-in player.
    /// </summary>
    internal void DrawInfoTo(CfObject who, string text)
    {
        if (who.Contr is not Player p)
        {
            return;
        }
        string shown = FileText.FromBytesText(text);
        Val fallback = TextFallback(p.Account, shown);
        if (Speaks11)
        {
            EmitPlayerEvent(Manifest.EventOnRPGMessage, p.Account, new[] { Val.Str(shown) }, fallback);
        }
        else
        {
            effects.Add(fallback);
        }
    }

    /// <summary>player.c <c>fix_weight</c> for every logged-in player (the tick of the original calls it).</summary>
    internal List<CfObject> FixWeight() => ItemMatching.FixWeight(world!, players.Values.Select(ps => ps.Contr).OfType<Player>().ToList());

    /// <summary>A member of an item in an inventory was written: the player's file is dirty and the item's string is written again at the end of the request.</summary>
    private void AfterItemSet(LevelObject item, bool changed)
    {
        if (!changed)
        {
            return;
        }
        if (HolderOf(item.Ob) is { } holder)
        {
            holder.State.Dirty = true;
            holder.Shown.MarkDirty(handles.TagOf(item));
        }
        else
        {
            // inside an object of a level (a bag on the ground, an NPC's stock): that level has to be saved
            CfObject top = item.Ob;
            while (top.Env != null)
            {
                top = top.Env;
            }
            foreach (Level level in levels.Values)
            {
                if (level.Objects.Any(o => o.Ob == top))
                {
                    level.Dirty = true;
                    break;
                }
            }
        }
    }

    // ------------------------------------------------------------------ the creation of items

    /// <summary>
    /// A new object of an archetype that an item is made of (a script's or the engine's): identified at creation, so that it has its name
    /// and its value as Graal Kingdoms' shops expect them (docs/mud-phase3.md section 3, fact 4; the original network's rule is not
    /// known, differences.md).
    /// </summary>
    internal CfObject NewItem(Archetype at)
    {
        CfObject ob = world!.ArchToObject(at)!;
        ob.SetFlag(Define.FLAG_IDENTIFIED);
        return ob;
    }

    // ------------------------------------------------------------------ login and logout

    /// <summary>
    /// A player was logged in: every top-level item gets its tag, and the first flush writes all the strings. A second login of an
    /// account without a logout (<paramref name="again"/>) forgets what the client holds and writes them again.
    /// </summary>
    private void StartItems(PlayerState player, bool again)
    {
        if (player.Contr is not Player p)
        {
            return;
        }
        if (again)
        {
            p.Shown.Sent.Clear();
        }
        for (CfObject? c = player.Ob.Inv; c != null; c = c.Below)
        {
            TagItem(p, c);
        }
        p.Reconcile = true;
        p.Touch();
    }

    // ------------------------------------------------------------------ the flush

    private static bool Visible(CfObject ob) => ob.Invisible == 0 && ob.Type != Define.PLAYER;

    /// <summary>Sets one of the player's strings that the flush owns; nothing is written when the client holds the same text.</summary>
    private void SetShown(Player p, string name, string value)
    {
        if (p.Shown.Sent.TryGetValue(name, out string? old) && old == value)
        {
            return;
        }
        PlayerState ps = p.State;
        if (!p.Shown.Sent.ContainsKey(name) && !ps.ClientOrder.Contains(name))
        {
            ps.ClientOrder.Add(name);
        }
        p.Shown.Sent[name] = value;
        effects.Add(Val.Map(("k", Val.Str("client.set")), ("player", Val.Str(ps.Account)), ("name", Val.Str(name)), ("value", Val.Str(value))));
    }

    private void DeleteShown(Player p, string name)
    {
        if (p.Shown.Sent.Remove(name))
        {
            p.State.ClientOrder.Remove(name);
            effects.Add(Val.Map(("k", Val.Str("client.del")), ("player", Val.Str(p.State.Account)), ("name", Val.Str(name))));
        }
    }

    partial void FlushShown(PlayerState player)
    {
        if (player.Contr is not Player p || options.FlushHook != null)
        {
            return; // a test's stand-in for the flush replaces this one (ExtensionOptions.FlushHook)
        }
        p.NeedsFlush = true; // the marks the flush makes itself queue nothing
        try
        {
            bool resend = p.NeedsResend;
            if (resend)
            {
                // the effects of an earlier request were lost: the strings of the stats go again with the items
                SendClientStrings(player);
            }
            if (resend || p.Reconcile)
            {
                ReconcileItems(p);
            }
            else
            {
                FlushMarked(p);
            }
            p.Reconcile = false;
            p.Shown.ClearMarks();
            FlushWeights(p);
        }
        finally
        {
            p.NeedsFlush = false;
        }
    }

    private void FlushWeights(Player p)
    {
        CfObject ob = p.State.Ob;
        SetShown(p, ItemStrings.WeightName, ((long)ob.Weight + ob.Carrying).ToString(System.Globalization.CultureInfo.InvariantCulture));
        p.WeightLimit = ItemMatching.WeightLimitOf(ob.Stats.Str);
        SetShown(p, ItemStrings.WeightLimitName, p.WeightLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>A mark without a change of the tree: the marked items that the window shows are written again when their text changed.</summary>
    private void FlushMarked(Player p)
    {
        CfObject ob = p.State.Ob;
        foreach (uint tag in p.Shown.Dirty)
        {
            if (handles.Resolve(HandleOfTag(tag)) is not LevelObject proxy || !itemEntries.ContainsKey(proxy.Ob))
            {
                continue;
            }
            CfObject item = proxy.Ob;
            bool top = item.Env == ob;
            if ((top || (ob.Container != null && item.Env == ob.Container && !item.QueryFlag(Define.FLAG_FREED))) && Visible(item))
            {
                SetShown(p, ItemStrings.ItemPrefix + tag, ItemStrings.Text(world!, item, !top));
            }
        }
    }

    /// <summary>
    /// The whole window: the strings of the top level and of the open container are compared with what the client holds, the
    /// strings of what left are deleted, and the tags of the objects that left the tree for good are killed (docs/mud-phase3.md
    /// section 4.2: every exit of an object from the tree kills its tag).
    /// </summary>
    private void ReconcileItems(Player p)
    {
        CfObject ob = p.State.Ob;
        var shown = new List<(CfObject Item, bool InContainer)>();
        for (CfObject? c = ob.Inv; c != null; c = c.Below)
        {
            if (Visible(c))
            {
                shown.Add((c, false));
            }
        }
        CfObject? open = ob.Container;
        if (open != null && open.QueryFlag(Define.FLAG_FREED))
        {
            open = ob.Container = null;
        }
        if (open != null)
        {
            for (CfObject? c = open.Inv; c != null; c = c.Below)
            {
                if (Visible(c))
                {
                    shown.Add((c, true));
                }
            }
        }
        if (shown.Count > MaxShownItems)
        {
            if (!shownCapLogged)
            {
                shownCapLogged = true;
                log(1, "player " + p.Account + " carries " + shown.Count + " items: only the first " + MaxShownItems + " are shown");
            }
            shown.RemoveRange(MaxShownItems, shown.Count - MaxShownItems);
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        var inWindow = new HashSet<CfObject>(ReferenceEqualityComparer.Instance);
        foreach ((CfObject item, bool inContainer) in shown)
        {
            LevelObject proxy = TagItem(p, item);
            string name = ItemStrings.ItemPrefix + handles.TagOf(proxy);
            names.Add(name);
            inWindow.Add(item);
            SetShown(p, name, ItemStrings.Text(world!, item, inContainer));
        }
        foreach (string name in p.Shown.Sent.Keys.Where(k => k.StartsWith(ItemStrings.ItemPrefix, StringComparison.Ordinal) && !names.Contains(k)).ToList())
        {
            DeleteShown(p, name);
        }
        if (open != null)
        {
            SetShown(p, ItemStrings.ContainerName, ItemStrings.ContainerText(world!, handles.TagOf(ItemProxy(open)), open));
        }
        else if (p.Shown.Sent.ContainsKey(ItemStrings.ContainerName))
        {
            SetShown(p, ItemStrings.ContainerName, "");
        }
        SweepTracked(p);
    }

    /// <summary>The tags of the objects a player's tree lost: freed objects, objects now in another tree or nowhere.</summary>
    private void SweepTracked(Player p)
    {
        CfObject ob = p.State.Ob;
        foreach (CfObject item in p.Tracked.ToList())
        {
            bool freed = item.QueryFlag(Define.FLAG_FREED);
            if (!freed && Holds(ob, item))
            {
                continue;
            }
            p.Tracked.Remove(item);
            if (itemEntries.TryGetValue(item, out ItemEntry? entry))
            {
                DeleteShown(p, ItemStrings.ItemPrefix + TagOfItem(entry));
                Player? other = freed ? null : HolderOf(item);
                if (other != null && other != p)
                {
                    // moved to another player's tree (a trade): the tag stays with the object, the other window shows it
                    other.Reconcile = true;
                    other.Touch();
                }
                else if (freed || entry.Wrapper.Level == Level.Inventory)
                {
                    KillItem(item);
                }
            }
        }
    }

    /// <summary>True when <paramref name="item"/> is in the tree of <paramref name="player"/>: below the player's object or inside the open container.</summary>
    private static bool Holds(CfObject player, CfObject item)
    {
        for (CfObject? c = item.Env; c != null; c = c.Env)
        {
            if (c == player || c == player.Container)
            {
                return true;
            }
        }
        return false;
    }
}
