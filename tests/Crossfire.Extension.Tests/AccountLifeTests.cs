// Not ported from a Crossfire file: tests of lane 7B of phase 3 (wave 7), the account completion (docs/mud-phase3.md section 4.7): what
// a login does to the tree it read (check_login: the experience, the skills, legal_range and fix_player), the equipment hooks at
// login and at loadmudaccount, loadmudaccount after unloadmudaccount, Mud_LoadAccount, and backupmudaccount with a name.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>The world and the two players of the tests of lane 7B: bob (who wears things) and carol (who has a sword).</summary>
internal sealed class W7Game : IDisposable
{
    public const string BobWorn =
        ApplyWorld.Skills + "arch sword\napplied 1\nend\narch plate\napplied 1\nend\narch wand\nend\narch mask\napplied 1\nend\narch amulet\napplied 1\nend\n";

    public const string CarolItems = ApplyWorld.Skills + "arch sword\nend\narch banana\nnrof 5\nend\n";

    private readonly List<string> order = new();

    public W7Game(string bob = BobWorn, string carol = CarolItems, long protoMinor = 1, bool slowWriter = false, TimeProvider? time = null, string header = ApplyWorld.Header)
    {
        TempFolder world = ApplyWorld.MakeWorld("bob", bob, header, accounts: new[] { "carol" });
        ItemWorld.Account(world, "carol", header, carol);
        var writer = new FileWriterOptions
        {
            Quiet = slowWriter ? TimeSpan.FromHours(1) : TimeSpan.FromMilliseconds(20),
            MaxDelay = slowWriter ? TimeSpan.FromHours(1) : TimeSpan.FromMilliseconds(100),
            BetweenMove = (source, destination) => order.Add(source + ">" + destination),
        };
        Ext = new DirectExt(world, new ExtensionOptions { Writer = writer, ExtraManifest = ApplyWorld.Register, Time = time ?? TimeProvider.System }, 1, true, 16_777_216, protoMinor);
        Host.Answer = (name, args) => name == "player.at" ? At(args) : new QueryAnswer(Status.Ok, Val.Nil);
        Ext.Dispatcher.QuerySource = Host.Ask;
    }

    public DirectExt Ext { get; }

    public FakeHost Host { get; } = new();

    /// <summary>What <c>player.at</c> answers: the account at an index of <c>level.players</c>.</summary>
    public Dictionary<long, string> Players { get; } = new() { [0] = "bob", [1] = "carol" };

    /// <summary>The files the writer wrote as moves, in the order it wrote them ("source>destination").</summary>
    public IReadOnlyList<string> WrittenMoves => order;

    private QueryAnswer At(IReadOnlyList<Val> args) =>
        new(Status.Ok, args.Count == 2 && Players.TryGetValue(args[1].AsInt, out string? who) ? Val.Str(who) : Val.Nil);

    public ReplyFrame Login(string account) => Ext.Call(5, new[] { S(account) }, player: account);

    public ulong Handle(string account) => Login(account).Result.AsHandle;

    public ReplyFrame Method(ulong target, int method, string account, params Val[] args) =>
        Ext.Call(13, new[] { H(target), Val.Int(method), Val.List(args) }, level: "hall", player: account);

    /// <summary>The items of the player as rows of (handle, name).</summary>
    public List<(ulong Handle, string Name)> Items(ulong holder)
    {
        ReplyFrame r = Ext.Call(12, new[] { H(holder), Val.Int(Ply("muditems")), Val.List(Val.Int(Obj("mudrealname"))) });
        Assert.Equal(0UL, r.Status);
        return r.Result.Items.Select(row => (row.Items[0].AsHandle, row.Items[1].AsString)).ToList();
    }

    public List<string> Names(ulong holder) => Items(holder).Select(i => i.Name).Order().ToList();

    public ulong Item(ulong holder, string name) => Items(holder).First(i => i.Name == name).Handle;

