// Not ported from a Crossfire file: tests of the accounts (player.login, player.logout, the members of TMudPlayer, the account files)
// and of the file names. Each test of the accounts runs twice: against the dispatcher in this process and over a Unix socket.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

public sealed class AccountDirectTests : AccountTests
{
    internal override IExt Start(TempFolder world, ExtensionOptions? options = null, long epoch = 1, bool owns = true) => new DirectExt(world, options, epoch, owns);
}

public sealed class AccountSocketTests : AccountTests
{
    internal override IExt Start(TempFolder world, ExtensionOptions? options = null, long epoch = 1, bool owns = true) => new SocketExt(world, options, epoch, owns);
}

public abstract class AccountTests
{
    internal abstract IExt Start(TempFolder world, ExtensionOptions? options = null, long epoch = 1, bool owns = true);

    private static ReplyFrame Login(IExt ext, string account) => ext.Call(5, new[] { S(account) }, player: account);

    private static IEnumerable<string> Names(ReplyFrame r, string k) => Effects(r, k).Select(e => e.Get("name")!.AsString);

    // ------------------------------------------------------------------ player.login

    [Fact]
    public void PlayerLogin_Given_an_account_without_a_file_When_called_Then_a_new_player_with_a_handle_whose_members_read()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame r = Login(ext, "bob");
        ulong h = r.Result.AsHandle;

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(ValKind.Handle, r.Result.Kind);
        Assert.Equal(1UL, h >> 32);
        Assert.Equal(10L, Get(ext, h, Ply("mudhp")).AsInt);
        Assert.Equal(12L, Get(ext, h, Ply("mudmaxhp")).AsInt);
        Assert.Equal(1L, Get(ext, h, Ply("mudlevel")).AsInt);
        Assert.Equal(2L, Get(ext, h, Ply("mudstrength")).AsInt);
        Assert.Equal(999L, Get(ext, h, Ply("mudfood")).AsInt);
        Assert.Equal("bob", Get(ext, h, Ply("mudrealname")).AsString);
        Assert.True(Get(ext, h, Ply("ismudobject")).AsBool);
        Assert.Equal((long)(uint)h, Get(ext, h, Ply("mudid")).AsInt);
        Assert.Equal("", Get(ext, h, Ply("mudclass")).AsString);
        Assert.False(Get(ext, h, Ply("peaceful")).AsBool);
        Assert.Equal(0L, Get(ext, h, Ply("lastdeath")).AsInt);
    }

    [Fact]
    public void PlayerLogin_Given_a_new_player_When_called_Then_the_life_and_stat_strings_are_set_and_onLoadedMudAccount_fires_for_the_player()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame r = Login(ext, "bob");

        // Assert
        var sets = Effects(r, "client.set");
        Assert.Equal("bob", sets[0].Get("player")!.AsString);
        Assert.Equal(new[] { "k", "player", "name", "value" }, sets[0].Pairs.Select(p => p.Key));
        Assert.Contains("clientr.mud_hp", Names(r, "client.set"));
        Assert.Contains("clientr.mud_maxhp", Names(r, "client.set"));
        Assert.Equal("10", sets.Single(e => e.Get("name")!.AsString == "clientr.mud_hp").Get("value")!.AsString);
        Assert.Equal("999", sets.Single(e => e.Get("name")!.AsString == "clientr.mud_food").Get("value")!.AsString);
        Assert.Equal(Names(r, "client.set").Distinct().Count(), sets.Count);
        Assert.Equal(
            new[] { "ac", "cha", "con", "dam", "dex", "exp", "food", "grace", "hp", "int", "level", "luck", "maxgrace", "maxhp", "maxsp", "pow", "sp", "speed", "str", "wc", "weight", "weight_lim", "wis" }
                .Select(n => "clientr.mud_" + n),
            Names(r, "client.set").OrderBy(n => n, StringComparer.Ordinal));
        Val ev = Assert.Single(r.Events);
        Assert.Equal("onLoadedMudAccount", ev.Get("name")!.AsString);
        Assert.Equal("player", ev.Get("target")!.AsString);
        Assert.Equal("bob", ev.Get("player")!.AsString);
        Assert.Equal(r.Result.AsHandle, ev.Get("args")!.Items.Single().AsHandle);
    }

    [Fact]
    public void PlayerLogin_Given_a_second_login_without_a_logout_When_called_Then_the_same_handle_the_strings_again_and_no_second_event()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ReplyFrame first = Login(ext, "bob");

        // Act
        ReplyFrame second = Login(ext, "bob");

        // Assert
        Assert.Equal(first.Result.AsHandle, second.Result.AsHandle);
        Assert.Equal(Names(first, "client.set").OrderBy(n => n), Names(second, "client.set").OrderBy(n => n));
        Assert.Empty(second.Events);
    }

    [Fact]
    public void PlayerLogin_Given_two_accounts_When_called_Then_two_handles_and_accounts_are_compared_as_bytes()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ulong lower = Login(ext, "abc").Result.AsHandle;
        ulong upper = Login(ext, "ABC").Result.AsHandle;
        ulong again = Login(ext, "abc").Result.AsHandle;

        // Assert
        Assert.NotEqual(lower, upper);
        Assert.Equal(lower, again);
    }

    [Theory]
    [InlineData("")]
    public void PlayerLogin_Given_an_account_that_cannot_be_a_file_name_When_called_Then_nil_and_a_log_line(string account)
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        int before = ext.Logs.Count;

        // Act
        ReplyFrame r = ext.Call(5, new[] { S(account) }, player: account);

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        Assert.Empty(r.Events);
        Assert.Single(ext.Logs.Skip(before));
    }

    [Fact]
    public void PlayerLogin_Given_an_account_name_longer_than_a_file_name_may_be_When_called_Then_nil_and_a_log_line()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        string account = new('a', FileNames.MaxEscapedLength + 1);

        // Act
        ReplyFrame r = Login(ext, account);
        ReplyFrame longest = Login(ext, new string('a', FileNames.MaxEscapedLength));

        // Assert
        Assert.True(r.Result.IsNil);
        Assert.Equal(ValKind.Handle, longest.Result.Kind);
    }

    [Fact]
    public void PlayerLogin_Given_wrong_arguments_When_called_Then_bad_args()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame number = ext.Call(5, new[] { Val.Int(3) });
        ReplyFrame none = ext.Call(5);
        ReplyFrame nil = ext.Call(5, new[] { Val.Nil });

        // Assert
        Assert.Equal((ulong)Status.BadArgs, number.Status);
        Assert.Equal((ulong)Status.BadArgs, none.Status);
        Assert.Equal(0UL, nil.Status);
        Assert.True(nil.Result.IsNil);
    }

    [Fact]
    public void PlayerLogin_Given_a_world_without_a_player_archetype_When_called_Then_nil_and_a_log_line()
    {
        // Arrange
        TempFolder world = TestWorld.Make();
        using IExt ext = Start(world);

        // Act
        ReplyFrame r = Login(ext, "bob");

        // Assert
        Assert.True(r.Result.IsNil);
        Assert.Contains(ext.Logs, l => l.Text.Contains("no player archetype"));
    }

    // ------------------------------------------------------------------ player.logout and the account files

    [Fact]
    public void PlayerLogout_Given_a_logged_in_player_When_called_Then_a_client_del_for_each_string_the_handle_dies_and_the_file_is_written()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ReplyFrame login = Login(ext, "bob");
        ulong h = login.Result.AsHandle;
        Set(ext, h, Ply("mudhp"), Val.Int(4));

        // Act
        ReplyFrame r = ext.Call(6, new[] { S("bob") }, player: "bob");
        ReplyFrame read = ext.Call(9, new[] { H(h), Val.Int(Ply("mudhp")) });
        string file = WaitFile(ext.World, "accounts/bob.txt");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Equal(Names(login, "client.set").OrderBy(n => n), Names(r, "client.del").OrderBy(n => n));
        Assert.All(Effects(r, "client.del"), e => Assert.Equal("bob", e.Get("player")!.AsString));
        Assert.Equal(new[] { "k", "player", "name" }, Effects(r, "client.del")[0].Pairs.Select(p => p.Key));
        Assert.Empty(Effects(r, "client.set"));
        Assert.Empty(r.Events);
        Assert.Equal((ulong)Status.BadHandle, read.Status);
        Assert.StartsWith("arch human_player\naccount bob\n", file);
        Assert.Contains("\nhp 4\n", file);
    }

    [Fact]
    public void PlayerLogout_Given_an_account_not_logged_in_When_called_Then_nil_and_no_effects()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());

        // Act
        ReplyFrame r = ext.Call(6, new[] { S("nobody") }, player: "nobody");
        ReplyFrame wrong = ext.Call(6, new[] { Val.Int(1) });

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        Assert.Equal((ulong)Status.BadArgs, wrong.Status);
    }

    [Fact]
    public void PlayerLogout_Given_a_second_logout_When_called_Then_nil_and_no_effects()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        Login(ext, "bob");
        ext.Call(6, new[] { S("bob") });

        // Act
        ReplyFrame r = ext.Call(6, new[] { S("bob") });

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Empty(r.Effects);
    }

    [Fact]
    public void Player_Given_a_logout_and_a_login_When_the_state_was_changed_Then_it_is_restored_with_a_new_handle()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong first = Login(ext, "bob").Result.AsHandle;
        Set(ext, first, Ply("mudhp"), Val.Int(4));
        Set(ext, first, Ply("mudclass"), S("warrior_class"));
        Set(ext, first, Ply("peaceful"), Val.True);
        Set(ext, first, Ply("lastdeath"), Val.Int(1524297788));
        Set(ext, first, Ply("levelcap"), Val.Int(25));
        Set(ext, first, Ply("mudrace"), S("dwarf"));
        ext.Call(6, new[] { S("bob") });

        // Act
        ReplyFrame r = Login(ext, "bob");
        ulong second = r.Result.AsHandle;

        // Assert
        Assert.NotEqual(first, second);
        Assert.Equal(4L, Get(ext, second, Ply("mudhp")).AsInt);
        Assert.Equal("warrior_class", Get(ext, second, Ply("mudclass")).AsString);
        Assert.True(Get(ext, second, Ply("peaceful")).AsBool);
        Assert.Equal(1524297788L, Get(ext, second, Ply("lastdeath")).AsInt);
        Assert.Equal(25L, Get(ext, second, Ply("levelcap")).AsInt);
        Assert.Equal("dwarf", Get(ext, second, Ply("mudrace")).AsString);
        Assert.Equal("4", Effects(r, "client.set").Single(e => e.Get("name")!.AsString == "clientr.mud_hp").Get("value")!.AsString);
        Assert.Single(r.Events);
    }

    [Fact]
    public void Player_Given_a_login_after_a_process_restart_When_the_account_was_saved_Then_the_file_on_disk_is_the_state()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        using (IExt ext = Start(world, owns: false))
        {
            ulong h = Login(ext, "bob").Result.AsHandle;
            Set(ext, h, Ply("mudexp"), Val.Int(5000));
        }

        // Act
        using IExt again = Start(world, epoch: 3);
        ReplyFrame r = Login(again, "bob");

        // Assert
        Assert.Equal(3UL, r.Result.AsHandle >> 32);
        Assert.Equal(5000L, Get(again, r.Result.AsHandle, Ply("mudexp")).AsInt);
        Assert.Equal("5000", Effects(r, "client.set").Single(e => e.Get("name")!.AsString == "clientr.mud_exp").Get("value")!.AsString);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a%2Fb")]
    [InlineData("../etc/passwd")]
    [InlineData(".hidden")]
    [InlineData("..")]
    [InlineData("name with spaces")]
    [InlineData("Ünïcode名前")]
    [InlineData("x.tmp")]
    [InlineData("pc:1234567890")]
    public void Player_Given_account_names_with_bytes_a_file_name_cannot_hold_When_logged_in_and_out_Then_each_has_its_own_file_inside_the_accounts_folder_and_reads_back(string account)
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        using (IExt ext = Start(world, owns: false))
        {
            ulong h = Login(ext, account).Result.AsHandle;
            Assert.Equal(ValKind.Handle, Login(ext, account).Result.Kind);
            Set(ext, h, Ply("mudexp"), Val.Int(77));
            ext.Call(6, new[] { S(account) });
        }

        // Act
        string[] files = Directory.GetFiles(Path.Combine(world.Path, "data", "accounts"));
        using IExt again = Start(world, epoch: 2);
        ulong back = Login(again, account).Result.AsHandle;

        // Assert
        string file = Assert.Single(files);
        Assert.Equal(Path.Combine(world.Path, "data", "accounts"), Path.GetDirectoryName(file));
        Assert.Equal(file, Path.Combine(world.Path, "data", FileNames.AccountFile(account)!));
        Assert.True(FileNames.TryUnescape(Path.GetFileName(file)[..^FileNames.AccountSuffix.Length], out string name));
        Assert.Equal(account, name);
        Assert.Equal(77L, Get(again, back, Ply("mudexp")).AsInt);
        world.Dispose();
    }

    [Fact]
    public void Player_Given_accounts_whose_escaped_names_could_collide_When_saved_Then_they_are_separate_files()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        using (IExt ext = Start(world, owns: false))
        {
            foreach (string a in new[] { "a/b", "a%2Fb", "a%2fb", "A%2Fb" })
            {
                Set(ext, Login(ext, a).Result.AsHandle, Ply("mudexp"), Val.Int(a.Length));
            }
        }

        // Act
        string[] files = Directory.GetFiles(Path.Combine(world.Path, "data", "accounts"));

        // Assert
        Assert.Equal(4, files.Select(Path.GetFileName).Distinct().Count());
        world.Dispose();
    }

    [Fact]
    public void Player_Given_a_file_that_belongs_to_another_account_When_logged_in_Then_the_name_of_the_file_decides_and_a_line_is_logged()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        Directory.CreateDirectory(Path.Combine(world.Path, "data", "accounts"));
        File.WriteAllText(Path.Combine(world.Path, "data", "accounts", "bob.txt"), "arch human_player\naccount alice\nname alice\nexp 9\nend\n");
        using IExt ext = Start(world);

        // Act
        ReplyFrame r = Login(ext, "bob");
        long exp = Get(ext, r.Result.AsHandle, Ply("mudexp")).AsInt;
        ext.Call(6, new[] { S("bob") });
        string saved = "";
        for (int i = 0; i < 300 && !saved.Contains("account bob"); i++)
        {
            Thread.Sleep(10);
            saved = File.ReadAllText(Path.Combine(world.Path, "data", "accounts", "bob.txt"));
        }

        // Assert
        Assert.Equal(9L, exp);
        Assert.Contains(ext.Logs, l => l.Text.Contains("another account"));
        Assert.Contains("account bob\n", saved);
        Assert.False(File.Exists(Path.Combine(world.Path, "data", "accounts", "alice.txt")));
    }

    [Theory]
    [InlineData("this is not a player file\n")]
    [InlineData("arch nosucharchetype\nexp 1\nend\n")]
    [InlineData("")]
    public void Player_Given_a_file_that_holds_no_player_When_logged_in_Then_nil_and_the_file_is_never_written_over(string content)
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        string path = Path.Combine(world.Path, "data", "accounts", "bob.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        using (IExt ext = Start(world, owns: false))
        {
            // Act
            ReplyFrame first = Login(ext, "bob");
            int logsAfterFirst = ext.Logs.Count;
            ReplyFrame second = Login(ext, "bob");
            ext.Call(6, new[] { S("bob") });
            ext.Call(3);

            // Assert
            Assert.True(first.Result.IsNil);
            Assert.True(second.Result.IsNil);
            Assert.Empty(first.Effects);
            Assert.Equal(logsAfterFirst, ext.Logs.Count); // the second login did not read the file again
        }
        Assert.Equal(content, File.ReadAllText(path));
        world.Dispose();
    }

    [Fact]
    public void Player_Given_an_account_file_with_an_inventory_block_When_logged_in_and_out_Then_the_contents_are_kept()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        Directory.CreateDirectory(Path.Combine(world.Path, "data", "accounts"));
        File.WriteAllText(Path.Combine(world.Path, "data", "accounts", "bob.txt"), "arch human_player\naccount bob\nname bob\narch banana\nnrof 4\nend\nend\n");
        using IExt ext = Start(world);

        // Act
        Login(ext, "bob");
        ext.Call(6, new[] { S("bob") });
        string saved = WaitFile(world, "accounts/bob.txt", 3000);
        for (int i = 0; i < 300 && !saved.Contains("nrof 4"); i++)
        {
            Thread.Sleep(10);
            saved = File.ReadAllText(Path.Combine(world.Path, "data", "accounts", "bob.txt"));
        }

        // Assert
        Assert.Contains("arch banana\nnrof 4\nend\n", saved);
    }

    // ------------------------------------------------------------------ the members of a player

    [Fact]
    public void Set_Given_a_stat_of_a_player_When_written_Then_the_client_string_changes_and_no_event_fires()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame r = Set(ext, h, Ply("mudhp"), Val.Int(7));
        ReplyFrame same = Set(ext, h, Ply("mudhp"), Val.Int(7));
        ReplyFrame noString = Set(ext, h, Ply("peaceful"), Val.True);

        // Assert
        Assert.Equal(7L, r.Result.AsInt);
        Val set = Assert.Single(r.Effects);
        Assert.Equal("client.set", set.Get("k")!.AsString);
        Assert.Equal("bob", set.Get("player")!.AsString);
        Assert.Equal("clientr.mud_hp", set.Get("name")!.AsString);
        Assert.Equal("7", set.Get("value")!.AsString);
        Assert.Empty(r.Events);
        Assert.Empty(same.Effects);
        Assert.Empty(noString.Effects);
    }

    [Fact]
    public void Set_Given_a_speed_and_an_alias_When_written_Then_the_string_is_the_text_of_the_number()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame speed = Set(ext, h, Ply("mudspeed"), N(1.5));
        ReplyFrame cha = Set(ext, h, Ply("mudcharisma"), Val.Int(3));

        // Assert
        Assert.Equal("1.5", Assert.Single(speed.Effects).Get("value")!.AsString);
        Assert.Equal("clientr.mud_cha", Assert.Single(cha.Effects).Get("name")!.AsString);
    }

    [Fact]
    public void Set_Given_a_read_only_member_or_a_member_of_an_object_When_written_on_a_player_Then_bad_args()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Login(ext, "bob").Result.AsHandle;

        // Act
        ReplyFrame readOnly = Set(ext, h, Ply("mudid"), Val.Int(1));
        ReplyFrame objectOnly = Set(ext, h, Obj("nopick"), Val.True);

        // Assert
        Assert.Equal((ulong)Status.BadArgs, readOnly.Status);
        Assert.Equal((ulong)Status.BadArgs, objectOnly.Status);
    }

    [Fact]
    public void Flush_Given_a_logged_in_player_When_flushed_Then_the_account_file_is_written()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong h = Login(ext, "bob").Result.AsHandle;
        Set(ext, h, Ply("mudexp"), Val.Int(321));

        // Act
        string text = Flushed(ext, "accounts/bob.txt", t => t.Contains("exp 321"));

        // Assert
        Assert.Contains("exp 321\n", text);
    }

    [Fact]
    public void Manifest_Given_the_members_of_TMudPlayer_When_listed_Then_only_ismudobject_and_mudid_are_stable()
    {
        // Arrange / Act
        var stable = TMudPlayerMembers.Table.Members.Where(m => m.Stable).Select(m => m.Name).ToList();

        // Assert
        Assert.Equal(new[] { "mudid", "ismudobject" }, stable.OrderBy(n => n == "ismudobject").ToArray());
        Assert.Equal("player", TMudPlayerMembers.Table.Host);
        Assert.Equal("npc", TMudObjectMembers.Table.Host);
    }

    [Fact]
    public void Dispatch_Given_a_player_and_an_object_handle_When_the_same_member_id_is_used_Then_each_type_answers_its_own_members()
    {
        // Arrange
        using IExt ext = Start(ObjectWorld.Make());
        ulong player = Login(ext, "bob").Result.AsHandle;
        ulong obj = Ok(ext.Call(Manifest.OpMudAddObject, new[] { N(1), N(1), S("banana") }, level: "main.nw"));

        // Act
        ReplyFrame many = ext.Call(11, new[] { H(player), Val.List(Val.Int(Ply("mudhp")), Val.Int(Ply("mudlevel"))) });
        ReplyFrame wrong = ext.Call(9, new[] { H(obj), Val.Int(Ply("mudhp")) });

        // Assert
        Assert.Equal(new[] { 10L, 1L }, many.Result.Items.Select(v => v.AsInt));
        Assert.Equal((ulong)Status.BadArgs, wrong.Status);
    }

    // ------------------------------------------------------------------ names with control characters, many logouts (review of ext-objects, findings 2 and 4)

    [Theory]
    [InlineData("evil\nlevel 50")]
    [InlineData("evil\nhp 77\nend")]
    [InlineData("a\rb")]
    [InlineData("tab\there")]
    public void PlayerLogin_Given_an_account_name_with_a_control_character_When_called_Then_nil_one_log_line_and_no_file(string account)
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        using IExt ext = Start(world);

        // Act
        ReplyFrame r = Login(ext, account);
        Flushed(ext, "session.json");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.IsNil);
        Assert.Empty(r.Effects);
        Assert.Contains(ext.Logs, l => l.Text.Contains("control character"));
        Assert.False(Directory.Exists(Path.Combine(world.Path, "data", "accounts")) && Directory.GetFiles(Path.Combine(world.Path, "data", "accounts")).Length > 0);
    }

    [Fact]
    public void PlayerLogout_Given_many_accounts_and_a_long_quiet_time_When_each_logs_out_Then_nothing_is_written_before_the_quiet_time()
    {
        // Arrange
        TempFolder world = ObjectWorld.Make();
        var options = new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromHours(1), MaxDelay = TimeSpan.FromHours(1) } };
        using IExt ext = Start(world, options);

        // Act
        for (int i = 0; i < 60; i++)
        {
            Login(ext, "acc" + i);
            ext.Call(6, new[] { S("acc" + i) });
        }
        Thread.Sleep(400);

        // Assert
        string folder = Path.Combine(world.Path, "data", "accounts");
        Assert.True(!Directory.Exists(folder) || Directory.GetFiles(folder).Length == 0);
        ReplyFrame again = Login(ext, "acc0");
        Assert.Equal(ValKind.Handle, again.Result.Kind);
    }
}

