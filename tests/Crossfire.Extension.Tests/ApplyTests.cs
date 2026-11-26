// Not ported from a Crossfire file: tests of lane 6B of phase 3 (wave 6), the apply core and wearing: the events of an apply and their
// order (onApplyItem, onApply with the fallback that invokes defaultApply, the hooks after a change of state), the cases of the switch of
// manual_apply that this lane owns, apply_special (wield, wear, ready, take off, the unapply of the rival and of a dropped object),
// check_weapon_power, command_apply and the class an item's type joins (docs/mud-phase3.md section 4.4).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>An invented world for the apply tests: a player who can wield and wear, the skills he needs, the items of every case, and a hall.</summary>
internal static class ApplyWorld
{
    public const string Arc =
        PickupWorld.Arc
        + "Object experience_agility\nname agility\ntype 44\ninvisible 1\nlevel 1\nexp 0\nDex 1\nend\n"
        + "Object experience_personality\nname personality\ntype 44\ninvisible 1\nlevel 1\nexp 0\nCha 1\nend\n"
        + "Object experience_mental\nname mental\ntype 44\ninvisible 1\nlevel 1\nexp 0\nInt 1\nend\n"
        + "Object experience_physique\nname physique\ntype 44\ninvisible 1\nlevel 1\nexp 0\nStr 1\nCon 1\nend\n"
        + "Object experience_magic\nname magic\ntype 44\ninvisible 1\nlevel 1\nexp 0\nPow 1\nend\n"
        + "Object experience_wis\nname wisdom\ntype 44\ninvisible 1\nlevel 1\nexp 0\nWis 1\nend\n"
        + "Object skill_melee_weapon\nname melee weapons\ntype 43\ninvisible 1\nno_drop 1\nsp 23\nend\n"
        + "Object skill_use_magic\nname use magic item\ntype 43\ninvisible 1\nno_drop 1\nsp 29\nend\n"
        + "Object skill_missile\nname missile weapons\ntype 43\ninvisible 1\nno_drop 1\nsp 24\nend\n"
        + "Object plate\nname plate\nface plate.1\ntype 16\nweight 9000\nac 5\nend\n"
        + "Object heavysword\nname heavysword\nface sword.2\ntype 15\nweight 3000\nlast_eat 6\nend\n"
        + "Object veryheavysword\nname veryheavysword\nface sword.3\ntype 15\nweight 3000\nlast_eat 8\nend\n"
        + "Object ragnarok\nname ragnarok\nface sword.4\ntype 15\nweight 3000\nlevel 3\nend\n"
        + "Object wand\nname wand\nface wand.1\ntype 109\nweight 100\nend\n"
        + "Object rod\nname rod\nface rod.1\ntype 3\nweight 100\nend\n"
        + "Object horn\nname horn\nface horn.1\ntype 35\nweight 100\nend\n"
        + "Object scroll\nname scroll\nname_pl scrolls\nface scroll.1\ntype 111\nweight 10\nend\n"
        + "Object spellbook\nname spellbook\nface book.1\ntype 85\nweight 10\nend\n"
        + "Object skillscroll\nname skillscroll\nface scroll.2\ntype 130\nweight 10\nend\n"
        + "Object clock\nname clock\nface clock.1\ntype 9\nweight 10\nend\n"
        + "Object amulet\nname amulet\nface amulet.1\ntype 39\nweight 10\nend\n"
        + "Object tool2\nname tool2\nface tool.2\ntype 163\nweight 500\nend\n"
        + "Object mask\nname mask\nface mask.1\ntype 164\nweight 100\nend\n"
        + "Object body\nname body\nface body.1\ntype 166\nweight 100\nend\n"
        + "Object door161\nname door161\nface door.2\ntype 161\nweight 100\nend\n"
        + "Object lever\nname lever\nface lever.1\ntype 27\nweight 100\nend\n"
        + "Object chest\nname treasure chest\nface chest.2\ntype 4\nweight 100\nend\n"
        + "Object gem2\nname gem2\nface gem.2\ntype 60\nweight 5\nend\n"
        + "Object legsb\nname legsb\nface legs.2\ntype 165\nweight 900\nend\n"
        + "Object ring_a\nname ring_a\nface ring.2\ntype 70\nweight 10\nend\n"
        + "Object ring_b\nname ring_b\nface ring.3\ntype 70\nweight 10\nend\n"
        + "Object ring_c\nname ring_c\nface ring.4\ntype 70\nweight 10\nend\n";

    public const string Header = "can_use_weapon 1\ncan_use_armour 1\nmudorigstats 14 12 10 9 7 8 11\nmudlevhp 8 8 8 8 8 8 8 8 8 8\nmudlevsp 3 3 3 3 3 3 3 3 3 3\nmudlevgrace 2 2 2 2 2 2 2 2 2 2\nlevel 1\n";

    /// <summary>The skills of a player that can use what he is given.</summary>
    public const string Skills = "arch skill_melee_weapon\nend\narch skill_use_magic\nend\narch skill_missile\nend\n";

    /// <summary>Classes the world has: the types' classes and the player's.</summary>
    public static readonly string[] TypeClasses =
    {
        "mudplayers", "mudtype_general", "mudtype_weapon", "mudtype_armour", "mudtype_cloth", "mudtype_tool", "mudtype_fireweapon",
    };

    public const int OpDo = 9900;

    /// <summary>
    /// <c>t6bdo(action, account, a, b)</c>: calls the ported C of the apply code itself, as the commands of wave 7 will: "command" is
    /// <c>command_apply(a)</c>, "below" is <c>player_apply_below</c>, "power" is <c>check_weapon_power</c> with the level of the player's wc experience object set,
    /// "head" applies the second part of a two-part object, "monster" is a monster's <c>manual_apply</c> of an unpaid or a treasure
    /// object, "fly" sets <c>FLAG_FLYING</c> and applies an object that lies on no level, "lastused" gives the player's last used object.
    /// </summary>
    public static void Register(ManifestBuilder b)
    {
        b.AddFunction(Lane.Wave6B, new FunctionDef(OpDo, "t6bdo", new[] { "string", "string", "any", "any" }, "any", Handler: Do));
    }

