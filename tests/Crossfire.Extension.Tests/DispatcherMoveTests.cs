// Not ported from a Crossfire file: tests of Dispatcher.SubmitMove, the call of the operations that move an object across a level file and
// an account file (docs/extension.md "Moves across files").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Reflection;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

public class DispatcherMoveTests
{
    private static readonly FileWriterOptions LongQuiet = new() { Quiet = TimeSpan.FromHours(1), MaxDelay = TimeSpan.FromHours(1) };

    private static T Field<T>(Dispatcher d, string name) =>
        (T)typeof(Dispatcher).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(d)!;

    private static Dispatcher Start(TempFolder world, FileWriterOptions? writer = null)
    {
        var options = new ExtensionOptions { Writer = writer ?? LongQuiet };
        HostHello hello = HostHello.Parse(ScriptedHost.HostHello(world.Path, 1, maxFrame: 16_777_216));
        var d = new Dispatcher(hello, 16_777_216, (_, _) => { }, options, new Counters());
        Assert.Equal(0UL, Call(d, 1).Status);
        return d;
    }

    private static ulong next = 1;

    private static ReplyFrame Call(Dispatcher d, ulong op, IEnumerable<Val>? args = null, string? level = null, string? player = null) =>
        d.Handle(new ReqFrame(next++, op, Array.Empty<ulong>(),
            Val.Map(("player", player == null ? Val.Nil : Val.Str(player)), ("level", level == null ? Val.Nil : Val.Str(level))),
            (args ?? Array.Empty<Val>()).ToArray()));

    private static bool WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 500 && !condition(); i++)
        {
            Thread.Sleep(10);
        }
        return condition();
    }

    private static string Data(TempFolder world, string rel) => Path.Combine(world.Path, "data", rel);

    [Fact]
    public void SubmitMove_Given_a_level_and_an_account_When_a_pick_up_is_recorded_Then_the_level_is_written_first_the_account_second_at_once_and_both_are_clean()
    {
        // Arrange
        using TempFolder world = ObjectWorld.Make(("hall.nw", ObjectWorld.SeedHall));
        Dispatcher d = Start(world);
        Call(d, 7, new[] { Say.S("hall.nw"), Val.Nil }, "hall.nw");
        Call(d, 5, new[] { Say.S("ann") }, player: "ann");
        Level level = Field<Dictionary<string, Level>>(d, "levels")["hall.nw"];
        PlayerState ann = Field<Dictionary<string, PlayerState>>(d, "players")["ann"];
        level.Dirty = true;
        ann.Dirty = true;

        // Act
        d.SubmitMove(level, null, null, ann);
        bool written = WaitFor(() => File.Exists(Data(world, "accounts/ann.txt")));

        // Assert
        Assert.True(written); // a quiet time of an hour: written at once
        Assert.Single(Directory.GetFiles(Data(world, "levels")));
        Assert.False(level.Dirty);
        Assert.False(ann.Dirty);
        d.Finish(true);
        d.Release();
    }

    [Fact]
    public void SubmitMove_Given_two_accounts_When_a_trade_is_recorded_Then_both_files_are_written_at_once()
    {
        // Arrange
        using TempFolder world = ObjectWorld.Make();
        Dispatcher d = Start(world);
        Call(d, 5, new[] { Say.S("ann") }, player: "ann");
        Call(d, 5, new[] { Say.S("bob") }, player: "bob");
        var players = Field<Dictionary<string, PlayerState>>(d, "players");

        // Act
        d.SubmitMove(null, players["ann"], null, players["bob"]);
        bool written = WaitFor(() => Directory.Exists(Data(world, "accounts")) && Directory.GetFiles(Data(world, "accounts")).Length == 2);

        // Assert
        Assert.True(written);
        d.Finish(true);
        d.Release();
    }

    [Fact]
    public void SubmitMove_Given_only_one_side_When_called_Then_that_file_goes_through_the_writer_as_an_ordinary_file_after_the_quiet_time()
    {
        // Arrange
        using TempFolder world = ObjectWorld.Make();
        Dispatcher d = Start(world);
        Call(d, 5, new[] { Say.S("ann") }, player: "ann");
        PlayerState ann = Field<Dictionary<string, PlayerState>>(d, "players")["ann"];

        // Act
        d.SubmitMove(null, ann, null, null);
        Thread.Sleep(300);

        // Assert
        Assert.False(File.Exists(Data(world, "accounts/ann.txt"))); // not at once: nothing crossed two files
        d.Finish(true);
        Assert.True(File.Exists(Data(world, "accounts/ann.txt")));
        d.Release();
    }
}
