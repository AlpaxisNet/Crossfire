// Not ported from a Crossfire file: tests of lane 6D of phase 3 (wave 6), signs, books, lighters and treasure chests applied by the extension: the
// ported C of server/apply.c (apply_sign, apply_book, apply_lighter, apply_treasure), server/attack.c (did_make_save_item, save_throw_object for
// fire) and server/skill_util.c (SK_level), and Graal Kingdoms' types 160 and 161 (docs/mud-phase3.md section 4.4). The apply is run through
// the dispatcher's ApplyByLane, which lane 6B's manual_apply asks first, in a test function of the lane's range (t6dapply).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>An invented world for the tests of lane 6D: things to read, light and open, and a hall with a chest on the ground.</summary>
internal static class ReadableWorld
{
    public const string Arc =
        ItemWorld.Arc
        + "Object sign\nname sign\nface sign.1\ntype 98\nweight 100\nend\n"
        + "Object tome\nname tome\nname_pl tomes\nface tome.1\ntype 8\nweight 100\nend\n"
        + "Object literacy\nname literacy\ntype 43\nsp 8\nlevel 3\ninvisible 1\nend\n"
        + "Object lighter\nname lighter\nname_pl lighters\nface lighter.1\ntype 75\nweight 50\nend\n"
        + "Object torch\nname torch\nname_pl torches\nface torch.1\nweight 500\nmaterial 16\nother_arch torch_lit\nglow_radius 1\nend\n"
        + "Object torch_lit\nname lit torch\nname_pl lit torches\nface torch.2\nweight 500\nmaterial 16\nglow_radius 3\nend\n"
        + "Object paper\nname paper\nname_pl papers\nface paper.1\nweight 10\nmaterial 1\nend\n"
        + "Object lamp\nname lamp\nname_pl lamps\nface lamp.1\nweight 50\nmaterial 16\nglow_radius 2\nend\n"
        + "Object tinder\nname tinder\nname_pl tinders\nface tinder.1\nweight 50\nmaterial 16\nother_arch torch_lit\nend\n"
        + "Object burnout\nname burnout\nface burnout.1\nweight 0\nend\n"
        + "Object chest\nname chest\nname_pl chests\nface chest.1\ntype 4\nhp 1\nweight 100\nrandomitems chest_items\nend\n"
        + "Object bare_chest\nname bare chest\nname_pl bare chests\nface chest.2\ntype 4\nweight 100\nend\n"
        + "Object auto_chest\nname auto chest\nname_pl auto chests\nface chest.3\ntype 4\nhp 1\nauto_apply 1\nrandomitems chest_items\nend\n"
        + "Object door\nname door\nface door.2\ntype 161\nend\n";

    /// <summary>The treasure list of the chests: a sword and two bananas.</summary>
    public const string Treasures = "treasure chest_items\n  arch sword\n  more\n  arch banana\n  nrof 2\n  end\n";

    /// <summary>The hall: a sign with a message at (3, 4), a chest with a sword and a banana in it at (3, 4).</summary>
    public const string HallSeed =
        "arch map\nname hall\nwidth 16\nheight 16\ndifficulty 7\nend\n"
        + "arch sign\nx 3\ny 4\nmsg\nKeep out.\nendmsg\nend\n"
        + "arch bare_chest\nx 3\ny 4\narch sword\nnrof 1\nend\narch banana\nnrof 1\nend\nend\n"
        + "arch bare_chest\nx 3\ny 4\nnrof 2\nend\n"
        + "arch sign\nx 3\ny 4\nfood 1\nmsg\nOnce.\nendmsg\nend\n"
        + "arch bare_chest\nx 3\ny 4\nnrof 1\nend\n";

    public const int GroundSign = 0;
    public const int GroundChest = 1;
    public const int GroundEmptyChests = 2;
    public const int GroundCountedSign = 3;
    public const int GroundOneEmptyChest = 4;

    public const int OpApply = 11900;

    /// <summary>
    /// The test lane's function <c>t6dapply(account, target)</c>: <c>manual_apply</c>'s ask of the lane for the object (an item id of the
    /// player's tree, or the handle of an object of a level); answers what <c>ApplyByLane</c> answers (nil when no lane owns the type).
    /// </summary>
    public static void Register(ManifestBuilder b)
    {
        b.AddFunction(Lane.Wave6D, new FunctionDef(OpApply, "t6dapply", new[] { "string", "any" }, "any", Handler: Apply));
    }

    private static Outcome Apply(Dispatcher d, IReadOnlyList<Val> a)
    {
        Player p = d.ControllerOf(a[0].AsString)!;
        CfObject? ob = a[1].Kind == ValKind.Handle
            ? ((LevelObject)d.HandlesOfTests.Resolve(a[1].AsHandle)!).Ob
            : d.FindItemById(p.State, a[1].AsInt, deep: true);
        int? answer = d.ApplyByLane(p.State.Ob, ob!, 0);
        return Outcome.Ok(answer == null ? Val.Nil : Val.Int(answer.Value));
    }

    public static TempFolder MakeWorld(string account, string inventory, string header = "")
    {
        TempFolder world = TestWorld.Make(arc: Arc);
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts"));
        foreach (string c in ObjectWorld.Classes)
        {
            File.WriteAllText(Path.Combine(world.Path, "scripts", c + ".txt"), "// class\n");
        }
        Directory.CreateDirectory(Path.Combine(world.Path, "mudlevels"));
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall"), HallSeed);
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "annex"), "arch map\nname annex\nwidth 8\nheight 8\nend\n");
        File.WriteAllText(Path.Combine(world.Path, "treasures"), Treasures);
        ItemWorld.Account(world, account, header, inventory);
        return world;
    }

    public static ExtensionOptions Options(FileWriterOptions? writer = null) => new()
    {
        Writer = writer ?? new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = Register,
        Gk = new GkSettings(),
    };
}