    private static Outcome Do(Dispatcher d, IReadOnlyList<Val> a)
    {
        CfWorld w = d.WorldOrNull!;
        Player p = d.ControllerOf(a[1].AsString)!;
        CfObject pl = p.State.Ob;
        switch (a[0].AsString)
        {
            case "command":
                return Outcome.Ok(Val.Int(d.Applies.CommandApply(pl, a[2].IsNil ? null : a[2].AsString)));
            case "below":
            {
                pl.Container = d.FindItemById(p.State, a[2].AsInt, deep: true);
                d.Applies.PlayerApplyBelow(pl);
                pl.Container = null;
                return Outcome.Ok(Val.Nil);
            }
            case "power": // check_weapon_power with the level of the wc experience object set to a (made when the player has none), for b improvements
            {
                CfObject exp = pl.Inv!;
                while (exp != null && (exp.Type != Define.EXPERIENCE || exp.Stats.Str == 0))
                {
                    exp = exp.Below!;
                }
                if (exp == null)
                {
                    exp = w.InsertObInOb(d.NewItem(w.FindArchetype("experience_physique")!), pl);
                }
                exp.Level = (short)a[2].AsInt;
                return Outcome.Ok(Val.Bool(d.Applies.CheckWeaponPower(pl, (int)a[3].AsInt)));
            }
            case "powernoexp": // the same for a player who has no wc experience object: his own level counts (the login gave him the objects: they are taken away)
            {
                for (CfObject? c = pl.Inv, next; c != null; c = next)
                {
                    next = c.Below;
                    if (c.Type == Define.EXPERIENCE)
                    {
                        w.RemoveOb(c);
                    }
                }
                pl.Level = (short)a[2].AsInt;
                return Outcome.Ok(Val.Bool(d.Applies.CheckWeaponPower(pl, (int)a[3].AsInt)));
            }
            case "monsterpower": // check_weapon_power for a monster of level a and b improvements
            {
                CfObject rat = w.ArchToObject(w.FindArchetype("rat")!)!;
                rat.Level = (short)a[2].AsInt;
                return Outcome.Ok(Val.Bool(d.Applies.CheckWeaponPower(rat, (int)a[3].AsInt)));
            }
            case "head":
            {
                CfObject whole = d.NewItem(w.FindArchetype("sword")!);
                CfObject part = d.NewItem(w.FindArchetype("sword")!);
                w.InsertObInOb(whole, pl);
                part.Head = whole;
                return Outcome.Ok(Val.Int(d.Applies.ManualApply(pl, part, 0)) );
            }
            case "monster":
            {
                CfObject rat = w.ArchToObject(w.FindArchetype("rat")!)!;
                CfObject item = d.NewItem(w.FindArchetype(a[2].AsString)!);
                if (a[3].AsBool)
                {
                    item.SetFlag(Define.FLAG_UNPAID);
                }
                return Outcome.Ok(Val.Int(d.Applies.ManualApply(rat, item, 0)));
            }
            case "fly":
            {
                pl.SetFlag(Define.FLAG_FLYING);
                CfObject item = d.NewItem(w.FindArchetype("sword")!);
                int answer = d.Applies.PlayerApply(pl, item, 0, quiet: false);
                pl.ClearFlag(Define.FLAG_FLYING);
                return Outcome.Ok(Val.Int(answer));
            }
            case "flyinv": // a flying player applies an object of his inventory (by id)
            {
                CfObject item = d.FindItemById(p.State, a[2].AsInt, deep: true)!;
                pl.SetFlag(Define.FLAG_FLYING);
                int answer = d.Applies.PlayerApply(pl, item, 0, quiet: false);
                pl.ClearFlag(Define.FLAG_FLYING);
                return Outcome.Ok(Val.Int(answer));
            }
            case "lastused":
            {
                CfObject item = d.FindItemById(p.State, a[2].AsInt, deep: true)!;
                d.Applies.PlayerApply(pl, item, 0, quiet: true);
                return Outcome.Ok(Val.Int(p.LastUsed == item && p.LastUsedId == item.Count ? 1 : 0));
            }
            default:
                return Outcome.Fail(Status.BadArgs, "unknown action");
        }
    }

    public static TempFolder MakeWorld(string account = "bob", string inventory = Skills, string header = Header, string[]? classes = null, string[]? accounts = null)
    {
        TempFolder world = new();
        string dir = Path.Combine(world.Path, "Arch", "Test");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "test.arc"), Arc);
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts"));
        foreach (string c in ObjectWorld.Classes.Concat(classes ?? TypeClasses))
        {
            File.WriteAllText(Path.Combine(world.Path, "scripts", c + ".txt"), "// class\n");
        }
        Directory.CreateDirectory(Path.Combine(world.Path, "mudlevels"));
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall"), PickupWorld.HallSeed);
        ItemWorld.Account(world, account, header, inventory);
        foreach (string other in accounts ?? Array.Empty<string>())
        {
            ItemWorld.Account(world, other, header, Skills + "arch sword\nend\n");
        }
        return world;
    }

    public static ExtensionOptions Options() => new()
    {
        Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = Register,
    };
}

public sealed partial class ApplyTests
{
    private sealed class Game : IDisposable
    {
        public Game(string items = "", long protoMinor = 1, string header = ApplyWorld.Header, string skills = ApplyWorld.Skills, string[]? others = null)
        {
            Ext = new DirectExt(ApplyWorld.MakeWorld("bob", skills + items, header, accounts: others), ApplyWorld.Options(), 1, true, 16_777_216, protoMinor);
            Player = Login("bob");
        }

        public DirectExt Ext { get; }

        public ulong Player { get; }

        public ulong Login(string account) => Ext.Call(5, new[] { S(account) }, player: account).Result.AsHandle;

        public List<ulong> Hall() => LoadAll(Ext, "hall").Places.Select(p => p.Get("handle")!.AsHandle).ToList();

        /// <summary>The items of the player as rows of (handle, name).</summary>
        public List<(ulong Handle, string Name)> Items(ulong? holder = null)
        {
            ReplyFrame r = Ext.Call(12, new[] { H(holder ?? Player), Val.Int(Ply("muditems")), Val.List(Val.Int(Obj("mudrealname"))) });
            Assert.Equal(0UL, r.Status);
            return r.Result.Items.Select(row => (row.Items[0].AsHandle, row.Items[1].AsString)).ToList();
        }

        public ulong Item(string name, ulong? holder = null) => Items(holder).First(i => i.Name == name).Handle;

        public long Id(ulong item) => Read(item, "mudid").AsInt;

        public Val Read(ulong handle, string member) => Get(Ext, handle, Obj(member));

        public bool Applied(ulong item) => Read(item, "applied").AsBool;

        public ReplyFrame ApplyId(ulong item, string account = "bob") =>
            Ext.Call(Manifest.OpMudApplyInvObject, new[] { Val.Int(Id(item)) }, level: "hall", player: account);

        /// <summary>What the host does when no class handles <c>onApply</c>: the fallback's invoke.</summary>
        public ReplyFrame DefaultApply(ulong item, string account = "bob", ulong? who = null) =>
            Ext.Call(13, new[] { H(item), Val.Int(TMudObjectMembers.MethodDefaultApply), Val.List(H(who ?? Player)) }, level: "hall", player: account);

        /// <summary>The apply of an item as the host runs it: the function, then the fallback of the onApply event it sent.</summary>
        public (ReplyFrame First, ReplyFrame Second) Apply(ulong item)
        {
            ReplyFrame first = ApplyId(item);
            Assert.Equal(0UL, first.Status);
            Val fallback = first.Events.Single(e => e.Get("name")!.AsString == "onApply").Get("fallback")!;
            Assert.Equal("defaultApply", fallback.Get("invoke")!.AsString);
            ReplyFrame second = DefaultApply(item);
            return (first, second);
        }

        public ReplyFrame Do(string action, Val? a = null, Val? b = null, string account = "bob") =>
            Ext.Call(ApplyWorld.OpDo, new[] { S(action), S(account), a ?? Val.Nil, b ?? Val.Nil }, level: "hall", player: account);

        public Val ReadPlayer(string member) => Get(Ext, Player, Ply(member));

        public ReplyFrame Command(string text) => Ext.Call(ApplyWorld.OpDo, new[] { S("command"), S("bob"), S(text), Val.Nil }, level: "hall", player: "bob");

        public void Dispose() => Ext.Dispose();
    }

    private static List<string> Lines(ReplyFrame r) =>
        r.Events.Where(e => e.Get("name")!.AsString == "onRPGMessage").Select(e => e.Get("args")!.Items[0].AsString)
            .Concat(Effects(r, "client.text").Select(e => e.Get("text")!.AsString)).ToList();

    private static List<string> EventNames(ReplyFrame r) => r.Events.Select(e => e.Get("name")!.AsString).Where(n => n != "onRPGMessage").ToList();

    private static Val EventOf(ReplyFrame r, string name) => r.Events.Single(e => e.Get("name")!.AsString == name);

    private static Dictionary<string, string> Sets(ReplyFrame r) => Effects(r, "client.set").ToDictionary(e => e.Get("name")!.AsString, e => e.Get("value")!.AsString);

    private static bool Result(ReplyFrame r)
    {
        Assert.Equal(0UL, r.Status);
        return r.Result.AsBool;
    }

    private const string Sword = "arch sword\nend\n";

    // ------------------------------------------------------------------ the manifest

