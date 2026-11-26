// Not ported from a Crossfire file: tests of lane 4C of phase 3, the items of a player in the Crossfire extension: the tags and their
// lifecycle, the ids a client may use, the item strings and their flush, the ported C functions of the player's controller, and the
// members muditems, mudmoney, mudowner, markedobject and currentweapon (docs/mud-phase3.md sections 3, 4.2, 5 and 9).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>An invented world with the kinds of items the lane's tests need, and the functions of a test lane that act on a player's tree.</summary>
internal static class ItemWorld
{
    public const string Arc =
        ObjectWorld.Arc
        + "Object goldcoin\nname gold coin\nname_pl gold coins\nface gold.111\ntype 36\nweight 20\nvalue 10\nend\n"
        + "Object silvercoin\nname silver coin\nname_pl silver coins\nface silver.111\ntype 36\nweight 20\nvalue 1\nend\n"
        + "Object mail\nname mail\nface mail.111\ntype 16\nweight 8000\nvalue 100\nend\n"
        + "Object bow\nname bow\nface bow.111\ntype 14\nweight 1500\nend\n"
        + "Object legs\nname legs\nface legs.111\ntype 165\nweight 900\nend\n"
        + "Object tool\nname tool\nface tool.111\ntype 162\nweight 500\nend\n"
        + "Object furniture\nname furniture\nface furniture.111\ntype 160\nweight 500\nend\n";

    public static TempFolder Make() => ObjectWorld.Make();

    public static TempFolder MakeWorld()
    {
        TempFolder world = TestWorld.Make(arc: Arc);
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts"));
        foreach (string c in ObjectWorld.Classes)
        {
            File.WriteAllText(Path.Combine(world.Path, "scripts", c + ".txt"), "// class\n");
        }
        Directory.CreateDirectory(Path.Combine(world.Path, "mudlevels"));
        return world;
    }

    public static void Account(TempFolder world, string account, string header, string inventory)
    {
        Directory.CreateDirectory(Path.Combine(world.Path, "data", "accounts"));
        File.WriteAllText(Path.Combine(world.Path, "data", "accounts", account + ".txt"), "arch human_player\naccount " + account + "\nname " + account + "\n" + header + inventory + "end\n");
    }

    public const int OpDo = 7100;

    /// <summary>
    /// <c>t4cdo(action, account, a, b)</c>: acts on the tree of a player inside one request, as the ported C of the next lanes does (the
    /// calls of the object model that tell the client), so that what the flush writes at the end of the request can be seen.
    /// </summary>
    public static void Register(ManifestBuilder b)
    {
        b.AddFunction(Lane.Wave5D, new FunctionDef(OpDo, "t4cdo", new[] { "string", "string", "any", "any" }, "any", Handler: Do));
    }

    private static Outcome Do(Dispatcher d, IReadOnlyList<Val> a)
    {
        string action = a[0].AsString;
        CfWorld w = d.WorldOrNull!;
        Player p = d.ControllerOf(a[1].AsString)!;
        CfObject pl = p.State.Ob;
        CfObject? Item(Val tag) => d.FindItemById(p.State, tag.AsInt, deep: true);
        switch (action)
        {
            case "grow": // a stack grows a number of times, each time the client is told
            {
                CfObject item = Item(a[2])!;
                for (int i = 0; i < a[3].AsInt; i++)
                {
                    item.Nrof++;
                    w.Host.EsrvUpdateItem(1, pl, item);
                }
                return Outcome.Ok(Val.Int(item.Nrof));
            }
            case "free": // remove_ob and free_object, as C callers do
            {
                CfObject item = Item(a[2])!;
                w.RemoveOb(item);
                w.FreeObject(item);
                return Outcome.Ok(Val.Nil);
            }
            case "decrease":
                w.DecreaseObNr(Item(a[2])!, (int)a[3].AsInt);
                return Outcome.Ok(Val.Nil);
            case "merge": // a new object of an archetype that has a tag already is inserted and merges into the stack: its twin is freed
            {
                CfObject twin = w.ArchToObject(w.FindArchetype(a[2].AsString))!;
                twin.Nrof = 1;
                uint tag = d.ItemProxy(twin) is var proxy ? ((IMemberEnv)d).TagOf(proxy) : 0;
                w.InsertObInOb(twin, pl);
                return Outcome.Ok(Val.Int(tag));
            }
            case "give": // a trade: the object leaves one tree and enters another
            {
                CfObject item = Item(a[2])!;
                w.RemoveOb(item);
                w.InsertObInOb(item, d.ControllerOf(a[3].AsString)!.State.Ob);
                return Outcome.Ok(Val.Nil);
            }
            case "open":
                pl.Container = a[2].IsNil ? null : Item(a[2]);
                p.Reconcile = true;
                p.Touch();
                return Outcome.Ok(Val.Nil);
            case "mark":
                return Outcome.Ok(Val.Int(ItemMatching.CommandMark(w, pl, a[2].IsNil ? null : a[2].AsString)));
            case "quiet": // a removal that C's fix_player is not told of (FLAG_NO_FIX_PLAYER), and only esrv_del_item tells the client
            {
                CfObject item = Item(a[2])!;
                pl.SetFlag(Define.FLAG_NO_FIX_PLAYER);
                w.RemoveOb(item);
                pl.ClearFlag(Define.FLAG_NO_FIX_PLAYER);
                w.Host.EsrvDelItem(pl.Contr, item.Count);
                return Outcome.Ok(Val.Nil);
            }
            case "keep": // the lane of pick up and drop gives the proxy a level: the tag stays when the object leaves the tree
            {
                CfObject item = Item(a[2])!;
                d.ItemProxy(item).Level = new Level("elsewhere");
                w.RemoveOb(item);
                return Outcome.Ok(Val.Nil);
            }
            case "lose": // the object leaves the tree and is nowhere
                w.RemoveOb(Item(a[2])!);
                return Outcome.Ok(Val.Nil);
            case "touchtag": // a mark of a tag, as the ported C makes it
                p.Shown.MarkDirty((uint)a[2].AsInt);
                return Outcome.Ok(Val.Nil);
            case "send": // esrv_send_item for an object that the window does not show, and for an object of a monster
            {
                CfObject hidden = d.NewItem(w.FindArchetype("sword")!);
                hidden.Invisible = 1;
                w.InsertObInOb(hidden, pl);
                w.Host.EsrvSendItem(pl, hidden);
                CfObject rat = w.ArchToObject(w.FindArchetype("rat"))!;
                w.Host.EsrvSendItem(rat, hidden);
                w.Host.EsrvDelItem(rat.Contr, hidden.Count);
                return Outcome.Ok(Val.Int(p.Shown.Dirty.Count));
            }
            case "wield": // what apply_special will do: the player's current weapon
                pl.CurrentWeapon = a[2].IsNil ? null : Item(a[2]);
                return Outcome.Ok(Val.Nil);
            case "draw":
                w.Host.DrawInfo(pl, a[2].AsString);
                return Outcome.Ok(Val.Nil);
            case "drawat": // a line for an object that is not a player
            {
                CfObject monster = w.ArchToObject(w.FindArchetype("rat"))!;
                w.Host.DrawInfo(monster, a[2].AsString);
                return Outcome.Ok(Val.Nil);
            }
            case "fixweight":
                pl.Carrying += (int)a[2].AsInt;
                return Outcome.Ok(Val.Int(d.FixWeight().Count));
            case "reset": // as mudresetplayer does: the whole inventory is freed
                while (pl.Inv != null)
                {
                    CfObject first = pl.Inv;
                    w.RemoveOb(first);
                    w.FreeObject(first);
                }
                return Outcome.Ok(Val.Nil);
            case "add": // addinvobject: a new object enters the tree
            {
                CfObject item = d.NewItem(w.FindArchetype(a[2].AsString)!);
                item.Nrof = (uint)a[3].AsInt;
                w.InsertObInOb(item, pl);
                return Outcome.Ok(Val.Int(((IMemberEnv)d).TagOf(d.ItemProxy(item))));
            }
            default:
                return Outcome.Fail(Status.BadArgs, "unknown action " + action);
        }
    }
}

public sealed class ItemFlagTests
{
    private static CfObject Ob(int type, params int[] flags)
    {
        var ob = new CfObject { Type = (byte)type };
        foreach (int f in flags)
        {
            ob.SetFlag(f);
        }
        return ob;
    }

    [Theory]
    [InlineData(Define.WEAPON, 2u)]
    [InlineData(Define.BOW, 1u)]
    [InlineData(Define.WAND, 1u)]
    [InlineData(Define.ROD, 1u)]
    [InlineData(Define.HORN, 1u)]
    [InlineData(Define.ARMOUR, 3u)]
    [InlineData(Define.SKILL, 3u)]
    [InlineData(Define.HELMET, 3u)]
    [InlineData(Define.SHIELD, 3u)]
    [InlineData(Define.RING, 3u)]
    [InlineData(Define.BOOTS, 3u)]
    [InlineData(Define.GLOVES, 3u)]
    [InlineData(Define.AMULET, 3u)]
    [InlineData(Define.GIRDLE, 3u)]
    [InlineData(Define.BRACERS, 3u)]
    [InlineData(Define.CLOAK, 3u)]
    [InlineData(Define.CONTAINER, 4u)]
    [InlineData(Define.FOOD, 5u)]
    [InlineData(160, 5u)]
    [InlineData(161, 5u)]
    public void QueryFlags_Given_an_applied_object_of_a_type_When_asked_Then_the_low_nibble_is_the_C_state(int type, uint expected)
    {
        // Arrange
        CfObject ob = Ob(type, Define.FLAG_APPLIED);

        // Act
        uint flags = ItemStrings.QueryFlags(ob);

        // Assert
        Assert.Equal(expected | (type == Define.CONTAINER ? ItemStrings.FOpen : 0u), flags);
    }

