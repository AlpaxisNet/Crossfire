// Not ported from a Crossfire file: tests of the lock on the data folder (docs/protocol.md section 1, "Running"), with a second process
// (the flock(1) tool) as the other side, so that the lock is shown to be a real flock that another process sees.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

/// <summary>Another process that holds the flock of a file: <c>flock -x file sleep n</c>.</summary>
internal sealed class ExternalLock : IDisposable
{
    private readonly Process process;

    public ExternalLock(string path, int seconds)
    {
        process = Process.Start(new ProcessStartInfo("flock", new[] { "-x", path, "sleep", seconds.ToString() })
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        // the tool takes the lock a moment after it starts: wait until this process sees it held
        var watch = Stopwatch.StartNew();
        for (; ; )
        {
            using DataLock? probe = DataLock.Acquire(path, TimeSpan.Zero);
            if (probe == null)
            {
                return;
            }
            Assert.True(watch.ElapsedMilliseconds < 10_000, "flock(1) did not take the lock");
            probe.Dispose();
            Thread.Sleep(20);
        }
    }

    /// <summary>The exit status of <c>flock -n -x file true</c>: 0 when the lock was free, 1 when somebody holds it.</summary>
    public static int Probe(string path)
    {
        using Process probe = Process.Start(new ProcessStartInfo("flock", new[] { "-n", "-x", path, "true" }) { RedirectStandardError = true })!;
        probe.WaitForExit();
        return probe.ExitCode;
    }

    public void Dispose()
    {
        try
        {
            process.Kill(true);
        }
        catch (InvalidOperationException)
        {
        }
        process.WaitForExit();
        process.Dispose();
    }
}

public class LockTests
{
    private static string LockPath(TempFolder folder) => Path.Combine(folder.Path, ".lock");

    private static (ScriptedHost Host, TempFolder World) Handshaken(ExtensionOptions options, string archFolder = "Arch")
    {
        TempFolder world = TestWorld.Make(archFolder);
        Directory.CreateDirectory(Path.Combine(world.Path, "data"));
        return (ScriptedHost.Handshake(world.Path, out _, options), world);
    }

    [Fact]
    public void Acquire_Given_a_missing_file_When_acquired_Then_the_file_is_made_and_another_process_sees_the_lock_held_until_it_is_disposed()
    {
        // Arrange
        using var folder = new TempFolder();

        // Act
        using DataLock? taken = DataLock.Acquire(LockPath(folder), TimeSpan.Zero);
        int whileHeld = ExternalLock.Probe(LockPath(folder));
        taken!.Dispose();
        int afterwards = ExternalLock.Probe(LockPath(folder));

        // Assert
        Assert.NotNull(taken);
        Assert.True(File.Exists(LockPath(folder)));
        Assert.Equal(1, whileHeld);
        Assert.Equal(0, afterwards);
    }

    [Fact]
    public void Acquire_Given_another_process_holding_the_lock_When_acquired_without_waiting_Then_null_and_after_that_process_ends_the_lock_is_taken()
    {
        // Arrange
        using var folder = new TempFolder();
        ExternalLock other = new(LockPath(folder), 60);

        // Act
        DataLock? whileHeld = DataLock.Acquire(LockPath(folder), TimeSpan.Zero);
        other.Dispose();
        using DataLock? afterwards = DataLock.Acquire(LockPath(folder), TimeSpan.Zero);

        // Assert
        Assert.Null(whileHeld);
        Assert.NotNull(afterwards);
    }

    [Fact]
    public void Acquire_Given_a_lock_held_in_this_process_When_acquired_again_with_a_wait_Then_null_after_the_wait()
    {
        // Arrange
        using var folder = new TempFolder();
        using DataLock? first = DataLock.Acquire(LockPath(folder), TimeSpan.Zero);

        // Act
        var watch = Stopwatch.StartNew();
        DataLock? second = DataLock.Acquire(LockPath(folder), TimeSpan.FromMilliseconds(400));

        // Assert
        Assert.NotNull(first);
        Assert.Null(second);
        Assert.InRange(watch.ElapsedMilliseconds, 350, 4000);
    }

    [Fact]
    public void Acquire_Given_the_holder_ending_during_the_wait_When_acquired_Then_the_lock_is_taken_before_the_wait_is_over()
    {
        // Arrange
        using var folder = new TempFolder();
        using var other = new ExternalLock(LockPath(folder), 1);

        // Act
        var watch = Stopwatch.StartNew();
        using DataLock? taken = DataLock.Acquire(LockPath(folder), TimeSpan.FromSeconds(20));

        // Assert
        Assert.NotNull(taken);
        Assert.InRange(watch.ElapsedMilliseconds, 300, 10_000);
    }

    [Fact]
    public void Acquire_Given_a_cancelled_token_When_the_lock_is_held_Then_null_at_once()
    {
        // Arrange
        using var folder = new TempFolder();
        using DataLock? first = DataLock.Acquire(LockPath(folder), TimeSpan.Zero);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        // Act
        var watch = Stopwatch.StartNew();
        DataLock? second = DataLock.Acquire(LockPath(folder), TimeSpan.FromSeconds(20), cancelled.Token);

        // Assert
        Assert.Null(second);
        Assert.True(watch.ElapsedMilliseconds < 2000);
    }