    [Fact]
    public void Manifest_Given_a_1_1_link_When_built_Then_the_apply_events_have_their_targets_and_typed_parameters_and_the_function_is_slow()
    {
        // Arrange / Act
        Val manifest = Manifest.Build(false, 1);

        // Assert
        Val OfEvent(string name) => manifest.Get("events")!.Items.Single(e => e.Get("name")!.AsString == name);
        Val OfEventFor(string name, string target) => manifest.Get("events")!.Items.Single(e => e.Get("name")!.AsString == name && e.Get("target")!.AsString == target);
        Val onApply = OfEventFor("onApply", "item");
        Assert.Equal("item", onApply.Get("target")!.AsString);
        Assert.Equal("sync", onApply.Get("mode")!.AsString);
        Assert.Equal(new[] { "handle:TMudPlayer" }, onApply.Get("params")!.Items.Select(p => p.AsString));
        Val onApplyObject = OfEventFor("onApply", "object"); // the same name for an object a level shows: GK's container class checks restrictaccess for both
        Assert.Equal("sync", onApplyObject.Get("mode")!.AsString);
        Assert.Equal(new[] { "handle:TMudPlayer" }, onApplyObject.Get("params")!.Items.Select(p => p.AsString));
        Val onApplyItem = OfEvent("onApplyItem");
        Assert.Equal("player", onApplyItem.Get("target")!.AsString);
        Assert.Equal(new[] { "handle:TMudObject" }, onApplyItem.Get("params")!.Items.Select(p => p.AsString));
        foreach (string hook in new[] { "onWeaponWielded", "onWeaponUnWielded", "onWandReadied", "onWandUnReadied", "onArmorWorn", "onArmorUnWorn", "onClothWorn", "onClothUnWorn" })
        {
            Assert.Equal("player", OfEvent(hook).Get("target")!.AsString);
            Assert.Equal(new[] { "handle:TMudObject" }, OfEvent(hook).Get("params")!.Items.Select(p => p.AsString));
        }
        Val apply = manifest.Get("functions")!.Items.Single(f => f.Get("name")!.AsString == "mudapplyinvobject");
        Assert.True(apply.Get("slow")!.AsBool);
        Assert.Equal(Manifest.OpMudApplyInvObject, (int)apply.Get("id")!.AsInt);
    }

    [Fact]
    public void Manifest_Given_a_1_0_link_When_built_Then_no_apply_event_is_declared()
    {
        // Arrange / Act
        Val manifest = Manifest.Build(false, 0);

        // Assert
        Assert.DoesNotContain(manifest.Get("events")!.Items, e => e.Get("name")!.AsString.StartsWith("onApply", StringComparison.Ordinal));
        Assert.DoesNotContain(manifest.Get("events")!.Items, e => e.Get("name")!.AsString.StartsWith("onWeapon", StringComparison.Ordinal));
    }

    [Fact]
    public void Methods_Given_the_manifest_When_listed_Then_the_ids_and_signatures_of_the_lane_are_these()
    {
        // Arrange / Act
        Val manifest = Manifest.Build(false, 1);
        Val Methods(string type) => manifest.Get("types")!.Items.Single(t => t.Get("name")!.AsString == type).Get("methods")!;
        string Of(string type, string name)
        {
            Val m = Methods(type).Items.Single(x => x.Get("name")!.AsString == name);
            return m.Get("id")!.AsInt + " " + m.Get("ret")!.AsString + " (" + string.Join(",", m.Get("params")!.Items.Select(p => p.AsString)) + ")";
        }

        // Assert
        Assert.Equal("9000 bool ()", Of("TMudObject", "apply"));
        Assert.Equal("9001 bool ()", Of("TMudObject", "mudapplyobject"));
        Assert.Equal("9002 int (handle:TMudPlayer)", Of("TMudObject", "defaultApply"));
        Assert.Equal("9500 bool (int)", Of("TMudPlayer", "applyinvobject"));
        Assert.Equal("9501 handle:TMudObject ()", Of("TMudPlayer", "getwieldedobject"));
    }

    // ------------------------------------------------------------------ the class of an item's type

    [Theory]
    [InlineData(Define.WEAPON, "mudtype_weapon")]
    [InlineData(Define.ARMOUR, "mudtype_armour")]
    [InlineData(Define.RING, "mudtype_armour")]
    [InlineData(Define.AMULET, "mudtype_armour")]
    [InlineData(Define.BOW, "mudtype_fireweapon")]
    [InlineData(Define.FOOD, "mudtype_food")]
    [InlineData(Define.DRINK, "mudtype_food")]
    [InlineData(Define.FLESH, "mudtype_food")]
    [InlineData(161, "mudtype_housedoor")]
    [InlineData(162, "mudtype_tool")]
    [InlineData(163, "mudtype_tool")]
    [InlineData(164, "mudtype_cloth")]
    [InlineData(165, "mudtype_cloth")]
    [InlineData(166, "mudtype_cloth")]
    [InlineData(Define.WAND, "mudtype_general")] // no class of the name: the general one
    [InlineData(160, "mudtype_general")]
    [InlineData(Define.HELMET, "mudtype_general")]
    public void MudTypeClass_Given_a_type_When_asked_Then_the_class_of_its_name_or_the_general_one(int type, string expected)
    {
        // Arrange
        GkClasses classes = GkClasses.Of(new[] { "mudtype_weapon", "mudtype_armour", "mudtype_fireweapon", "mudtype_food", "mudtype_housedoor", "mudtype_tool", "mudtype_cloth", "mudtype_general" });

        // Act / Assert
        Assert.Equal(expected, classes.MudTypeClass(type));
    }

    [Fact]
    public void MudTypeClass_Given_a_world_without_the_class_of_a_type_When_asked_Then_the_general_class_and_without_that_one_too_its_name()
    {
        // Arrange
        GkClasses withoutWeapon = GkClasses.Of(new[] { "mudtype_general" });
        GkClasses none = GkClasses.Of(Array.Empty<string>());

        // Act / Assert
        Assert.Equal("mudtype_general", withoutWeapon.MudTypeClass(Define.WEAPON));
        Assert.Equal("mudtype_general", none.MudTypeClass(Define.WEAPON));
        Assert.Equal("MUDTYPE_WEAPON", GkClasses.Of(new[] { "mudtype_general", "MUDTYPE_WEAPON" }).MudTypeClass(Define.WEAPON)); // names compare without regard to case
    }

    // ------------------------------------------------------------------ the events and their order

    [Fact]
    public void MudApplyInvObject_Given_a_sword_on_a_1_1_link_When_applied_Then_onApplyItem_then_onApply_with_the_fallback_and_nothing_is_changed_yet()
    {
        // Arrange
        using var g = new Game(Sword);
        ulong sword = g.Item("sword");

        // Act
        ReplyFrame r = g.ApplyId(sword);

        // Assert
        Assert.Equal(new[] { "onApplyItem", "onApply" }, EventNames(r)); // the player first, then the item
        Val first = EventOf(r, "onApplyItem");
        Assert.Equal("player", first.Get("target")!.AsString);
        Assert.Equal("bob", first.Get("player")!.AsString);
        Assert.Equal(sword, Assert.Single(first.Get("args")!.Items).AsHandle);
        Assert.Null(first.Get("fallback"));
        Val second = EventOf(r, "onApply");
        Assert.Equal("item", second.Get("target")!.AsString);
        Assert.Equal(sword, second.Get("handle")!.AsHandle);
        Assert.Equal("TMudObject", second.Get("type")!.AsString);
        Assert.Equal(new[] { "mudtype_weapon" }, second.Get("classes")!.Items.Select(c => c.AsString));
        Assert.Equal(g.Player, Assert.Single(second.Get("args")!.Items).AsHandle); // the player is the first argument: the host's player context
        Val fallback = second.Get("fallback")!;
        Assert.Equal("defaultApply", fallback.Get("invoke")!.AsString);
        Assert.Equal(sword, fallback.Get("handle")!.AsHandle);
        Assert.Equal(g.Player, Assert.Single(fallback.Get("args")!.Items).AsHandle);
        Assert.False(g.Applied(sword)); // the script decides: the sword is not wielded yet
        Assert.Empty(Lines(r));
        Assert.True(Result(r));
    }