public sealed class ReadableTests
{
    private sealed class Game : IDisposable
    {
        public Game(string inventory = "", ExtensionOptions? options = null, string header = "")
        {
            Ext = new DirectExt(ReadableWorld.MakeWorld("bob", inventory, header), options ?? ReadableWorld.Options(), 1, true, 16_777_216, 1);
            Assert.Equal(0UL, Ext.Call(5, new[] { S("bob") }, player: "bob").Status);
            Hall = Say.LoadAll(Ext, "hall").Places.Select(p => p.Get("handle")!.AsHandle).ToList();
            // the literacy skill is readied by lane 6B's apply_special; a stand-in readies it here
            Ext.Dispatcher.SkillsOfTests!.ApplySpecial = (who, item, flag) =>
            {
                who.ChosenSkill = flag == 1 ? item : null;
                return 0;
            };
            Feet();
            Assert.Equal(0UL, Ext.Call(3).Status); // what login made dirty is written: a file that changes now is changed by the apply
        }

        public DirectExt Ext { get; }

        public List<ulong> Hall { get; }

        public Player Control => Ext.Dispatcher.ControllerOf("bob")!;

        public CfObject Pl => Control.State.Ob;

        public CfWorld World => Ext.Dispatcher.WorldOrNull!;

        /// <summary>The player's feet, as the host answers <c>player.pos</c>.</summary>
        public FakeHost Feet(string level = "hall", double x = 5.5, double y = 6.5)
        {
            var host = new FakeHost();
            host.Answer = (name, _) => name == "player.pos" ? new QueryAnswer(Status.Ok, Val.List(S(level), N(x), N(y))) : new QueryAnswer(Status.Ok, Val.List());
            Ext.Dispatcher.QuerySource = host.Ask;
            return host;
        }

        /// <summary>The first object of the pack (top level) of that name.</summary>
        public CfObject Item(string name)
        {
            for (CfObject? c = Pl.Inv; c != null; c = c.Below)
            {
                if (c.Name == name)
                {
                    return c;
                }
            }
            throw new InvalidOperationException("no " + name);
        }

        public List<string> Names() => Names(Pl);

        public static List<string> Names(CfObject holder)
        {
            var names = new List<string>();
            for (CfObject? c = holder.Inv; c != null; c = c.Below)
            {
                names.Add(c.Name + (c.Nrof > 1 ? " x" + c.Nrof : ""));
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        public ulong Id(CfObject item) => Ext.Dispatcher.HandlesOfTests.TagOf(Ext.Dispatcher.ItemProxy(item));

        public ReplyFrame Apply(CfObject item) =>
            Ext.Call(ReadableWorld.OpApply, new[] { S("bob"), Val.Int((long)Id(item)) }, level: "hall", player: "bob");

        public ReplyFrame ApplyGround(int index) =>
            Ext.Call(ReadableWorld.OpApply, new[] { S("bob"), H(Hall[index]) }, level: "hall", player: "bob");

        public void Dispose() => Ext.Dispose();
    }

    private static List<string> Lines(ReplyFrame r) =>
        r.Events.Where(e => e.Get("name")!.AsString == "onRPGMessage").Select(e => e.Get("args")!.Items[0].AsString)
            .Concat(Effects(r, "client.text").Select(e => e.Get("text")!.AsString)).ToList();

    private static long Answer(ReplyFrame r)
    {
        Assert.Equal(0UL, r.Status);
        return r.Result.AsInt;
    }

    private static string Account(Game g, string contains) => Flushed(g.Ext, "accounts/bob.txt", text => text.Contains(contains, StringComparison.Ordinal));

    // ------------------------------------------------------------------ signs

    [Fact]
    public void ApplySign_Given_a_sign_without_a_message_When_applied_Then_nothing_is_written_on_it()
    {
        // Arrange
        using var g = new Game("arch sign\nend\n");

        // Act
        ReplyFrame r = g.Apply(g.Item("sign"));

        // Assert
        Assert.Equal(1, Answer(r));
        Assert.Equal(new[] { "Nothing is written on it." }, Lines(r));
    }

    [Fact]
    public void ApplySign_Given_a_sign_with_a_message_When_applied_Then_the_message_is_one_line_of_text()
    {
        // Arrange
        using var g = new Game("arch sign\nmsg\nBeware.\nOf the dog.\nendmsg\nend\n");

        // Act
        ReplyFrame r = g.Apply(g.Item("sign"));

        // Assert: C's new_draw_info is one call for the whole message
        Assert.Equal(1, Answer(r));
        Assert.Equal(new[] { "Beware.\nOf the dog.\n" }, Lines(r));
    }

    [Fact]
    public void ApplySign_Given_a_sign_with_a_counter_When_applied_often_Then_it_says_so_after_the_count_and_the_count_is_saved()
    {
        // Arrange
        using var g = new Game("arch sign\nfood 2\nmsg\nHello.\nendmsg\nend\n");
        CfObject sign = g.Item("sign");

        // Act
        ReplyFrame first = g.Apply(sign);
        ReplyFrame second = g.Apply(sign);
        ReplyFrame third = g.Apply(sign);

        // Assert
        Assert.Equal(new[] { "Hello.\n" }, Lines(first));
        Assert.Equal(new[] { "Hello.\n" }, Lines(second));
        Assert.Equal(new[] { "You cannot read it anymore." }, Lines(third));
        Assert.Equal(2, sign.LastEat);
        Assert.Contains("last_eat 2", Account(g, "last_eat 2"));
    }

    [Fact]
    public void ApplySign_Given_a_magic_mouth_with_a_counter_that_is_used_up_When_applied_Then_it_is_silent()
    {
        // Arrange
        using var g = new Game("arch sign\nfood 1\nlast_eat 1\nwalk_on 1\nmsg\nHello.\nendmsg\nend\n");

        // Act
        ReplyFrame r = g.Apply(g.Item("sign"));

        // Assert
        Assert.Equal(1, Answer(r));
        Assert.Empty(Lines(r));
    }

    [Fact]
    public void ApplySign_Given_a_blind_player_When_he_applies_a_sign_Then_he_cannot_read_it_but_the_counter_counts()
    {
        // Arrange
        using var g = new Game("arch sign\nfood 3\nmsg\nHello.\nendmsg\nend\n");
        g.Pl.SetFlag(Define.FLAG_BLIND);
        CfObject sign = g.Item("sign");

        // Act
        ReplyFrame r = g.Apply(sign);

        // Assert: C counts the reading before it looks at the eyes
        Assert.Equal(new[] { "You are unable to read while blind." }, Lines(r));
        Assert.Equal(1, sign.LastEat);
    }

    [Fact]
    public void ApplySign_Given_a_blind_player_and_a_magic_mouth_or_a_wizard_When_applied_Then_it_is_read()
    {
        // Arrange
        using var g = new Game("arch sign\nwalk_on 1\nmsg\nBoo.\nendmsg\nend\narch sign\nmsg\nPlain.\nendmsg\nend\n");
        g.Pl.SetFlag(Define.FLAG_BLIND);

        // Act
        CfObject mouthSign = g.Pl.Inv!.Msg == "Boo.\n" ? g.Pl.Inv : g.Pl.Inv.Below!;
        CfObject plain = mouthSign == g.Pl.Inv ? g.Pl.Inv.Below! : g.Pl.Inv;
        ReplyFrame mouth = g.Apply(mouthSign);
        g.Pl.SetFlag(Define.FLAG_WIZ);
        ReplyFrame wizard = g.Apply(plain);

        // Assert
        Assert.Equal(new[] { "Boo.\n" }, Lines(mouth));
        Assert.Equal(new[] { "Plain.\n" }, Lines(wizard));
    }

    [Fact]
    public void ApplySign_Given_a_sign_on_the_ground_When_applied_Then_it_is_read_and_the_level_is_not_written_for_it()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.ApplyGround(ReadableWorld.GroundSign);

        // Assert
        Assert.Equal(1, Answer(r));
        Assert.Equal(new[] { "Keep out.\n" }, Lines(r));
    }

