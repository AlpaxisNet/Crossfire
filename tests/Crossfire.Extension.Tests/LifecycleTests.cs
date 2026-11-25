// Not ported from a Crossfire file: tests of the lifecycle of the extension against a scripted host: flush, shutdown, the exit, the
// state file, ticks, the spin of the worker and the counters of `stats`.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;
using System.Text.Json;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

public class LifecycleTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <summary>Options whose writer waits an hour: nothing is written unless something forces it.</summary>
    private static ExtensionOptions Lazy(Action<string>? beforeRename = null, TimeSpan? spin = null, bool wantsTick = false, Action<ulong>? afterDequeue = null,
        TimeSpan? exitSave = null) => new()
    {
        Writer = new FileWriterOptions { Quiet = Hour, MaxDelay = Hour, BeforeRename = beforeRename },
        Spin = spin ?? TimeSpan.Zero,
        WantsTick = wantsTick,
        AfterDequeue = afterDequeue,
        ExitSaveTimeout = exitSave ?? TimeSpan.FromSeconds(2),
    };

    private static (ScriptedHost Host, TempFolder World) Started(ExtensionOptions options)
    {
        TempFolder world = TestWorld.Make();
        ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, options, epoch: 7);
        Assert.Equal(0UL, host.Init().Status);
        return (host, world);
    }

    private static string StateFile(TempFolder world) => Path.Combine(world.Path, "data", SessionState.FileName);

    private static JsonDocument ReadState(TempFolder world) => JsonDocument.Parse(File.ReadAllText(StateFile(world)));

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

    private static long Stat(ReplyFrame stats, string key) => stats.Result.Get(key)?.AsInt ?? throw new InvalidOperationException("no stat " + key);

    // ------------------------------------------------------------------ the writer in the session

    [Fact]
    public void SessionInit_Given_a_short_quiet_time_When_the_world_is_loaded_Then_the_state_file_is_written_within_it_and_holds_what_the_process_knows()
    {
        // Arrange
        using var world = TestWorld.Make();
        var options = new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(300), MaxDelay = TimeSpan.FromSeconds(1) } };
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, options, epoch: 7);

        // Act
        host.Init();
        bool early = File.Exists(StateFile(world));
        bool written = WaitFor(() => File.Exists(StateFile(world)), 4000);

        // Assert
        Assert.False(early);
        Assert.True(written);
        using JsonDocument state = ReadState(world);
        JsonElement root = state.RootElement;
        Assert.Equal(1, root.GetProperty("format").GetInt32());
        Assert.Equal("init", root.GetProperty("reason").GetString());
        Assert.Equal(7, root.GetProperty("epoch").GetInt64());
        Assert.Equal("Test", root.GetProperty("server_name").GetString());
        Assert.Equal(TestWorld.Blocks, root.GetProperty("archetypes").GetInt64());
        Assert.True(root.TryGetProperty("started_utc", out _));
        Assert.True(root.TryGetProperty("initialized_utc", out _));
        Assert.True(root.TryGetProperty("saved_utc", out _));
        Assert.Equal(-1, root.GetProperty("last_tick_ms").GetInt64());
    }

    [Fact]
    public void Flush_Given_a_writer_with_a_long_quiet_time_When_flushed_Then_the_files_are_written_at_once()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy());
        using (host)
        using (world)
        {
            Assert.False(File.Exists(StateFile(world)));

            // Act
            ReplyFrame reply = host.Call(3);

            // Assert
            Assert.Equal(0UL, reply.Status);
            Assert.True(reply.Result.IsNil);
            Assert.True(WaitFor(() => File.Exists(StateFile(world)) && ReadState(world).RootElement.GetProperty("reason").GetString() == "flush"));
            Assert.Equal(1, ReadState(world).RootElement.GetProperty("flushes").GetInt64());
        }
    }

    [Fact]
    public void Flush_Given_a_slow_write_When_flushed_Then_the_reply_comes_before_the_files_are_written_and_they_are_written_afterwards()
    {
        // Arrange
        using var release = new ManualResetEventSlim();
        (ScriptedHost host, TempFolder world) = Started(Lazy(_ => release.Wait(10_000)));
        using (host)
        using (world)
        {
            // Act
            ReplyFrame reply = host.Call(3);
            bool writtenAtReply = File.Exists(StateFile(world));
            release.Set();

            // Assert
            Assert.Equal(0UL, reply.Status);
            Assert.False(writtenAtReply);
            Assert.True(WaitFor(() => File.Exists(StateFile(world))));
        }
    }

    [Fact]
    public void Flush_Given_an_argument_When_called_Then_bad_args_and_nothing_is_written()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy());
        using (host)
        using (world)
        {
            // Act
            ReplyFrame reply = host.Call(3, new[] { Val.Nil });
            Thread.Sleep(200);

            // Assert
            Assert.Equal((ulong)Status.BadArgs, reply.Status);
            Assert.False(File.Exists(StateFile(world)));
        }
    }

    [Fact]
    public void Shutdown_Given_save_true_and_a_slow_write_When_called_Then_the_reply_comes_after_the_files_are_written()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(_ => Thread.Sleep(400)));
        using (host)
        using (world)
        {
            // Act
            var watch = Stopwatch.StartNew();
            ReplyFrame reply = host.Call(4, new[] { Val.True });
            long took = watch.ElapsedMilliseconds;
            bool writtenAtReply = File.Exists(StateFile(world));

            // Assert
            Assert.Equal(0UL, reply.Status);
            Assert.True(writtenAtReply);
            Assert.True(took >= 350, "the reply came after " + took + " ms");
            Assert.Equal("shutdown", ReadState(world).RootElement.GetProperty("reason").GetString());
        }
    }

    [Fact]
    public void Shutdown_Given_save_true_When_the_host_then_closes_Then_the_files_are_not_written_a_second_time()
    {
        // Arrange
        int renames = 0;
        (ScriptedHost host, TempFolder world) = Started(Lazy(_ => Interlocked.Increment(ref renames)));
        using (host)
        using (world)
        {
            host.Call(4, new[] { Val.True });

            // Act
            host.CloseHostSide();
            int exit = host.WaitExit();

            // Assert
            Assert.Equal(ExtensionRunner.ExitOk, exit);
            Assert.Equal(1, Volatile.Read(ref renames));
            Assert.Equal("shutdown", ReadState(world).RootElement.GetProperty("reason").GetString());
        }
    }

    [Fact]
    public void Shutdown_Given_save_true_and_files_that_cannot_be_written_When_called_Then_internal_and_the_session_goes_on()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy());
        using (host)
        using (world)
        {
            Directory.CreateDirectory(StateFile(world)); // a folder where the file goes

            // Act
            ReplyFrame reply = host.Call(4, new[] { Val.True });
            ReplyFrame after = host.Call(15);

            // Assert
            Assert.Equal((ulong)Status.Internal, reply.Status);
            Assert.Equal(0UL, after.Status);
            Assert.True(Stat(after, "writer_failures") >= 1);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shutdown_Given_save_false_or_nil_and_a_snapshot_waiting_in_the_writer_When_called_Then_that_snapshot_is_never_written_not_even_by_the_exit(bool nil)
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy()); // the snapshot of the init waits an hour
        using (host)
        using (world)
        {
            // Act
            ReplyFrame reply = host.Call(4, new[] { nil ? Val.Nil : Val.False });
            host.CloseHostSide();
            int exit = host.WaitExit();

            // Assert
            Assert.Equal(0UL, reply.Status);
            Assert.True(reply.Result.IsNil);
            Assert.Equal(ExtensionRunner.ExitOk, exit);
            Assert.False(File.Exists(StateFile(world)));
        }
    }

    [Fact]
    public void Shutdown_Given_save_false_and_a_short_quiet_time_When_the_quiet_time_passes_with_the_connection_still_open_Then_nothing_is_written()
    {
        // Arrange
        using var world = TestWorld.Make();
        var options = new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(400), MaxDelay = TimeSpan.FromMilliseconds(400) } };
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, options);
        host.Init();

        // Act
        host.Call(4, new[] { Val.False });
        Thread.Sleep(1200);

        // Assert
        Assert.False(File.Exists(StateFile(world)));
    }

    [Fact]
    public void Exit_Given_the_host_closing_without_shutdown_When_the_connection_ends_Then_the_state_is_saved_and_the_process_exits_within_5_s()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy());
        using (host)
        using (world)
        {
            host.Call(Manifest.OpFindArchetype, new[] { Val.Str("sword") });
            var watch = Stopwatch.StartNew();

            // Act
            host.CloseHostSide();
            int exit = host.WaitExit();

            // Assert
            Assert.Equal(ExtensionRunner.ExitOk, exit);
            Assert.True(watch.ElapsedMilliseconds < 5000);
            using JsonDocument state = ReadState(world);
            Assert.Equal("exit", state.RootElement.GetProperty("reason").GetString());
            Assert.Equal(2, state.RootElement.GetProperty("requests").GetInt64()); // init and findarchetype
        }
    }

    [Fact]
    public void Exit_Given_a_protocol_error_When_the_connection_ends_Then_the_state_is_saved_too()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy());
        using (host)
        using (world)
        {
            // Act
            host.Call(15);
            host.Send(new ReqFrame(99, 15, Array.Empty<ulong>(), ScriptedHost.NoCtx(), Array.Empty<Val>()));
            int exit = host.WaitExit();

            // Assert
            Assert.Equal(ExtensionRunner.ExitProtocol, exit);
            Assert.Equal("exit", ReadState(world).RootElement.GetProperty("reason").GetString());
        }
    }

    [Fact]
    public void Exit_Given_the_host_closing_before_session_init_When_the_connection_ends_Then_nothing_is_written_and_the_exit_is_ok()
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, Lazy());

        // Act
        host.CloseHostSide();
        int exit = host.WaitExit();

        // Assert
        Assert.Equal(ExtensionRunner.ExitOk, exit);
        Assert.False(Directory.Exists(Path.Combine(world.Path, "data")));
    }

    [Fact]
    public void Exit_Given_a_writer_that_never_finishes_When_the_connection_ends_Then_the_process_still_exits_within_5_s()
    {
        // Arrange
        using var release = new ManualResetEventSlim();
        (ScriptedHost host, TempFolder world) = Started(Lazy(_ => release.Wait(20_000), exitSave: TimeSpan.FromSeconds(1)));
        using (host)
        using (world)
        {
            var watch = Stopwatch.StartNew();

            // Act
            host.CloseHostSide();
            int exit = host.WaitExit();
            release.Set();

            // Assert
            Assert.Equal(ExtensionRunner.ExitOk, exit);
            Assert.True(watch.ElapsedMilliseconds < 5000);
        }
    }

    [Fact]
    public void Exit_Given_the_exit_save_When_the_process_has_ended_Then_the_folder_holds_the_lock_and_the_state_file_and_no_temporary_file()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy());
        using (host)
        using (world)
        {
            // Act
            host.CloseHostSide();
            host.WaitExit();

            // Assert
            string[] files = Directory.GetFiles(Path.Combine(world.Path, "data")).Select(Path.GetFileName).OrderBy(n => n).ToArray()!;
            Assert.Equal(new[] { ".lock", SessionState.FileName }, files);
        }
    }

    // ------------------------------------------------------------------ ticks

    [Fact]
    public void Manifest_Given_the_shipped_manifest_When_built_Then_wants_tick_is_false_and_with_the_option_it_is_true()
    {
        // Arrange / Act
        using var world = TestWorld.Make();
        using ScriptedHost plain = ScriptedHost.Handshake(world.Path, out Val shipped, new ExtensionOptions());
        using ScriptedHost ticking = ScriptedHost.Handshake(world.Path, out Val wanted, new ExtensionOptions { WantsTick = true });

        // Assert
        Assert.False(shipped.Get("manifest")!.Get("wants_tick")!.AsBool);
        Assert.True(wanted.Get("manifest")!.Get("wants_tick")!.AsBool);
    }

    [Fact]
    public void Tick_Given_a_manifest_without_wants_tick_When_sent_anyway_Then_unknown_op_and_nothing_is_kept()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy());
        using (host)
        using (world)
        {
            // Act
            ReplyFrame tick = host.Call(2, new[] { Val.Int(100) });
            ReplyFrame stats = host.Call(15);

            // Assert
            Assert.Equal((ulong)Status.UnknownOp, tick.Status);
            Assert.Equal(0L, Stat(stats, "ticks"));
            Assert.Equal(-1L, Stat(stats, "last_tick_ms"));
        }
    }

    [Fact]
    public void Tick_Given_wants_tick_When_now_ms_is_sent_Then_nil_and_the_time_is_kept_as_now_ms_not_as_a_count()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(wantsTick: true));
        using (host)
        using (world)
        {
            // Act
            ReplyFrame first = host.Call(2, new[] { Val.Int(5_000) });
            ReplyFrame second = host.Call(2, new[] { Val.Int(5_100) });
            ReplyFrame same = host.Call(2, new[] { Val.Int(5_100) });
            ReplyFrame stats = host.Call(15);

            // Assert
            Assert.Equal(0UL, first.Status);
            Assert.True(first.Result.IsNil);
            Assert.Equal(0UL, second.Status);
            Assert.Equal(0UL, same.Status);
            Assert.Equal(3L, Stat(stats, "ticks"));
            Assert.Equal(5_100L, Stat(stats, "last_tick_ms"));
        }
    }

    [Fact]
    public void Tick_Given_a_bad_argument_or_a_time_going_back_When_sent_Then_bad_args_and_the_last_time_stays()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(wantsTick: true));
        using (host)
        using (world)
        {
            host.Call(2, new[] { Val.Int(1_000) });

            // Act
            ReplyFrame none = host.Call(2);
            ReplyFrame text = host.Call(2, new[] { Val.Str("1") });
            ReplyFrame two = host.Call(2, new[] { Val.Int(1), Val.Int(2) });
            ReplyFrame negative = host.Call(2, new[] { Val.Int(-1) });
            ReplyFrame back = host.Call(2, new[] { Val.Int(999) });
            ReplyFrame stats = host.Call(15);

            // Assert
            Assert.All(new[] { none, text, two, negative, back }, r => Assert.Equal((ulong)Status.BadArgs, r.Status));
            Assert.Equal(1_000L, Stat(stats, "last_tick_ms"));
            Assert.Equal(1L, Stat(stats, "ticks"));
        }
    }

    [Fact]
    public void Tick_Given_wants_tick_When_sent_before_session_init_Then_unavailable()
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, Lazy(wantsTick: true));

        // Act
        ReplyFrame tick = host.Call(2, new[] { Val.Int(1) });

        // Assert
        Assert.Equal((ulong)Status.Unavailable, tick.Status);
    }

    [Fact]
    public void Tick_Given_ticks_When_the_state_is_flushed_Then_the_file_records_the_last_now_ms_and_the_tick_count()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(wantsTick: true));
        using (host)
        using (world)
        {
            host.Call(2, new[] { Val.Int(42_000) });
            host.Call(2, new[] { Val.Int(42_100) });

            // Act
            host.Call(4, new[] { Val.True });

            // Assert
            using JsonDocument state = ReadState(world);
            Assert.Equal(42_100L, state.RootElement.GetProperty("last_tick_ms").GetInt64());
            Assert.Equal(2L, state.RootElement.GetProperty("ticks").GetInt64());
        }
    }

    [Fact]
    public void HostClock_Given_ticks_When_read_between_them_Then_it_goes_on_from_the_last_now_ms_by_the_stopwatch()
    {
        // Arrange
        var clock = new HostClock();
        long before = clock.NowMs;

        // Act
        Assert.True(clock.Tick(10_000));
        Thread.Sleep(150);
        long now = clock.NowMs;
        bool back = clock.Tick(9_999);
        bool same = clock.Tick(10_000);

        // Assert
        Assert.Equal(-1, before);
        Assert.False(clock.HasTime && clock.LastTickMs < 0);
        Assert.InRange(now, 10_100, 12_000);
        Assert.False(back);
        Assert.True(same);
        Assert.Equal(10_000, clock.LastTickMs);
        Assert.False(new HostClock().Tick(-1));
    }

    // ------------------------------------------------------------------ the spin

    [Fact]
    public void Spin_Given_a_request_that_comes_during_the_spin_When_the_worker_has_replied_Then_it_is_handled_and_counted_as_a_hit()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(spin: TimeSpan.FromSeconds(3)));
        using (host)
        using (world)
        {
            // Act
            ReplyFrame first = host.Call(Manifest.OpFindArchetype, new[] { Val.Str("sword") });
            Thread.Sleep(50);
            ReplyFrame second = host.Call(Manifest.OpFindArchetype, new[] { Val.Str("house") });
            ReplyFrame stats = host.Call(15);

            // Assert
            Assert.Equal(0UL, first.Status);
            Assert.Equal(0UL, second.Status);
            Assert.True(Stat(stats, "spin_hits") >= 2);
            Assert.Equal(0L, Stat(stats, "spin_misses"));
        }
    }

    [Fact]
    public void Spin_Given_a_request_that_comes_after_the_spin_When_the_worker_has_gone_to_sleep_Then_it_is_handled_and_the_spin_was_a_miss()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(spin: TimeSpan.FromMilliseconds(20)));
        using (host)
        using (world)
        {
            // Act
            ReplyFrame first = host.Call(Manifest.OpFindArchetype, new[] { Val.Str("sword") });
            Thread.Sleep(300);
            ReplyFrame second = host.Call(Manifest.OpFindArchetype, new[] { Val.Str("house") });
            ReplyFrame stats = host.Call(15);

            // Assert
            Assert.Equal(0UL, first.Status);
            Assert.Equal(0UL, second.Status);
            Assert.True(Stat(stats, "spin_misses") >= 1);
        }
    }

    [Fact]
    public void Spin_Given_the_spin_is_off_When_requests_come_Then_they_are_handled_and_nothing_is_counted()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(spin: TimeSpan.Zero));
        using (host)
        using (world)
        {
            // Act
            host.Call(Manifest.OpFindArchetype, new[] { Val.Str("sword") });
            ReplyFrame stats = host.Call(15);

            // Assert
            Assert.Equal(0L, Stat(stats, "spin_hits"));
            Assert.Equal(0L, Stat(stats, "spin_misses"));
        }
    }

    [Fact]
    public void Spin_Given_the_connection_ending_during_the_spin_When_the_host_closes_Then_the_worker_ends_and_the_process_exits()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(spin: TimeSpan.FromSeconds(3)));
        using (host)
        using (world)
        {
            host.Call(15);

            // Act
            host.CloseHostSide();
            int exit = host.WaitExit();

            // Assert
            Assert.Equal(ExtensionRunner.ExitOk, exit);
        }
    }

    // ------------------------------------------------------------------ the counters

    [Fact]
    public void Stats_Given_calls_of_several_ops_When_asked_Then_per_op_calls_errors_and_bytes_equal_what_went_over_the_link()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy());
        using (host)
        using (world)
        {
            Val[] sword = { Val.Str("sword") };
            ReplyFrame find1 = host.Call(Manifest.OpFindArchetype, sword);
            ReplyFrame find2 = host.Call(Manifest.OpFindArchetype, sword);
            ReplyFrame bad = host.Call(9, new[] { Val.Nil, Val.Int(3) });
            ReplyFrame unknown = host.Call(99);

            // Act
            ReplyFrame stats = host.Call(15);

            // Assert
            int reqBytes = FrameCodec.Encode(new ReqFrame(2, Manifest.OpFindArchetype, Array.Empty<ulong>(), ScriptedHost.NoCtx(), sword)).Length;
            int replyBytes = FrameCodec.Encode(find1).Length;
            Assert.Equal(2L, Stat(stats, "op_findarchetype_calls"));
            Assert.Equal(0L, Stat(stats, "op_findarchetype_errors"));
            Assert.Equal(2 * reqBytes, Stat(stats, "op_findarchetype_bytes_in")); // reqids 2 and 3 are one varint byte each
            Assert.Equal(replyBytes + FrameCodec.Encode(find2).Length, Stat(stats, "op_findarchetype_bytes_out"));
            Assert.Equal(1L, Stat(stats, "op_get_calls"));
            Assert.Equal(1L, Stat(stats, "op_get_errors"));
            Assert.Equal(FrameCodec.Encode(bad).Length, Stat(stats, "op_get_bytes_out"));
            Assert.Equal(1L, Stat(stats, "op_unknown_99_calls"));
            Assert.Equal(1L, Stat(stats, "op_unknown_99_errors"));
            Assert.Equal(FrameCodec.Encode(unknown).Length, Stat(stats, "op_unknown_99_bytes_out"));
            Assert.Equal(1L, Stat(stats, "op_session_init_calls"));
            Assert.True(Stat(stats, "bytes_in") >= Stat(stats, "op_findarchetype_bytes_in") + Stat(stats, "op_get_bytes_in"));
            Assert.True(Stat(stats, "bytes_out") >= Stat(stats, "op_findarchetype_bytes_out"));
        }
    }

    [Fact]
    public void Stats_Given_handled_requests_When_asked_Then_handling_times_are_positive_and_the_maximum_is_within_the_total()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(afterDequeue: null));
        using (host)
        using (world)
        {
            for (int i = 0; i < 5; i++)
            {
                host.Call(Manifest.OpFindArchetype, new[] { Val.Str("sword") });
            }

            // Act
            ReplyFrame stats = host.Call(15);

            // Assert
            long total = Stat(stats, "op_findarchetype_handle_ns_total");
            long max = Stat(stats, "op_findarchetype_handle_ns_max");
            Assert.True(total > 0);
            Assert.InRange(max, 1, total);
            Assert.True(max * 5 >= total);
            Assert.True(Stat(stats, "op_findarchetype_wait_ns_total") >= Stat(stats, "op_findarchetype_wait_ns_max"));
            Assert.True(Stat(stats, "op_session_init_handle_ns_total") > 0);
        }
    }

    [Fact]
    public void Stats_Given_a_request_that_waits_in_the_queue_When_asked_Then_the_wait_is_counted_apart_from_the_handling()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy(afterDequeue: op => { if (op == Manifest.OpFindArchetype) { Thread.Sleep(150); } }));
        using (host)
        using (world)
        {
            host.Call(Manifest.OpFindArchetype, new[] { Val.Str("sword") });

            // Act
            ReplyFrame stats = host.Call(15);

            // Assert
            Assert.InRange(Stat(stats, "op_findarchetype_wait_ns_max"), 100_000_000L, 5_000_000_000L);
            Assert.Equal(Stat(stats, "op_findarchetype_wait_ns_total"), Stat(stats, "op_findarchetype_wait_ns_max"));
            Assert.True(Stat(stats, "op_findarchetype_handle_ns_max") < 100_000_000L);
        }
    }

    [Fact]
    public void Stats_Given_a_collection_and_a_flush_When_asked_Then_the_gc_counts_the_pause_and_the_writer_are_there()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(Lazy());
        using (host)
        using (world)
        {
            ReplyFrame before = host.Call(15);
            GC.Collect(2, GCCollectionMode.Forced, true);
            host.Call(3);
            Assert.True(WaitFor(() => File.Exists(StateFile(world))));
            Assert.True(WaitFor(() => Stat(host.Call(15), "writer_writes") >= 1));

            // Act
            ReplyFrame after = host.Call(15);

            // Assert
            Assert.True(Stat(after, "gc_gen2") > Stat(before, "gc_gen2"));
            Assert.True(Stat(after, "gc_gen0") >= Stat(after, "gc_gen2"));
            Assert.True(Stat(after, "gc_pause_ns") > 0);
            Assert.True(Stat(after, "gc_pause_ns") >= Stat(before, "gc_pause_ns"));
            Assert.True(Stat(after, "gc_allocated_bytes") > 0);
            Assert.True(Stat(after, "writer_writes") >= 1);
            Assert.True(Stat(after, "writer_files") >= 1);
            Assert.True(Stat(after, "writer_ns_total") > 0);
            Assert.True(Stat(after, "writer_ns_max") > 0);
            Assert.Equal(0L, Stat(after, "writer_failures"));
            Assert.Equal(1L, Stat(after, "flushes"));
        }
    }

    [Fact]
    public void Counters_Given_recorded_ops_When_turned_into_pairs_Then_ticks_become_nanoseconds_and_the_maximum_is_kept()
    {
        // Arrange
        var counters = new Counters();
        long second = Stopwatch.Frequency; // one second of stopwatch ticks

        // Act
        counters.RecordOp(9, 2 * second, 3 * second, 10, 20, false);
        counters.RecordOp(9, 1 * second, 1 * second, 5, 6, true);
        Val stats = Val.Map(counters.ToPairs().ToArray());

        // Assert
        Assert.Equal(2L, stats.Get("op_get_calls")!.AsInt);
        Assert.Equal(1L, stats.Get("op_get_errors")!.AsInt);
        Assert.Equal(4_000_000_000L, stats.Get("op_get_handle_ns_total")!.AsInt);
        Assert.Equal(3_000_000_000L, stats.Get("op_get_handle_ns_max")!.AsInt);
        Assert.Equal(3_000_000_000L, stats.Get("op_get_wait_ns_total")!.AsInt);
        Assert.Equal(2_000_000_000L, stats.Get("op_get_wait_ns_max")!.AsInt);
        Assert.Equal(15L, stats.Get("op_get_bytes_in")!.AsInt);
        Assert.Equal(26L, stats.Get("op_get_bytes_out")!.AsInt);
    }

    [Fact]
    public void Counters_Given_frames_and_bytes_When_counted_Then_the_totals_add_up()
    {
        // Arrange
        var counters = new Counters();

        // Act
        counters.CountFrameIn(10);
        counters.CountFrameIn(5);
        counters.CountFrameOut(7);

        // Assert
        Assert.Equal(2, counters.FramesIn);
        Assert.Equal(15, counters.BytesIn);
        Assert.Equal(1, counters.FramesOut);
        Assert.Equal(7, counters.BytesOut);
    }
}