/// <summary>The names of files and the text of a file's lines.</summary>
public class FileNamesTests
{
    [Theory]
    [InlineData("hall.nw", "hall.nw")]
    [InlineData("a/b", "a%2Fb")]
    [InlineData("a%b", "a%25b")]
    [InlineData(".hidden", "%2Ehidden")]
    [InlineData("..", "%2E.")]
    [InlineData("a b", "a%20b")]
    [InlineData("é", "%C3%A9")]
    [InlineData("x.tmp", "x.tmp")]
    [InlineData("-_Az09", "-_Az09")]
    public void Escape_Given_a_name_When_escaped_Then_only_safe_bytes_remain_and_the_rest_is_percent_hex_in_capitals(string name, string expected)
    {
        // Arrange / Act
        string escaped = FileNames.Escape(name);

        // Assert
        Assert.Equal(expected, escaped);
        Assert.True(FileNames.TryUnescape(escaped, out string back));
        Assert.Equal(name, back);
    }

    [Fact]
    public void Escape_Given_every_byte_value_When_escaped_Then_the_name_reads_back_and_no_slash_or_nul_is_left()
    {
        // Arrange
        string name = string.Concat(Enumerable.Range(1, 0xD7FF).Where(c => c is < 0xD800).Select(c => (char)c).Take(3000));

        // Act
        string escaped = FileNames.Escape(name);

        // Assert
        Assert.DoesNotContain('/', escaped);
        Assert.DoesNotContain('\0', escaped);
        Assert.DoesNotContain('\\', escaped);
        Assert.True(FileNames.TryUnescape(escaped, out string back));
        Assert.Equal(name, back);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("%4")]
    [InlineData("%zz")]
    [InlineData("%2f")]
    [InlineData("a/b")]
    [InlineData(".x")]
    [InlineData("a b")]
    public void TryUnescape_Given_text_that_Escape_does_not_make_When_read_Then_false(string text)
    {
        // Arrange / Act / Assert
        Assert.False(FileNames.TryUnescape(text, out _));
    }