    [Fact]
    public void ApplySign_Given_a_sign_with_a_counter_on_the_ground_When_applied_twice_Then_the_second_time_it_is_used_up_and_the_level_is_written_with_the_count()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame first = g.ApplyGround(ReadableWorld.GroundCountedSign);
        ReplyFrame second = g.ApplyGround(ReadableWorld.GroundCountedSign);

        // Assert
        Assert.Equal(new[] { "Once.\n" }, Lines(first));
        Assert.Equal(new[] { "You cannot read it anymore." }, Lines(second));
        Assert.Contains("last_eat 1", Flushed(g.Ext, "levels/hall.map", text => text.Contains("last_eat 1")));
    }

    // ------------------------------------------------------------------ books

    private const string Reader = "arch literacy\nlevel 3\nend\n";

    private static string Tome(int level, string msg = "Once upon a time.") => "arch tome\nlevel " + level + "\nmsg\n" + msg + "\nendmsg\nend\n";

    [Fact]
    public void ApplyBook_Given_a_book_within_the_readers_level_When_applied_Then_the_title_line_and_the_text_follow()
    {
        // Arrange: the skill's level 3 and five more: a book of level 8 is still read
        using var g = new Game(Reader + Tome(8));

        // Act
        ReplyFrame r = g.Apply(g.Item("tome"));

        // Assert
        Assert.Equal(1, Answer(r));
        Assert.Equal(new[] { "You open the tome and start reading.", "Once upon a time.\n" }, Lines(r));
    }

    [Theory]
    [InlineData(9, "This book is just barely beyond your comprehension.")]
    [InlineData(10, "This book is slightly beyond your comprehension.")]
    [InlineData(11, "This book is beyond your comprehension.")]
    [InlineData(12, "This book is beyond your comprehension.")]
    [InlineData(13, "This book is quite a bit beyond your comprehension.")]
    [InlineData(15, "This book is quite a bit beyond your comprehension.")]
    [InlineData(16, "This book is way beyond your comprehension.")]
    [InlineData(22, "This book is way beyond your comprehension.")]
    [InlineData(23, "This book is totally beyond your comprehension.")]
    [InlineData(40, "This book is totally beyond your comprehension.")]
    public void ApplyBook_Given_a_book_beyond_the_readers_level_When_applied_Then_the_difference_names_the_line(int bookLevel, string line)
    {
        // Arrange: the reader reads up to level 8
        using var g = new Game(Reader + Tome(bookLevel));

        // Act
        ReplyFrame r = g.Apply(g.Item("tome"));

        // Assert
        Assert.Equal(1, Answer(r));
        Assert.Equal(new[] { line }, Lines(r));
    }

    [Fact]
    public void ApplyBook_Given_a_wizard_and_a_book_far_beyond_him_When_applied_Then_he_reads_it()
    {
        // Arrange
        using var g = new Game(Reader + Tome(40));
        g.Pl.SetFlag(Define.FLAG_WIZ);

        // Act
        ReplyFrame r = g.Apply(g.Item("tome"));

        // Assert
        Assert.Equal(new[] { "You open the tome and start reading.", "Once upon a time.\n" }, Lines(r));
    }

    [Fact]
    public void ApplyBook_Given_a_book_without_text_When_applied_Then_it_is_empty()
    {
        // Arrange
        using var g = new Game(Reader + "arch tome\nend\n");

        // Act
        ReplyFrame r = g.Apply(g.Item("tome"));

        // Assert
        Assert.Equal(new[] { "You open the tome and find it empty." }, Lines(r));
    }

    [Fact]
    public void ApplyBook_Given_a_blind_player_When_applied_Then_he_cannot_read_it_even_when_it_is_empty()
    {
        // Arrange
        using var g = new Game(Reader + "arch tome\nend\n");
        g.Pl.SetFlag(Define.FLAG_BLIND);

        // Act
        ReplyFrame r = g.Apply(g.Item("tome"));

        // Assert
        Assert.Equal(new[] { "You are unable to read while blind." }, Lines(r));
    }