    [Fact]
    public void MudApplyInvObject_Given_a_1_0_link_When_a_sword_is_applied_Then_no_event_is_sent_and_it_is_wielded_at_once_with_the_text_as_effects()
    {
        // Arrange
        using var g = new Game(Sword, protoMinor: 0);
        ulong sword = g.Item("sword");

        // Act
        ReplyFrame r = g.ApplyId(sword);

        // Assert
        Assert.Empty(r.Events); // no item events and no hooks with typed arguments on a 1.0 link
        Assert.True(g.Applied(sword));
        Assert.Equal(new[] { "Readied skill: melee weapons.", "You wield swords (wielded)." }, Lines(r));
        Assert.Equal(new[] { "client.text", "client.text" }, Effects(r, "client.text").Select(e => e.Get("k")!.AsString));
        Assert.True(Result(r));
    }

    [Fact]
    public void DefaultApply_Given_the_fallback_of_a_sword_When_the_host_invokes_it_Then_the_sword_is_wielded_the_hook_follows_the_text_and_the_strings_follow_in_the_request()
    {
        // Arrange
        using var g = new Game(Sword);
        ulong sword = g.Item("sword");

        // Act
        (ReplyFrame first, ReplyFrame r) = g.Apply(sword);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(1L, r.Result.AsInt);
        Assert.True(g.Applied(sword));
        Assert.Equal(new[] { "Readied skill: melee weapons.", "You wield swords (wielded)." }, Lines(r));
        // the hook is sent after the state change, with the item as its argument, and after the lines of text
        Val hook = Assert.Single(r.Events, e => e.Get("name")!.AsString == "onWeaponWielded");
        Assert.Equal("player", hook.Get("target")!.AsString);
        Assert.Equal("bob", hook.Get("player")!.AsString);
        Assert.Equal(sword, Assert.Single(hook.Get("args")!.Items).AsHandle);
        Assert.Equal(new[] { "onRPGMessage", "onRPGMessage", "onWeaponWielded" }, r.Events.Select(e => e.Get("name")!.AsString));
        // wearing runs the chain of the living core: the weapon class and damage of the window follow in the same request
        Dictionary<string, string> sets = Sets(r);
        Assert.Equal(g.ReadPlayer("mudwc").AsInt.ToString(), sets["clientr.mud_wc"]);
        Assert.Equal(g.ReadPlayer("muddamage").AsInt.ToString(), sets["clientr.mud_dam"]);
        Assert.Contains("clientr.mud_item" + (uint)sword, sets.Keys); // the item's own string: its state changed
        Assert.Equal(sword, g.ReadPlayer("currentweapon").AsHandle);
        Assert.Equal(0UL, first.Status);
    }

    [Fact]
    public void GetWieldedObject_Given_a_player_When_he_wields_and_unwields_Then_the_item_then_nothing_and_the_hooks_name_the_item()
    {
        // Arrange
        using var g = new Game(Sword);
        ulong sword = g.Item("sword");
        ReplyFrame Call() => g.Ext.Call(13, new[] { H(g.Player), Val.Int(TMudPlayerMembers.MethodGetWieldedObject), Val.List() }, player: "bob");
        Assert.True(Call().Result.IsNil); // bare hands

        // Act
        g.Apply(sword);
        ReplyFrame wielded = Call();
        (_, ReplyFrame off) = g.Apply(sword);

        // Assert
        Assert.Equal(sword, wielded.Result.AsHandle);
        Assert.False(g.Applied(sword));
        Assert.Equal("You unwield swords.", Lines(off).Last());
        Assert.Equal(new[] { "onWeaponUnWielded" }, EventNames(off));
        Assert.Equal(sword, EventOf(off, "onWeaponUnWielded").Get("args")!.Items[0].AsHandle);
        Assert.True(Call().Result.IsNil);
    }

    [Fact]
    public void ApplyInvObject_Given_the_player_method_When_called_Then_it_is_the_same_apply_with_the_same_events()
    {
        // Arrange
        using var g = new Game(Sword);
        ulong sword = g.Item("sword");

        // Act
        ReplyFrame r = g.Ext.Call(13, new[] { H(g.Player), Val.Int(TMudPlayerMembers.MethodApplyInvObject), Val.List(Val.Int(g.Id(sword))) }, level: "hall", player: "bob");

        // Assert
        Assert.True(Result(r));
        Assert.Equal(new[] { "onApplyItem", "onApply" }, EventNames(r));
    }

    [Fact]
    public void Apply_Given_the_method_on_an_inventory_item_When_called_Then_the_same_apply_and_for_the_item_of_another_player_nothing()
    {
        // Arrange
        using var g = new Game(Sword, others: new[] { "alice" });
        ulong aliceHandle = g.Login("alice");
        ulong sword = g.Item("sword");
        ulong aliceSword = g.Item("sword", aliceHandle);
        ReplyFrame Method(ulong item, int id, string account) =>
            g.Ext.Call(13, new[] { H(item), Val.Int(id), Val.List() }, level: "hall", player: account);

        // Act
        ReplyFrame own = Method(sword, TMudObjectMembers.MethodApply, "bob");
        ReplyFrame own2 = Method(sword, TMudObjectMembers.MethodMudApplyObject, "bob");
        ReplyFrame foreign = Method(aliceSword, TMudObjectMembers.MethodApply, "bob");

        // Assert
        Assert.True(Result(own));
        Assert.Equal(new[] { "onApplyItem", "onApply" }, EventNames(own));
        Assert.True(Result(own2));
        Assert.False(Result(foreign)); // ids and handles are scoped to the actor's own tree
        Assert.Empty(foreign.Events);
        Assert.False(g.Applied(aliceSword));
    }

    [Fact]
    public void MudApplyInvObject_Given_an_id_of_another_players_item_or_none_When_applied_Then_false_and_nothing_changes()
    {
        // Arrange
        using var g = new Game(Sword, others: new[] { "alice" });
        ulong aliceHandle = g.Login("alice");
        ulong aliceSword = g.Item("sword", aliceHandle);

        // Act
        ReplyFrame foreign = g.Ext.Call(Manifest.OpMudApplyInvObject, new[] { Val.Int(g.Id(aliceSword)) }, level: "hall", player: "bob");
        ReplyFrame unknown = g.Ext.Call(Manifest.OpMudApplyInvObject, new[] { Val.Int(987654) }, level: "hall", player: "bob");
        ReplyFrame bad = g.Ext.Call(Manifest.OpMudApplyInvObject, new[] { S("x") }, level: "hall", player: "bob");
        ReplyFrame noPlayer = g.Ext.Call(Manifest.OpMudApplyInvObject, new[] { Val.Int(1) });

        // Assert
        Assert.False(Result(foreign));
        Assert.Empty(foreign.Events);
        Assert.False(Result(unknown));
        Assert.Equal((ulong)Status.BadArgs, bad.Status);
        Assert.Equal((ulong)Status.BadArgs, noPlayer.Status);
        Assert.False(g.Applied(aliceSword));
    }