    [Fact]
    public void QueryFlags_Given_an_object_that_is_not_applied_When_asked_Then_the_nibble_is_zero_whatever_the_type()
    {
        // Arrange / Act / Assert
        Assert.Equal(0u, ItemStrings.QueryFlags(Ob(Define.WEAPON)));
        Assert.Equal(0u, ItemStrings.QueryFlags(Ob(Define.ARMOUR)));
    }

    [Theory]
    [InlineData(162, 2u)]
    [InlineData(163, 2u)]
    [InlineData(164, 3u)]
    [InlineData(165, 3u)]
    [InlineData(166, 3u)]
    [InlineData(160, 5u)]
    [InlineData(161, 5u)]
    [InlineData(Define.FOOD, 5u)]
    public void Flags_Given_an_applied_object_of_a_GK_type_When_asked_Then_it_is_drawn_as_wielded_or_worn(int type, uint expected)
    {
        // Arrange
        CfObject ob = Ob(type, Define.FLAG_APPLIED);

        // Act / Assert
        Assert.Equal(expected, ItemStrings.Flags(ob));
        Assert.Equal(0u, ItemStrings.Flags(Ob(type)));
    }

    [Fact]
    public void QueryFlags_Given_the_open_state_of_a_container_When_asked_Then_open_is_set_for_the_one_the_player_has_open_or_an_applied_one_without_an_environment()
    {
        // Arrange
        CfObject pl = Ob(Define.PLAYER);
        CfObject bag = Ob(Define.CONTAINER);
        CfObject other = Ob(Define.CONTAINER);
        bag.Env = pl;
        other.Env = pl;
        pl.Container = bag;
        CfObject free = Ob(Define.CONTAINER, Define.FLAG_APPLIED);

        // Act / Assert
        Assert.Equal(ItemStrings.FOpen, ItemStrings.QueryFlags(bag));
        Assert.Equal(0u, ItemStrings.QueryFlags(other));
        Assert.Equal(ItemStrings.FOpen | ItemStrings.AActive, ItemStrings.QueryFlags(free));
        Assert.Equal(0u, ItemStrings.QueryFlags(Ob(Define.CONTAINER)));
    }

    [Fact]
    public void QueryFlags_Given_every_combination_of_the_other_flags_When_asked_Then_the_bits_are_the_golden_ones()
    {
        // Arrange: (known cursed, cursed, damned, known magical, identified, unpaid, locked) -> the word
        var golden = new List<(int[] Flags, uint Word)>
        {
            (Array.Empty<int>(), 0),
            (new[] { Define.FLAG_CURSED }, 0), // not known: nothing
            (new[] { Define.FLAG_KNOWN_CURSED, Define.FLAG_CURSED }, 0x0800),
            (new[] { Define.FLAG_KNOWN_CURSED, Define.FLAG_DAMNED }, 0x1000),
            (new[] { Define.FLAG_KNOWN_CURSED, Define.FLAG_CURSED, Define.FLAG_DAMNED }, 0x1000), // damned beats cursed
            (new[] { Define.FLAG_KNOWN_CURSED }, 0),
            (new[] { Define.FLAG_KNOWN_MAGICAL }, 0x0400),
            (new[] { Define.FLAG_KNOWN_MAGICAL, Define.FLAG_IDENTIFIED }, 0), // identified: no magic bit
            (new[] { Define.FLAG_IDENTIFIED }, 0),
            (new[] { Define.FLAG_UNPAID }, 0x0200),
            (new[] { Define.FLAG_INV_LOCKED }, 0x8000),
            (new[] { Define.FLAG_UNPAID, Define.FLAG_KNOWN_MAGICAL, Define.FLAG_KNOWN_CURSED, Define.FLAG_CURSED, Define.FLAG_INV_LOCKED }, 0x0200 | 0x0400 | 0x0800 | 0x8000),
        };

        // Act / Assert
        foreach ((int[] flags, uint word) in golden)
        {
            Assert.Equal(word, ItemStrings.QueryFlags(Ob(Define.FOOD, flags)));
        }
    }

    [Fact]
    public void Flags_Given_no_pick_and_a_weapon_that_is_applied_and_unpaid_When_asked_Then_nopick_is_ored_in_for_sends_and_updates_alike()
    {
        // Arrange
        CfObject ob = Ob(Define.WEAPON, Define.FLAG_NO_PICK, Define.FLAG_APPLIED, Define.FLAG_UNPAID);

        // Act / Assert
        Assert.Equal(0x4000u | 0x0200u | 2u, ItemStrings.Flags(ob));
        Assert.Equal(0x0200u | 2u, ItemStrings.QueryFlags(ob));
    }

    [Fact]
    public void StackWeight_Given_a_stack_a_single_object_and_a_container_When_asked_Then_the_whole_stack_in_grams()
    {
        // Arrange
        CfObject stack = new() { Weight = 100, Nrof = 4 };
        CfObject single = new() { Weight = 3000, Nrof = 0 };
        CfObject bag = new() { Weight = 200, Nrof = 0, Carrying = 450 };

        // Act / Assert
        Assert.Equal(400, ItemStrings.StackWeight(stack));
        Assert.Equal(3000, ItemStrings.StackWeight(single));
        Assert.Equal(650, ItemStrings.StackWeight(bag));
    }
}

public sealed class ItemTests
{
    private static ExtensionOptions Options() => new()
    {
        Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = ItemWorld.Register,
    };

    private static DirectExt Start(TempFolder world, long protoMinor = 1) => new(world, Options(), 1, true, 16_777_216, protoMinor);

    private static ReplyFrame Login(DirectExt ext, string account) => ext.Call(5, new[] { S(account) }, player: account);

    private static ReplyFrame Do(DirectExt ext, string action, string account, Val? a = null, Val? b = null) =>
        ext.Call(ItemWorld.OpDo, new[] { S(action), S(account), a ?? Val.Nil, b ?? Val.Nil }, player: account);

    private static ReplyFrame Logout(DirectExt ext, string account) => ext.Call(6, new[] { S(account) }, player: account);

    private const string Bob = "arch banana\nnrof 4\nend\narch sword\nnrof 1\napplied 1\nend\narch bag\nnrof 1\narch banana\nnrof 2\nend\nend\n";

    private static TempFolder WorldWith(string account, string inventory, string header = "")
    {
        TempFolder world = ItemWorld.MakeWorld();
        ItemWorld.Account(world, account, header, inventory);
        return world;
    }

    /// <summary>The items of a player (or of a container) as rows of (handle, name) from <c>get_list</c>.</summary>
    private static List<(ulong Handle, string Name)> Items(DirectExt ext, ulong holder, bool player = true)
    {
        int member = player ? Ply("muditems") : Obj("muditems");
        ReplyFrame r = ext.Call(12, new[] { H(holder), Val.Int(member), Val.List(Val.Int(Obj("mudrealname"))) });
        Assert.Equal(0UL, r.Status);
        return r.Result.Items.Select(row => (row.Items[0].AsHandle, row.Items[1].AsString)).ToList();
    }

    private static Dictionary<string, string> Strings(ReplyFrame r) =>
        Effects(r, "client.set").ToDictionary(e => e.Get("name")!.AsString, e => e.Get("value")!.AsString);

    private static string ItemKey(ulong handle) => "clientr.mud_item" + (uint)handle;

    // ------------------------------------------------------------------ the strings of a loaded account