    [Fact]
    public void ApplyBook_Given_a_player_without_the_skill_literacy_When_applied_Then_the_symbols_are_strange()
    {
        // Arrange
        using var g = new Game(Tome(1));

        // Act
        ReplyFrame r = g.Apply(g.Item("tome"));

        // Assert
        Assert.Equal(new[] { "You have no knowledge of literacy.", "You are unable to decipher the strange symbols." }, Lines(r));
    }

    [Fact]
    public void ApplyBook_Given_a_book_read_for_the_first_time_When_applied_Then_nothing_is_marked_and_no_experience_is_given()
    {
        // Arrange
        using var g = new Game(Reader + Tome(1));
        CfObject tome = g.Item("tome");
        long exp = g.Pl.Stats.Exp;

        // Act
        g.Apply(tome);

        // Assert: C gives experience once and marks the book (NO_SKILL_IDENT); phase 3 does neither
        Assert.False(tome.QueryFlag(Define.FLAG_NO_SKILL_IDENT));
        Assert.Equal(exp, g.Pl.Stats.Exp);
    }

    [Fact]
    public void SkLevel_Given_a_chosen_skill_without_a_level_and_a_player_without_one_When_asked_Then_the_players_own_level_and_at_least_one()
    {
        // Arrange
        using var g = new Game();
        CfObject skill = new() { Type = Define.SKILL, Level = 0 };
        CfObject pl = new() { Type = Define.PLAYER, Level = 6, ChosenSkill = skill };

        // Act / Assert
        Assert.Equal(6, Readable.SkLevel(g.World, pl));
        skill.Level = 4;
        Assert.Equal(4, Readable.SkLevel(g.World, pl));
        pl.Level = 0;
        pl.ChosenSkill = null;
        Assert.Equal(1, Readable.SkLevel(g.World, pl));
        CfObject monster = new() { Type = Define.MONSTER, Level = 9, ChosenSkill = skill };
        Assert.Equal(9, Readable.SkLevel(g.World, monster));
    }

    // ------------------------------------------------------------------ the types of other players and of housing

    [Fact]
    public void Apply_Given_a_monster_applies_a_book_a_lighter_or_a_chest_When_asked_Then_it_cannot_and_nothing_changes()
    {
        // Arrange
        using var g = new Game("arch tome\nmsg\nx\nendmsg\nend\narch lighter\nend\narch bare_chest\narch sword\nend\nend\n");
        CfObject monster = new() { Type = Define.MONSTER, Name = "orc" };

        // Act / Assert
        foreach (string name in new[] { "tome", "lighter", "bare chest" })
        {
            Assert.Equal(0, g.Ext.Dispatcher.ApplyByLane(monster, g.Item(name), 0)!.Value);
        }
        Assert.Equal(new[] { "bare chest", "lighter", "tome" }, g.Names());
        Assert.NotNull(g.Item("bare chest").Inv);
    }

    [Fact]
    public void Apply_Given_a_monster_applies_a_sign_When_asked_Then_it_is_applied_and_nobody_is_told()
    {
        // Arrange
        using var g = new Game("arch sign\nfood 2\nmsg\nx\nendmsg\nend\n");
        CfObject monster = new() { Type = Define.MONSTER, Name = "orc" };
        CfObject sign = g.Item("sign");

        // Act / Assert
        Assert.Equal(1, g.Ext.Dispatcher.ApplyByLane(monster, sign, 0)!.Value);
        Assert.Equal(1, sign.LastEat);
    }

    [Fact]
    public void Apply_Given_furniture_or_a_house_door_When_applied_Then_it_cannot_be_applied()
    {
        // Arrange
        using var g = new Game("arch furniture\nend\narch door\nend\n");

        // Act
        ReplyFrame furniture = g.Apply(g.Item("furniture"));
        ReplyFrame door = g.Apply(g.Item("door"));

        // Assert
        Assert.Equal(1, Answer(furniture));
        Assert.Equal(new[] { "You can't apply that." }, Lines(furniture));
        Assert.Equal(1, Answer(door));
        Assert.Equal(new[] { "You can't apply that." }, Lines(door));
        Assert.Equal(new[] { "door", "furniture" }, g.Names());
    }

    [Fact]
    public void Apply_Given_a_type_of_no_lane_of_this_one_When_asked_Then_the_answer_is_nil()
    {
        // Arrange
        using var g = new Game("arch sword\nend\narch tool\nend\n");

        // Act / Assert: swords (15) and the tools of 162 are not this lane's
        Assert.True(g.Apply(g.Item("sword")).Result.IsNil);
        Assert.True(g.Apply(g.Item("tool")).Result.IsNil);
    }

    // ------------------------------------------------------------------ lighters

    private static void Roll(Game g, params int[] rolls)
    {
        int next = 0;
        g.World.Random = () => rolls[Math.Min(next++, rolls.Length - 1)];
    }

    /// <summary>The lines of an account whose stats were rolled, so that fix_player works on it fully.</summary>
    private const string Rolled = "mudorigstats 14 12 10 9 7 8 11\nmudlevhp 8 8 8 8 8 8 8 8 8 8\nmudlevsp 3 3 3 3 3 3 3 3 3 3\nmudlevgrace 2 2 2 2 2 2 2 2 2 2\nlevel 1\n";

    private static void Strong(Game g) => g.Pl.Stats.Dam = 10; // a weak attack saves what it hits (did_make_save_item)

    [Fact]
    public void ApplyLighter_Given_nothing_marked_When_applied_Then_the_player_is_told_to_mark_something()
    {
        // Arrange
        using var g = new Game("arch lighter\nend\n");

        // Act
        ReplyFrame r = g.Apply(g.Item("lighter"));

        // Assert
        Assert.Equal(1, Answer(r));
        Assert.Equal(new[] { "You need to mark a lightable object." }, Lines(r));
    }