    [Fact]
    public void Apply_Given_the_server_is_busy_When_an_item_is_applied_Then_busy_and_nothing_changes_in_any_entry_point()
    {
        // Arrange
        using var g = new Game(Sword + "arch heavysword\nend\n");
        ulong sword = g.Item("sword");
        g.Ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame function = g.ApplyId(sword);
        ReplyFrame viaMethod = g.Ext.Call(13, new[] { H(g.Player), Val.Int(TMudPlayerMembers.MethodApplyInvObject), Val.List(Val.Int(g.Id(sword))) }, player: "bob");
        ReplyFrame apply = g.Ext.Call(13, new[] { H(sword), Val.Int(TMudObjectMembers.MethodApply), Val.List() }, player: "bob");
        ReplyFrame mudApply = g.Ext.Call(13, new[] { H(sword), Val.Int(TMudObjectMembers.MethodMudApplyObject), Val.List() }, player: "bob");
        ReplyFrame defaultApply = g.DefaultApply(sword);
        g.Ext.Dispatcher.Busy = null;

        // Assert
        foreach (ReplyFrame r in new[] { function, viaMethod, apply, mudApply, defaultApply })
        {
            Assert.Equal((ulong)Status.Busy, r.Status);
            Assert.Empty(r.Events);
        }
        Assert.False(g.Applied(sword));
        Assert.True(Result(g.ApplyId(sword))); // the server is free again
    }

    // ------------------------------------------------------------------ apply_special: wield, wear, ready

    [Fact]
    public void ApplySpecial_Given_armour_over_armour_When_the_second_is_worn_Then_the_first_is_unapplied_first_with_its_hook_then_the_second_with_its_own()
    {
        // Arrange
        using var g = new Game("arch mail\nend\narch plate\nend\n");
        ulong mail = g.Item("mail");
        ulong plate = g.Item("plate");
        g.Apply(mail);
        Assert.True(g.Applied(mail));

        // Act
        (_, ReplyFrame r) = g.Apply(plate);

        // Assert
        Assert.False(g.Applied(mail));
        Assert.True(g.Applied(plate));
        Assert.Equal(new[] { "You unwear mail.", "You wear plate (worn)." }, Lines(r));
        Assert.Equal(new[] { "onArmorUnWorn", "onArmorWorn" }, EventNames(r));
        Assert.Equal(mail, EventOf(r, "onArmorUnWorn").Get("args")!.Items[0].AsHandle);
        Assert.Equal(plate, EventOf(r, "onArmorWorn").Get("args")!.Items[0].AsHandle);
        Assert.Equal(g.ReadPlayer("mudac").AsInt.ToString(), Sets(r)["clientr.mud_ac"]); // the armour class of the window follows
        Assert.True(g.ReadPlayer("mudac").AsInt < 10);
    }

    [Fact]
    public void ApplySpecial_Given_two_rings_on_When_a_third_is_put_on_Then_two_stay_on_and_the_third_is_one_of_them()
    {
        // Arrange
        using var g = new Game("arch ring_a\nend\narch ring_b\nend\narch ring_c\nend\n");
        ulong a = g.Item("ring_a");
        ulong b = g.Item("ring_b");
        ulong c = g.Item("ring_c");
        g.DefaultApply(a);
        g.DefaultApply(b);
        Assert.True(g.Applied(a) && g.Applied(b)); // two rings are allowed

        // Act
        ReplyFrame r = g.DefaultApply(c);

        // Assert
        Assert.True(g.Applied(c));
        Assert.Equal(2, new[] { a, b, c }.Count(g.Applied));
        Assert.Single(Lines(r), l => l.StartsWith("You unwear ring_", StringComparison.Ordinal));
        Assert.Contains("You wear ring_c (worn).", Lines(r));
        Assert.Equal(new[] { "onArmorUnWorn", "onArmorWorn" }, EventNames(r));
    }

    [Fact]
    public void ApplySpecial_Given_a_cursed_applied_sword_When_applied_again_Then_it_cannot_be_removed_and_nothing_changes()
    {
        // Arrange
        using var g = new Game("arch sword\ncursed 1\nend\n");
        ulong sword = g.Item("sword");
        g.DefaultApply(sword);
        Assert.True(g.Applied(sword));

        // Act
        ReplyFrame r = g.DefaultApply(sword);

        // Assert
        Assert.True(g.Applied(sword));
        Assert.Equal("No matter how hard you try, you just can't\nremove swords (cursed) (wielded).", string.Join("|", Lines(r)));
        Assert.Empty(EventNames(r)); // no hook: nothing changed
        Assert.Equal(1L, r.Result.AsInt);
    }

    [Fact]
    public void ApplySpecial_Given_a_cursed_sword_When_wielded_Then_oops_it_feels_deadly_cold_and_the_item_string_follows()
    {
        // Arrange
        using var g = new Game("arch sword\ndamned 1\nend\n");
        ulong sword = g.Item("sword");

        // Act
        ReplyFrame r = g.DefaultApply(sword);

        // Assert
        Assert.Equal(new[] { "Readied skill: melee weapons.", "You wield swords (wielded).", "Oops, it feels deadly cold!" }, Lines(r));
        Assert.Contains("clientr.mud_item" + (uint)sword, Sets(r).Keys);
    }

    [Fact]
    public void ApplySpecial_Given_a_player_who_cannot_use_weapons_or_armour_When_he_applies_them_Then_he_cannot_use_them_and_nothing_is_applied()
    {
        // Arrange
        using var g = new Game("arch sword\nend\narch mail\nend\narch legs\nend\n", header: "mudorigstats 14 12 10 9 7 8 11\nlevel 1\n");
        ulong sword = g.Item("sword");
        ulong mail = g.Item("mail");
        ulong legs = g.Item("legs");

        // Act
        ReplyFrame swordReply = g.DefaultApply(sword);
        ReplyFrame mailReply = g.DefaultApply(mail);
        ReplyFrame legsReply = g.DefaultApply(legs);

        // Assert
        Assert.Equal(new[] { "You can't use swords." }, Lines(swordReply));
        Assert.Equal(new[] { "You can't use mail." }, Lines(mailReply));
        Assert.False(g.Applied(sword));
        Assert.False(g.Applied(mail));
        Assert.Equal(new[] { "You wear legs (applied)." }, Lines(legsReply)); // clothes are not armour: anyone may wear them
        Assert.True(g.Applied(legs));
        Assert.Empty(EventNames(swordReply));
    }

    [Fact]
    public void ApplySpecial_Given_a_weapon_too_powerful_for_the_level_When_wielded_Then_it_would_consume_the_soul_and_the_stack_is_whole_again()
    {
        // Arrange
        using var g = new Game("arch heavysword\nnrof 3\nend\n");
        ulong heavy = g.Item("heavysword");

        // Act
        ReplyFrame r = g.DefaultApply(heavy);

        // Assert: level 1 may use 5 improvements, the sword has 6
        Assert.Equal(new[] { "That weapon is too powerful for you to use.", "It would consume your soul!." }, Lines(r));
        Assert.False(g.Applied(heavy));
        (ulong stack, _) = Assert.Single(g.Items(), i => i.Name == "heavysword"); // the part that was split off came back
        Assert.Equal(3L, g.Read(stack, "mudnrof").AsInt);
    }

    [Fact]
    public void ApplySpecial_Given_a_stack_of_swords_and_a_player_without_the_skill_When_wielded_Then_he_cannot_use_it_and_the_whole_stack_is_still_his()
    {
        // Arrange: no skill_melee_weapon in his inventory
        using var g = new Game("arch sword\nnrof 3\nend\n", skills: "");
        ulong stack = g.Item("sword");

        // Act
        ReplyFrame r = g.DefaultApply(stack);

        // Assert
        Assert.Contains("You can't use that!", Lines(r));
        Assert.DoesNotContain(g.Items(), i => g.Applied(i.Handle));
        Assert.Equal(3L, g.Items().Where(i => i.Name == "sword").Sum(i => g.Read(i.Handle, "mudnrof").AsInt)); // the part that was split off came back
        Assert.Empty(EventNames(r)); // nothing was wielded: no hook
        Assert.True(g.ReadPlayer("currentweapon").IsNil);
    }