    [Fact]
    public void SessionInit_Given_another_process_holding_the_lock_When_the_wait_is_over_Then_busy_no_world_is_loaded_and_the_exit_is_ok()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Handshaken(new ExtensionOptions { LockWait = TimeSpan.FromMilliseconds(300) });
        using (host)
        using (world)
        using (new ExternalLock(Path.Combine(world.Path, "data", ".lock"), 60))
        {
            // Act
            ReplyFrame init = host.Init();
            ReplyFrame next = host.Call(Manifest.OpFindArchetype, new[] { Val.Str("sword") });
            host.CloseHostSide();
            int exit = host.WaitExit();

            // Assert
            Assert.Equal((ulong)Status.Busy, init.Status);
            Assert.Equal(ValKind.String, init.Result.Kind);
            Assert.Equal((ulong)Status.Unavailable, next.Status);
            Assert.Equal(ExtensionRunner.ExitOk, exit);
            Assert.False(File.Exists(Path.Combine(world.Path, "data", SessionState.FileName)));
        }
    }

    [Fact]
    public void SessionInit_Given_a_world_without_arch_and_another_process_holding_the_lock_When_asked_Then_busy_because_the_lock_comes_before_the_world()
    {
        // Arrange
        using var world = new TempFolder();
        Directory.CreateDirectory(Path.Combine(world.Path, "data"));
        using var other = new ExternalLock(Path.Combine(world.Path, "data", ".lock"), 60);
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, new ExtensionOptions { LockWait = TimeSpan.FromMilliseconds(100) });

        // Act
        ReplyFrame init = host.Init();

        // Assert
        Assert.Equal((ulong)Status.Busy, init.Status);
    }

    [Fact]
    public void SessionInit_Given_an_older_process_that_ends_during_the_wait_When_asked_Then_the_world_is_loaded_after_the_lock_is_free()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Handshaken(new ExtensionOptions());
        using (host)
        using (world)
        using (new ExternalLock(Path.Combine(world.Path, "data", ".lock"), 1))
        {
            // Act
            var watch = Stopwatch.StartNew();
            ReplyFrame init = host.Init();

            // Assert
            Assert.Equal(0UL, init.Status);
            Assert.Equal((long)TestWorld.Blocks, init.Result.Get("archetypes")!.AsInt);
            Assert.InRange(watch.ElapsedMilliseconds, 300, 15_000);
        }
    }

    [Fact]
    public void SessionInit_Given_the_session_is_running_When_another_process_probes_the_lock_Then_it_is_held_until_the_extension_exits()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Handshaken(new ExtensionOptions());
        using (host)
        using (world)
        {
            string path = Path.Combine(world.Path, "data", ".lock");
            Assert.Equal(0UL, host.Init().Status);

            // Act
            int whileRunning = ExternalLock.Probe(path);
            host.CloseHostSide();
            host.WaitExit();
            int afterExit = ExternalLock.Probe(path);

            // Assert
            Assert.Equal(1, whileRunning);
            Assert.Equal(0, afterExit);
        }
    }

    [Fact]
    public void SessionInit_Given_a_data_folder_that_does_not_exist_When_asked_Then_it_is_made_with_the_lock_file_in_it()
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, new ExtensionOptions());
        Assert.False(Directory.Exists(Path.Combine(world.Path, "data")));

        // Act
        ReplyFrame init = host.Init();

        // Assert
        Assert.Equal(0UL, init.Status);
        Assert.True(File.Exists(Path.Combine(world.Path, "data", ".lock")));
    }

    [Fact]
    public void SessionInit_Given_the_host_closing_while_the_lock_is_awaited_When_the_wait_is_long_Then_the_extension_exits_within_5_s_without_a_world()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Handshaken(new ExtensionOptions { LockWait = TimeSpan.FromSeconds(60) });
        using (host)
        using (world)
        using (new ExternalLock(Path.Combine(world.Path, "data", ".lock"), 60))
        {
            host.SendReq(1);
            Thread.Sleep(200);

            // Act
            var watch = Stopwatch.StartNew();
            host.CloseHostSide();
            int exit = host.WaitExit();

            // Assert
            Assert.Equal(ExtensionRunner.ExitOk, exit);
            Assert.True(watch.ElapsedMilliseconds < 1500, "the wait for the lock was not cut short: " + watch.ElapsedMilliseconds + " ms");
        }
    }

    [Fact]
    public void SessionInit_Given_a_first_init_that_failed_after_the_lock_When_asked_again_Then_it_does_not_wait_for_its_own_lock()
    {
        // Arrange
        using var world = new TempFolder();
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, new ExtensionOptions { LockWait = TimeSpan.FromMilliseconds(200) });
        ReplyFrame failed = host.Init(); // no Arch folder
        Directory.CreateDirectory(Path.Combine(world.Path, "Arch", "T"));
        File.WriteAllText(Path.Combine(world.Path, "Arch", "T", "t.arc"), TestWorld.Arc);

        // Act
        ReplyFrame again = host.Init();

        // Assert
        Assert.Equal((ulong)Status.Internal, failed.Status);
        Assert.Equal(0UL, again.Status);
    }
}