    [Fact]
    public void ApplyLighter_Given_a_marked_torch_that_burns_When_lit_Then_it_becomes_the_lit_torch_and_a_charge_is_used()
    {
        // Arrange: the roll 1 does not save a torch of wood (fire save 13)
        using var g = new Game("arch lighter\nlast_eat 1\nfood 3\nend\narch torch\nend\n", header: Rolled);
        CfObject lighter = g.Item("lighter");
        g.Control.Mark = g.Item("torch");
        g.Control.MarkCount = g.Item("torch").Count;
        Strong(g);
        Roll(g, 0);

        // Act
        ReplyFrame r = g.Apply(lighter);

        // Assert
        Assert.Equal(new[] { "You light the torch with the lighter." }, Lines(r));
        Assert.Equal(3, g.Pl.GlowRadius); // fix_player ran after the lit torch came in: the player shines with it
        Assert.Equal(new[] { "lighter", "lit torch" }, g.Names()); // a torch that glows is replaced by its other_arch: no burnout
        Assert.Equal(2, lighter.Stats.Food);
        Assert.True(g.Item("lit torch").QueryFlag(Define.FLAG_IDENTIFIED)); // made by NewItem
        Assert.Contains("food 2", Account(g, "food 2"));
    }

    [Fact]
    public void ApplyLighter_Given_a_torch_that_saves_When_lit_Then_it_stays_and_the_player_fails_but_the_charge_is_gone()
    {
        // Arrange: the roll 20 saves the wood
        using var g = new Game("arch lighter\nlast_eat 1\nfood 3\nend\narch torch\nend\n");
        CfObject lighter = g.Item("lighter");
        g.Control.Mark = g.Item("torch");
        g.Control.MarkCount = g.Item("torch").Count;
        Strong(g);
        Roll(g, 19);

        // Act
        ReplyFrame r = g.Apply(lighter);

        // Assert
        Assert.Equal(new[] { "You attempt to light the torch with the lighter and fail." }, Lines(r));
        Assert.Equal(new[] { "lighter", "torch" }, g.Names());
        Assert.Equal(2, lighter.Stats.Food);
    }

    [Fact]
    public void ApplyLighter_Given_a_lighter_with_no_charge_left_When_applied_Then_it_is_used_up_and_nothing_burns()
    {
        // Arrange
        using var g = new Game("arch lighter\nlast_eat 1\nend\narch torch\nend\n");
        g.Control.Mark = g.Item("torch");
        g.Control.MarkCount = g.Item("torch").Count;
        Strong(g);
        Roll(g, 0);

        // Act
        ReplyFrame r = g.Apply(g.Item("lighter"));

        // Assert
        Assert.Equal(new[] { "You attempt to light the torch with a used up lighter." }, Lines(r));
        Assert.Equal(new[] { "lighter", "torch" }, g.Names());
    }

    [Fact]
    public void ApplyLighter_Given_a_lighter_without_charges_When_applied_Then_it_never_runs_out()
    {
        // Arrange
        using var g = new Game("arch lighter\nfood 5\nend\narch torch\nend\n");
        CfObject lighter = g.Item("lighter");
        g.Control.Mark = g.Item("torch");
        g.Control.MarkCount = g.Item("torch").Count;
        Strong(g);
        Roll(g, 0);

        // Act
        g.Apply(lighter);

        // Assert: last_eat is 0, so the food is no counter
        Assert.Equal(5, lighter.Stats.Food);
        Assert.Contains("lit torch", g.Names());
    }

    [Fact]
    public void ApplyLighter_Given_a_stack_of_lighters_with_charges_When_applied_Then_one_is_split_off_and_loses_the_charge()
    {
        // Arrange
        using var g = new Game("arch lighter\nnrof 2\nlast_eat 1\nfood 4\nend\narch torch\nend\n");
        CfObject stack = g.Item("lighter");
        g.Control.Mark = g.Item("torch");
        g.Control.MarkCount = g.Item("torch").Count;
        Strong(g);
        Roll(g, 0);

        // Act
        ReplyFrame r = g.Apply(stack);

        // Assert
        Assert.Equal(new[] { "You light the torch with the lighter." }, Lines(r));
        List<CfObject> lighters = new();
        for (CfObject? c = g.Pl.Inv; c != null; c = c.Below)
        {
            if (c.Name == "lighter")
            {
                lighters.Add(c);
            }
        }
        Assert.Equal(new[] { (1u, 4), (1u, 3) }, lighters.Select(l => (l.Nrof, (int)l.Stats.Food)).OrderByDescending(t => t.Item2));
    }

    [Fact]
    public void ApplyLighter_Given_a_paper_stack_that_burns_When_lit_Then_a_random_part_of_it_burns_and_the_burnout_comes()
    {
        // Arrange: paper (fire save 17): the roll 1 fails the save; rndm(0, 4) of the second roll
        using var g = new Game("arch lighter\nend\narch paper\nnrof 5\nend\n");
        g.Control.Mark = g.Item("paper");
        g.Control.MarkCount = g.Item("paper").Count;
        Strong(g);
        Roll(g, 0, 2); // the throw, then 2 % 5 + 0 papers go

        // Act
        ReplyFrame r = g.Apply(g.Item("lighter"));

        // Assert
        Assert.Equal(new[] { "You light the paper with the lighter." }, Lines(r));
        Assert.Equal(new[] { "burnout", "lighter", "paper x3" }, g.Names());
    }

    [Theory]
    [InlineData("lamp")]
    [InlineData("tinder")]
    public void ApplyLighter_Given_an_object_with_only_one_of_other_arch_and_glow_radius_When_it_burns_Then_it_is_no_light_and_the_burnout_comes(string arch)
    {
        // Arrange: a light needs both (a glow_radius and an other_arch to change to); the roll 1 fails the save of wood
        using var g = new Game("arch lighter\nend\narch " + arch + "\nend\n");
        g.Control.Mark = g.Item(arch);
        g.Control.MarkCount = g.Item(arch).Count;
        Strong(g);
        Roll(g, 0);

        // Act
        ReplyFrame r = g.Apply(g.Item("lighter"));

        // Assert
        Assert.Equal(new[] { "You light the " + arch + " with the lighter." }, Lines(r));
        Assert.Equal(new[] { "burnout", "lighter" }, g.Names());
    }