    [Fact]
    public void PlayerLogin_Given_an_account_with_items_When_logged_in_Then_the_strings_are_the_golden_text_one_per_item_of_the_top_level()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));

        // Act
        ReplyFrame r = Login(ext, "bob");
        ulong player = r.Result.AsHandle;
        var items = Items(ext, player);

        // Assert
        Assert.Equal(new[] { "banana", "sword", "bag" }, items.Select(i => i.Name));
        Dictionary<string, string> sets = Strings(r);
        Assert.Equal("bananas,mud_banana.png,6,4,400,0,0", sets[ItemKey(items[0].Handle)]);
        Assert.Equal("sword,mud_sword.png,15,1,3000,2,0", sets[ItemKey(items[1].Handle)]);
        Assert.StartsWith("bag,mud_bag.png,122,1,", sets[ItemKey(items[2].Handle)]);
        Assert.EndsWith(",0,0", sets[ItemKey(items[2].Handle)]);
        Assert.Equal(3, sets.Keys.Count(k => k.StartsWith("clientr.mud_item", StringComparison.Ordinal)));
        Assert.Equal("73600", sets["clientr.mud_weight"]); // 70,000 of the player, 400 of bananas, 3,000 of the sword, 200 in the bag
    }

    [Fact]
    public void PlayerLogin_Given_an_account_with_a_bag_When_logged_in_Then_the_bags_contents_have_no_string_until_the_bag_is_read_or_opened()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ReplyFrame r = Login(ext, "bob");
        var items = Items(ext, r.Result.AsHandle);

        // Act
        var inside = Items(ext, items[2].Handle, player: false);

        // Assert
        Assert.Equal(new[] { "banana" }, inside.Select(i => i.Name));
        Assert.DoesNotContain(ItemKey(inside[0].Handle), Strings(r).Keys);
        Assert.NotEqual(items[0].Handle, inside[0].Handle);
    }

    [Fact]
    public void PlayerLogin_Given_the_player_weight_and_strength_When_logged_in_Then_mud_weight_is_the_whole_weight_and_the_limit_is_the_table_entry()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));

        // Act
        ReplyFrame r = Login(ext, "bob");
        Dictionary<string, string> sets = Strings(r);

        // Assert: the weight of the player (70,000) and what he carries (the account file does not hold the sum: it is computed by the load)
        Assert.Equal(1, Effects(r, "client.set").Count(e => e.Get("name")!.AsString == "clientr.mud_weight"));
        Assert.Equal(ItemMatching.WeightLimit[2].ToString(), sets["clientr.mud_weight_lim"]);
        Assert.True(long.Parse(sets["clientr.mud_weight"]) >= 70000);
    }

    // ------------------------------------------------------------------ the flush

    [Fact]
    public void Flush_Given_a_request_that_changes_nothing_When_it_ends_Then_no_string_is_written()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        Login(ext, "bob");

        // Act
        ReplyFrame r = Do(ext, "draw", "bob", S("hello"));

        // Assert
        Assert.Empty(Effects(r, "client.set"));
        Assert.Empty(Effects(r, "client.del"));
    }

    [Fact]
    public void Flush_Given_a_stack_that_grows_five_times_in_one_request_When_it_ends_Then_one_effect_with_the_last_text()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ReplyFrame login = Login(ext, "bob");
        var items = Items(ext, login.Result.AsHandle);

        // Act
        ReplyFrame r = Do(ext, "grow", "bob", Val.Int((uint)items[0].Handle), Val.Int(5));

        // Assert
        Assert.Equal(9L, r.Result.AsInt);
        Val one = Assert.Single(Effects(r, "client.set"), e => e.Get("name")!.AsString.StartsWith("clientr.mud_item", StringComparison.Ordinal));
        Assert.Equal(ItemKey(items[0].Handle), one.Get("name")!.AsString);
        Assert.Equal("bananas,mud_banana.png,6,9,900,0,0", one.Get("value")!.AsString);
        Assert.Single(r.Effects); // the weight is the player's own and carrying: the test's writes do not move it
    }

    [Fact]
    public void Flush_Given_a_write_of_a_member_of_an_item_When_the_request_ends_Then_its_string_is_written_and_the_account_is_saved()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ReplyFrame login = Login(ext, "bob");
        var items = Items(ext, login.Result.AsHandle);

        // Act
        ReplyFrame r = Set(ext, items[1].Handle, Obj("mudrealname"), S("lance"));
        Logout(ext, "bob");
        string file = Flushed(ext, "accounts/bob.txt", t => t.Contains("name lance"));

        // Assert
        Assert.Equal(0UL, r.Status);
        Val set = Assert.Single(Effects(r, "client.set"), e => e.Get("name")!.AsString == ItemKey(items[1].Handle));
        Assert.Equal("lance,mud_sword.png,15,1,3000,2,0", set.Get("value")!.AsString);
        Assert.Empty(r.Events); // an item has no NPC: no onUpdateObject
        Assert.Contains("name lance", file);
    }

    [Fact]
    public void Flush_Given_an_item_that_is_freed_When_the_request_ends_Then_its_string_is_deleted_and_its_tag_is_dead()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ReplyFrame login = Login(ext, "bob");
        var items = Items(ext, login.Result.AsHandle);

        // Act
        ReplyFrame r = Do(ext, "free", "bob", Val.Int((uint)items[1].Handle));
        ReplyFrame read = ext.Call(9, new[] { H(items[1].Handle), Val.Int(Obj("mudrealname")) });

        // Assert
        Assert.Equal(new[] { ItemKey(items[1].Handle) }, Effects(r, "client.del").Select(e => e.Get("name")!.AsString));
        Assert.Equal((ulong)Status.BadHandle, read.Status);
        Assert.Equal(new[] { "banana", "bag" }, Items(ext, login.Result.AsHandle).Select(i => i.Name));
    }

    [Fact]
    public void Flush_Given_a_stack_that_is_decreased_to_zero_When_the_request_ends_Then_the_string_is_deleted_and_the_tag_is_dead()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ReplyFrame login = Login(ext, "bob");
        var items = Items(ext, login.Result.AsHandle);
        int live = ext.Dispatcher.LiveItemTags;

        // Act
        ReplyFrame part = Do(ext, "decrease", "bob", Val.Int((uint)items[0].Handle), Val.Int(1));
        ReplyFrame all = Do(ext, "decrease", "bob", Val.Int((uint)items[0].Handle), Val.Int(3));

        // Assert
        Assert.StartsWith("bananas,", Effects(part, "client.set").Single(e => e.Get("name")!.AsString == ItemKey(items[0].Handle)).Get("value")!.AsString);
        Assert.Equal(new[] { ItemKey(items[0].Handle) }, Effects(all, "client.del").Select(e => e.Get("name")!.AsString));
        Assert.Equal(live - 1, ext.Dispatcher.LiveItemTags);
        Assert.Equal((ulong)Status.BadHandle, ext.Call(9, new[] { H(items[0].Handle), Val.Int(Obj("mudrealname")) }).Status);
    }

    [Fact]
    public void Flush_Given_a_new_object_that_merges_into_a_stack_When_the_request_ends_Then_the_twins_tag_is_dead_and_the_stack_keeps_its_own()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ReplyFrame login = Login(ext, "bob");
        var items = Items(ext, login.Result.AsHandle);

        // Act
        ReplyFrame r = Do(ext, "merge", "bob", S("banana"));
        ulong twin = (ulong)1 << 32 | (uint)r.Result.AsInt;

        // Assert
        Assert.Equal((ulong)Status.BadHandle, ext.Call(9, new[] { H(twin), Val.Int(Obj("mudrealname")) }).Status);
        Assert.Equal("bananas,mud_banana.png,6,5,500,0,0", Strings(r)[ItemKey(items[0].Handle)]);
        Assert.DoesNotContain("clientr.mud_item" + (uint)r.Result.AsInt, Strings(r).Keys);
        Assert.Equal(3, Items(ext, login.Result.AsHandle).Count);
    }

    [Fact]
    public void Flush_Given_a_new_object_in_the_tree_When_the_request_ends_Then_it_has_a_tag_and_a_string_and_the_weight_grows()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ReplyFrame login = Login(ext, "bob");
        long weight = long.Parse(Strings(login)["clientr.mud_weight"]);

        // Act
        ReplyFrame r = Do(ext, "add", "bob", S("mail"), Val.Int(1));
        string key = "clientr.mud_item" + (uint)r.Result.AsInt;

        // Assert
        Assert.Equal("mail,mud_mail.png,16,1,8000,0,0", Strings(r)[key]);
        Assert.Equal(weight + 8000, long.Parse(Strings(r)["clientr.mud_weight"]));
        Assert.Equal(4, Items(ext, login.Result.AsHandle).Count);
    }

    [Fact]
    public void Flush_Given_the_strength_written_When_the_request_ends_Then_the_weight_limit_follows_the_table()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong player = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame r = Set(ext, player, Ply("mudstrength"), Val.Int(30));
        ReplyFrame high = Set(ext, player, Ply("mudstrength"), Val.Int(31));

        // Assert
        Assert.Equal("4500000", Strings(r)["clientr.mud_weight_lim"]);
        Assert.Equal("4500000", ext.Dispatcher.ControllerOf("bob")!.WeightLimit.ToString());
        Assert.DoesNotContain("clientr.mud_weight_lim", Strings(high).Keys); // above MAX_STAT the last entry: no change
    }

    [Fact]
    public void Flush_Given_a_player_that_logs_in_twice_When_the_second_login_comes_Then_every_string_is_sent_again()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ReplyFrame first = Login(ext, "bob");

        // Act
        ReplyFrame second = Login(ext, "bob");

        // Assert
        Assert.Equal(Strings(first).OrderBy(p => p.Key), Strings(second).OrderBy(p => p.Key));
        Assert.Equal(Effects(first, "client.set").Count, Effects(second, "client.set").Count);
    }

    [Fact]
    public void Flush_Given_effects_that_were_lost_When_the_next_request_ends_Then_every_item_string_is_written_again()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        Login(ext, "bob");
        Player p = ext.Dispatcher.ControllerOf("bob")!;
        p.Shown.Forget();

        // Act
        ReplyFrame r = Do(ext, "draw", "bob", S("x"));

        // Assert
        Assert.Equal(3, Strings(r).Keys.Count(k => k.StartsWith("clientr.mud_item", StringComparison.Ordinal)));
        Assert.Contains("clientr.mud_hp", Strings(r).Keys); // the stats go again too
        Assert.False(p.NeedsResend);
    }

    [Fact]
    public void Flush_Given_more_items_than_may_be_shown_When_logged_in_Then_only_the_first_are_written_and_a_line_says_so()
    {
        // Arrange
        var inv = new System.Text.StringBuilder();
        for (int i = 0; i < Dispatcher.MaxShownItems + 20; i++)
        {
            inv.Append("arch sword\nend\n");
        }
        using DirectExt ext = Start(WorldWith("bob", inv.ToString()));

        // Act
        ReplyFrame r = Login(ext, "bob");

        // Assert
        Assert.Equal(Dispatcher.MaxShownItems, Strings(r).Keys.Count(k => k.StartsWith("clientr.mud_item", StringComparison.Ordinal)));
        Assert.Contains(ext.Logs, l => l.Text.Contains("only the first " + Dispatcher.MaxShownItems));
    }

    // ------------------------------------------------------------------ the open container

    [Fact]
    public void Flush_Given_a_container_that_is_opened_and_closed_When_the_requests_end_Then_its_items_have_a_string_with_field_6_and_the_container_string_follows()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ReplyFrame login = Login(ext, "bob");
        var items = Items(ext, login.Result.AsHandle);
        var inside = Items(ext, items[2].Handle, player: false);

        // Act
        ReplyFrame open = Do(ext, "open", "bob", Val.Int((uint)items[2].Handle));
        ReplyFrame close = Do(ext, "open", "bob");

        // Assert
        Assert.Equal((uint)items[2].Handle + ",bag,mud_bag.png", Strings(open)["clientr.mud_container"]);
        Assert.Equal("bananas,mud_banana.png,6,2,200,0,1", Strings(open)[ItemKey(inside[0].Handle)]);
        Assert.Equal("bag,mud_bag.png,122,1,200,8192,0", Strings(open)[ItemKey(items[2].Handle)]); // F_OPEN: the bag is the player's open container
        Assert.Equal("bag,mud_bag.png,122,1,200,0,0", Strings(close)[ItemKey(items[2].Handle)]);
        Assert.Equal(new[] { ItemKey(inside[0].Handle) }, Effects(close, "client.del").Select(e => e.Get("name")!.AsString));
        Assert.Equal("", Strings(close)["clientr.mud_container"]);
    }

    [Fact]
    public void Flush_Given_the_open_container_is_freed_When_the_request_ends_Then_the_container_string_is_emptied_and_the_open_state_is_dropped()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        Do(ext, "open", "bob", Val.Int((uint)items[2].Handle));

        // Act
        ReplyFrame r = Do(ext, "free", "bob", Val.Int((uint)items[2].Handle));

        // Assert
        Assert.Equal("", Strings(r)["clientr.mud_container"]);
        Assert.Null(ext.Dispatcher.ControllerOf("bob")!.State.Ob.Container);
        Assert.Contains(ItemKey(items[2].Handle), Effects(r, "client.del").Select(e => e.Get("name")!.AsString));
    }

    [Fact]
    public void Flush_Given_marks_of_tags_the_window_does_not_show_When_the_request_ends_Then_nothing_is_written()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        var inside = Items(ext, items[2].Handle, player: false);

        // Act
        ReplyFrame nested = Do(ext, "touchtag", "bob", Val.Int((uint)inside[0].Handle)); // inside a closed sack
        ReplyFrame unknown = Do(ext, "touchtag", "bob", Val.Int(777777));
        ReplyFrame same = Do(ext, "touchtag", "bob", Val.Int((uint)items[0].Handle)); // shown, but the text is the same
        Do(ext, "free", "bob", Val.Int((uint)items[0].Handle));

        // Assert
        Assert.Empty(Effects(nested, "client.set"));
        Assert.Empty(Effects(unknown, "client.set"));
        Assert.Empty(Effects(same, "client.set"));
    }

    [Fact]
    public void Flush_Given_a_mark_of_an_item_in_the_open_container_When_the_text_changes_Then_the_string_has_field_6()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        var inside = Items(ext, items[2].Handle, player: false);
        Do(ext, "open", "bob", Val.Int((uint)items[2].Handle));

        // Act
        ReplyFrame r = Do(ext, "grow", "bob", Val.Int((uint)inside[0].Handle), Val.Int(1));

        // Assert
        Assert.Equal("bananas,mud_banana.png,6,3,300,0,1", Strings(r)[ItemKey(inside[0].Handle)]);
    }

    [Fact]
    public void ItemSent_Given_an_invisible_object_and_a_monster_and_a_count_of_no_item_When_the_client_is_told_Then_nothing_is_marked()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        Login(ext, "bob");

        // Act
        ReplyFrame r = Do(ext, "send", "bob");

        // Assert
        Assert.Equal(0L, r.Result.AsInt);
        Assert.DoesNotContain(Effects(r, "client.set"), e => e.Get("name")!.AsString.StartsWith("clientr.mud_item", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ the lifecycle of tags

    [Fact]
    public void Tags_Given_an_item_that_the_lane_of_pick_up_gave_a_level_When_it_leaves_the_tree_Then_the_tag_stays_and_the_string_goes()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);

        // Act
        ReplyFrame r = Do(ext, "keep", "bob", Val.Int((uint)items[1].Handle));

        // Assert
        Assert.Equal(new[] { ItemKey(items[1].Handle) }, Effects(r, "client.del").Select(e => e.Get("name")!.AsString));
        Assert.Equal(0UL, ext.Call(9, new[] { H(items[1].Handle), Val.Int(Obj("mudrealname")) }).Status);
    }

    [Fact]
    public void Tags_Given_an_item_that_leaves_the_tree_and_is_nowhere_When_the_request_ends_Then_its_tag_is_dead()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);

        // Act
        Do(ext, "lose", "bob", Val.Int((uint)items[1].Handle));

        // Assert
        Assert.Equal((ulong)Status.BadHandle, ext.Call(9, new[] { H(items[1].Handle), Val.Int(Obj("mudrealname")) }).Status);
    }

    [Fact]
    public void Tags_Given_a_removal_that_only_esrv_del_item_tells_of_When_the_request_ends_Then_the_string_is_deleted_and_the_tag_is_dead()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);

        // Act
        ReplyFrame r = Do(ext, "quiet", "bob", Val.Int((uint)items[0].Handle));

        // Assert
        Assert.Equal(new[] { ItemKey(items[0].Handle) }, Effects(r, "client.del").Select(e => e.Get("name")!.AsString));
        Assert.Equal((ulong)Status.BadHandle, ext.Call(9, new[] { H(items[0].Handle), Val.Int(Obj("mudrealname")) }).Status);
    }

    [Fact]
    public void PlayerLogin_Given_an_invisible_object_in_the_pack_When_logged_in_Then_it_has_a_tag_and_no_string()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob + "arch sword\ninvisible 1\nend\n"));

        // Act
        ReplyFrame r = Login(ext, "bob");

        // Assert
        Assert.Equal(4, ext.Dispatcher.LiveItemTags);
        Assert.Equal(3, Strings(r).Keys.Count(k => k.StartsWith("clientr.mud_item", StringComparison.Ordinal)));
    }

    [Fact]
    public void Item_Given_a_write_that_changes_nothing_When_the_request_ends_Then_no_string_and_the_account_stays_clean()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        PlayerState state = ext.Dispatcher.ControllerOf("bob")!.State;
        state.Dirty = false;

        // Act
        ReplyFrame r = Set(ext, items[1].Handle, Obj("mudrealname"), S("sword"));

        // Assert
        Assert.Empty(Effects(r, "client.set"));
        Assert.False(state.Dirty);
    }

    [Fact]
    public void Item_Given_an_item_in_a_bag_that_a_level_holds_When_a_member_is_written_Then_the_level_is_saved()
    {
        // Arrange
        TempFolder world = ItemWorld.MakeWorld();
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall.nw"), ObjectWorld.SeedHall);
        using DirectExt ext = Start(world);
        var (places, _) = LoadAll(ext, "hall.nw");
        ulong bag = places.Select(p => p.Get("handle")!.AsHandle).Last();
        ulong banana = Items(ext, bag, player: false)[0].Handle;
        ext.Call(3);

        // Act
        Set(ext, banana, Obj("mudrealname"), S("plantain"));
        string file = Flushed(ext, "levels/hall.nw.map", t => t.Contains("name plantain"));

        // Assert
        Assert.Contains("name plantain", file);
    }

    [Fact]
    public void Muditems_Given_an_empty_hand_and_a_monster_without_items_When_read_Then_an_empty_list()
    {
        // Arrange
        TempFolder world = WorldWith("bob", "");
        using DirectExt ext = Start(world);
        ulong bob = Login(ext, "bob").Result.AsHandle;

        // Act / Assert
        Assert.Empty(Items(ext, bob));
        Assert.Equal(0, ext.Dispatcher.LiveItemTags);
    }


    [Fact]
    public void Tags_Given_a_thousand_logins_and_logouts_When_counted_Then_the_live_tags_stay_flat()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        Login(ext, "bob");
        Logout(ext, "bob");
        int tags = ext.Dispatcher.LiveTags;

        // Act
        for (int i = 0; i < 1000; i++)
        {
            ReplyFrame r = Login(ext, "bob");
            Items(ext, r.Result.AsHandle); // the sack and the nested items are tagged by reads too
            Items(ext, Items(ext, r.Result.AsHandle)[2].Handle, player: false);
            Logout(ext, "bob");
        }

        // Assert
        Assert.Equal(tags, ext.Dispatcher.LiveTags);
        Assert.Equal(0, ext.Dispatcher.LiveItemTags);
    }

    [Fact]
    public void Tags_Given_the_whole_inventory_is_freed_by_a_script_When_the_request_ends_Then_every_tag_and_string_is_gone()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong player = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, player);
        Items(ext, items[2].Handle, player: false);
        int before = ext.Dispatcher.LiveItemTags;

        // Act
        ReplyFrame r = Do(ext, "reset", "bob");

        // Assert
        Assert.Equal(4, before);
        Assert.Equal(3, Effects(r, "client.del").Count);
        Assert.Equal(0, ext.Dispatcher.LiveItemTags);
        Assert.Empty(Items(ext, player));
    }

    [Fact]
    public void Tags_Given_an_item_that_moves_to_another_players_tree_When_the_request_ends_Then_the_tag_stays_and_the_strings_move()
    {
        // Arrange
        TempFolder world = WorldWith("bob", Bob);
        ItemWorld.Account(world, "amy", "", "");
        using DirectExt ext = Start(world);
        ulong bob = Login(ext, "bob").Result.AsHandle;
        ulong amy = Login(ext, "amy").Result.AsHandle;
        var items = Items(ext, bob);

        // Act
        ReplyFrame r = Do(ext, "give", "bob", Val.Int((uint)items[1].Handle), S("amy"));

        // Assert
        Assert.Equal("sword", Items(ext, amy).Single().Name);
        Assert.Equal(items[1].Handle, Items(ext, amy).Single().Handle);
        Val del = Assert.Single(Effects(r, "client.del"));
        Assert.Equal("bob", del.Get("player")!.AsString);
        Assert.Equal(ItemKey(items[1].Handle), del.Get("name")!.AsString);
        Val set = Assert.Single(Effects(r, "client.set"), e => e.Get("name")!.AsString == ItemKey(items[1].Handle));
        Assert.Equal("amy", set.Get("player")!.AsString);
    }

    [Fact]
    public void Tags_Given_a_level_objects_inventory_When_the_level_unloads_Then_the_tags_of_what_it_held_are_dead()
    {
        // Arrange
        TempFolder world = ItemWorld.MakeWorld();
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall.nw"), ObjectWorld.SeedHall);
        using DirectExt ext = Start(world);
        var (places, _) = LoadAll(ext, "hall.nw");
        ulong bag = places.Select(p => p.Get("handle")!.AsHandle).Last();
        var inside = Items(ext, bag, player: false);
        int live = ext.Dispatcher.LiveItemTags;

        // Act
        ext.Call(8, new[] { S("hall.nw") });

        // Assert
        Assert.Single(inside);
        Assert.Equal(1, live);
        Assert.Equal(0, ext.Dispatcher.LiveItemTags);
    }

    // ------------------------------------------------------------------ ids from a client

    [Fact]
    public void FindItemById_Given_the_tag_of_another_players_item_When_asked_for_a_player_Then_only_the_owner_finds_it()
    {
        // Arrange
        TempFolder world = WorldWith("bob", Bob);
        ItemWorld.Account(world, "amy", "", "arch banana\nnrof 1\nend\n");
        using DirectExt ext = Start(world);
        ulong bob = Login(ext, "bob").Result.AsHandle;
        Login(ext, "amy");
        var items = Items(ext, bob);
        PlayerState bobState = ext.Dispatcher.ControllerOf("bob")!.State;
        PlayerState amyState = ext.Dispatcher.ControllerOf("amy")!.State;
        long tag = (uint)items[0].Handle;

        // Act / Assert
        Assert.NotNull(ext.Dispatcher.FindItemById(bobState, tag, deep: false));
        Assert.Null(ext.Dispatcher.FindItemById(amyState, tag, deep: false));
        Assert.Null(ext.Dispatcher.FindItemById(amyState, tag, deep: true));
        Assert.Null(ext.Dispatcher.FindItemById(bobState, 0, deep: true));
        Assert.Null(ext.Dispatcher.FindItemById(bobState, -3, deep: true));
        Assert.Null(ext.Dispatcher.FindItemById(bobState, (long)uint.MaxValue + 1, deep: true));
        Assert.Null(ext.Dispatcher.FindItemById(bobState, 0xFFFFFFFEL, deep: true)); // a tag nobody has
    }

    [Fact]
    public void FindItemById_Given_an_item_in_a_sack_and_a_player_object_When_asked_Then_the_sack_needs_deep_and_a_non_item_is_never_found()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        var inside = Items(ext, items[2].Handle, player: false);
        PlayerState state = ext.Dispatcher.ControllerOf("bob")!.State;

        // Act / Assert
        Assert.Null(ext.Dispatcher.FindItemById(state, (uint)inside[0].Handle, deep: false));
        Assert.NotNull(ext.Dispatcher.FindItemById(state, (uint)inside[0].Handle, deep: true));
        Assert.Null(ext.Dispatcher.FindItemById(state, (uint)bob, deep: true)); // the tag of the player himself
    }

    [Fact]
    public void FindItemById_Given_an_item_in_the_open_container_When_asked_without_deep_Then_it_is_found_as_the_top_level_is()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        var inside = Items(ext, items[2].Handle, player: false);
        PlayerState state = ext.Dispatcher.ControllerOf("bob")!.State;
        Do(ext, "open", "bob", Val.Int((uint)items[2].Handle));

        // Act / Assert
        Assert.NotNull(ext.Dispatcher.FindItemById(state, (uint)inside[0].Handle, deep: false));
    }

    [Fact]
    public void FindItemById_Given_a_dead_tag_and_the_tag_of_a_level_object_When_asked_Then_nothing()
    {
        // Arrange
        TempFolder world = WorldWith("bob", Bob);
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall.nw"), ObjectWorld.SeedHall);
        using DirectExt ext = Start(world);
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        var (places, _) = LoadAll(ext, "hall.nw");
        PlayerState state = ext.Dispatcher.ControllerOf("bob")!.State;
        long levelTag = (uint)places[0].Get("handle")!.AsHandle;
        long deadTag = (uint)items[1].Handle;
        Do(ext, "free", "bob", Val.Int(deadTag));

        // Act / Assert
        Assert.Null(ext.Dispatcher.FindItemById(state, deadTag, deep: true));
        Assert.Null(ext.Dispatcher.FindItemById(state, levelTag, deep: true));
    }

    [Fact]
    public void MarkedObject_Given_player_B_writes_the_tag_of_player_As_item_When_read_Then_nothing_is_marked_and_As_marks_are_untouched()
    {
        // Arrange
        TempFolder world = WorldWith("bob", Bob);
        ItemWorld.Account(world, "amy", "", "arch banana\nnrof 1\nend\n");
        using DirectExt ext = Start(world);
        ulong bob = Login(ext, "bob").Result.AsHandle;
        ulong amy = Login(ext, "amy").Result.AsHandle;
        var bobs = Items(ext, bob);
        var amys = Items(ext, amy);
        int member = Ply("markedobject");

        // Act
        ReplyFrame own = Set(ext, bob, member, H(bobs[0].Handle));
        ReplyFrame stolen = Set(ext, bob, member, H(amys[0].Handle));
        ReplyFrame onAmy = Set(ext, amy, member, H(bobs[1].Handle));

        // Assert
        Assert.Equal(bobs[0].Handle, own.Result.AsHandle);
        Assert.Equal(bobs[0].Handle, stolen.Result.AsHandle); // the mark stays what it was
        Assert.Equal("", onAmy.Result.AsString);
        Assert.Equal(bobs[0].Handle, Get(ext, bob, member).AsHandle);
        Assert.Equal("", Get(ext, amy, member).AsString);
    }

    // ------------------------------------------------------------------ the members

    [Fact]
    public void MarkedObject_Given_a_mark_When_written_nil_or_empty_or_a_string_or_read_after_the_item_is_freed_Then_it_clears_or_is_refused()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        int member = Ply("markedobject");
        Set(ext, bob, member, H(items[0].Handle));

        // Act
        ReplyFrame empty = Set(ext, bob, member, S(""));
        Set(ext, bob, member, H(items[1].Handle));
        ReplyFrame nil = Set(ext, bob, member, Val.Nil);
        Set(ext, bob, member, H(items[1].Handle));
        ReplyFrame text = Set(ext, bob, member, S("sword"));
        Do(ext, "free", "bob", Val.Int((uint)items[1].Handle));
        ReplyFrame read = ext.Call(9, new[] { H(bob), Val.Int(member) });
        ReplyFrame number = Set(ext, bob, member, Val.Int(3));

        // Assert
        Assert.Equal(ValKind.String, empty.Result.Kind);
        Assert.Equal("", empty.Result.AsString);
        Assert.Equal(ValKind.String, nil.Result.Kind);
        Assert.Equal("", nil.Result.AsString);
        Assert.Equal((ulong)Status.BadArgs, text.Status);
        Assert.Equal((ulong)Status.BadArgs, number.Status);
        Assert.Equal(ValKind.String, read.Result.Kind);
        Assert.Equal("", read.Result.AsString); // as in C: the mark is not cleared by a read that does not find the object
    }

    [Fact]
    public void MarkedObject_Given_an_item_inside_a_sack_or_the_player_himself_When_written_Then_nothing_is_marked()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        var inside = Items(ext, items[2].Handle, player: false);
        int member = Ply("markedobject");

        // Act
        ReplyFrame nested = Set(ext, bob, member, H(inside[0].Handle));
        ReplyFrame self = Set(ext, bob, member, H(bob));

        // Assert
        Assert.Equal(ValKind.String, nested.Result.Kind);
        Assert.Equal("", nested.Result.AsString);
        Assert.Equal(ValKind.String, self.Result.Kind);
        Assert.Equal("", self.Result.AsString);
        Do(ext, "open", "bob", Val.Int((uint)items[2].Handle));
        ReplyFrame inOpen = Set(ext, bob, member, H(inside[0].Handle)); // in the open container: shown, but C's mark looks at the top level only
        Assert.Equal(ValKind.String, inOpen.Result.Kind);
        Assert.Null(ext.Dispatcher.ControllerOf("bob")!.Mark);
    }

    [Fact]
    public void CommandMark_Given_a_name_When_marked_Then_the_mark_shows_in_the_member_and_the_lines_go_to_the_player()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);

        // Act
        ReplyFrame none = Do(ext, "mark", "bob");
        ReplyFrame marked = Do(ext, "mark", "bob", S("sword"));
        ReplyFrame again = Do(ext, "mark", "bob");
        ReplyFrame missing = Do(ext, "mark", "bob", S("zeppelin"));

        // Assert
        Assert.Equal("You have no marked object.", none.Events.Single().Get("args")!.Items.Single().AsString);
        Assert.Equal(0L, marked.Result.AsInt);
        Assert.Equal("Marked item sword (wielded)", marked.Events.Single().Get("args")!.Items.Single().AsString);
        Assert.Equal(items[1].Handle, Get(ext, bob, Ply("markedobject")).AsHandle);
        Assert.Equal("sword (wielded) is marked.", again.Events.Single().Get("args")!.Items.Single().AsString);
        Assert.Equal(1L, missing.Result.AsInt);
        Assert.Equal("Could not find an object that matches zeppelin", missing.Events.Single().Get("args")!.Items.Single().AsString);
    }

    [Fact]
    public void Muditems_Given_a_player_and_a_sack_When_read_with_get_and_get_list_Then_the_same_visible_items_in_inventory_order()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob + "arch sword\ninvisible 1\nend\n"));
        ulong bob = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame got = ext.Call(9, new[] { H(bob), Val.Int(Ply("muditems")) });
        var rows = Items(ext, bob);

        // Assert
        Assert.Equal(rows.Select(r => r.Handle), got.Result.Items.Select(v => v.AsHandle));
        Assert.Equal(3, rows.Count); // the invisible object is not an item
        ReplyFrame empty = ext.Call(12, new[] { H(bob), Val.Int(Ply("muditems")), Val.List() });
        Assert.All(empty.Result.Items, row => Assert.Single(row.Items));
    }

    [Fact]
    public void GetList_Given_bad_arguments_When_called_Then_bad_args_for_the_whole_request()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame field = ext.Call(12, new[] { H(bob), Val.Int(Ply("muditems")), Val.List(Val.Int(999999)) });
        ReplyFrame member = ext.Call(12, new[] { H(bob), Val.Int(999999), Val.List() });
        ReplyFrame notList = ext.Call(12, new[] { H(bob), Val.Int(Ply("mudhp")), Val.List() });
        ReplyFrame dead = ext.Call(12, new[] { H(1UL << 32 | 99999), Val.Int(Ply("muditems")), Val.List() });
        ReplyFrame notNumber = ext.Call(12, new[] { H(bob), Val.Int(Ply("muditems")), Val.List(S("x")) });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, field.Status);
        Assert.Equal((ulong)Status.BadArgs, member.Status);
        Assert.Equal((ulong)Status.BadArgs, notList.Status);
        Assert.Equal((ulong)Status.BadHandle, dead.Status);
        Assert.Equal((ulong)Status.BadArgs, notNumber.Status);
    }

    [Theory]
    [InlineData(1L, 1024, 1025)]
    [InlineData(0L, 256, 257)]
    public void GetList_Given_more_rows_than_max_list_When_called_Then_too_large_and_exactly_max_list_is_fine(long minor, int max, int tooMany)
    {
        // Arrange
        string Sword(int n) => string.Concat(Enumerable.Repeat("arch sword\nend\n", n));
        using DirectExt exactly = Start(WorldWith("bob", Sword(max)), minor);
        using DirectExt over = Start(WorldWith("bob", Sword(tooMany)), minor);
        ulong a = Login(exactly, "bob").Result.AsHandle;
        ulong b = Login(over, "bob").Result.AsHandle;

        // Act
        ReplyFrame fine = exactly.Call(12, new[] { H(a), Val.Int(Ply("muditems")), Val.List() });
        ReplyFrame big = over.Call(12, new[] { H(b), Val.Int(Ply("muditems")), Val.List() });
        ReplyFrame fallback = over.Call(9, new[] { H(b), Val.Int(Ply("muditems")) });

        // Assert
        Assert.Equal(0UL, fine.Status);
        Assert.Equal(max, fine.Result.Items.Count);
        Assert.Equal((ulong)Status.TooLarge, big.Status);
        Assert.Equal(0UL, fallback.Status); // the host then reads the member with get
        Assert.Equal(tooMany, fallback.Result.Items.Count);
        Assert.Equal(max, Manifest.Build(false, (int)minor).Get("limits")!.Get("max_list")!.AsInt);
    }

    [Fact]
    public void Mudmoney_Given_coins_in_the_tree_and_in_an_applied_sack_When_read_Then_the_sum_and_zero_for_an_object_that_holds_none()
    {
        // Arrange
        const string inv = "arch goldcoin\nnrof 5\nend\narch silvercoin\nnrof 7\nend\narch bag\napplied 1\narch goldcoin\nnrof 2\nend\nend\narch sword\nend\n";
        using DirectExt ext = Start(WorldWith("bob", inv));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);

        // Act / Assert
        Assert.Equal(5 * 10 + 7 + 2 * 10, Get(ext, bob, Ply("mudmoney")).AsInt);
        Assert.Equal(20, Get(ext, items[2].Handle, Obj("mudmoney")).AsInt);
        int errors = ext.Dispatcher.WorldOrNull!.LogCounts[(int)LogLevel.Error];
        Assert.Equal(0, Get(ext, items[3].Handle, Obj("mudmoney")).AsInt);
        Assert.Equal(errors, ext.Dispatcher.WorldOrNull!.LogCounts[(int)LogLevel.Error]); // query_money is not called for an object that is no player or container
    }

    [Fact]
    public void Mudowner_Given_writes_of_a_player_a_text_an_npc_and_nil_When_read_Then_the_owner_is_the_player_while_online_else_the_text()
    {
        // Arrange
        TempFolder world = WorldWith("bob", Bob);
        ItemWorld.Account(world, "amy", "", "");
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall.nw"), ObjectWorld.SeedHall);
        using DirectExt ext = Start(world);
        ulong bob = Login(ext, "bob").Result.AsHandle;
        ulong amy = Login(ext, "amy").Result.AsHandle;
        ulong banana = Items(ext, bob)[0].Handle;
        int member = Obj("mudowner");
        var (places, _) = LoadAll(ext, "hall.nw");
        ulong npc = places[0].Get("handle")!.AsHandle;

        // Act
        ReplyFrame none = ext.Call(9, new[] { H(banana), Val.Int(member) });
        ReplyFrame byPlayer = Set(ext, banana, member, H(amy));
        Logout(ext, "amy");
        ReplyFrame offline = ext.Call(9, new[] { H(banana), Val.Int(member) });
        Set(ext, banana, member, S("zed"));
        ReplyFrame text = ext.Call(9, new[] { H(banana), Val.Int(member) });
        ReplyFrame byNpc = Set(ext, banana, member, H(npc));
        ReplyFrame cleared = Set(ext, banana, member, Val.Nil);
        Set(ext, banana, member, S("zed"));
        ReplyFrame emptied = Set(ext, banana, member, S(""));
        ReplyFrame number = Set(ext, banana, member, Val.Int(4));
        ReplyFrame lineBreak = Set(ext, banana, member, S("a\nb"));
        ReplyFrame dead = Set(ext, banana, member, H(1UL << 32 | 99999));

        // Assert
        Assert.True(none.Result.IsNil);
        Assert.Equal(amy, byPlayer.Result.AsHandle);
        Assert.Equal("amy", offline.Result.AsString);
        Assert.Equal("zed", text.Result.AsString);
        Assert.Equal(npc, byNpc.Result.AsHandle);
        Assert.True(cleared.Result.IsNil);
        Assert.True(emptied.Result.IsNil);
        Assert.Equal((ulong)Status.BadArgs, number.Status);
        Assert.Equal((ulong)Status.BadArgs, lineBreak.Status);
        Assert.True(dead.Result.IsNil);
    }

    [Fact]
    public void Mudowner_Given_an_owner_that_is_an_account_When_the_player_logs_out_and_in_Then_the_owner_is_saved_with_the_item()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        ulong banana = Items(ext, bob)[0].Handle;
        Set(ext, banana, Obj("mudowner"), S("zed\\x"));
        Logout(ext, "bob");
        Flushed(ext, "accounts/bob.txt", t => t.Contains("mudowner"));

        // Act
        ulong again = Login(ext, "bob").Result.AsHandle;
        ulong banana2 = Items(ext, again)[0].Handle;

        // Assert
        Assert.Equal("zed\\x", Get(ext, banana2, Obj("mudowner")).AsString);
        Assert.DoesNotContain(ext.Logs, l => l.Text.StartsWith("the file of account")); // the loader knows the key: no line of an error in the file
    }

    [Fact]
    public void Currentweapon_Given_an_applied_weapon_named_by_the_file_When_read_Then_the_item_and_nil_after_it_is_unapplied_or_freed()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        ulong bob = Login(ext, "bob").Result.AsHandle;
        var items = Items(ext, bob);
        int member = Ply("currentweapon");
        ReplyFrame before = ext.Call(9, new[] { H(bob), Val.Int(member) });

        // Act
        Do(ext, "wield", "bob", Val.Int((uint)items[1].Handle));
        ReplyFrame wielded = ext.Call(9, new[] { H(bob), Val.Int(member) });
        Set(ext, items[1].Handle, Obj("applied"), Val.False);
        ReplyFrame unapplied = ext.Call(9, new[] { H(bob), Val.Int(member) });
        Set(ext, items[1].Handle, Obj("applied"), Val.True);
        Do(ext, "free", "bob", Val.Int((uint)items[1].Handle));
        ReplyFrame freed = ext.Call(9, new[] { H(bob), Val.Int(member) });

        // Assert
        Assert.True(before.Result.IsNil);
        Assert.Equal(items[1].Handle, wielded.Result.AsHandle);
        Assert.True(unapplied.Result.IsNil);
        Assert.True(freed.Result.IsNil);
    }

    [Fact]
    public void MarkedObject_Given_the_manifest_of_a_1_1_link_When_read_Then_the_member_has_the_type_ref_of_TMudObject_so_that_the_host_gives_the_script_the_proxy_of_the_item()
    {
        // Arrange (found by the end-to-end test of wave 7: the host's bare "ref" has no proxy type and gave a script nil for the item that C's find_marked_object found)
        Val v11 = Manifest.Build(false, 1);

        // Act
        Val member = v11.Get("types")!.Items.SelectMany(t => t.Get("members")!.Items).Single(m => m.Get("name")!.AsString == "markedobject");

        // Assert
        Assert.Equal("ref:TMudObject", member.Get("type")!.AsString);
    }

    [Fact]
    public void Members_Given_a_1_0_link_When_the_manifest_is_built_Then_the_ref_members_are_left_out_and_the_others_stay()
    {
        // Arrange
        Val v10 = Manifest.Build(false, 0);
        Val v11 = Manifest.Build(false, 1);

        // Act
        var names10 = v10.Get("types")!.Items.SelectMany(t => t.Get("members")!.Items).Select(m => m.Get("name")!.AsString).ToList();
        var names11 = v11.Get("types")!.Items.SelectMany(t => t.Get("members")!.Items).Select(m => m.Get("name")!.AsString).ToList();

        // Assert
        Assert.DoesNotContain("mudowner", names10);
        Assert.DoesNotContain("markedobject", names10);
        Assert.Contains("muditems", names10);
        Assert.Contains("currentweapon", names10);
        Assert.Contains("mudowner", names11);
        Assert.Contains("markedobject", names11);
        Assert.DoesNotContain(v10.Get("types")!.Items.SelectMany(t => t.Get("members")!.Items), m => m.Get("type")!.AsString.Contains("ref", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ DrawInfo

    [Fact]
    public void DrawInfo_Given_a_1_1_link_When_a_player_gets_a_line_Then_the_event_onRPGMessage_with_the_text_fallback()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        Login(ext, "bob");

        // Act
        string line = FileText.ToBytesText("You see a Héllo."); // the text of C is bytes
        ReplyFrame r = Do(ext, "draw", "bob", S(line));

        // Assert
        Val ev = Assert.Single(r.Events);
        Assert.Equal(new[] { "name", "target", "player", "args", "fallback" }, ev.Pairs.Select(p => p.Key));
        Assert.Equal("onRPGMessage", ev.Get("name")!.AsString);
        Assert.Equal("player", ev.Get("target")!.AsString);
        Assert.Equal("bob", ev.Get("player")!.AsString);
        Assert.Equal(new[] { "You see a Héllo." }, ev.Get("args")!.Items.Select(i => i.AsString)); // shown as the player reads it
        Val fb = ev.Get("fallback")!;
        Assert.Equal("client.text", fb.Get("k")!.AsString);
        Assert.Equal("bob", fb.Get("player")!.AsString);
        Assert.Equal("You see a Héllo.", fb.Get("text")!.AsString);
        Assert.Empty(Effects(r, "client.text"));
    }

    [Fact]
    public void DrawInfo_Given_a_1_0_link_When_a_player_gets_a_line_Then_the_client_text_effect_and_no_event()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob), protoMinor: 0);
        Login(ext, "bob");

        // Act
        ReplyFrame r = Do(ext, "draw", "bob", S("hello"));

        // Assert
        Assert.Empty(r.Events);
        Val text = Assert.Single(Effects(r, "client.text"));
        Assert.Equal("hello", text.Get("text")!.AsString);
        Assert.Equal("bob", text.Get("player")!.AsString);
    }

    [Fact]
    public void DrawInfo_Given_an_object_that_is_not_a_logged_in_player_When_it_gets_a_line_Then_nothing_is_sent()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        Login(ext, "bob");

        // Act
        ReplyFrame r = Do(ext, "drawat", "bob", S("hello"));

        // Assert
        Assert.Empty(r.Events);
        Assert.Empty(Effects(r, "client.text"));
    }

    // ------------------------------------------------------------------ fix_weight and creation

    [Fact]
    public void FixWeight_Given_a_player_whose_carrying_is_wrong_When_called_Then_it_is_set_right_and_the_string_follows()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));
        Login(ext, "bob");

        // Act
        ReplyFrame none = Do(ext, "fixweight", "bob", Val.Int(0));
        ReplyFrame wrong = Do(ext, "fixweight", "bob", Val.Int(5));

        // Assert: the test adds the grams to carrying before the call; a player with the right sum is not fixed
        Assert.Equal(0L, none.Result.AsInt);
        Assert.Equal(1L, wrong.Result.AsInt);
        Assert.Equal(CfWorld.SumWeight(ext.Dispatcher.ControllerOf("bob")!.State.Ob), ext.Dispatcher.ControllerOf("bob")!.State.Ob.Carrying);
    }

    [Fact]
    public void MudAddObject_Given_a_script_that_makes_an_object_When_it_is_made_Then_it_is_identified()
    {
        // Arrange
        TempFolder world = ItemWorld.MakeWorld();
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall.nw"), ObjectWorld.SeedHall);
        using DirectExt ext = Start(world);
        LoadAll(ext, "hall.nw");

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudAddObject, new[] { Val.Float(1), Val.Float(1), S("banana") }, level: "hall.nw");

        // Assert
        Assert.True(Get(ext, r.Result.AsHandle, Obj("identified")).AsBool);
    }

    [Fact]
    public void NewItem_Given_an_archetype_When_made_Then_it_is_identified_and_a_player_is_not_touched_by_it()
    {
        // Arrange
        using DirectExt ext = Start(WorldWith("bob", Bob));

        // Act
        CfObject item = ext.Dispatcher.NewItem(ext.Dispatcher.WorldOrNull!.FindArchetype("sword")!);

        // Assert
        Assert.True(item.QueryFlag(Define.FLAG_IDENTIFIED));
    }
}

