// Not ported from a Crossfire file: the tests of lane 7A of wave 7, the <c>mudcommand</c> table (docs/mud-phase3.md section 4.6): the
// function, its parsing, and the ported commands <c>title</c>, <c>peaceful</c>, <c>brace</c>, <c>fix_me</c>, <c>verbose</c>, <c>who</c>,
// <c>tell</c>, <c>save</c>, <c>mark</c> and <c>help</c> (server/c_misc.c, c_chat.c, commands.c), and the page filter of <c>level.load</c>.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>The test function of lane 7A: <c>t7a(action, account, a)</c> calls ported code in a request.</summary>
internal static class CommandWorld
{
    public const int OpTest = 12900;

    public static void Register(ManifestBuilder b)
    {
        PickupWorld.Register(b);
        b.AddFunction(Lane.Wave7A, new FunctionDef(OpTest, "t7a", new[] { "string", "string", "any" }, "any", Handler: Do));
    }

    private static Outcome Do(Dispatcher d, IReadOnlyList<Val> a)
    {
        Player p = d.ControllerOf(a[1].AsString)!;
        d.Pickups.Begin();
        switch (a[0].AsString)
        {
            case "pickunplaced":
            {
                // the first object of the level "big" that no page has placed yet is taken as a take at the feet takes an object
                Level level = d.DropPlaceAt("big", 1, 1, Val.Int(1), Val.Int(1))!.Level;
                LevelObject lo = level.Objects.First(o => !o.Placed);
                return Outcome.Ok(Val.Bool(d.Pickups.PickUp(p.State.Ob, lo.Ob, lo)));
            }
            case "tag": // the tag of the item of the pack with that name, visible or not
            {
                for (CfObject? c = p.State.Ob.Inv; c != null; c = c.Below)
                {
                    if (c.Name == a[2].AsString)
                    {
                        return Outcome.Ok(Val.Int(((IMemberEnv)d).TagOf(d.ItemProxy(c))));
                    }
                }
                return Outcome.Ok(Val.Nil);
            }
            case "title":
                return Outcome.Ok(Val.Str(p.OwnTitle));
            case "braced":
                return Outcome.Ok(Val.Bool(p.Braced));
            default:
                return Outcome.Fail(Status.BadArgs, "unknown action");
        }
    }

    /// <summary>A level with 70 bananas on one square: two pages of <c>level.load</c>.</summary>
    public static string BigSeed()
    {
        var sb = new System.Text.StringBuilder("arch map\nname big\nwidth 8\nheight 8\nend\n");
        for (int i = 0; i < 70; i++)
        {
            sb.Append("arch banana\nx 1\ny 1\nend\n");
        }
        return sb.ToString();
    }

    public static TempFolder Make(string account, string inventory)
    {
        TempFolder world = PickupWorld.MakeWorld(account, inventory);
        File.WriteAllText(Path.Combine(world.Path, "mudlevels", "big"), BigSeed());
        return world;
    }

    public static ExtensionOptions Options(FileWriterOptions? writer = null) => new()
    {
        Writer = writer ?? new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = Register,
        Gk = new GkSettings(),
    };
}

public sealed class CommandTests
{
    private sealed class Game : IDisposable
    {
        public Game(string inventory = "", long protoMinor = 1, ExtensionOptions? options = null)
        {
            Ext = new DirectExt(CommandWorld.Make("bob", inventory), options ?? CommandWorld.Options(), 1, true, 16_777_216, protoMinor);
            Player = Login("bob");
            Hall = Say.LoadAll(Ext, "hall").Places.Select(p => p.Get("handle")!.AsHandle).ToList();
        }

        public DirectExt Ext { get; }

        public ulong Player { get; }

        public List<ulong> Hall { get; }

        public ulong Login(string account)
        {
            Assert.Equal(0UL, Ext.Call(5, new[] { S(account) }, player: account).Status);
            return Ext.Call(5, new[] { S(account) }, player: account).Result.AsHandle;
        }

        public ReplyFrame Mud(string command, string? parameters = null, string account = "bob", string? level = "hall") =>
            Ext.Call(Manifest.OpMudCommand, parameters == null ? new[] { S(command) } : new[] { S(command), S(parameters) }, level: level, player: account);

        /// <summary>Rolls the stats of the player (<c>rollstats</c>): <c>fix_player</c> computes the armour and weapon class only for a player who has them.</summary>
        public void Roll(string account = "bob")
        {
            ulong handle = Ext.Call(5, new[] { S(account) }, player: account).Result.AsHandle;
            Assert.Equal(0UL, Ext.Call(13, new[] { H(handle), Val.Int(TMudPlayerMembers.MethodRollStats), Val.List() }, player: account).Status);
        }