    [Fact]
    public void ApplyLighter_Given_a_paper_stack_where_none_burns_When_lit_Then_the_player_fails_and_the_burnout_comes_all_the_same()
    {
        // Arrange
        using var g = new Game("arch lighter\nend\narch paper\nnrof 5\nend\n");
        g.Control.Mark = g.Item("paper");
        g.Control.MarkCount = g.Item("paper").Count;
        Strong(g);
        Roll(g, 0, 5); // 5 % 5: nothing (a range one too large would take 5 % 6 = 5, all of them)

        // Act
        ReplyFrame r = g.Apply(g.Item("lighter"));

        // Assert: C's save_throw_object decreases by rndm(0, nrof - 1) and puts the burnout in any case
        Assert.Equal(new[] { "You attempt to light the paper with the lighter and fail." }, Lines(r));
        Assert.Equal(new[] { "burnout", "lighter", "paper x5" }, g.Names());
    }

    [Fact]
    public void ApplyLighter_Given_two_papers_that_burn_When_lit_Then_one_of_them_burns()
    {
        // Arrange
        using var g = new Game("arch lighter\nend\narch paper\nnrof 2\nend\n");
        g.Control.Mark = g.Item("paper");
        g.Control.MarkCount = g.Item("paper").Count;
        Strong(g);
        Roll(g, 0, 1); // rndm(0, 1) of 1

        // Act
        ReplyFrame r = g.Apply(g.Item("lighter"));

        // Assert: a stack of two is a stack
        Assert.Equal(new[] { "You light the paper with the lighter." }, Lines(r));
        Assert.Equal(new[] { "burnout", "lighter", "paper" }, g.Names());
    }

    [Fact]
    public void ApplyLighter_Given_a_single_paper_that_burns_When_lit_Then_it_is_gone_and_its_string_goes()
    {
        // Arrange
        using var g = new Game("arch lighter\nend\narch paper\nend\n");
        CfObject paper = g.Item("paper");
        ulong id = g.Id(paper);
        g.Control.Mark = paper;
        g.Control.MarkCount = paper.Count;
        Strong(g);
        Roll(g, 0);

        // Act
        ReplyFrame r = g.Apply(g.Item("lighter"));

        // Assert
        Assert.Equal(new[] { "You light the paper with the lighter." }, Lines(r));
        Assert.Equal(new[] { "burnout", "lighter" }, g.Names());
        Assert.Contains(Effects(r, "client.del"), e => e.Get("name")!.AsString == "clientr.mud_item" + (uint)id);
        Assert.Null(ItemMatching.FindMarkedObject(g.Pl));
    }

    // ------------------------------------------------------------------ the saving throw

    private sealed class Rolls
    {
        private readonly Queue<int> values;

        public Rolls(params int[] values) => this.values = new Queue<int>(values);

        public int Next() => values.Count > 0 ? values.Dequeue() : 0;
    }

    private static bool Saves(CfObject op, int dam, params int[] rolls)
    {
        var w = new CfWorld(new ExamineHost());
        w.Random = new Rolls(rolls).Next;
        CfObject who = new() { Type = Define.PLAYER };
        who.Stats.Dam = (short)dam;
        return Lighter.DidMakeSaveItemFire(w, op, who);
    }

    private static CfObject Made(ushort material, int magic = 0, int resist = 0)
    {
        var op = new CfObject { Material = material, Magic = (sbyte)magic };
        op.Resist[Define.ATNR_FIRE] = (short)resist;
        return op;
    }

    private const ushort Wood = 1 << 4;
    private const ushort PaperBit = 1 << 0;

    [Fact]
    public void DidMakeSaveItemFire_Given_an_object_immune_to_fire_or_of_no_material_When_thrown_Then_it_saves_without_a_roll()
    {
        // Act / Assert
        Assert.True(Saves(Made(Wood, resist: 100), 10, 0));
        Assert.True(Saves(Made(0), 10, 0));
    }

    [Fact]
    public void DidMakeSaveItemFire_Given_wood_When_the_roll_is_at_least_its_fire_save_Then_it_saves()
    {
        // Arrange: wood saves against fire on 13 (rndm(1, 20) is Random % 20 + 1)
        // Act / Assert
        Assert.True(Saves(Made(Wood), 10, 12)); // roll 13
        Assert.False(Saves(Made(Wood), 10, 11)); // roll 12
    }

    [Fact]
    public void DidMakeSaveItemFire_Given_magic_and_resistance_When_thrown_Then_the_numbers_move_the_save()
    {
        // Act / Assert: magic +2 saves a roll of 11; a resistance of -100 (a vulnerability) needs 14
        Assert.True(Saves(Made(Wood, magic: 2), 10, 10)); // roll 11 against 11
        Assert.False(Saves(Made(Wood, magic: 2), 10, 9)); // roll 10
        Assert.False(Saves(Made(Wood, resist: -100), 10, 12)); // roll 13 against 14
        Assert.True(Saves(Made(Wood, resist: -100), 10, 13)); // roll 14
        Assert.True(Saves(Made(Wood, resist: 99), 10, 12)); // 99 / 100 is 0: still 13
        Assert.True(Saves(Made(Wood, resist: 50), 10, 12));
    }

    [Fact]
    public void DidMakeSaveItemFire_Given_a_weak_attack_When_thrown_Then_the_object_saves_each_material_once_more()
    {
        // Act / Assert: wood: (20 - 13) / 3 = 2 is more than a damage of 1, not more than 2
        Assert.True(Saves(Made(Wood), 1, 0));
        Assert.False(Saves(Made(Wood), 2, 0));
    }