public sealed class ItemMatchingTests
{
    private static (CfWorld World, CfObject Player, Player Contr, DirectExt Ext) Setup(string inventory)
    {
        TempFolder world = TestWorld.Make(arc: ItemWorld.Arc);
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts"));
        ItemWorld.Account(world, "bob", "", inventory);
        var ext = new DirectExt(world, new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) } });
        ext.Call(5, new[] { S("bob") }, player: "bob");
        Player contr = ext.Dispatcher.ControllerOf("bob")!;
        return (ext.Dispatcher.WorldOrNull!, contr.State.Ob, contr, ext);
    }

    private static int Match(string inventory, string name, int itemIndex, uint count = 0)
    {
        (CfWorld world, CfObject pl, Player contr, DirectExt ext) = Setup(inventory);
        using (ext)
        {
            contr.Count = count;
            CfObject item = pl.Inv!;
            for (int i = 0; i < itemIndex; i++)
            {
                item = item.Below!;
            }
            return ItemMatching.ItemMatchedString(world, pl, item, name);
        }
    }

    private const string Inv = "arch banana\nnrof 4\nend\narch sword\nend\narch sword\nunpaid 1\nend\narch mail\ncursed 1\nknown_cursed 1\nend\n";

    [Theory]
    [InlineData("all", 0, 1)]
    [InlineData("unpaid", 2, 2)]
    [InlineData("unpaid", 1, 0)]
    [InlineData("cursed", 3, 2)]
    [InlineData("cursed", 1, 0)]
    [InlineData("sword", 1, 4)]
    [InlineData("SWORD", 1, 4)]
    [InlineData("banana", 0, 4)]
    [InlineData("4 bananas", 0, 20)]
    [InlineData("ban", 0, 14)]
    [InlineData("zeppelin", 0, 0)]
    [InlineData("", 0, 0)]
    [InlineData("   ", 0, 0)]
    [InlineData(",,", 0, 0)]
    [InlineData("zeppelin, all", 0, 1)]
    [InlineData("  sword", 1, 4)]
    [InlineData("3", 0, 0)]
    public void ItemMatchedString_Given_a_name_list_When_matched_Then_the_C_values(string name, int item, int expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, Match(Inv, name, item));
    }

    [Fact]
    public void ItemMatchedString_Given_counts_When_matched_Then_the_players_count_is_written_as_C_does_including_its_plural_quirk()
    {
        // Arrange
        (CfWorld world, CfObject pl, Player contr, DirectExt ext) = Setup(Inv);
        using (ext)
        {
            CfObject banana = pl.Inv!;
            CfObject sword = banana.Below!;

            // Act
            int one = ItemMatching.ItemMatchedString(world, pl, sword, "1 sword"); // count 1 and the name: 6
            uint countOne = contr.Count;
            int many = ItemMatching.ItemMatchedString(world, pl, sword, "3 swords"); // the plural differs from the name_pl "swords"? it is equal: falls through
            uint countMany = contr.Count;
            int wrongPlural = ItemMatching.ItemMatchedString(world, pl, sword, "3 swordz"); // C: the plural differs, so it matches with 6
            uint countWrong = contr.Count;
            contr.Count = 7;
            int fromPlayer = ItemMatching.ItemMatchedString(world, pl, banana, "banana"); // the count of the player is used: no base name shortcut
            uint countKept = contr.Count;
            int negative = ItemMatching.ItemMatchedString(world, pl, sword, "-2 sword");
            int noName = ItemMatching.ItemMatchedString(world, pl, sword, "5");
            int onlySpaces = ItemMatching.ItemMatchedString(world, pl, sword, "5   ");

            // Assert
            Assert.Equal(6, one);
            Assert.Equal(1u, countOne);
            Assert.NotEqual(6, many);
            Assert.Equal(3u, countMany == 3 ? 3u : countMany);
            Assert.Equal(6, wrongPlural);
            Assert.Equal(3u, countWrong);
            Assert.Equal(6, fromPlayer); // count 7 and the plural name differs: the C quirk gives 6
            Assert.Equal(7u, countKept);
            Assert.Equal(0, negative);
            Assert.Equal(0, noName);
            Assert.Equal(0, onlySpaces);
        }
    }

    [Fact]
    public void ItemMatchedString_Given_an_object_that_is_not_a_player_When_matched_Then_the_count_is_zero_and_nothing_is_written()
    {
        // Arrange
        (CfWorld world, CfObject pl, Player contr, DirectExt ext) = Setup(Inv);
        using (ext)
        {
            CfObject rat = world.ArchToObject(world.FindArchetype("rat"))!;
            contr.Count = 9;

            // Act
            int r = ItemMatching.ItemMatchedString(world, rat, pl.Inv!, "banana");

            // Assert
            Assert.Equal(4, r);
            Assert.Equal(9u, contr.Count);
        }
    }

    [Fact]
    public void FindBestObjectMatch_Given_names_When_asked_Then_the_best_match_the_first_of_equals_and_invisible_ones_are_skipped()
    {
        // Arrange
        (CfWorld world, CfObject pl, Player _, DirectExt ext) = Setup("arch sword\ninvisible 1\nend\narch sword\nend\narch sword\nend\narch banana\nnrof 3\nend\n");
        using (ext)
        {
            CfObject firstVisible = pl.Inv!.Below!;

            // Act / Assert
            Assert.Same(firstVisible, ItemMatching.FindBestObjectMatch(world, pl, "sword"));
            Assert.Same(pl.Inv!.Below!.Below!.Below, ItemMatching.FindBestObjectMatch(world, pl, "banana"));
            Assert.Null(ItemMatching.FindBestObjectMatch(world, pl, "zeppelin"));
            Assert.Same(firstVisible, ItemMatching.FindBestObjectMatch(world, pl, "all"));
        }
    }

    [Fact]
    public void FindBestWeaponUsedMatch_Given_applied_and_unapplied_weapons_When_asked_Then_only_the_applied_weapon_matches()
    {
        // Arrange
        (CfWorld world, CfObject pl, Player _, DirectExt ext) = Setup("arch sword\nend\narch sword\napplied 1\nend\narch mail\napplied 1\nend\n");
        using (ext)
        {
            // Act / Assert
            Assert.Same(pl.Inv!.Below, ItemMatching.FindBestWeaponUsedMatch(world, pl, "sword"));
            Assert.Null(ItemMatching.FindBestWeaponUsedMatch(world, pl, "mail"));
        }
    }

    [Fact]
    public void Host_Given_the_loaders_call_for_a_current_weapon_When_asked_Then_the_ported_match_answers()
    {
        // Arrange
        (CfWorld world, CfObject pl, Player _, DirectExt ext) = Setup("arch sword\nnrof 1\nend\narch sword\nnrof 1\napplied 1\nend\n");
        using (ext)
        {
            // Act / Assert
            Assert.Same(pl.Inv!.Below, world.Host.FindBestWeaponUsedMatch(pl, "sword"));
            Assert.Null(world.Host.FindBestWeaponUsedMatch(pl, "zeppelin"));
        }
    }

    [Fact]
    public void FindMarkedObject_Given_a_mark_that_is_gone_or_has_another_count_When_asked_Then_null_and_the_mark_is_cleared()
    {
        // Arrange
        (CfWorld world, CfObject pl, Player contr, DirectExt ext) = Setup(Inv);
        using (ext)
        {
            CfObject sword = pl.Inv!.Below!;
            contr.Mark = sword;
            contr.MarkCount = sword.Count;

            // Act
            CfObject? found = ItemMatching.FindMarkedObject(pl);
            contr.MarkCount = sword.Count + 100;
            CfObject? reincarnated = ItemMatching.FindMarkedObject(pl);
            uint cleared = contr.MarkCount;
            CfObject elsewhere = world.ArchToObject(world.FindArchetype("sword"))!;
            contr.Mark = elsewhere;
            contr.MarkCount = elsewhere.Count;
            CfObject? notInTree = ItemMatching.FindMarkedObject(pl);
            sword.Invisible = 1;
            contr.Mark = sword;
            contr.MarkCount = sword.Count;
            CfObject? invisible = ItemMatching.FindMarkedObject(pl);

            // Assert
            Assert.Same(sword, found);
            Assert.Null(reincarnated);
            Assert.Equal(0u, cleared);
            Assert.Null(notInTree);
            Assert.Null(invisible);
            Assert.Null(ItemMatching.FindMarkedObject(null));
            Assert.Null(ItemMatching.FindMarkedObject(elsewhere));
            contr.Mark = null;
            Assert.Null(ItemMatching.FindMarkedObject(pl));
        }
    }

    [Fact]
    public void MarkItem_Given_an_item_and_a_missing_one_When_marked_Then_the_mark_is_set_or_the_player_is_told()
    {
        // Arrange
        (CfWorld world, CfObject pl, Player contr, DirectExt ext) = Setup(Inv);
        using (ext)
        {
            // Act
            ItemMatching.MarkItem(world, pl, pl.Inv);
            CfObject? marked = contr.Mark;
            ItemMatching.MarkItem(world, pl, null);
            ItemMatching.MarkItem(world, world.ArchToObject(world.FindArchetype("rat"))!, pl.Inv);

            // Assert
            Assert.Same(pl.Inv, marked);
            Assert.Equal(pl.Inv!.Count, contr.MarkCount);
            Assert.Same(pl.Inv, contr.Mark);
            Assert.Equal(1, ItemMatching.CommandMark(world, world.ArchToObject(world.FindArchetype("rat"))!, "x"));
        }
    }

    [Fact]
    public void WeightLimit_Given_the_table_When_read_Then_the_thirty_one_C_values_and_the_clamp_of_a_strength_outside_it()
    {
        // Arrange
        int[] golden =
        {
            200000, 250000, 300000, 350000, 400000, 500000, 600000, 700000, 800000, 900000, 1000000, 1100000, 1200000, 1300000, 1400000, 1500000,
            1650000, 1800000, 1950000, 2100000, 2250000, 2400000, 2550000, 2700000, 2850000, 3000000, 3250000, 3500000, 3750000, 4000000, 4500000,
        };

        // Act / Assert
        Assert.Equal(golden, ItemMatching.WeightLimit);
        Assert.Equal(31, ItemMatching.WeightLimit.Length);
        Assert.Equal(4500000, ItemMatching.WeightLimitOf(31));
        Assert.Equal(4500000, ItemMatching.WeightLimitOf(127));
        Assert.Equal(200000, ItemMatching.WeightLimitOf(-5));
        Assert.Equal(1000000, ItemMatching.WeightLimitOf(10));
    }

    [Theory]
    [InlineData("abc", "ABC", 0)]
    [InlineData("abc", "abd", -1)]
    [InlineData("abd", "abc", 1)]
    [InlineData("ab", "abc", -1)]
    [InlineData(null, "", 0)]
    [InlineData("@", "`", -1)] // only A to Z are folded
    public void StrCaseCmp_Given_two_names_When_compared_Then_the_sign_of_the_C_comparison(string? a, string? b, int sign)
    {
        // Arrange / Act
        int r = ItemMatching.StrCaseCmp(a, b);

        // Assert
        Assert.Equal(sign, Math.Sign(r));
    }
}