        public ReplyFrame T(string action, string account = "bob", string? text = null) =>
            Ext.Call(CommandWorld.OpTest, new[] { S(action), S(account), text == null ? Val.Nil : S(text) }, level: "hall", player: account);

        public Val Read(ulong player, string member) => Say.Get(Ext, player, Ply(member));

        public List<string> Lines(string command, string? parameters = null, string account = "bob")
        {
            ReplyFrame r = Mud(command, parameters, account);
            Assert.Equal(0UL, r.Status);
            return CommandTests.Lines(r);
        }

        public void Dispose() => Ext.Dispose();
    }

    private static List<string> Lines(ReplyFrame r, string? account = null) =>
        r.Events.Where(e => e.Get("name")!.AsString == "onRPGMessage" && (account == null || e.Get("player")!.AsString == account))
            .Select(e => e.Get("args")!.Items[0].AsString)
            .Concat(Effects(r, "client.text").Where(e => account == null || e.Get("player")!.AsString == account).Select(e => e.Get("text")!.AsString)).ToList();

    private static FakeHost Feet(Game g, string level = "hall", double x = 3.5, double y = 4.5)
    {
        var host = new FakeHost();
        host.Answer = (name, _) => name == "player.pos" ? new QueryAnswer(Status.Ok, Val.List(S(level), N(x), N(y))) : new QueryAnswer(Status.Ok, Val.List());
        g.Ext.Dispatcher.QuerySource = host.Ask;
        return host;
    }

    // ------------------------------------------------------------------ the function and its parsing

    [Fact]
    public void Manifest_Given_the_lane_When_read_Then_mudcommand_is_a_slow_function_of_op_12000_with_two_strings()
    {
        // Arrange
        using var g = new Game();

        // Act
        FunctionDef def = Manifest.Collect(1).Functions.Single(f => f.Name == "mudcommand");

        // Assert
        Assert.Equal(12000, def.Id);
        Assert.Equal(Manifest.OpMudCommand, def.Id);
        Assert.True(def.Slow);
        Assert.Equal(new[] { "string", "string" }, def.Params);
        Assert.Equal("void", def.Ret);
    }

    [Theory]
    [InlineData("useskill")]
    [InlineData("readyskill")]
    [InlineData("party")]
    [InlineData("blood")]
    [InlineData("search")]
    [InlineData("cast")]
    [InlineData("invoke")]
    [InlineData("dm")]
    [InlineData("xyzzy")]
    public void Mud_Given_a_phase_4_or_unknown_word_When_run_Then_the_text_of_C_with_two_spaces_and_nothing_changes(string word)
    {
        // Arrange
        using var g = new Game(inventory: "arch banana\nnrof 3\nend\n");

        // Act
        ReplyFrame r = g.Mud(word, "leave");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Equal(new[] { "Unknown command.  Try help." }, Lines(r));
        Assert.Empty(Effects(r, "npc.place"));
    }

    [Fact]
    public void Mud_Given_an_empty_command_When_run_Then_it_is_unknown()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("");