    [Fact]
    public void DidMakeSaveItemFire_Given_two_materials_When_one_saves_Then_a_second_roll_decides_between_them()
    {
        // Arrange: paper (fire save 17, the first material of the table) and wood (13): the first roll saves the paper (19 + 1), the second
        // fails the wood (0 + 1). Act / Assert: saves 1 of 2; rndm(1, 2) of 2 is more than 1: not saved; of 1: saved
        Assert.False(Saves(Made(Wood | PaperBit), 10, 19, 0, 1)); // the third roll: 1 % 2 + 1 = 2
        Assert.True(Saves(Made(Wood | PaperBit), 10, 19, 0, 0)); // 0 % 2 + 1 = 1
        // both fail: nothing saved, burns without a third roll; both save: saved
        Assert.False(Saves(Made(Wood | PaperBit), 10, 0, 0));
        Assert.True(Saves(Made(Wood | PaperBit), 10, 19, 19));
    }

    // ------------------------------------------------------------------ treasure chests

    [Fact]
    public void ApplyTreasure_Given_a_chest_in_the_pack_When_applied_Then_each_thing_is_found_and_placed_at_his_feet_and_the_chest_is_gone()
    {
        // Arrange
        using var g = new Game("arch bare_chest\narch sword\nnrof 1\nend\narch banana\nnrof 3\nend\nend\n");
        CfObject chest = g.Item("bare chest");
        ulong id = g.Id(chest);

        // Act
        ReplyFrame r = g.Apply(chest);

        // Assert
        Assert.Equal(1, Answer(r));
        Assert.Equal(new[] { "You find sword in the chest.", "You find bananas in the chest." }, Lines(r));
        Assert.Equal(2, Effects(r, "npc.place").Count);
        Assert.All(Effects(r, "npc.place"), p =>
        {
            Assert.Equal("hall", p.Get("level")!.AsString);
            Assert.Equal(5.5, p.Get("x")!.AsFloat);
            Assert.Equal(6.5, p.Get("y")!.AsFloat);
        });
        Assert.Empty(g.Names());
        Assert.Contains(Effects(r, "client.del"), e => e.Get("name")!.AsString == "clientr.mud_item" + (uint)id);
    }

    [Fact]
    public void ApplyTreasure_Given_a_chest_in_the_pack_When_applied_Then_the_account_is_written_before_the_level()
    {
        // Arrange
        var order = new List<string>();
        var writer = new FileWriterOptions
        {
            Quiet = TimeSpan.FromHours(1),
            MaxDelay = TimeSpan.FromHours(1),
            BetweenMove = (source, destination) => order.Add(source + ">" + destination),
        };
        using var g = new Game("arch bare_chest\narch sword\nnrof 1\nend\nend\n", ReadableWorld.Options(writer));

        // Act
        g.Apply(g.Item("bare chest"));
        for (int i = 0; i < 500 && order.Count == 0; i++)
        {
            Thread.Sleep(10);
        }

        // Assert
        Assert.Equal(new[] { "accounts/bob.txt>levels/hall.map" }, order);
    }

    [Fact]
    public void ApplyTreasure_Given_a_chest_that_holds_nothing_When_applied_Then_it_was_empty_and_it_is_gone()
    {
        // Arrange
        using var g = new Game("arch bare_chest\nend\narch sword\nend\n");

        // Act
        ReplyFrame r = g.Apply(g.Item("bare chest"));

        // Assert
        Assert.Equal(new[] { "The chest was empty." }, Lines(r));
        Assert.Equal(new[] { "sword" }, g.Names());
        Assert.Empty(Effects(r, "npc.place"));
    }

    [Fact]
    public void ApplyTreasure_Given_a_stack_of_empty_chests_When_applied_Then_one_goes()
    {
        // Arrange
        using var g = new Game("arch bare_chest\nnrof 3\nend\n");

        // Act
        ReplyFrame r = g.Apply(g.Item("bare chest"));

        // Assert
        Assert.Equal(new[] { "The chest was empty." }, Lines(r));
        Assert.Equal(new[] { "bare chest x2" }, g.Names());
    }

    [Fact]
    public void ApplyTreasure_Given_no_level_at_his_feet_When_applied_Then_nothing_changes_and_he_cannot_drop_that_here()
    {
        // Arrange
        using var g = new Game("arch bare_chest\narch sword\nnrof 1\nend\nend\n");
        g.Feet("nowhere");
        CfObject chest = g.Item("bare chest");

        // Act
        ReplyFrame r = g.Apply(chest);

        // Assert
        Assert.Equal(1, Answer(r));
        Assert.Equal(new[] { "You can't drop that here." }, Lines(r));
        Assert.Same(chest, g.Item("bare chest"));
        Assert.Equal(new[] { "sword" }, Game.Names(chest));
        Assert.Empty(Effects(r, "npc.place"));
    }

    [Fact]
    public void ApplyTreasure_Given_the_host_does_not_answer_the_position_When_applied_Then_nothing_changes()
    {
        // Arrange
        using var g = new Game("arch bare_chest\narch sword\nnrof 1\nend\nend\n");
        FakeHost host = g.Feet();
        host.Answer = (_, _) => new QueryAnswer(Status.Internal, Val.Nil);
        CfObject chest = g.Item("bare chest");

        // Act
        ReplyFrame r = g.Apply(chest);

        // Assert
        Assert.Equal(new[] { "You can't drop that here." }, Lines(r));
        Assert.Equal(new[] { "sword" }, Game.Names(chest));
    }

    [Fact]
    public void ApplyTreasure_Given_a_chest_nobody_has_filled_When_applied_Then_its_treasure_is_made_identified_and_comes_out()
    {
        // Arrange: the list is a sword and two bananas; hp 1 makes it once
        using var g = new Game("arch chest\nend\n");
        Roll(g, 1); // two bananas: rndm of nrof 2
        CfObject chest = g.Item("chest");

        // Act
        ReplyFrame r = g.Apply(chest);

        // Assert
        Assert.Equal(new[] { "You find bananas in the chest.", "You find swords in the chest." }, Lines(r).Order(StringComparer.Ordinal));
        Assert.Equal(2, Effects(r, "npc.place").Count);
        Assert.Empty(g.Names());
        foreach (Val place in Effects(r, "npc.place"))
        {
            var face = (LevelObject)g.Ext.Dispatcher.HandlesOfTests.Resolve(place.Get("handle")!.AsHandle)!;
            Assert.True(face.Ob.QueryFlag(Define.FLAG_IDENTIFIED)); // made by the engine: identified
        }
    }

