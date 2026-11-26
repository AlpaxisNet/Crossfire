// Not ported from a Crossfire file: tests of the write order across files (docs/extension.md "Moves across files"): FileWriter.SubmitMove
// and the fault tests with a real child process that is killed between the two writes.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Crossfire.Extension.Tests;

public class FileWriterMoveTests
{
    private const string Level = "levels/hall.map";
    private const string Account = "accounts/ann.txt";
    private const string Other = "accounts/bob.txt";

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    private static string Read(TempFolder folder, string rel) =>
        File.Exists(Path.Combine(folder.Path, rel)) ? File.ReadAllText(Path.Combine(folder.Path, rel)) : "";

    private static int Count(TempFolder folder, params string[] rels) =>
        rels.Sum(r => Regex.Matches(Read(folder, r), "arch sword").Count);

    private static FileWriter Make(TempFolder folder, FileWriterOptions? options = null, Counters? counters = null, List<string>? logs = null) =>
        new(folder.Path, options ?? new FileWriterOptions(), counters ?? new Counters(), (_, text) => logs?.Add(text));

    private static bool WaitFor(Func<bool> condition, int milliseconds = 5000)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            if (condition())
            {
                return true;
            }
            Thread.Sleep(10);
        }
        return condition();
    }

    private static void Seed(TempFolder folder, string rel, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(folder.Path, rel))!);
        File.WriteAllText(Path.Combine(folder.Path, rel), text);
    }

    private static readonly FileWriterOptions Long = new() { Quiet = TimeSpan.FromHours(1), MaxDelay = TimeSpan.FromHours(1) };

    [Fact]
    public void SubmitMove_Given_a_long_quiet_time_When_a_move_is_submitted_Then_the_source_is_written_first_the_destination_second_and_at_once()
    {
        // Arrange
        using var folder = new TempFolder();
        Seed(folder, Level, "arch sword\n");
        var seen = new List<string>();
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromHours(1),
            MaxDelay = TimeSpan.FromHours(1),
            BetweenMove = (_, _) => seen.Add(Read(folder, Level) + "|" + Read(folder, Account)),
        });

        // Act
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        bool written = WaitFor(() => Read(folder, Account) == "arch sword\n");

        // Assert
        Assert.True(written); // nothing forced the write: the quiet time is an hour
        Assert.Equal(new[] { "empty\n|" }, seen); // between the writes: the source is new, the destination does not exist yet
        Assert.Equal("empty\n", Read(folder, Level));
        Assert.True(WaitFor(writer.IsIdle));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_two_moves_When_submitted_Then_they_are_written_in_the_order_submitted()
    {
        // Arrange
        using var folder = new TempFolder();
        var order = new List<string>();
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromHours(1),
            MaxDelay = TimeSpan.FromHours(1),
            BeforeRename = temporary => order.Add(Path.GetFileName(temporary).Split('.')[0]),
        });

        // Act
        writer.SubmitMove(Level, Bytes("1"), Account, Bytes("2"));
        writer.SubmitMove(Account, Bytes("3"), Other, Bytes("4"));
        Assert.True(writer.WaitWritten(TimeSpan.FromSeconds(5)));

        // Assert
        Assert.Equal(new[] { "hall", "ann", "ann", "bob" }, order);
        Assert.Equal("3", Read(folder, Account));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_a_source_that_cannot_be_written_When_tried_Then_the_destination_is_not_written_and_both_follow_in_order_when_the_cause_goes_away()
    {
        // Arrange
        using var folder = new TempFolder();
        string blocker = Path.Combine(folder.Path, Level);
        Directory.CreateDirectory(blocker); // a folder where the level's file goes
        var counters = new Counters();
        var between = new List<string>();
        FileWriter writer = Make(
            folder,
            new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(40), MaxDelay = TimeSpan.FromMilliseconds(40), BetweenMove = (s, d) => between.Add(s + ">" + d) },
            counters);

        // Act
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        bool failed = WaitFor(() => counters.WriterFailures >= 2);
        string destinationWhileFailing = Read(folder, Account);
        Directory.Delete(blocker);
        bool done = WaitFor(() => Read(folder, Account) == "arch sword\n");

        // Assert
        Assert.True(failed);
        Assert.Equal("", destinationWhileFailing); // never written while its source could not be
        Assert.True(done);
        Assert.Equal("empty\n", Read(folder, Level));
        Assert.Equal(new[] { Level + ">" + Account }, between);
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_a_source_that_never_works_When_the_passes_are_used_up_Then_the_move_is_given_up_with_neither_file_written()
    {
        // Arrange
        using var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, Level));
        var logs = new List<string>();
        FileWriter writer = Make(
            folder,
            new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(10), MaxDelay = TimeSpan.FromMilliseconds(10), MaxFailedPasses = 3 },
            null,
            logs);

        // Act
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        bool gaveUp = WaitFor(() => logs.Any(l => l.Contains("given up")));
        bool saved = writer.WaitWritten(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(gaveUp);
        Assert.False(saved);
        Assert.False(writer.IsIdle());
        Assert.Equal("", Read(folder, Account));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_a_destination_that_cannot_be_written_When_tried_Then_the_source_stays_written_and_only_the_destination_is_tried_again()
    {
        // Arrange
        using var folder = new TempFolder();
        string blocker = Path.Combine(folder.Path, Account);
        Directory.CreateDirectory(blocker);
        var counters = new Counters();
        int betweenCalls = 0;
        FileWriter writer = Make(
            folder,
            new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(40), MaxDelay = TimeSpan.FromMilliseconds(40), BetweenMove = (_, _) => betweenCalls++ },
            counters);

        // Act
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        bool failed = WaitFor(() => counters.WriterFailures >= 2);
        Directory.Delete(blocker);
        bool done = WaitFor(() => Read(folder, Account) == "arch sword\n");

        // Assert
        Assert.True(failed);
        Assert.Equal("empty\n", Read(folder, Level));
        Assert.True(done);
        Assert.Equal(1, betweenCalls); // the move was not tried again as a whole
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_a_waiting_move_When_a_file_of_it_is_submitted_again_Then_the_move_writes_the_newest_content()
    {
        // Arrange
        using var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, Level));
        var counters = new Counters();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(200), MaxDelay = TimeSpan.FromMilliseconds(200) }, counters);
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("old\n"));
        Assert.True(WaitFor(() => counters.WriterFailures >= 1)); // the move waits for its back-off now

        // Act
        writer.Submit(Account, Bytes("arch sword\n"));
        writer.Submit(Level, Bytes("newest\n"));
        Directory.Delete(Path.Combine(folder.Path, Level));
        bool done = WaitFor(() => Read(folder, Account) != "");

        // Assert
        Assert.True(done);
        Assert.Equal("arch sword\n", Read(folder, Account));
        Assert.Equal("newest\n", Read(folder, Level));
        Assert.True(writer.WaitWritten(TimeSpan.FromSeconds(5)));
        Assert.Equal("arch sword\n", Read(folder, Account));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_older_submissions_of_its_files_When_submitted_Then_they_are_superseded_and_never_written()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, Long);
        writer.Submit(Level, Bytes("stale level\n"));
        writer.Submit(Account, Bytes("stale account\n"));

        // Act
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        Assert.True(WaitFor(() => Read(folder, Account) != ""));
        Assert.True(writer.WaitWritten(TimeSpan.FromSeconds(5)));

        // Assert
        Assert.Equal("empty\n", Read(folder, Level));
        Assert.Equal("arch sword\n", Read(folder, Account));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_a_failed_move_and_a_later_move_of_a_file_of_it_When_tried_Then_the_later_move_waits_for_the_first()
    {
        // Arrange: move 1 (level to ann) cannot write the level; move 2 (ann to bob) would put the item in bob while the level still has it
        using var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, Level));
        var counters = new Counters();
        var order = new List<string>();
        FileWriter writer = Make(
            folder,
            new FileWriterOptions
            {
                Quiet = TimeSpan.FromMilliseconds(40),
                MaxDelay = TimeSpan.FromMilliseconds(40),
                BeforeRename = temporary => order.Add(Path.GetFileName(temporary).Split('.')[0]),
            },
            counters);
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        writer.SubmitMove(Account, Bytes("ann without\n"), Other, Bytes("arch sword\n"));

        // Act
        bool failed = WaitFor(() => counters.WriterFailures >= 2);
        string bobWhileFailing = Read(folder, Other);
        Directory.Delete(Path.Combine(folder.Path, Level));
        bool done = WaitFor(() => Read(folder, Other) != "");

        // Assert
        Assert.True(failed);
        Assert.Equal("", bobWhileFailing);
        Assert.True(done);
        // the level's temporary file is made at every failed try (the rename fails): the first write of ann comes after the last of those
        Assert.True(order.IndexOf("ann") > order.LastIndexOf("hall"));
        Assert.Equal(new[] { "ann", "ann", "bob" }, order.Where(o => o != "hall"));
        Assert.Equal(1, Count(folder, Level, Account, Other));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_a_waiting_move_When_other_files_are_submitted_Then_they_are_written_at_their_own_times()
    {
        // Arrange
        using var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, Level));
        var counters = new Counters();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(30), MaxDelay = TimeSpan.FromMilliseconds(30), MaxRetryDelay = TimeSpan.FromHours(1) }, counters);
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        Assert.True(WaitFor(() => counters.WriterFailures >= 2)); // the move's back-off has doubled

        // Act
        writer.Submit("accounts/carl.txt", Bytes("carl\n"));
        bool other = WaitFor(() => Read(folder, "accounts/carl.txt") == "carl\n", 500);

        // Assert
        Assert.True(other);
        Assert.Equal("", Read(folder, Account));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_a_waiting_move_When_discarded_Then_nothing_of_it_is_written()
    {
        // Arrange
        using var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, Level));
        var counters = new Counters();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(30), MaxDelay = TimeSpan.FromMilliseconds(30) }, counters);
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        Assert.True(WaitFor(() => counters.WriterFailures >= 1));
        Directory.Delete(Path.Combine(folder.Path, Level));

        // Act
        writer.Discard();
        Thread.Sleep(300);

        // Assert
        Assert.Equal("", Read(folder, Account));
        Assert.False(File.Exists(Path.Combine(folder.Path, Level)));
        Assert.True(writer.IsIdle());
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_a_source_name_the_system_refuses_When_tried_Then_the_destination_is_not_written_and_the_move_is_given_up_at_once()
    {
        // Arrange
        using var folder = new TempFolder();
        var logs = new List<string>();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(10) }, null, logs);
        string tooLong = "levels/" + new string('a', 5000) + ".map";

        // Act
        writer.SubmitMove(tooLong, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        bool gaveUp = WaitFor(() => logs.Any(l => l.Contains("given up")));

        // Assert
        Assert.True(gaveUp);
        Assert.Equal("", Read(folder, Account));
        Assert.False(writer.WaitWritten(TimeSpan.FromSeconds(2)));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SubmitMove_Given_a_destination_name_the_system_refuses_When_tried_Then_the_source_stays_written_and_the_destination_is_given_up()
    {
        // Arrange
        using var folder = new TempFolder();
        var logs = new List<string>();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(10) }, null, logs);
        string tooLong = "accounts/" + new string('a', 5000) + ".txt";

        // Act
        writer.SubmitMove(Level, Bytes("empty\n"), tooLong, Bytes("arch sword\n"));
        bool gaveUp = WaitFor(() => logs.Any(l => l.Contains("given up")));

        // Assert
        Assert.True(gaveUp);
        Assert.Equal("empty\n", Read(folder, Level));
        Assert.False(writer.WaitWritten(TimeSpan.FromSeconds(2)));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("levels/a.map", "levels/a.map")]
    [InlineData("../x", "accounts/a.txt")]
    [InlineData("levels/a.map", "/x")]
    [InlineData("levels/a.map", "accounts/a.tmp")]
    public void SubmitMove_Given_the_same_file_twice_or_a_path_outside_the_data_folder_When_submitted_Then_an_argument_error_and_nothing_is_queued(string source, string destination)
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, Long);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => writer.SubmitMove(source, Bytes("1"), destination, Bytes("2")));
        Assert.True(writer.IsIdle());
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void IsIdle_Given_a_move_in_the_queue_When_asked_Then_not_idle_until_it_is_written()
    {
        // Arrange
        using var folder = new TempFolder();
        using var release = new ManualResetEventSlim();
        FileWriter writer = Make(folder, new FileWriterOptions { BetweenMove = (_, _) => release.Wait(10_000) });

        // Act
        writer.SubmitMove(Level, Bytes("empty\n"), Account, Bytes("arch sword\n"));
        bool idleWhileWriting = writer.IsIdle();
        release.Set();
        bool idleAfter = WaitFor(writer.IsIdle);

        // Assert
        Assert.False(idleWhileWriting);
        Assert.True(idleAfter);
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    // ------------------------------------------------------------------ the fault tests: a real process killed between the two writes

    // pick up (level to account), drop (account to level) and a trade (account to account): the item is in the source file at first
    [Theory]
    [InlineData("pickup", Level, Account)]
    [InlineData("drop", Account, Level)]
    [InlineData("trade", Account, Other)]
    public void Kill_Given_a_process_killed_between_the_two_writes_of_a_move_When_both_files_are_read_Then_the_item_is_lost_never_copied(string kind, string source, string destination)
    {
        // Arrange
        using var folder = new TempFolder();
        using var scratch = new TempFolder();
        Seed(folder, source, "arch sword\nend\n");
        Seed(folder, destination, "end\n");
        string sourceContent = Path.Combine(scratch.Path, "source.txt");
        string destinationContent = Path.Combine(scratch.Path, "destination.txt");
        File.WriteAllText(sourceContent, "end\n");
        File.WriteAllText(destinationContent, "arch sword\nend\n");
        string marker = Path.Combine(scratch.Path, "marker");
        Assert.Equal(1, Count(folder, source, destination));

        // Act
        string child = Path.Combine(AppContext.BaseDirectory, "Crossfire.WriterChild.dll");
        var start = new ProcessStartInfo(Environment.ProcessPath ?? "dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(child);
        foreach (string a in new[] { folder.Path, source, sourceContent, destination, destinationContent, marker })
        {
            start.ArgumentList.Add(a);
        }
        using Process process = Process.Start(start)!;
        bool reached = WaitFor(() => File.Exists(marker), 30_000);
        process.Kill(true); // SIGKILL: nothing of the process runs on
        process.WaitForExit(10_000);

        // Assert
        Assert.True(reached, kind);
        Assert.Equal("end\n", Read(folder, source)); // the source side was written first
        Assert.Equal("end\n", Read(folder, destination)); // the destination is as it was
        Assert.Equal(0, Count(folder, source, destination)); // lost, never two
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(Level, Account)]
    [InlineData(Account, Level)]
    [InlineData(Account, Other)]
    public void SubmitMove_Given_a_move_that_was_written_whole_When_both_files_are_read_Then_the_item_is_in_the_destination_once(string source, string destination)
    {
        // Arrange
        using var folder = new TempFolder();
        Seed(folder, source, "arch sword\nend\n");
        Seed(folder, destination, "end\n");
        FileWriter writer = Make(folder, Long);

        // Act
        writer.SubmitMove(source, Bytes("end\n"), destination, Bytes("arch sword\nend\n"));
        Assert.True(WaitFor(() => Count(folder, destination) == 1));

        // Assert
        Assert.Equal(1, Count(folder, source, destination));
        Assert.Equal(0, Count(folder, source));
        writer.Stop(TimeSpan.FromSeconds(2));
    }
}
