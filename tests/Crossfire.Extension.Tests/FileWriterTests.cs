// Not ported from a Crossfire file: tests of the file writer and of the lock on the data folder.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;
using System.Text;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

public class FileWriterTests
{
    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

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

    [Fact]
    public void Submit_Given_a_quiet_time_When_a_file_is_submitted_Then_it_is_not_written_before_it_and_is_written_after_it()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(600), MaxDelay = TimeSpan.FromSeconds(30) });
        string path = Path.Combine(folder.Path, "a.txt");

        // Act
        var watch = Stopwatch.StartNew();
        writer.Submit("a.txt", Bytes("one"));
        Thread.Sleep(200);
        bool earlyWritten = File.Exists(path);
        bool written = WaitFor(() => File.Exists(path), 4000);
        long at = watch.ElapsedMilliseconds;

        // Assert
        Assert.False(earlyWritten);
        Assert.True(written);
        Assert.InRange(at, 500, 3000);
        Assert.Equal("one", File.ReadAllText(path));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Submit_Given_changes_that_keep_coming_When_the_max_delay_passes_Then_the_file_is_written_though_the_quiet_time_never_came()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(500), MaxDelay = TimeSpan.FromMilliseconds(900) });
        string path = Path.Combine(folder.Path, "a.txt");

        // Act
        var watch = Stopwatch.StartNew();
        int n = 0;
        while (!File.Exists(path) && watch.ElapsedMilliseconds < 4000)
        {
            writer.Submit("a.txt", Bytes("v" + n++));
            Thread.Sleep(100);
        }

        // Assert
        Assert.True(File.Exists(path));
        Assert.InRange(watch.ElapsedMilliseconds, 700, 3000);
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Submit_Given_two_contents_of_one_name_When_written_Then_the_newest_is_the_file()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromHours(1), MaxDelay = TimeSpan.FromHours(1) });

        // Act
        writer.Submit("a.txt", Bytes("old"));
        writer.Submit("a.txt", Bytes("new"));
        bool done = writer.WaitWritten(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(done);
        Assert.Equal("new", File.ReadAllText(Path.Combine(folder.Path, "a.txt")));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Kick_Given_a_long_quiet_time_When_kicked_Then_the_file_is_written_soon_without_the_caller_waiting()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromHours(1), MaxDelay = TimeSpan.FromHours(1) });
        writer.Submit("a.txt", Bytes("x"));
        Thread.Sleep(100);
        Assert.False(File.Exists(Path.Combine(folder.Path, "a.txt")));

        // Act
        writer.Kick();

        // Assert
        Assert.True(WaitFor(() => File.Exists(Path.Combine(folder.Path, "a.txt"))));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void WaitWritten_Given_a_slow_write_When_waited_Then_it_returns_false_at_the_timeout_and_true_when_the_write_is_done()
    {
        // Arrange
        using var folder = new TempFolder();
        using var release = new ManualResetEventSlim();
        FileWriter writer = Make(folder, new FileWriterOptions { BeforeRename = _ => release.Wait(10_000) });
        writer.Submit("a.txt", Bytes("x"));

        // Act
        bool early = writer.WaitWritten(TimeSpan.FromMilliseconds(200));
        bool existsEarly = File.Exists(Path.Combine(folder.Path, "a.txt"));
        release.Set();
        bool late = writer.WaitWritten(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(early);
        Assert.False(existsEarly);
        Assert.True(late);
        Assert.True(File.Exists(Path.Combine(folder.Path, "a.txt")));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void IsIdle_Given_a_pending_file_When_asked_Then_false_and_no_write_is_forced_and_true_after_the_write()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromHours(1), MaxDelay = TimeSpan.FromHours(1) });
        Assert.True(writer.IsIdle());
        writer.Submit("a.txt", Bytes("x"));

        // Act
        bool idle = writer.IsIdle();
        Thread.Sleep(200);
        bool written = File.Exists(Path.Combine(folder.Path, "a.txt"));
        writer.Kick();

        // Assert
        Assert.False(idle);
        Assert.False(written);
        Assert.True(WaitFor(() => File.Exists(Path.Combine(folder.Path, "a.txt")) && writer.IsIdle()));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void WaitWritten_Given_nothing_submitted_When_waited_Then_true_at_once()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder);

        // Act
        bool done = writer.WaitWritten(TimeSpan.Zero);

        // Assert
        Assert.True(done);
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Discard_Given_pending_files_When_discarded_Then_nothing_is_written_and_waiting_returns_true()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(100), MaxDelay = TimeSpan.FromMilliseconds(100) });
        writer.Submit("a.txt", Bytes("x"));
        writer.Discard();

        // Act
        bool done = writer.WaitWritten(TimeSpan.FromSeconds(1));
        Thread.Sleep(400);

        // Assert
        Assert.True(done);
        Assert.Empty(Directory.GetFileSystemEntries(folder.Path));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Write_Given_a_reader_that_reads_all_the_time_When_two_large_contents_alternate_Then_every_read_is_one_of_them_whole()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.Zero, MaxDelay = TimeSpan.Zero });
        byte[] a = Enumerable.Repeat((byte)'a', 3_000_000).ToArray();
        byte[] b = Enumerable.Repeat((byte)'b', 2_000_000).ToArray();
        string path = Path.Combine(folder.Path, "big.bin");
        writer.Submit("big.bin", a);
        Assert.True(writer.WaitWritten(TimeSpan.FromSeconds(10)));
        int torn = 0;
        int reads = 0;
        bool stop = false;
        var reader = new Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                byte[] seen = File.ReadAllBytes(path);
                reads++;
                bool whole = (seen.Length == a.Length && seen.All(x => x == (byte)'a')) || (seen.Length == b.Length && seen.All(x => x == (byte)'b'));
                if (!whole)
                {
                    torn++;
                }
            }
        });
        reader.Start();

        // Act
        for (int i = 0; i < 30; i++)
        {
            writer.Submit("big.bin", i % 2 == 0 ? b : a);
            Assert.True(writer.WaitWritten(TimeSpan.FromSeconds(10)));
        }
        Volatile.Write(ref stop, true);
        reader.Join();

        // Assert
        Assert.True(reads > 5);
        Assert.Equal(0, torn);
        Assert.DoesNotContain(Directory.GetFiles(folder.Path), f => f.EndsWith(".tmp"));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Write_Given_a_name_in_a_folder_that_does_not_exist_When_written_Then_the_folder_is_made()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder);

        // Act
        writer.Submit("accounts/x/y.txt", Bytes("deep"));
        bool done = writer.WaitWritten(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(done);
        Assert.Equal("deep", File.ReadAllText(Path.Combine(folder.Path, "accounts", "x", "y.txt")));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Write_Given_a_failing_write_When_the_cause_goes_away_Then_the_file_is_written_by_the_next_pass_and_the_failure_was_counted_and_logged()
    {
        // Arrange
        using var folder = new TempFolder();
        string blocker = Path.Combine(folder.Path, "a.txt");
        Directory.CreateDirectory(blocker); // a folder where the file goes: the rename fails
        var counters = new Counters();
        var logs = new List<string>();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(50), MaxDelay = TimeSpan.FromMilliseconds(50) }, counters, logs);
        writer.Submit("a.txt", Bytes("x"));

        // Act
        bool first = writer.WaitWritten(TimeSpan.FromSeconds(5));
        Directory.Delete(blocker);
        bool second = writer.WaitWritten(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(first);
        Assert.True(counters.WriterFailures >= 1);
        Assert.Contains(logs, l => l.Contains("a.txt"));
        Assert.True(second);
        Assert.Equal("x", File.ReadAllText(Path.Combine(folder.Path, "a.txt")));
        Assert.DoesNotContain(Directory.GetFiles(folder.Path), f => f.EndsWith(".tmp"));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/etc/x")]
    [InlineData("../x")]
    [InlineData("a/../x")]
    [InlineData("a//x")]
    [InlineData("./x")]
    [InlineData("a\\x")]
    public void Submit_Given_a_name_that_is_not_under_the_data_folder_When_submitted_Then_an_argument_error(string name)
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => writer.Submit(name, Bytes("x")));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Write_Given_written_files_When_the_counters_are_read_Then_passes_files_and_time_are_counted()
    {
        // Arrange
        using var folder = new TempFolder();
        var counters = new Counters();
        FileWriter writer = Make(folder, null, counters);

        // Act
        writer.Submit("a.txt", Bytes("1"));
        writer.Submit("b.txt", Bytes("2"));
        Assert.True(writer.WaitWritten(TimeSpan.FromSeconds(5)));
        Val stats = Val.Map(counters.ToPairs().ToArray());

        // Assert
        Assert.Equal(1L, stats.Get("writer_writes")!.AsInt);
        Assert.Equal(2L, stats.Get("writer_files")!.AsInt);
        Assert.True(stats.Get("writer_ns_total")!.AsInt > 0);
        Assert.True(stats.Get("writer_ns_max")!.AsInt > 0);
        Assert.Equal(0L, stats.Get("writer_failures")!.AsInt);
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Stop_Given_a_running_writer_When_stopped_Then_its_thread_ends()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder);

        // Act
        bool ended = writer.Stop(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(ended);
    }

    // ------------------------------------------------------------------ review findings

    [Theory]
    [InlineData("acc/a\0b.txt")]
    [InlineData("acc/a\nb.txt")]
    [InlineData("acc/a\u007fb.txt")]
    [InlineData("acc/a.tmp")]
    public void Submit_Given_a_name_with_a_control_character_or_ending_in_tmp_When_submitted_Then_it_is_refused_before_it_reaches_the_thread(string name)
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(50) });

        // Act
        Action submit = () => writer.Submit(name, Bytes("x"));

        // Assert
        Assert.Throws<ArgumentException>(submit);
        Assert.True(writer.Stop(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Run_Given_a_write_that_throws_something_other_than_an_io_error_When_written_Then_the_thread_lives_the_failure_is_counted_and_the_file_comes_later()
    {
        // Arrange
        using var folder = new TempFolder();
        var counters = new Counters();
        int calls = 0;
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromMilliseconds(30),
            BeforeRename = _ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new InvalidOperationException("a surprise");
                }
            },
        }, counters);

        // Act
        writer.Submit("a.txt", Bytes("one"));
        bool written = WaitFor(() => File.Exists(Path.Combine(folder.Path, "a.txt")));

        // Assert
        Assert.True(written);
        Assert.Equal(1L, counters.WriterFailures);
        Assert.True(writer.Stop(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Run_Given_a_name_the_system_refuses_When_written_Then_it_is_given_up_once_and_other_files_are_still_written()
    {
        // Arrange
        using var folder = new TempFolder();
        var counters = new Counters();
        var logs = new List<string>();
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromMilliseconds(30),
            BeforeRename = path =>
            {
                if (path.Contains("bad"))
                {
                    throw new ArgumentException("refused");
                }
            },
        }, counters, logs);

        // Act
        writer.Submit("bad.txt", Bytes("x"));
        writer.Submit("good.txt", Bytes("y"));
        bool written = WaitFor(() => File.Exists(Path.Combine(folder.Path, "good.txt")));
        Thread.Sleep(300);
        bool waited = writer.WaitWritten(TimeSpan.FromSeconds(1));

        // Assert
        Assert.True(written);
        Assert.Equal(1L, counters.WriterFailures);
        Assert.False(File.Exists(Path.Combine(folder.Path, "bad.txt")));
        Assert.Single(logs);
        Assert.False(waited); // a file was given up: it is not all on disk
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Discard_Given_a_batch_that_is_being_written_When_discarded_before_its_rename_Then_the_file_is_never_written()
    {
        // Arrange
        using var folder = new TempFolder();
        using var inside = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromHours(1),
            MaxDelay = TimeSpan.FromHours(1),
            BeforeRename = _ =>
            {
                inside.Set();
                release.Wait(10_000);
            },
        });
        writer.Submit("a.txt", Bytes("one"));
        writer.Kick();
        Assert.True(inside.Wait(5000));

        // Act
        writer.Discard();
        release.Set();
        Thread.Sleep(300);

        // Assert
        Assert.False(File.Exists(Path.Combine(folder.Path, "a.txt")));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
        Assert.True(writer.WaitWritten(TimeSpan.FromSeconds(2)));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Discard_Given_a_batch_that_failed_while_being_written_When_discarded_Then_it_does_not_come_back()
    {
        // Arrange
        using var folder = new TempFolder();
        using var inside = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int calls = 0;
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromMilliseconds(30),
            BeforeRename = _ =>
            {
                Interlocked.Increment(ref calls);
                inside.Set();
                release.Wait(10_000);
                throw new IOException("disk full");
            },
        });
        writer.Submit("a.txt", Bytes("one"));
        Assert.True(inside.Wait(5000));

        // Act
        writer.Discard();
        release.Set();
        Thread.Sleep(500);

        // Assert
        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.False(File.Exists(Path.Combine(folder.Path, "a.txt")));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Run_Given_a_target_that_cannot_be_written_When_the_quiet_time_is_short_Then_the_tries_get_further_apart_and_only_the_first_failure_is_logged()
    {
        // Arrange
        using var folder = new TempFolder();
        int tries = 0;
        var logs = new List<string>();
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            MaxFailedPasses = 100,
            BeforeRename = _ =>
            {
                Interlocked.Increment(ref tries);
                throw new IOException("disk full");
            },
        }, logs: logs);

        // Act
        writer.Submit("a.txt", Bytes("x"));
        Thread.Sleep(1300); // tries at about 0.1, 0.2, 0.4, 0.8 and 1.6 s
        int count = Volatile.Read(ref tries);
        string[] seen;
        lock (logs)
        {
            seen = logs.ToArray();
        }

        // Assert
        Assert.InRange(count, 3, 5);
        Assert.Single(seen);
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Run_Given_a_target_that_cannot_be_written_When_it_fails_for_the_allowed_passes_Then_the_file_is_given_up_and_wait_written_says_false()
    {
        // Arrange
        using var folder = new TempFolder();
        var counters = new Counters();
        var logs = new List<string>();
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromMilliseconds(5),
            MaxDelay = TimeSpan.FromMilliseconds(5),
            MaxRetryDelay = TimeSpan.FromMilliseconds(10),
            MaxFailedPasses = 3,
            BeforeRename = _ => throw new IOException("disk full"),
        }, counters, logs);

        // Act
        writer.Submit("a.txt", Bytes("x"));
        bool done = WaitFor(() => counters.WriterFailures >= 3);
        Thread.Sleep(200);
        bool waited = writer.WaitWritten(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(done);
        Assert.Equal(3L, counters.WriterFailures); // not tried a fourth time
        Assert.False(waited);
        Assert.Contains(logs, l => l.Contains("given up"));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Run_Given_a_failed_pass_and_a_newer_submission_of_the_same_file_When_written_again_Then_the_newer_content_is_the_file()
    {
        // Arrange
        using var folder = new TempFolder();
        using var inside = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int calls = 0;
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromMilliseconds(20),
            MaxDelay = TimeSpan.FromMilliseconds(20),
            BeforeRename = _ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    inside.Set();
                    release.Wait(10_000);
                    throw new IOException("disk full");
                }
            },
        });
        writer.Submit("a.txt", Bytes("old"));
        Assert.True(inside.Wait(5000));

        // Act
        writer.Submit("a.txt", Bytes("new"));
        release.Set();
        bool written = WaitFor(() => File.Exists(Path.Combine(folder.Path, "a.txt")));
        Thread.Sleep(100);

        // Assert
        Assert.True(written);
        Assert.Equal("new", File.ReadAllText(Path.Combine(folder.Path, "a.txt")));
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Kick_Given_nothing_pending_When_kicked_or_waited_for_Then_the_next_submission_still_waits_for_the_quiet_time()
    {
        // Arrange
        using var folder = new TempFolder();
        FileWriter writer = Make(folder, new FileWriterOptions { Quiet = TimeSpan.FromHours(1), MaxDelay = TimeSpan.FromHours(1) });

        // Act
        writer.Kick();
        bool first = writer.WaitWritten(TimeSpan.FromSeconds(1));
        bool second = writer.WaitWritten(TimeSpan.FromSeconds(1));
        writer.Submit("b.txt", Bytes("b"));
        Thread.Sleep(500);

        // Assert
        Assert.True(first);
        Assert.True(second);
        Assert.False(File.Exists(Path.Combine(folder.Path, "b.txt")));
        writer.Discard();
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Kick_Given_a_batch_in_flight_with_nothing_pending_When_kicked_Then_the_next_submission_still_waits_for_the_quiet_time()
    {
        // Arrange
        using var folder = new TempFolder();
        using var inside = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int calls = 0;
        FileWriter writer = Make(folder, new FileWriterOptions
        {
            Quiet = TimeSpan.FromHours(1),
            MaxDelay = TimeSpan.FromHours(1),
            BeforeRename = _ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    inside.Set();
                    release.Wait(10_000);
                }
            },
        });
        writer.Submit("a.txt", Bytes("a"));
        writer.Kick();
        Assert.True(inside.Wait(5000));

        // Act
        writer.Kick(); // nothing is pending: the a.txt batch is already taken
        release.Set();
        Assert.True(writer.WaitWritten(TimeSpan.FromSeconds(5)));
        writer.Submit("b.txt", Bytes("b"));
        Thread.Sleep(400);

        // Assert
        Assert.False(File.Exists(Path.Combine(folder.Path, "b.txt")));
        writer.Discard();
        writer.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_Given_temporary_files_of_a_crashed_process_When_a_writer_starts_Then_they_are_removed_and_other_files_stay()
    {
        // Arrange
        using var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, "acc"));
        File.WriteAllText(Path.Combine(folder.Path, "session.json.4242.tmp"), "torn");
        File.WriteAllText(Path.Combine(folder.Path, "acc", "a.json.77.tmp"), "torn");
        File.WriteAllText(Path.Combine(folder.Path, "keep.tmp"), "not ours");
        File.WriteAllText(Path.Combine(folder.Path, "session.json"), "good");

        // Act
        FileWriter writer = Make(folder);

        // Assert
        Assert.False(File.Exists(Path.Combine(folder.Path, "session.json.4242.tmp")));
        Assert.False(File.Exists(Path.Combine(folder.Path, "acc", "a.json.77.tmp")));
        Assert.True(File.Exists(Path.Combine(folder.Path, "keep.tmp")));
        Assert.True(File.Exists(Path.Combine(folder.Path, "session.json")));
        writer.Stop(TimeSpan.FromSeconds(2));
    }
}