    [Fact]
    public void ApplyTreasure_Given_a_chest_with_a_list_that_holds_something_When_applied_Then_only_that_comes_out()
    {
        // Arrange: a chest read from a file keeps what the file says
        using var g = new Game("arch chest\narch sword\nnrof 1\nend\nend\n");

        // Act
        ReplyFrame r = g.Apply(g.Item("chest"));

        // Assert
        Assert.Equal(new[] { "You find sword in the chest." }, Lines(r));
    }

    [Fact]
    public void ApplyTreasure_Given_a_chest_filled_once_and_emptied_When_applied_again_Then_it_is_empty_because_its_count_was_used()
    {
        // Arrange: the player keeps two chests of one stack: the first apply fills, empties and removes one
        using var g = new Game("arch chest\nnrof 2\nend\n");
        CfObject chest = g.Item("chest");

        // Act
        ReplyFrame first = g.Apply(chest);

        // Assert: the stack is filled (hp 1) and emptied by the first apply, and one chest of it goes
        Assert.Equal(2, Effects(first, "npc.place").Count);
        Assert.Equal(new[] { "chest" }, g.Names());
    }

    [Fact]
    public void ApplyTreasure_Given_a_chest_that_an_automatic_apply_would_open_When_applied_Then_it_is_empty()
    {
        // Arrange
        using var g = new Game("arch auto_chest\nend\n");

        // Act
        ReplyFrame r = g.Apply(g.Item("auto chest"));

        // Assert: C's fix_auto_apply fills such a template at a map load; it is no chest to fill later
        Assert.Equal(new[] { "The chest was empty." }, Lines(r));
    }

    [Fact]
    public void ApplyTreasure_Given_a_chest_in_a_bag_When_applied_Then_it_leaves_the_bag()
    {
        // Arrange
        using var g = new Game("arch bag\narch bare_chest\narch sword\nnrof 1\nend\nend\nend\n");
        CfObject bag = g.Item("bag");

        // Act
        ReplyFrame r = g.Apply(bag.Inv!);

        // Assert
        Assert.Equal(new[] { "You find sword in the chest." }, Lines(r));
        Assert.Empty(Game.Names(bag));
        Assert.Single(Effects(r, "npc.place"));
    }

    [Fact]
    public void ApplyTreasure_Given_a_chest_on_the_ground_When_applied_Then_its_things_are_at_his_feet_and_its_face_is_gone()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.ApplyGround(ReadableWorld.GroundChest);

        // Assert
        Assert.Equal(1, Answer(r));
        Assert.Equal(new[] { "You find banana in the chest.", "You find sword in the chest." }, Lines(r).Order(StringComparer.Ordinal));
        Assert.Equal(2, Effects(r, "npc.place").Count);
        Assert.Equal(new[] { g.Hall[ReadableWorld.GroundChest] }, Effects(r, "npc.remove").Select(e => e.Get("handle")!.AsHandle));
        Assert.Equal((ulong)Status.BadHandle, g.Ext.Call(9, new[] { H(g.Hall[ReadableWorld.GroundChest]), Val.Int(Obj("mudrealname")) }).Status); // the handle is dead
    }

    [Fact]
    public void ApplyTreasure_Given_an_empty_chest_of_a_stack_of_one_on_the_ground_When_applied_Then_it_goes_with_its_face()
    {
        // Arrange: "nrof 1" is a stack of one, which is not the same as C's nrof 0 for a single object, and goes the same way
        using var g = new Game();

        // Act
        ReplyFrame r = g.ApplyGround(ReadableWorld.GroundOneEmptyChest);

        // Assert
        Assert.Equal(new[] { "The chest was empty." }, Lines(r));
        Assert.Equal(new[] { g.Hall[ReadableWorld.GroundOneEmptyChest] }, Effects(r, "npc.remove").Select(e => e.Get("handle")!.AsHandle));
        Assert.Equal((ulong)Status.BadHandle, g.Ext.Call(9, new[] { H(g.Hall[ReadableWorld.GroundOneEmptyChest]), Val.Int(Obj("mudrealname")) }).Status);
    }

    [Fact]
    public void ApplyTreasure_Given_a_stack_of_empty_chests_on_the_ground_When_applied_Then_one_goes_and_the_script_hears_of_it()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.ApplyGround(ReadableWorld.GroundEmptyChests);

        // Assert
        Assert.Equal(new[] { "The chest was empty." }, Lines(r));
        Assert.Empty(Effects(r, "npc.remove"));
        Assert.Contains(r.Events, e => e.Get("name")!.AsString == "onUpdateObject");
    }

    [Fact]
    public void ApplyTreasure_Given_a_chest_on_the_ground_and_the_player_in_another_level_When_applied_Then_the_things_go_there_and_the_files_are_written_in_order()
    {
        // Arrange
        var order = new List<string>();
        var writer = new FileWriterOptions
        {
            Quiet = TimeSpan.FromHours(1),
            MaxDelay = TimeSpan.FromHours(1),
            BetweenMove = (source, destination) => order.Add(source + ">" + destination),
        };
        using var g = new Game("", ReadableWorld.Options(writer));
        Assert.Equal(0UL, g.Ext.Call(7, new[] { S("annex"), Val.Nil }, level: "annex").Status);
        g.Feet("annex", 2.5, 2.5);

        // Act
        ReplyFrame r = g.ApplyGround(ReadableWorld.GroundChest);
        for (int i = 0; i < 500 && order.Count == 0; i++)
        {
            Thread.Sleep(10);
        }

        // Assert
        Assert.All(Effects(r, "npc.place"), p => Assert.Equal("annex", p.Get("level")!.AsString));
        Assert.Equal(new[] { "levels/hall.map>levels/annex.map" }, order);
    }
}