        // Assert
        Assert.Equal(new[] { "Unknown command.  Try help." }, lines);
    }

    [Fact]
    public void Mud_Given_the_command_in_capitals_with_spaces_or_with_its_parameters_in_the_first_argument_When_run_Then_it_is_parsed_as_C_does()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> upper = g.Lines("  WHO ");
        List<string> inOne = g.Lines("title   Boss"); // "cmd params" in one argument: the leading spaces of the parameters are taken off
        List<string> read = g.Lines("title");
        List<string> emptyParams = g.Lines("title", "");
        List<string> trailingSpace = g.Lines("title ");

        // Assert
        Assert.Equal(new[] { "Your title is 'Boss'." }, trailingSpace); // the text after the space is empty: no parameters
        Assert.Equal(new[] { "Players:", "bob the human W" }, upper);
        Assert.Empty(inOne);
        Assert.Equal(new[] { "Your title is 'Boss'." }, read);
        Assert.Equal(new[] { "Your title is 'Boss'." }, emptyParams); // an empty text is no parameters
    }

    [Fact]
    public void Mud_Given_the_parameters_in_the_second_argument_with_leading_spaces_When_run_Then_they_are_taken_off()
    {
        // Arrange
        using var g = new Game();

        // Act
        g.Lines("title", "   Boss");
        List<string> read = g.Lines("title");

        // Assert
        Assert.Equal(new[] { "Your title is 'Boss'." }, read);
    }

    [Fact]
    public void Mud_Given_three_arguments_or_none_When_run_Then_bad_args()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame three = g.Ext.Call(Manifest.OpMudCommand, new[] { S("who"), S(""), S("x") }, level: "hall", player: "bob");
        ReplyFrame none = g.Ext.Call(Manifest.OpMudCommand, Array.Empty<Val>(), level: "hall", player: "bob");
        ReplyFrame number = g.Ext.Call(Manifest.OpMudCommand, new[] { Val.Int(3), S("") }, level: "hall", player: "bob");

        // Assert
        Assert.Equal((ulong)Status.BadArgs, three.Status);
        Assert.Equal((ulong)Status.BadArgs, none.Status);
        Assert.Equal((ulong)Status.BadArgs, number.Status);
    }

    [Fact]
    public void Mud_Given_no_player_in_the_context_When_run_Then_it_answers_nil_and_says_nothing()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.Ext.Call(Manifest.OpMudCommand, new[] { S("who"), S("") }, level: "hall");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Events);
        Assert.Empty(r.Effects);
    }

    [Fact]
    public void Mud_Given_a_1_0_link_When_a_command_speaks_Then_the_text_is_a_client_text_effect_and_no_event()
    {
        // Arrange
        using var g = new Game(protoMinor: 0);

        // Act
        ReplyFrame r = g.Mud("who");
        ReplyFrame unknown = g.Mud("party", "leave");

        // Assert
        Assert.Empty(r.Events);
        Assert.Equal(new[] { "Players:", "bob the human W" }, Effects(r, "client.text").Select(e => e.Get("text")!.AsString));
        Assert.Equal(new[] { "Unknown command.  Try help." }, Effects(unknown, "client.text").Select(e => e.Get("text")!.AsString));
    }

    [Fact]
    public void Mud_Given_a_1_1_link_When_a_command_speaks_Then_the_line_is_an_onRPGMessage_event_with_the_text_fallback()
    {
        // Arrange
        using var g = new Game();

        // Act
        ReplyFrame r = g.Mud("party", "leave");

        // Assert
        Val ev = Assert.Single(r.Events);
        Assert.Equal("onRPGMessage", ev.Get("name")!.AsString);
        Assert.Equal("bob", ev.Get("player")!.AsString);
        Assert.Equal("client.text", ev.Get("fallback")!.Get("k")!.AsString);
    }

    // ------------------------------------------------------------------ title

    [Fact]
    public void Title_Given_no_parameters_When_run_Then_it_says_the_name_of_the_archetype()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("title");

        // Assert
        Assert.Equal(new[] { "Your title is 'human'." }, lines);
    }

    [Fact]
    public void Title_Given_a_text_When_set_then_read_then_cleared_Then_C_says_what_it_says()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> set = g.Lines("title", "the Bold");
        List<string> read = g.Lines("title");
        List<string> clear = g.Lines("title", "clear");
        List<string> again = g.Lines("title", "default");

        // Assert
        Assert.Empty(set); // C says nothing when it sets the title
        Assert.Equal(new[] { "Your title is 'the Bold'." }, read);
        Assert.Equal(new[] { "Title set to default." }, clear);
        Assert.Equal(new[] { "Your title is the default title." }, again);
        Assert.Equal("", g.T("title").Result.AsString);
    }

    [Theory]
    [InlineData("123456789012345", false)]
    [InlineData("1234567890123456", true)]
    public void Title_Given_the_longest_text_When_set_Then_a_title_of_MAX_NAME_characters_is_too_long(string title, bool tooLong)
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("title", title);

        // Assert
        Assert.Equal(tooLong ? new[] { "Title too long." } : Array.Empty<string>(), lines);
        Assert.Equal(tooLong ? "" : title, g.T("title").Result.AsString);
    }

    [Fact]
    public void Title_Given_a_dragon_player_When_run_Then_he_cannot_change_titles()
    {
        // Arrange
        using var g = new Game();
        g.Ext.Dispatcher.ControllerOf("bob")!.State.Ob.Arch!.Clone.Race = "dragon";

        // Act
        List<string> set = g.Lines("title", "Wyrm");
        List<string> read = g.Lines("title");

        // Assert
        Assert.Equal(new[] { "Dragons cannot change titles." }, set);
        Assert.Equal(new[] { "Dragons cannot change titles." }, read);
        Assert.Equal("", g.T("title").Result.AsString);
    }

    // ------------------------------------------------------------------ peaceful, brace, fix_me, verbose

    [Fact]
    public void Peaceful_Given_the_command_When_run_twice_Then_it_toggles_the_member_and_says_so()
    {
        // Arrange
        using var g = new Game();
        Assert.False(g.Read(g.Player, "peaceful").AsBool);

        // Act
        List<string> first = g.Lines("peaceful");
        bool afterFirst = g.Read(g.Player, "peaceful").AsBool;
        List<string> second = g.Lines("peaceful");
        bool afterSecond = g.Read(g.Player, "peaceful").AsBool;

        // Assert
        Assert.Equal(new[] { "You will not attack other players." }, first);
        Assert.True(afterFirst);
        Assert.Equal(new[] { "You will attack other players." }, second);
        Assert.False(afterSecond);
    }

    [Fact]
    public void Peaceful_Given_the_command_When_run_Then_the_account_is_written_with_the_state()
    {
        // Arrange
        using var g = new Game();

        // Act
        g.Lines("peaceful");
        string file = Flushed(g.Ext, "accounts/bob.txt", t => t.Contains("mudpeaceful 1", StringComparison.Ordinal));

        // Assert
        Assert.Contains("mudpeaceful 1", file);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("on", true)]
    [InlineData("off", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    [InlineData("kylla", true)]
    [InlineData("sure", true)]
    [InlineData("dunno", true)]
    [InlineData("ei", false)]
    [InlineData("unlikely", false)]
    [InlineData("o", false)]
    [InlineData("7 times", true)]
    [InlineData("-1", true)]
    public void Brace_Given_a_parameter_When_run_Then_the_player_is_braced_as_onoff_value_says(string? parameter, bool braced)
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("brace", parameter);

        // Assert
        Assert.Equal(new[] { braced ? "You are braced." : "Not braced." }, lines);
        Assert.Equal(braced, g.T("braced").Result.AsBool);
    }

    [Fact]
    public void Brace_Given_no_parameter_When_run_twice_Then_it_toggles()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> first = g.Lines("brace");
        List<string> second = g.Lines("brace");

        // Assert
        Assert.Equal(new[] { "You are braced." }, first);
        Assert.Equal(new[] { "Not braced." }, second);
        Assert.False(g.T("braced").Result.AsBool);
    }

    [Fact]
    public void Brace_Given_the_command_When_run_Then_fix_player_follows_and_the_weapon_class_changes_by_the_bonus_of_C()
    {
        // Arrange
        using var g = new Game();
        g.Roll();
        long before = g.Read(g.Player, "mudwc").AsInt;

        // Act
        g.Lines("brace", "on");
        long braced = g.Read(g.Player, "mudwc").AsInt;
        g.Lines("brace", "off");
        long after = g.Read(g.Player, "mudwc").AsInt;

        // Assert
        Assert.Equal(before + 4, braced); // FixPlayer: wc += 4 when braced
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("fix_me")]
    [InlineData("fixme")]
    public void FixMe_Given_a_wrong_weapon_class_When_run_Then_fix_player_computes_it_again(string command)
    {
        // Arrange
        using var g = new Game();
        g.Roll();
        long right = g.Read(g.Player, "mudwc").AsInt;
        g.Ext.Dispatcher.ControllerOf("bob")!.State.Ob.Stats.Wc = (sbyte)(right + 5);
        Assert.Equal(right + 5, g.Read(g.Player, "mudwc").AsInt);

        // Act
        ReplyFrame r = g.Mud(command);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(right, g.Read(g.Player, "mudwc").AsInt);
        Assert.Empty(Lines(r)); // C says nothing
    }

    [Fact]
    public void Verbose_Given_a_number_When_run_Then_the_level_is_the_member_and_C_says_so()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> none = g.Lines("verbose");
        List<string> set = g.Lines("verbose", "7");
        List<string> word = g.Lines("verbose", "loud");
        List<string> signed = g.Lines("verbose", " -3 and more");

        // Assert
        Assert.Equal(new[] { "Set listen to what (presently 0)?" }, none);
        Assert.Equal(new[] { "Your verbose level is now 7." }, set);
        Assert.Equal(new[] { "Set listen to what (presently 7)?" }, word);
        Assert.Equal(new[] { "Your verbose level is now -3." }, signed);
        Assert.Equal(-3L, g.Read(g.Player, "mudverbose").AsInt);
    }

    [Fact]
    public void Verbose_Given_a_number_too_large_for_a_char_When_run_Then_C_says_the_number_and_keeps_the_char()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("verbose", "200");

        // Assert
        Assert.Equal(new[] { "Your verbose level is now 200." }, lines);
        Assert.Equal(-56L, g.Read(g.Player, "mudverbose").AsInt); // (char) 200
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData("  +5x", 5)]
    [InlineData("\n\v\f\r\t 9", 9)]
    [InlineData("-7", -7)]
    [InlineData("99999999999", int.MaxValue)]
    [InlineData("-99999999999", int.MinValue)]
    [InlineData("99999999999999999999999999", int.MaxValue)]
    [InlineData("-99999999999999999999999999", int.MinValue)]
    public void ScanInt_Given_a_number_When_scanned_Then_it_is_what_sscanf_reads(string text, int expected)
    {
        // Act
        bool ok = Commands.ScanInt(text, out int value);

        // Assert
        Assert.True(ok);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x1")]
    [InlineData("+")]
    [InlineData("- 1")]
    public void ScanInt_Given_no_number_When_scanned_Then_it_fails(string text)
    {
        Assert.False(Commands.ScanInt(text, out _));
    }

    // ------------------------------------------------------------------ who and tell

    [Fact]
    public void Who_Given_two_players_When_run_Then_both_are_listed_by_name_with_title_and_P_or_W()
    {
        // Arrange
        using var g = new Game();
        g.Login("amy");
        g.Lines("title", "the Bold", "amy");
        g.Lines("peaceful", null, "bob");

        // Act
        List<string> lines = g.Lines("who");

        // Assert
        Assert.Equal(new[] { "Players:", "amy the the Bold W", "bob the human P" }, lines);
    }

    [Fact]
    public void Tell_Given_a_player_When_told_Then_he_gets_the_line_and_the_teller_the_echo_with_the_name_as_typed()
    {
        // Arrange
        using var g = new Game();
        g.Login("amy");

        // Act
        ReplyFrame r = g.Mud("tell", "AMY hello there, friend");

        // Assert
        Assert.Equal(new[] { "bob tells you: hello there, friend" }, Lines(r, "amy"));
        Assert.Equal(new[] { "You tell to AMY: hello there, friend" }, Lines(r, "bob"));
    }

    [Fact]
    public void Tell_Given_a_name_of_more_than_MAX_NAME_characters_When_told_Then_only_the_first_16_are_compared()
    {
        // Arrange
        using var g = new Game();
        g.Login("amy_with_a_long_name");

        // Act
        ReplyFrame same = g.Mud("tell", "amy_with_a_long_nameZZ hi");
        ReplyFrame other = g.Mud("tell", "amy_with_a_lonX hi");
        ReplyFrame shorter = g.Mud("tell", "AMY_WITH_A_LONG_XXXXXXXX ho");

        // Assert
        Assert.Equal(new[] { "bob tells you: ho" }, Lines(shorter, "amy_with_a_long_name")); // the first 16 characters are the same, in any case
        Assert.Equal(new[] { "bob tells you: hi" }, Lines(same, "amy_with_a_long_name"));
        Assert.Equal(new[] { "No such player." }, Lines(other, "bob")); // the 16th character differs
    }

    [Fact]
    public void Tell_Given_no_name_no_message_or_no_such_player_When_told_Then_the_texts_of_C()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> nobody = g.Lines("tell");
        List<string> nomessage = g.Lines("tell", "amy");
        List<string> nomessageSpace = g.Lines("tell", "amy ");
        List<string> unknown = g.Lines("tell", "amy hello");

        // Assert
        Assert.Equal(new[] { "Tell whom what?" }, nobody);
        Assert.Equal(new[] { "Tell amy what?" }, nomessage);
        Assert.Equal(new[] { "Tell amy what?" }, nomessageSpace);
        Assert.Equal(new[] { "No such player." }, unknown);
    }

    [Fact]
    public void Tell_Given_a_message_longer_than_the_buffer_of_C_When_told_Then_the_line_the_other_gets_is_cut_to_255_characters()
    {
        // Arrange
        using var g = new Game();
        g.Login("amy");
        string message = new('x', 400);

        // Act
        ReplyFrame r = g.Mud("tell", "amy " + message);

        // Assert
        string got = Assert.Single(Lines(r, "amy"));
        Assert.Equal(255, got.Length);
        Assert.StartsWith("bob tells you: xxx", got);
        Assert.Equal("You tell to amy: " + message, Assert.Single(Lines(r, "bob")));
    }

    // ------------------------------------------------------------------ save

    [Fact]
    public void Save_Given_the_command_When_run_Then_the_account_is_written_now_and_C_says_so()
    {
        // Arrange
        using var g = new Game(options: CommandWorld.Options(new FileWriterOptions { Quiet = TimeSpan.FromHours(1), MaxDelay = TimeSpan.FromHours(1) }),
            inventory: "arch banana\nnrof 3\nend\n");
        Assert.Equal(0UL, Say.Set(g.Ext, g.Player, Ply("levelcap"), Val.Int(77)).Status);

        // Act
        List<string> lines = g.Lines("save");
        string path = ObjectWorld.Data(g.Ext.World, "accounts/bob.txt");
        string file = "";
        for (int i = 0; i < 300 && !file.Contains("mudlevelcap 77", StringComparison.Ordinal); i++)
        {
            Thread.Sleep(10); // the quiet time of this writer is an hour: only the save writes it
            file = File.Exists(path) ? File.ReadAllText(path) : "";
        }

        // Assert
        Assert.Equal(new[] { "You have been saved." }, lines);
        Assert.Contains("mudlevelcap 77", file);
    }

    [Fact]
    public void Save_Given_an_unloaded_account_When_run_Then_SAVE_FAILED_and_nothing_is_written()
    {
        // Arrange
        using var g = new Game(options: CommandWorld.Options(new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(5), MaxDelay = TimeSpan.FromMilliseconds(20) }));
        Assert.Equal(0UL, g.Ext.Call(13, new[] { H(g.Player), Val.Int(TMudPlayerMembers.MethodUnloadMudAccount), Val.List() }, player: "bob").Status);
        string path = ObjectWorld.Data(g.Ext.World, "accounts/bob.txt");
        Thread.Sleep(150); // the file of the new account is written at the quiet time
        string? before = File.Exists(path) ? File.ReadAllText(path) : null;
        Assert.Equal(0UL, Say.Set(g.Ext, g.Player, Ply("levelcap"), Val.Int(55)).Status);

        // Act
        List<string> lines = g.Lines("save");
        Thread.Sleep(150);

        // Assert
        Assert.Equal(new[] { "SAVE FAILED!" }, lines);
        Assert.Equal(before, File.Exists(path) ? File.ReadAllText(path) : null);
        Assert.DoesNotContain("mudlevelcap 55", before ?? "");
    }

    [Fact]
    public void Save_Given_the_worker_is_busy_When_run_Then_busy_and_nothing_is_written()
    {
        // Arrange
        using var g = new Game();
        g.Ext.Dispatcher.Busy = "recovering";

        // Act
        ReplyFrame r = g.Mud("save");

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.Empty(r.Events);
    }

    // ------------------------------------------------------------------ mark

    [Fact]
    public void Mark_Given_no_parameter_or_a_name_When_run_Then_C_says_what_it_says_and_markedobject_reads_the_item()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\nend\narch banana\nnrof 4\nend\n");

        // Act
        List<string> none = g.Lines("mark");
        List<string> miss = g.Lines("mark", "unicorn");
        Val before = g.Read(g.Player, "markedobject");
        List<string> named = g.Lines("mark", "banana");
        Val marked = g.Read(g.Player, "markedobject");
        List<string> again = g.Lines("mark");

        // Assert
        Assert.Equal(new[] { "You have no marked object." }, none);
        Assert.Equal(new[] { "Could not find an object that matches unicorn" }, miss);
        Assert.Equal("", before.AsString);
        Assert.Equal(new[] { "Marked item bananas" }, named);
        Assert.Equal("banana", Say.Get(g.Ext, marked.AsHandle, Obj("mudrealname")).AsString);
        Assert.Equal(new[] { "bananas is marked." }, again);
    }

    [Fact]
    public void Mark_Given_the_id_of_an_item_as_text_When_run_Then_that_item_is_marked_and_a_wrong_id_says_so()
    {
        // Arrange
        using var g = new Game(inventory: "arch sword\nend\narch banana\nnrof 4\nend\n");
        ReplyFrame items = g.Ext.Call(12, new[] { H(g.Player), Val.Int(Ply("muditems")), Val.List(Val.Int(Obj("mudrealname"))) });
        ulong sword = items.Result.Items.First(r => r.Items[1].AsString == "sword").Items[0].AsHandle;
        ulong banana = items.Result.Items.First(r => r.Items[1].AsString == "banana").Items[0].AsHandle;
        ulong hall = g.Hall[PickupWorld.Sword];

        // Act
        List<string> byId = g.Lines("mark", ((uint)sword).ToString());
        Val first = g.Read(g.Player, "markedobject");
        List<string> ground = g.Lines("mark", ((uint)hall).ToString());
        List<string> zero = g.Lines("mark", "0");
        List<string> one = g.Lines("mark", "20"); // the number is no count of items to match: the name path would take the first item
        List<string> plural = g.Lines("mark", "30");
        List<string> stale = g.Lines("mark", "99999");
        List<string> big = g.Lines("mark", "99999999999999999999999");
        Val last = g.Read(g.Player, "markedobject");

        // Assert
        Assert.Equal(new[] { "Marked item swords" }, byId);
        Assert.Equal(sword, first.AsHandle);
        Assert.Equal(new[] { "Could not find an object that matches " + (uint)hall }, ground); // an object of a level is no item of his
        Assert.Equal(new[] { "Could not find an object that matches 0" }, zero);
        Assert.Equal(new[] { "Could not find an object that matches 20" }, one);
        Assert.Equal(new[] { "Could not find an object that matches 30" }, plural);
        Assert.Equal(new[] { "Could not find an object that matches 99999" }, stale);
        Assert.Equal(new[] { "Could not find an object that matches 99999999999999999999999" }, big);
        Assert.Equal(sword, last.AsHandle); // the failures leave the mark
        Assert.NotEqual(banana, last.AsHandle);
    }

    [Fact]
    public void Mark_Given_the_id_of_an_invisible_item_When_run_Then_it_is_not_marked()
    {
        // Arrange
        using var g = new Game(inventory: "arch mist\nend\n");
        long mist = g.T("tag", text: "mist").Result.AsInt;
        Assert.True(mist > 0);

        // Act
        List<string> lines = g.Lines("mark", mist.ToString());

        // Assert
        Assert.Equal(new[] { "Could not find an object that matches " + mist }, lines);
        Assert.Equal("", g.Read(g.Player, "markedobject").AsString);
    }

    [Fact]
    public void Who_Given_a_player_whose_account_is_unloaded_When_run_Then_he_is_not_listed()
    {
        // Arrange
        using var g = new Game();
        ulong amy = g.Login("amy");
        Assert.Equal(0UL, g.Ext.Call(13, new[] { H(amy), Val.Int(TMudPlayerMembers.MethodUnloadMudAccount), Val.List() }, player: "amy").Status);

        // Act
        List<string> lines = g.Lines("who");

        // Assert
        Assert.Equal(new[] { "Players:", "bob the human W" }, lines);
    }

    [Fact]
    public void Mark_Given_the_id_of_an_item_in_a_sack_When_run_Then_it_is_not_marked()
    {
        // Arrange
        using var g = new Game(inventory: "arch bag\narch banana\nnrof 2\nend\nend\n");
        ReplyFrame inner = g.Ext.Call(12, new[] { H(g.Player), Val.Int(Ply("muditems")), Val.List(Val.Int(Obj("mudrealname"))) });
        ulong bag = inner.Result.Items.Single().Items[0].AsHandle;
        ReplyFrame inBag = g.Ext.Call(12, new[] { H(bag), Val.Int(Obj("muditems")), Val.List(Val.Int(Obj("mudrealname"))) });
        ulong banana = inBag.Result.Items.Single().Items[0].AsHandle;
        CfObject ob = g.Ext.Dispatcher.ControllerOf("bob")!.State.Ob;
        CfObject sack = ob.Inv!;
        while (sack.Type != Define.CONTAINER)
        {
            sack = sack.Below!;
        }
        ob.Container = sack; // the bag is open: the id of its banana is found at the second level, which only the top level may mark
        sack.SetFlag(Define.FLAG_APPLIED);

        // Act
        List<string> lines = g.Lines("mark", ((uint)banana).ToString());

        // Assert
        Assert.Equal(new[] { "Could not find an object that matches " + (uint)banana }, lines);
        Assert.Equal("", g.Read(g.Player, "markedobject").AsString);
    }

    // ------------------------------------------------------------------ help

    [Fact]
    public void Help_Given_the_files_of_Crossfire_When_listed_Then_the_26_topics_of_lib_help_are_shipped_in_the_order_of_their_names()
    {
        // Assert
        Assert.Equal(26, HelpCommand.Topics.Count);
        Assert.Equal(HelpCommand.Topics.Order(StringComparer.Ordinal).ToList(), HelpCommand.Topics);
        Assert.Contains("take", HelpCommand.Topics);
        Assert.Contains("output-count", HelpCommand.Topics);
    }

    [Fact]
    public void Help_Given_no_topic_When_run_Then_the_main_page_def_help_is_said_line_by_line()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("help");

        // Assert
        Assert.Equal(new[]
        {
            "How to use help:",
            "  'help topics    (list of help topics)",
            "  'help commands  (list of valid commands)",
            "  'help <command> (help on a topic or command)",
            "",
        }, lines);
    }

    [Fact]
    public void Help_Given_a_topic_When_run_Then_the_file_is_said_after_its_title()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("help", "take");

        // Assert
        Assert.Equal("Help about 'take'", lines[0]);
        Assert.True(lines.Count > 1);
        Assert.Equal(HelpCommand.Lines("help/take")!, lines.Skip(1).ToList());
        Assert.Equal("The take commands take object(s) on the", lines[1]);
    }

    [Fact]
    public void Help_Given_a_topic_with_a_hyphen_When_run_Then_it_is_found()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("help", "output-count");

        // Assert
        Assert.Equal("Help about 'output-count'", lines[0]);
    }

    [Fact]
    public void Help_Given_no_such_topic_or_illegal_characters_When_run_Then_the_texts_of_C_with_its_typo()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> missing = g.Lines("help", "nothing");
        List<string> wizard = g.Lines("help", "def_help");
        List<string> dot = g.Lines("help", "a.b");
        List<string> space = g.Lines("help", "take it");
        List<string> slash = g.Lines("help", "../take");
        List<string> slashOnly = g.Lines("help", "x/y");

        // Assert
        Assert.Equal(new[] { "No help availble on 'nothing'" }, missing);
        Assert.Equal(new[] { "No help availble on 'def_help'" }, wizard);
        Assert.Equal(new[] { "Illegal characters in 'a.b'" }, dot);
        Assert.Equal(new[] { "Illegal characters in 'take it'" }, space);
        Assert.Equal(new[] { "Illegal characters in '../take'" }, slash);
        Assert.Equal(new[] { "Illegal characters in 'x/y'" }, slashOnly);
    }

    [Fact]
    public void Help_Given_topics_When_run_Then_the_names_are_listed_in_lines_of_at_most_42_characters()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("help", "topics");

        // Assert
        Assert.Equal("      Misc help:", lines[0]); // lib/mischelp does not exist: the heading and no files
        Assert.Equal("      Commands:", lines[1]);
        Assert.All(lines.Skip(2), l => Assert.True(l.Length <= 43, l));
        Assert.Equal(HelpCommand.Topics, lines.Skip(2).SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList());
        Assert.Equal(" apply bind cast golem invoke keys mark", lines[2]); // the next name would make 45 characters
        Assert.Equal(" melee mouse move name output output-count", lines[3]);
    }

    [Fact]
    public void Help_Given_commands_When_run_Then_the_names_of_the_table_are_listed_then_the_communication_commands()
    {
        // Arrange
        using var g = new Game();

        // Act
        List<string> lines = g.Lines("help", "commands");

        // Assert
        int split = lines.IndexOf("      Communication commands:");
        Assert.Equal("      Commands:", lines[0]);
        Assert.True(split > 1);
        Assert.Equal(Commands.Names, lines.Skip(1).Take(split - 1).SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList());
        Assert.Equal(new[] { " tell" }, lines.Skip(split + 1));
        Assert.All(lines, l => Assert.True(l.Length <= 43, l));
    }

    [Fact]
    public void SplitLines_Given_a_long_line_and_a_last_line_without_a_newline_When_split_Then_fgets_gives_pieces_of_255_characters()
    {
        // Arrange
        string text = new string('a', 300) + "\nshort\nlast";

        // Act
        List<string> lines = HelpCommand.SplitLines(text);

        // Assert
        Assert.Equal(new[] { new string('a', 255), new string('a', 45), "short", "last" }, lines);
    }

    [Fact]
    public void SplitLines_Given_a_line_of_exactly_255_characters_with_its_newline_When_split_Then_fgets_leaves_the_newline_for_an_empty_line()
    {
        // Act
        List<string> lines = HelpCommand.SplitLines(new string('c', 255) + "\nz\n");

        // Assert: fgets reads 255 characters at most, and the next call reads the newline alone
        Assert.Equal(new[] { new string('c', 255), "", "z" }, lines);
    }

    [Fact]
    public void SplitLines_Given_a_line_of_exactly_254_characters_with_its_newline_When_split_Then_it_is_one_line()
    {
        // Act
        List<string> lines = HelpCommand.SplitLines(new string('b', 254) + "\nnext\n");

        // Assert
        Assert.Equal(new[] { new string('b', 254), "next" }, lines);
    }

    // ------------------------------------------------------------------ the page filter of level.load

    [Fact]
    public void LevelLoad_Given_an_object_of_a_later_page_that_a_pick_up_adopted_When_its_page_is_asked_for_Then_the_page_does_not_place_it()
    {
        // Arrange
        using var g = new Game();
        ReplyFrame page1 = g.Ext.Call(7, new[] { S("big"), Val.Nil }, level: "big");
        Assert.Equal(0UL, page1.Status);
        Assert.True(page1.Result.Get("more")!.AsBool);
        Assert.Equal(64, Effects(page1, "npc.place").Count);
        ReplyFrame take = g.Ext.Call(CommandWorld.OpTest, new[] { S("pickunplaced"), S("bob"), Val.Nil }, level: "big", player: "bob");
        Assert.Equal(0UL, take.Status);
        Assert.True(take.Result.AsBool);

        // Act
        ReplyFrame page2 = g.Ext.Call(7, new[] { S("big"), page1.Result.Get("cursor")! }, level: "big");

        // Assert
        Assert.Equal(0UL, page2.Status);
        Assert.False(page2.Result.Get("more")!.AsBool);
        Assert.Equal(5, Effects(page2, "npc.place").Count); // 70 objects: 64, then 6 minus the one that is in the pack
    }

    [Fact]
    public void LevelLoad_Given_no_pick_up_When_the_second_page_is_asked_for_Then_it_places_the_rest()
    {
        // Arrange
        using var g = new Game();
        ReplyFrame page1 = g.Ext.Call(7, new[] { S("big"), Val.Nil }, level: "big");

        // Act
        ReplyFrame page2 = g.Ext.Call(7, new[] { S("big"), page1.Result.Get("cursor")! }, level: "big");

        // Assert
        Assert.Equal(6, Effects(page2, "npc.place").Count);
    }
}