    [Fact]
    public void LevelFile_And_AccountFile_Given_names_When_made_Then_a_relative_path_in_their_folder_or_null_for_empty_and_long_ones()
    {
        // Arrange / Act / Assert
        Assert.Equal("levels/hall.nw.map", FileNames.LevelFile("hall.nw"));
        Assert.Equal("accounts/bob.txt", FileNames.AccountFile("bob"));
        Assert.Null(FileNames.LevelFile(""));
        Assert.Null(FileNames.AccountFile(""));
        Assert.Null(FileNames.AccountFile(new string('a', 201)));
        Assert.NotNull(FileNames.AccountFile(new string('a', 200)));
        Assert.Null(FileNames.AccountFile(new string('é', 51)));
    }

    [Fact]
    public void EscapeLine_Given_text_with_controls_and_backslashes_When_escaped_Then_one_line_that_reads_back()
    {
        // Arrange
        string text = "a\\b\nc\r\nd\x01\x7f\\n\\x41 é";
        string bytes = FileText.ToBytesText(text);

        // Act
        string line = FileText.EscapeLine(bytes);

        // Assert
        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.Equal(text, FileText.FromBytesText(FileText.UnescapeLine(line)));
        Assert.Equal("\\\\x41", FileText.EscapeLine("\\x41"));
        Assert.Equal("\\q", FileText.UnescapeLine("\\q"));
        Assert.Equal("end\\", FileText.UnescapeLine("end\\"));
    }
}