    [Theory]
    [InlineData(1, 5, true)]
    [InlineData(1, 6, false)]
    [InlineData(4, 5, true)]
    [InlineData(5, 6, true)]
    [InlineData(14, 7, true)]
    [InlineData(14, 8, false)]
    [InlineData(15, 8, true)]
    public void CheckWeaponPower_Given_the_level_of_the_wc_experience_object_When_asked_Then_one_improvement_per_five_levels_over_five(int level, int improvs, bool allowed)
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.Do("power", Val.Int(level), Val.Int(improvs));

        // Assert
        Assert.Equal(allowed, Result(r));
    }

    [Theory]
    [InlineData(1, 5, true)]
    [InlineData(1, 6, false)]
    [InlineData(10, 7, true)]
    [InlineData(10, 8, false)]
    public void CheckWeaponPower_Given_a_player_without_a_wc_experience_object_When_asked_Then_his_own_level_is_used(int level, int improvs, bool allowed)
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.Do("powernoexp", Val.Int(level), Val.Int(improvs));

        // Assert
        Assert.Equal(allowed, Result(r));
    }

    [Theory]
    [InlineData(10, 7, true)]
    [InlineData(10, 8, false)]
    public void CheckWeaponPower_Given_a_monster_When_asked_Then_its_level_is_used(int level, int improvs, bool allowed)
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.Do("monsterpower", Val.Int(level), Val.Int(improvs));

        // Assert
        Assert.Equal(allowed, Result(r));
    }

    [Fact]
    public void ApplySpecial_Given_a_named_weapon_of_another_owner_When_wielded_Then_it_does_not_recognise_him()
    {
        // Arrange
        using var g = new Game("arch ragnarok\nend\n");
        ulong sword = g.Item("ragnarok");

        // Act
        ReplyFrame r = g.DefaultApply(sword);

        // Assert
        Assert.Equal(new[] { "The weapon does not recognize you as its owner." }, Lines(r));
        Assert.False(g.Applied(sword));
    }

    [Fact]
    public void ApplySpecial_Given_a_stack_of_swords_When_one_is_wielded_Then_one_is_split_off_and_the_rest_stays_and_the_hook_names_the_wielded_one()
    {
        // Arrange
        using var g = new Game("arch sword\nnrof 3\nend\n");
        ulong stack = g.Item("sword");

        // Act
        ReplyFrame on = g.DefaultApply(stack);

        // Assert
        List<(ulong Handle, string Name)> swords = g.Items().Where(i => i.Name == "sword").ToList();
        ulong wielded = swords.Single(i => g.Applied(i.Handle)).Handle;
        ulong rest = swords.Single(i => !g.Applied(i.Handle)).Handle;
        Assert.Equal(1L, g.Read(wielded, "mudnrof").AsInt);
        Assert.Equal(2L, g.Read(rest, "mudnrof").AsInt);
        Assert.Equal(wielded, EventOf(on, "onWeaponWielded").Get("args")!.Items[0].AsHandle);
        Assert.Equal(wielded, g.ReadPlayer("currentweapon").AsHandle);
        Assert.Contains("clientr.mud_item" + (uint)rest, Sets(on).Keys); // both slots of the window are written again
        Assert.Contains("clientr.mud_item" + (uint)wielded, Sets(on).Keys);
    }

    [Fact]
    public void ApplySpecial_Given_a_wielded_sword_with_an_identical_twin_When_taken_off_Then_it_merges_into_the_twin_and_the_hook_names_the_survivor()
    {
        // Arrange: both have been applied before (C does not merge an object that was applied with one that was not)
        using var g = new Game("arch sword\nnrof 1\nbeen_applied 1\napplied 1\nend\narch sword\nbeen_applied 1\nnrof 2\nend\n");
        List<(ulong Handle, string Name)> before = g.Items().Where(i => i.Name == "sword").ToList();
        ulong wielded = before.Single(i => g.Applied(i.Handle)).Handle;

        // Act
        ReplyFrame off = g.DefaultApply(wielded);

        // Assert
        (ulong survivor, _) = Assert.Single(g.Items(), i => i.Name == "sword");
        Assert.Equal(3L, g.Read(survivor, "mudnrof").AsInt);
        Assert.Equal(survivor, EventOf(off, "onWeaponUnWielded").Get("args")!.Items[0].AsHandle); // the object that is still there
        Assert.Equal(new[] { "You unwield sword." }, Lines(off));
    }

    [Fact]
    public void ApplySpecial_Given_a_sword_wielded_when_a_tool_is_wielded_Then_the_tool_takes_the_place_of_the_weapon()
    {
        // Arrange
        using var g = new Game("arch sword\nend\narch tool\nend\narch tool2\nend\n");
        ulong sword = g.Item("sword");
        ulong tool = g.Item("tool");
        ulong tool2 = g.Item("tool2");
        g.DefaultApply(sword);

        // Act
        ReplyFrame r = g.DefaultApply(tool);
        ReplyFrame r2 = g.DefaultApply(tool2);

        // Assert
        Assert.False(g.Applied(sword));
        Assert.False(g.Applied(tool));
        Assert.True(g.Applied(tool2));
        Assert.Equal(new[] { "You unwield swords.", "You wield tool (applied)." }, Lines(r));
        Assert.Equal(new[] { "onWeaponUnWielded", "onWeaponWielded" }, EventNames(r));
        Assert.Equal(new[] { "You unwield tool.", "You wield tool2 (applied)." }, Lines(r2));
        ReplyFrame wielded = g.Ext.Call(13, new[] { H(g.Player), Val.Int(TMudPlayerMembers.MethodGetWieldedObject), Val.List() }, player: "bob");
        Assert.Equal(tool2, wielded.Result.AsHandle); // getwieldedobject names the tool
    }

    [Theory]
    [InlineData("legs", "legs", "onClothWorn", "onClothUnWorn")]
    [InlineData("mask", "mask", "onClothWorn", "onClothUnWorn")]
    [InlineData("body", "body", "onClothWorn", "onClothUnWorn")]
    [InlineData("ring", "ring", "onArmorWorn", "onArmorUnWorn")]
    [InlineData("amulet", "amulet", "onArmorWorn", "onArmorUnWorn")]
    [InlineData("helmet", "helmet", "onArmorWorn", "onArmorUnWorn")]
    [InlineData("tool", "tool", "onWeaponWielded", "onWeaponUnWielded")]
    [InlineData("wand", "wand", "onWandReadied", "onWandUnReadied")]
    public void Hooks_Given_an_item_of_a_kind_When_it_is_put_on_and_taken_off_Then_the_hook_of_its_kind_follows_each_change(string arch, string name, string onHook, string offHook)
    {
        // Arrange
        using var g = new Game("arch " + arch + "\nend\n");
        ulong item = g.Item(name);

        // Act
        ReplyFrame on = g.DefaultApply(item);
        ReplyFrame off = g.DefaultApply(item);

        // Assert
        Assert.Equal(new[] { onHook }, EventNames(on));
        Assert.Equal(item, EventOf(on, onHook).Get("args")!.Items[0].AsHandle);
        Assert.Equal(new[] { offHook }, EventNames(off));
        Assert.Equal(item, EventOf(off, offHook).Get("args")!.Items[0].AsHandle);
    }

    [Fact]
    public void ApplySpecial_Given_clothes_of_one_type_When_a_second_is_worn_Then_the_first_comes_off_and_clothes_of_another_type_stay()
    {
        // Arrange
        using var g = new Game("arch legs\nend\narch legsb\nend\narch mask\nend\narch body\nend\n");
        List<ulong> legs = new() { g.Item("legs"), g.Item("legsb") };
        ulong mask = g.Item("mask");
        g.DefaultApply(legs[0]);
        g.DefaultApply(mask);

        // Act
        ReplyFrame r = g.DefaultApply(legs[1]);

        // Assert
        Assert.False(g.Applied(legs[0]));
        Assert.True(g.Applied(legs[1]));
        Assert.True(g.Applied(mask));
        Assert.Equal(new[] { "You unwear legs.", "You wear legsb (applied)." }, Lines(r));
    }

    [Fact]
    public void ApplySpecial_Given_a_wand_and_a_bow_When_applied_Then_they_are_readied_with_their_texts_and_the_bow_has_no_hook()
    {
        // Arrange
        using var g = new Game("arch wand\nend\narch bow\nrace arrows\nend\n");
        ulong wand = g.Item("wand");
        ulong bow = g.Item("bow");

        // Act
        ReplyFrame wandOn = g.DefaultApply(wand);
        ReplyFrame bowOn = g.DefaultApply(bow);
        ReplyFrame bowOff = g.DefaultApply(bow);

        // Assert
        Assert.Equal("You ready wand (readied).", Lines(wandOn).Last());
        Assert.Equal(new[] { "You ready bow (readied).", "You will now fire arrows with bow (readied)." }, Lines(bowOn).Where(l => !l.StartsWith("Readied")).ToArray());
        Assert.Empty(EventNames(bowOn)); // a bow has no hook in mudplayers
        Assert.Equal(new[] { "You unready bow." }, Lines(bowOff));
        Assert.True(g.Applied(wand));
        Assert.False(g.Applied(bow));
    }

    [Theory]
    [InlineData("scroll")]
    [InlineData("spellbook")]
    [InlineData("skillscroll")]
    [InlineData("rod")]
    [InlineData("horn")]
    [InlineData("clock")]
    public void DefaultApply_Given_a_type_that_needs_phase_4_When_applied_Then_it_says_not_available_yet_and_nothing_is_used_up(string arch)
    {
        // Arrange
        using var g = new Game("arch " + arch + "\nnrof 3\nend\n");
        ulong item = g.Item(arch);

        // Act
        ReplyFrame r = g.DefaultApply(item);

        // Assert
        Assert.Equal(new[] { "That is not available yet." }, Lines(r));
        Assert.Equal(1L, r.Result.AsInt); // told, so player_apply says nothing more
        Assert.Equal(3L, g.Read(item, "mudnrof").AsInt); // nothing was consumed
        Assert.False(g.Applied(item));
        Assert.Empty(EventNames(r));
    }

    [Theory]
    [InlineData("furniture")]
    [InlineData("door161")]
    public void DefaultApply_Given_furniture_or_a_house_door_When_applied_Then_it_cannot_be_applied(string arch)
    {
        // Arrange
        using var g = new Game("arch " + arch + "\nend\n");

        // Act
        ReplyFrame r = g.DefaultApply(g.Item(arch));

        // Assert
        Assert.Equal(new[] { "You can't apply that." }, Lines(r));
    }

    [Fact]
    public void DefaultApply_Given_a_lever_When_applied_Then_nothing_happens_and_it_is_answered_with_1()
    {
        // Arrange
        using var g = new Game("arch lever\nend\n");

        // Act
        ReplyFrame r = g.DefaultApply(g.Item("lever"));

        // Assert
        Assert.Equal(1L, r.Result.AsInt);
        Assert.Empty(Lines(r));
    }

    [Fact]
    public void DefaultApply_Given_a_type_nobody_applies_When_applied_Then_i_dont_know_how_to_apply_it()
    {
        // Arrange: gems have no apply
        using var g = new Game("arch gem\nend\n");

        // Act
        ReplyFrame r = g.DefaultApply(g.Item("gem"));

        // Assert
        Assert.Equal(0L, r.Result.AsInt);
        Assert.Equal(new[] { "I don't know how to apply the gems." }, Lines(r));
    }

    [Fact]
    public void DefaultApply_Given_an_unpaid_item_When_applied_Then_pay_for_it_first_and_the_script_is_not_asked()
    {
        // Arrange
        using var g = new Game("arch sword\nunpaid 1\nend\n");
        ulong sword = g.Item("sword");

        // Act
        ReplyFrame first = g.ApplyId(sword);
        ReplyFrame r = g.DefaultApply(sword);

        // Assert
        Assert.Equal(new[] { "onApplyItem" }, EventNames(first)); // the player's hook always, the item's class never
        Assert.Equal(new[] { "You should pay for it first." }, Lines(first));
        Assert.Equal(new[] { "You should pay for it first." }, Lines(r));
        Assert.False(g.Applied(sword));
    }

    // ------------------------------------------------------------------ the unapply of a dropped object, the ground

    [Fact]
    public void Drop_Given_an_applied_sword_When_dropped_Then_it_is_unwielded_first_with_its_hook_and_lands_on_the_level()
    {
        // Arrange
        using var g = new Game(Sword);
        g.Hall();
        ulong sword = g.Item("sword");
        g.DefaultApply(sword);
        Assert.True(g.Applied(sword));

        // Act
        ReplyFrame r = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(g.Id(sword)), N(5), N(5), Val.Int(0) }, level: "hall", player: "bob");

        // Assert
        Assert.True(Result(r));
        Assert.Equal(new[] { "onWeaponUnWielded" }, EventNames(r));
        Assert.Equal(sword, EventOf(r, "onWeaponUnWielded").Get("args")!.Items[0].AsHandle);
        Assert.Contains("You unwield swords.", Lines(r));
        Assert.DoesNotContain(g.Items(), i => i.Name == "sword");
        Assert.Single(Effects(r, "npc.place"));
        Assert.False(g.Applied(sword)); // the same handle now names the object on the ground
        ReplyFrame wielded = g.Ext.Call(13, new[] { H(g.Player), Val.Int(TMudPlayerMembers.MethodGetWieldedObject), Val.List() }, player: "bob");
        Assert.True(wielded.Result.IsNil);
    }

    [Fact]
    public void Drop_Given_a_cursed_applied_sword_When_dropped_Then_it_cannot_be_unapplied_and_stays()
    {
        // Arrange
        using var g = new Game("arch sword\ncursed 1\nend\n");
        g.Hall();
        ulong sword = g.Item("sword");
        g.DefaultApply(sword);

        // Act
        ReplyFrame r = g.Ext.Call(Manifest.OpMudDropInvObject, new[] { Val.Int(g.Id(sword)), N(5), N(5), Val.Int(0) }, level: "hall", player: "bob");

        // Assert
        Assert.False(Result(r));
        Assert.True(g.Applied(sword));
        Assert.Contains(g.Items(), i => i.Name == "sword");
    }

    [Fact]
    public void MudApplyObject_Given_a_sword_on_the_ground_When_applied_Then_the_object_event_is_sent_and_its_fallback_says_you_must_get_it_first()
    {
        // Arrange
        using var g = new Game();
        ulong ground = g.Hall()[PickupWorld.Sword];

        // Act: the apply asks the script (the event of the face), and the host's fallback runs defaultApply when no class handles it
        ReplyFrame r = g.Ext.Call(13, new[] { H(ground), Val.Int(TMudObjectMembers.MethodMudApplyObject), Val.List() }, level: "hall", player: "bob");
        ReplyFrame fallback = g.DefaultApply(ground);

        // Assert: the hall's sword is an object of the level: C's manual_apply answers 2
        Assert.True(Result(r));
        Assert.Equal(new[] { "onApplyItem", "onApply" }, EventNames(r));
        Assert.Equal(ground, EventOf(r, "onApplyItem").Get("args")!.Items[0].AsHandle); // the level object's own handle: no proxy is made for it
        Val onApply = EventOf(r, "onApply");
        Assert.Equal(("object", ground), (onApply.Get("target")!.AsString, onApply.Get("handle")!.AsHandle));
        Assert.Equal(g.Player, onApply.Get("args")!.Items[0].AsHandle);
        Assert.Equal(("defaultApply", ground), (onApply.Get("fallback")!.Get("invoke")!.AsString, onApply.Get("fallback")!.Get("handle")!.AsHandle));
        Assert.False(Result(fallback));
        Assert.Equal(new[] { "You must get it first!\n" }, Lines(fallback));
    }

    [Fact]
    public void MudApplyObject_Given_a_1_0_link_When_a_sword_on_the_ground_is_applied_Then_no_event_is_sent_and_it_answers_at_once_that_it_must_be_got_first()
    {
        // Arrange: a link of 1.0 has neither the item event nor the object event of the same name
        using var g = new Game(protoMinor: 0);
        ulong ground = g.Hall()[PickupWorld.Sword];

        // Act
        ReplyFrame r = g.Ext.Call(13, new[] { H(ground), Val.Int(TMudObjectMembers.MethodMudApplyObject), Val.List() }, level: "hall", player: "bob");

        // Assert
        Assert.Empty(r.Events);
        Assert.False(Result(r));
        Assert.Equal(new[] { "You must get it first!\n" }, Lines(r));
    }

    [Fact]
    public void DefaultApply_Given_a_player_argument_that_is_not_the_holder_When_invoked_Then_nothing_changes()
    {
        // Arrange
        using var g = new Game(Sword, others: new[] { "alice" });
        ulong aliceHandle = g.Login("alice");
        ulong sword = g.Item("sword");

        // Act
        ReplyFrame r = g.DefaultApply(sword, account: "bob", who: aliceHandle); // alice does not hold bob's sword

        // Assert
        Assert.False(g.Applied(sword));
        Assert.Empty(Lines(r));
    }

    [Fact]
    public void DefaultApply_Given_a_bad_argument_When_invoked_Then_bad_args()
    {
        // Arrange
        using var g = new Game(Sword);
        ulong sword = g.Item("sword");

        // Act
        ReplyFrame r = g.Ext.Call(13, new[] { H(sword), Val.Int(TMudObjectMembers.MethodDefaultApply), Val.List(S("bob")) }, player: "bob");

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
    }

    // ------------------------------------------------------------------ the ported C, called itself

    [Fact]
    public void CommandApply_Given_names_and_flags_When_the_command_runs_Then_it_applies_the_best_match_toggles_or_forces()
    {
        // Arrange
        using var g = new Game("arch sword\nend\narch mail\nend\n");
        ulong sword = g.Item("sword");

        // Act
        ReplyFrame none = g.Command("zzz");
        ReplyFrame unapplyFirst = g.Command("-u sword"); // nothing to take off
        ReplyFrame forced = g.Command(" -a sword");
        ReplyFrame forcedAgain = g.Command("-a sword"); // already applied: nothing
        ReplyFrame toggled = g.Command("sword");
        ReplyFrame asked = g.Command("mail");

        // Assert
        Assert.Equal(new[] { "Could not find any match to the zzz." }, Lines(none));
        Assert.Empty(Lines(unapplyFirst));
        Assert.Equal(new[] { "Readied skill: melee weapons.", "You wield swords (wielded)." }, Lines(forced));
        Assert.Empty(Lines(forcedAgain));
        Assert.Equal(new[] { "onApply" }, EventNames(toggled)); // a plain apply asks the script (the command is not the script's mudapplyinvobject: no onApplyItem)
        Assert.Contains("onApply", EventNames(asked));
        Assert.DoesNotContain("onApply", EventNames(forced)); // -a and -u carry a flag the fallback cannot: they apply at once
        Assert.True(g.Applied(sword));
        Assert.Equal(0L, none.Result.AsInt);
    }

    [Fact]
    public void CommandApply_Given_no_parameters_and_no_container_When_the_command_runs_Then_nothing_is_below_the_player()
    {
        // Arrange
        using var g = new Game(Sword);

        // Act
        ReplyFrame r = g.Ext.Call(ApplyWorld.OpDo, new[] { S("command"), S("bob"), Val.Nil, Val.Nil }, player: "bob");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Empty(Lines(r));
        Assert.Empty(r.Events);
    }

    [Fact]
    public void PlayerApplyBelow_Given_an_open_container_When_applied_Then_its_top_object_is_applied_and_an_answer_of_1_ends_the_walk()
    {
        // Arrange
        using var g = new Game("arch sack\narch scroll\nnrof 1\nend\narch book\nend\nend\n");
        ulong sack = g.Item("sack");

        // Act
        ReplyFrame r = g.Do("below", Val.Int(g.Id(sack)));
        ReplyFrame none = g.Do("below", Val.Int(999999));

        // Assert: the first object that is applied (a script is asked) answers 1 and ends the walk: one event, not two
        Assert.Equal(0UL, r.Status);
        Assert.Single(EventNames(r), "onApply");
        Assert.DoesNotContain("onApplyItem", EventNames(r)); // the command is not the script's mudapplyinvobject
        Assert.Empty(none.Events);
    }

    [Fact]
    public void PlayerApplyBelow_Given_a_link_of_1_0_and_an_open_container_When_applied_Then_the_scroll_says_not_available_once_and_the_book_is_not_tried()
    {
        // Arrange
        using var g = new Game("arch sack\narch scroll\nnrof 1\nend\narch book\nend\nend\n", protoMinor: 0);
        ulong sack = g.Item("sack");

        // Act
        ReplyFrame r = g.Do("below", Val.Int(g.Id(sack)));

        // Assert
        Assert.Equal(new[] { "That is not available yet." }, Lines(r));
    }

    [Fact]
    public void PlayerApplyBelow_Given_objects_that_cannot_be_applied_in_a_container_When_applied_Then_each_is_tried_quietly_and_nothing_is_said()
    {
        // Arrange: gems answer 0 and the quiet walk goes on to the next one without a line
        using var g = new Game("arch sack\narch gem\nend\narch gem2\nend\nend\n", protoMinor: 0);
        ulong sack = g.Item("sack");

        // Act
        ReplyFrame r = g.Do("below", Val.Int(g.Id(sack)));

        // Assert
        Assert.Empty(Lines(r));
    }

    [Fact]
    public void PlayerApply_Given_an_object_When_applied_Then_it_is_the_last_used_one()
    {
        // Arrange
        using var g = new Game("arch scroll\nend\n");
        ulong scroll = g.Item("scroll");

        // Act
        ReplyFrame r = g.Ext.Call(ApplyWorld.OpDo, new[] { S("lastused"), S("bob"), Val.Int(g.Id(scroll)), Val.Nil }, player: "bob");

        // Assert
        Assert.Equal(1L, r.Result.AsInt);
    }

    [Fact]
    public void PlayerApply_Given_a_flying_player_and_an_object_on_no_level_When_applied_Then_he_is_floating_high_above_the_ground()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.Do("fly");

        // Assert
        Assert.Equal(new[] { "But you are floating high above the ground!" }, Lines(r));
        Assert.Equal(0L, r.Result.AsInt);
    }

    [Fact]
    public void ManualApply_Given_a_part_of_a_larger_object_When_applied_Then_the_whole_is_applied()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.Do("head");

        // Assert: the head, a sword of the player's, is wielded through the event path, as any object of his tree
        Assert.Equal(1L, r.Result.AsInt);
        Assert.Contains("onApply", EventNames(r));
    }

    [Fact]
    public void ManualApply_Given_a_monster_When_it_applies_an_unpaid_object_or_a_treasure_Then_it_does_not()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame unpaid = g.Do("monster", S("sword"), Val.True);
        ReplyFrame treasure = g.Do("monster", S("chest"), Val.False);

        // Assert
        Assert.Equal(0L, unpaid.Result.AsInt);
        Assert.Equal(0L, treasure.Result.AsInt);
        Assert.Empty(Lines(unpaid));
    }
}