    public long Id(ulong item) => Get(Ext, item, Obj("mudid")).AsInt;

    public string File(string relative) => ObjectWorld.Data(Ext.World, relative);

    public Dispatcher Dispatcher => Ext.Dispatcher;

    /// <summary>The id a client knows an item by (its tag), for an item of any depth.</summary>
    public long IdOf(CfObject item) => Ext.Dispatcher.HandlesOfTests.TagOf(Ext.Dispatcher.ItemProxy(item));

    public CfObject Ob(string account) => Ext.Dispatcher.ControllerOf(account)!.State.Ob;

    public void Dispose() => Ext.Dispose();

    /// <summary>The lines a player was told: the text events of the request (a 1.1 link) and the text effects of a 1.0 link.</summary>
    public static List<string> Lines(ReplyFrame r, string account) =>
        r.Events.Where(e => e.Get("name")!.AsString == "onRPGMessage" && e.Get("player")!.AsString == account).Select(e => e.Get("args")!.Items[0].AsString)
            .Concat(Effects(r, "client.text").Where(e => e.Get("player")!.AsString == account).Select(e => e.Get("text")!.AsString)).ToList();

    /// <summary>The events of the equipment hooks of a reply, as (name, handle of the item), in the order they were sent.</summary>
    public static List<(string Name, ulong Item)> Hooks(ReplyFrame r) =>
        r.Events.Where(e => e.Get("name")!.AsString is "onWeaponWielded" or "onWeaponUnWielded" or "onArmorWorn" or "onArmorUnWorn" or "onClothWorn" or "onClothUnWorn" or "onWandReadied" or "onWandUnReadied")
            .Select(e => (e.Get("name")!.AsString, e.Get("args")!.Items[0].AsHandle)).ToList();

    /// <summary>The strings a reply sets (the last value of a name that is set twice).</summary>
    public static Dictionary<string, string> Sets(ReplyFrame r)
    {
        var sets = new Dictionary<string, string>();
        foreach (Val e in Effects(r, "client.set"))
        {
            sets[e.Get("name")!.AsString] = e.Get("value")!.AsString;
        }
        return sets;
    }
}

public sealed class AccountLifeTests
{
    // waits until the file under the data folder has a text the condition accepts, and returns it
    private static string WaitFor(W7Game g, string relative, Func<string, bool> ready)
    {
        string text = "";
        for (int i = 0; i < 500; i++)
        {
            string path = g.File(relative);
            try
            {
                text = File.Exists(path) ? File.ReadAllText(path, System.Text.Encoding.Latin1) : "";
            }
            catch (IOException)
            {
                text = "";
            }
            if (ready(text))
            {
                break;
            }
            Thread.Sleep(10);
        }
        return text;
    }

    private static List<string> EventNames(ReplyFrame r) => r.Events.Select(e => e.Get("name")!.AsString).Where(n => n != "onRPGMessage").ToList();

    // ------------------------------------------------------------------ the equipment hooks at login

    [Fact]
    public void Login_Given_an_account_with_applied_items_When_the_player_logs_in_Then_the_hooks_of_the_applied_items_come_in_the_order_of_the_tree_before_onLoadedMudAccount()
    {
        // Arrange
        using var g = new W7Game();

        // Act
        ReplyFrame r = g.Login("bob");

        // Assert: the wand is not applied: no hook; the amulet's hook is the armour's
        ulong bob = r.Result.AsHandle;
        Assert.Equal(
            new[] { ("onWeaponWielded", g.Item(bob, "sword")), ("onArmorWorn", g.Item(bob, "plate")), ("onClothWorn", g.Item(bob, "mask")), ("onArmorWorn", g.Item(bob, "amulet")) },
            W7Game.Hooks(r).ToArray());
        Assert.Equal("onLoadedMudAccount", EventNames(r).Last());
        Assert.Single(r.Events, e => e.Get("name")!.AsString == "onLoadedMudAccount");
        Assert.All(r.Events.Where(e => e.Get("name")!.AsString != "onLoadedMudAccount"), e => Assert.Equal("bob", e.Get("player")!.AsString));
    }

    [Fact]
    public void Login_Given_a_wand_that_is_applied_When_the_player_logs_in_Then_onWandReadied_names_it()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills + "arch wand\napplied 1\nend\n");

        // Act
        ReplyFrame r = g.Login("bob");

        // Assert
        Assert.Equal(new[] { ("onWandReadied", g.Item(r.Result.AsHandle, "wand")) }, W7Game.Hooks(r).ToArray());
    }

    [Fact]
    public void Login_Given_an_account_with_applied_items_on_a_1_0_link_When_the_player_logs_in_Then_no_hook_is_sent()
    {
        // Arrange
        using var g = new W7Game(protoMinor: 0);

        // Act
        ReplyFrame r = g.Login("bob");

        // Assert
        Assert.Empty(W7Game.Hooks(r));
        Assert.Equal(new[] { "onLoadedMudAccount" }, EventNames(r));
    }

    [Fact]
    public void Login_Given_an_account_without_applied_items_When_the_player_logs_in_Then_only_onLoadedMudAccount_is_sent()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills + "arch sword\nend\narch plate\nend\n");

        // Act
        ReplyFrame r = g.Login("bob");

        // Assert
        Assert.Empty(W7Game.Hooks(r));
        Assert.Equal(new[] { "onLoadedMudAccount" }, EventNames(r));
    }

    [Fact]
    public void Login_Given_a_second_login_without_a_logout_When_called_Then_the_hooks_are_not_sent_again()
    {
        // Arrange
        using var g = new W7Game();
        g.Login("bob");

        // Act
        ReplyFrame again = g.Login("bob");

        // Assert: the player is still logged in: his strings are sent again, no load took place
        Assert.Empty(W7Game.Hooks(again));
        Assert.Empty(again.Events);
    }

    [Fact]
    public void Login_Given_a_new_account_When_the_player_logs_in_Then_no_hook_is_sent_and_nothing_is_linked()
    {
        // Arrange
        using var g = new W7Game();

        // Act
        ReplyFrame r = g.Login("dave"); // no file: a new player

        // Assert: C's new player does not pass check_login; the start path gives the experience objects
        Assert.Empty(W7Game.Hooks(r));
        Assert.DoesNotContain(Items(g.Ob("dave")), i => i.Type == Define.EXPERIENCE);
    }

    private static List<CfObject> Items(CfObject op)
    {
        var l = new List<CfObject>();
        for (CfObject? c = op.Inv; c != null; c = c.Below)
        {
            l.Add(c);
        }
        return l;
    }

    // ------------------------------------------------------------------ what check_login does

    [Fact]
    public void Login_Given_a_file_without_experience_objects_When_the_player_logs_in_Then_he_has_one_of_each_category_and_his_skills_are_linked()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills);

        // Act
        g.Login("bob");

        // Assert
        List<CfObject> items = Items(g.Ob("bob"));
        Assert.Equal(6, items.Count(i => i.Type == Define.EXPERIENCE));
        Assert.NotNull(items.Single(i => i.Type == Define.SKILL && i.Arch!.Name == "skill_melee_weapon").ExpObj);
    }

    [Fact]
    public void Login_Given_a_file_with_all_the_experience_objects_When_the_player_logs_in_Then_they_are_applied_and_his_experience_is_their_sum()
    {
        // Arrange: the objects were saved unapplied by a hand
        string[] categories = { "agility", "personality", "mental", "physique", "magic", "wis" };
        string objects = string.Concat(categories.Select((c, i) => "arch experience_" + c + "\nexp " + (1_000_000 * (i + 1)) + "\nend\n"));
        using var g = new W7Game(bob: ApplyWorld.Skills + objects, header: ApplyWorld.Header + "exp 5\n");

        // Act
        g.Login("bob");

        // Assert: init_player_exp makes the experience of the player the sum of the objects and their levels follow their experience
        CfObject pl = g.Ob("bob");
        Assert.Equal(21_000_000, pl.Stats.Exp);
        Assert.All(Items(pl).Where(i => i.Type == Define.EXPERIENCE), i => Assert.True(i.Level > 1));
        Assert.All(Items(pl).Where(i => i.Type == Define.EXPERIENCE), i => Assert.True(i.QueryFlag(Define.FLAG_APPLIED)));
        Assert.Equal(6, Items(pl).Count(i => i.Type == Define.EXPERIENCE));
    }

    [Fact]
    public void Login_Given_a_file_with_rolled_stats_When_the_player_logs_in_Then_the_strings_of_the_login_show_what_fix_player_makes_of_them()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills);

        // Act
        ReplyFrame r = g.Login("bob");

        // Assert: Str 14 from mudorigstats, damage 0 + dam_bonus[14] 1, the weight limit of Str 14
        Dictionary<string, string> sets = W7Game.Sets(r);
        Assert.Equal("14", sets["clientr.mud_str"]);
        Assert.Equal("1", sets["clientr.mud_dam"]);
    }

    [Fact]
    public void Login_Given_a_file_whose_player_was_never_rolled_When_the_player_logs_in_Then_fix_player_has_set_his_weight_limit()
    {
        // Arrange
        using var g = new W7Game(bob: "", header: "level 1\nStr 14\n"); // no item: nothing else calls fix_player

        // Act
        g.Login("bob");

        // Assert: the part of fix_player that every player gets
        Player contr = g.Ext.Dispatcher.ControllerOf("bob")!;
        Assert.Equal(ItemMatching.WeightLimitOf(g.Ob("bob").Stats.Str), contr.WeightLimit);
        Assert.True(contr.WeightLimit > 0);
    }

    [Fact]
    public void Login_Given_a_file_whose_items_weigh_something_When_the_player_logs_in_Then_the_carried_weight_is_the_sum_of_the_tree()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills + "arch plate\nend\narch sword\nend\n");

        // Act
        g.Login("bob");

        // Assert: plate 9000 and the sword of the world
        CfObject pl = g.Ob("bob");
        Assert.Equal(CfWorld.SumWeight(pl), pl.Carrying);
        Assert.True(pl.Carrying >= 9000);
    }

    [Fact]
    public void CheckLogin_Given_a_range_the_player_cannot_use_When_it_runs_Then_the_range_is_none()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills);
        g.Login("bob");
        CfObject pl = g.Ob("bob");
        var contr = (Player)pl.Contr!;
        contr.ShootType = AccountLife.RangeBow; // no bow is applied

        // Act
        AccountLife.CheckLogin(g.Ext.Dispatcher.WorldOfTests!, g.Ext.Dispatcher.SkillsOfTests, pl);

        // Assert
        Assert.Equal(AccountLife.RangeNone, contr.ShootType);
    }

    [Fact]
    public void CheckLogin_Given_a_range_the_player_can_use_When_it_runs_Then_it_stays()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills + "arch wand\napplied 1\nend\n");
        g.Login("bob");
        CfObject pl = g.Ob("bob");
        var contr = (Player)pl.Contr!;
        contr.ShootType = AccountLife.RangeWand;

        // Act
        AccountLife.CheckLogin(g.Ext.Dispatcher.WorldOfTests!, g.Ext.Dispatcher.SkillsOfTests, pl);

        // Assert
        Assert.Equal(AccountLife.RangeWand, contr.ShootType);
    }

    [Theory]
    [InlineData("arch wand\napplied 1\nend\n", AccountLife.RangeWand, true)]
    [InlineData("arch wand\nend\n", AccountLife.RangeWand, false)]
    [InlineData("arch rod\napplied 1\nend\n", AccountLife.RangeRod, true)]
    [InlineData("arch rod\napplied 1\nend\n", AccountLife.RangeWand, false)]
    [InlineData("arch rod\nend\n", AccountLife.RangeRod, false)]
    [InlineData("arch horn\napplied 1\nend\n", AccountLife.RangeHorn, true)]
    [InlineData("arch horn\napplied 1\nend\n", AccountLife.RangeRod, false)]
    [InlineData("arch horn\nend\n", AccountLife.RangeHorn, false)]
    [InlineData("arch wand\napplied 1\nend\n", AccountLife.RangeHorn, false)]
    [InlineData("arch wand\napplied 1\nend\n", AccountLife.RangeBow, false)]
    [InlineData("arch wand\napplied 1\nend\n", AccountLife.RangeNone, true)]
    [InlineData("arch wand\napplied 1\nend\n", AccountLife.RangeScroll, false)]
    [InlineData("arch wand\napplied 1\nend\n", AccountLife.RangeMagic, false)]
    [InlineData("arch wand\napplied 1\nend\n", AccountLife.RangeSkill, false)]
    [InlineData("arch wand\napplied 1\nend\n", 99, false)]
    public void LegalRange_Given_a_range_When_asked_Then_it_is_legal_when_the_player_has_what_it_needs(string items, int range, bool expected)
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills + items);
        g.Login("bob");

        // Act / Assert
        Assert.Equal(expected, AccountLife.LegalRange(g.Ob("bob"), range));
    }

    [Fact]
    public void LegalRange_Given_a_bow_a_known_spell_and_a_readied_skill_When_asked_Then_each_makes_its_range_legal()
    {
        // Arrange
        using var g = new W7Game(bob: ApplyWorld.Skills);
        g.Login("bob");
        CfObject pl = g.Ob("bob");
        CfWorld w = g.Ext.Dispatcher.WorldOfTests!;
        Assert.False(AccountLife.LegalRange(pl, AccountLife.RangeBow));
        Assert.False(AccountLife.LegalRange(pl, AccountLife.RangeMagic));
        Assert.False(AccountLife.LegalRange(pl, AccountLife.RangeSkill)); // link_player_skills unreadied the skill

        // Act
        CfObject bow = w.GetObject();
        bow.Type = Define.BOW;
        bow.SetFlag(Define.FLAG_APPLIED);
        w.InsertObInOb(bow, pl);
        ((Player)pl.Contr!).KnownSpells.Add(1);
        pl.ChosenSkill = pl.Inv;

        // Assert
        Assert.True(AccountLife.LegalRange(pl, AccountLife.RangeBow));
        Assert.True(AccountLife.LegalRange(pl, AccountLife.RangeMagic));
        Assert.True(AccountLife.LegalRange(pl, AccountLife.RangeSkill));
        bow.ClearFlag(Define.FLAG_APPLIED);
        Assert.False(AccountLife.LegalRange(pl, AccountLife.RangeBow));
    }

    // ------------------------------------------------------------------ loadmudaccount, unloadmudaccount, Mud_LoadAccount

    [Fact]
    public void LoadMudAccount_Given_a_loaded_account_When_called_with_the_empty_suffix_Then_nothing_happens()
    {
        // Arrange
        using var g = new W7Game();
        ulong bob = g.Handle("bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodLoadMudAccount, "bob", S(""));

        // Assert: no second event, no hook, no string
        Assert.True(r.Result.AsBool);
        Assert.Empty(r.Events);
        Assert.Empty(r.Effects);
    }

    [Fact]
    public void Title_Given_a_title_set_When_the_account_is_unloaded_and_loaded_Then_who_and_title_show_it()
    {
        // Arrange
        using var g = new W7Game();
        ulong bob = g.Handle("bob");
        g.Ext.Call(Manifest.OpMudCommand, new[] { S("title"), S("the Bold") }, level: "hall", player: "bob");
        g.Ext.Call(Manifest.OpMudCommand, new[] { S("save") }, level: "hall", player: "bob"); // unloadmudaccount drops the memory state unsaved
        g.Method(bob, TMudPlayerMembers.MethodUnloadMudAccount, "bob");
        g.Method(bob, TMudPlayerMembers.MethodLoadMudAccount, "bob", S(""));

        // Act
        List<string> who = W7Game.Lines(g.Ext.Call(Manifest.OpMudCommand, new[] { S("who") }, level: "hall", player: "bob"), "bob");
        List<string> title = W7Game.Lines(g.Ext.Call(Manifest.OpMudCommand, new[] { S("title") }, level: "hall", player: "bob"), "bob");

        // Assert
        Assert.Contains("bob the the Bold P", who);
        Assert.Equal(new[] { "Your title is 'the Bold'." }, title);
    }

    [Fact]
    public void Title_Given_a_title_set_When_the_player_logs_out_and_in_Then_the_title_is_kept()
    {
        // Arrange
        using var g = new W7Game();
        g.Login("bob");
        g.Ext.Call(Manifest.OpMudCommand, new[] { S("title"), S("the Bold") }, level: "hall", player: "bob");
        g.Ext.Call(6, new[] { S("bob") });
        g.Login("bob");

        // Act
        List<string> title = W7Game.Lines(g.Ext.Call(Manifest.OpMudCommand, new[] { S("title") }, level: "hall", player: "bob"), "bob");

        // Assert
        Assert.Equal(new[] { "Your title is 'the Bold'." }, title);
    }

    [Fact]
    public void LoadMudAccount_Given_an_unloaded_account_When_called_with_the_empty_suffix_Then_the_file_is_read_the_strings_sent_the_hooks_run_and_the_event_fires_once()
    {
        // Arrange
        using var g = new W7Game();
        ulong bob = g.Handle("bob");
        g.Method(bob, TMudPlayerMembers.MethodUnloadMudAccount, "bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodLoadMudAccount, "bob", S(""));

        // Assert
        Assert.True(r.Result.AsBool);
        (string Name, ulong Item)[] hooks = W7Game.Hooks(r).ToArray();
        Assert.Equal(new[] { "onWeaponWielded", "onArmorWorn", "onClothWorn", "onArmorWorn" }, hooks.Select(h => h.Name));
        Assert.All(hooks, h => Assert.Contains(g.Items(bob), i => i.Handle == h.Item));
        Assert.Equal("onLoadedMudAccount", EventNames(r).Last());
        Assert.Single(r.Events, e => e.Get("name")!.AsString == "onLoadedMudAccount");
        Assert.Equal("14", W7Game.Sets(r)["clientr.mud_str"]); // fix_player ran: the stats are the rolled ones
        Assert.Contains("sword", g.Names(bob));
    }

    [Fact]
    public void LoadMudAccount_Given_an_unloaded_account_on_a_1_0_link_When_loaded_Then_no_hook_is_sent_and_the_event_fires()
    {
        // Arrange
        using var g = new W7Game(protoMinor: 0);
        ulong bob = g.Handle("bob");
        g.Method(bob, TMudPlayerMembers.MethodUnloadMudAccount, "bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodLoadMudAccount, "bob", S(""));

        // Assert
        Assert.Empty(W7Game.Hooks(r));
        Assert.Equal(new[] { "onLoadedMudAccount" }, EventNames(r));
    }

    [Fact]
    public void LoadMudAccount_Given_a_suffix_When_the_file_of_that_name_exists_Then_it_is_loaded_with_its_hooks_and_the_account_is_saved_to_it_after()
    {
        // Arrange
        using var g = new W7Game();
        ItemWorld.Account(g.Ext.World, "bob_2", ApplyWorld.Header, ApplyWorld.Skills + "arch sword\napplied 1\nend\n");
        ulong bob = g.Handle("bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodLoadMudAccount, "bob", S("_2"));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Equal(new[] { "sword" }, g.Names(bob));
        Assert.Equal(new[] { "onWeaponWielded" }, W7Game.Hooks(r).Select(h => h.Name));
        Assert.Equal(new[] { "onLoadedMudAccount" }, EventNames(r).Skip(1));
    }

    [Fact]
    public void MudLoadAccount_Given_a_slot_When_its_file_exists_Then_it_is_loadmudaccount_with_an_underscore_and_the_slot()
    {
        // Arrange
        using var g = new W7Game();
        ItemWorld.Account(g.Ext.World, "bob_3", ApplyWorld.Header, ApplyWorld.Skills + "arch plate\napplied 1\nend\n");
        ulong bob = g.Handle("bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodMudLoadAccount, "bob", S("3"));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Equal(new[] { "plate" }, g.Names(bob));
        Assert.Equal(new[] { "onArmorWorn" }, W7Game.Hooks(r).Select(h => h.Name));
    }

    [Fact]
    public void LoadMudAccount_Given_a_missing_slot_When_called_Then_false_and_no_hook_and_no_event()
    {
        // Arrange
        using var g = new W7Game();
        ulong bob = g.Handle("bob");
        List<string> before = g.Names(bob);

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodMudLoadAccount, "bob", S("9"));

        // Assert
        Assert.False(r.Result.AsBool);
        Assert.Empty(r.Events);
        Assert.Equal(before, g.Names(bob));
    }

    [Fact]
    public void LoadMudAccount_Given_the_function_busy_When_called_Then_busy_and_the_account_is_as_it_was()
    {
        // Arrange
        using var g = new W7Game();
        ulong bob = g.Handle("bob");
        g.Method(bob, TMudPlayerMembers.MethodUnloadMudAccount, "bob");
        g.Ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodLoadMudAccount, "bob", S(""));
        g.Ext.Dispatcher.Busy = null;

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.Empty(g.Names(bob));
    }

    // ------------------------------------------------------------------ savemudaccount and backupmudaccount

    [Fact]
    public void SaveMudAccount_Given_a_slow_writer_When_called_Then_the_file_is_written_now()
    {
        // Arrange
        using var g = new W7Game(slowWriter: true);
        ulong bob = g.Handle("bob");
        Set(g.Ext, bob, Ply("mudexp"), Val.Int(77));

        // Act
        g.Method(bob, TMudPlayerMembers.MethodSaveMudAccount, "bob");

        // Assert
        Assert.Contains("exp 77", WaitFor(g, "accounts/bob.txt", t => t.Contains("exp 77")));
    }

    [Fact]
    public void BackupMudAccount_Given_a_name_When_called_Then_the_copy_is_in_the_backups_folder_under_the_account_and_the_name()
    {
        // Arrange
        var time = new FixedTime();
        using var g = new W7Game(slowWriter: true, time: time);
        ulong bob = g.Handle("bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodBackupMudAccount, "bob", S("_old"));
        string backup = WaitFile(g.Ext.World, "backups/bob_old.20261126-123045.txt");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Contains("arch sword", backup);
        Assert.Contains("account bob\n", backup);
    }

    [Fact]
    public void BackupMudAccount_Given_the_empty_name_When_called_Then_the_name_of_the_copy_is_the_account_and_the_time()
    {
        // Arrange
        var time = new FixedTime();
        using var g = new W7Game(slowWriter: true, time: time);
        ulong bob = g.Handle("bob");

        // Act
        g.Method(bob, TMudPlayerMembers.MethodBackupMudAccount, "bob", S(""));

        // Assert
        Assert.Contains("arch sword", WaitFile(g.Ext.World, "backups/bob.20261126-123045.txt"));
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("a/b")]
    [InlineData("..")]
    public void BackupMudAccount_Given_a_name_that_names_a_place_When_called_Then_the_copy_stays_inside_the_backups_folder_under_an_escaped_name(string name)
    {
        // Arrange
        var time = new FixedTime();
        using var g = new W7Game(slowWriter: true, time: time);
        ulong bob = g.Handle("bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodBackupMudAccount, "bob", S(name));
        WaitFile(g.Ext.World, "backups/" + FileNames.Escape("bob" + name) + ".20261126-123045.txt");

        // Assert
        Assert.Equal(0UL, r.Status);
        string backups = g.File("backups");
        Assert.Single(Directory.GetFiles(backups));
        Assert.Equal(backups, Path.GetDirectoryName(Directory.GetFiles(backups).Single()));
        Assert.Empty(Directory.GetDirectories(backups));
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(g.File("backups"))!, "etc")));
    }

    [Fact]
    public void BackupMudAccount_Given_a_name_with_a_control_character_When_called_Then_bad_args_and_nothing_is_written()
    {
        // Arrange
        using var g = new W7Game(slowWriter: true);
        ulong bob = g.Handle("bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodBackupMudAccount, "bob", S("a\nb"));
        Thread.Sleep(200);

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
        Assert.False(Directory.Exists(g.File("backups")));
    }

    [Fact]
    public void BackupMudAccount_Given_a_name_too_long_for_a_file_name_When_called_Then_bad_args_and_nothing_is_written()
    {
        // Arrange
        using var g = new W7Game(slowWriter: true);
        ulong bob = g.Handle("bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodBackupMudAccount, "bob", S(new string('x', 300)));
        Thread.Sleep(200);

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
        Assert.False(Directory.Exists(g.File("backups")));
    }

    [Fact]
    public void BackupMudAccount_Given_a_non_string_name_When_called_Then_bad_args()
    {
        // Arrange
        using var g = new W7Game();
        ulong bob = g.Handle("bob");

        // Act
        ReplyFrame r = g.Method(bob, TMudPlayerMembers.MethodBackupMudAccount, "bob", Val.Int(3));

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
    }

    [Fact]
    public void Manifest_Given_the_lane_When_built_Then_backupmudaccount_takes_a_string_and_the_ids_of_the_lane_are_there()
    {
        // Arrange / Act
        Val manifest = Manifest.Build(false, 1);
        Val player = manifest.Get("types")!.Items.Single(t => t.Get("name")!.AsString == "TMudPlayer");
        Val backup = player.Get("methods")!.Items.Single(m => m.Get("name")!.AsString == "backupmudaccount");
        Val examine = player.Get("methods")!.Items.Single(m => m.Get("name")!.AsString == "examineinvobject");
        var functions = manifest.Get("functions")!.Items.ToDictionary(f => f.Get("name")!.AsString);

        // Assert
        Assert.Equal(2507L, backup.Get("id")!.AsInt);
        Assert.Equal(new[] { "string" }, backup.Get("params")!.Items.Select(p => p.AsString));
        Assert.Equal(13500L, examine.Get("id")!.AsInt);
        Assert.Equal(new[] { "int" }, examine.Get("params")!.Items.Select(p => p.AsString));
        Assert.Equal(13000L, functions["mudexamineinvobject2"].Get("id")!.AsInt);
        Assert.False(functions["mudexamineinvobject2"].Get("slow")!.AsBool);
        Assert.Equal(13001L, functions["mudtransferobject"].Get("id")!.AsInt);
        Assert.True(functions["mudtransferobject"].Get("slow")!.AsBool);
        Assert.Equal(new[] { "int", "int", "int", "int" }, functions["mudtransferobject"].Get("params")!.Items.Select(p => p.AsString));
    }

    [Fact]
    public void Manifest_Given_a_1_0_link_When_built_Then_the_functions_that_need_player_at_are_not_there()
    {
        // Arrange / Act
        Val manifest = Manifest.Build(false, 0);
        var names = manifest.Get("functions")!.Items.Select(f => f.Get("name")!.AsString).ToList();

        // Assert
        Assert.DoesNotContain("mudexamineinvobject2", names);
        Assert.DoesNotContain("mudtransferobject", names);
    }
}
