// Not ported from a Crossfire file: what the Crossfire extension itself knows of its session (its one state file), and the time the
// host's ticks give it.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Crossfire.Extension;

/// <summary>
/// The host's clock as the ticks report it: <c>now_ms</c>, a monotonic count of milliseconds since the host's process started.
/// Between two ticks the time goes on by the extension's own stopwatch, so a reader gets the host's time and not a tick count.
/// Worker only.
/// </summary>
internal sealed class HostClock
{
    private long lastNowMs = -1;
    private long lastStamp;

    /// <summary>True once a tick has been seen.</summary>
    public bool HasTime => lastNowMs >= 0;

    /// <summary>The <c>now_ms</c> of the last tick, or -1 before the first.</summary>
    public long LastTickMs => lastNowMs;

    /// <summary>The host's time now in milliseconds: the last tick's <c>now_ms</c> plus the time since it arrived; -1 before the first tick.</summary>
    public long NowMs => lastNowMs < 0 ? -1 : lastNowMs + (long)((Stopwatch.GetTimestamp() - lastStamp) * 1000.0 / Stopwatch.Frequency);

    /// <summary>Takes a tick; false (and nothing changes) when <paramref name="nowMs"/> is negative or before the last tick's.</summary>
    public bool Tick(long nowMs)
    {
        if (nowMs < 0 || nowMs < lastNowMs)
        {
            return false;
        }
        lastNowMs = nowMs;
        lastStamp = Stopwatch.GetTimestamp();
        return true;
    }
}

/// <summary>
/// The state file <c>session.json</c> of the data folder: what this process knows of itself (not the world: the accounts and the
/// levels of later versions have files of their own). It is a snapshot, written as a whole each time: when the world is loaded, at
/// <c>session.flush</c>, at <c>session.shutdown</c> with save true and when the connection ends. Worker only.
/// </summary>
internal sealed class SessionState
{
    public const string FileName = "session.json";
    public const int Format = 1;

    private readonly TimeProvider time;
    private readonly DateTimeOffset started;
    private DateTimeOffset? initialized;

    public SessionState(TimeProvider time)
    {
        this.time = time;
        started = time.GetUtcNow();
    }

    public long Ticks { get; set; }

    public long Flushes { get; set; }

    public long Archetypes { get; set; }

    public void MarkInitialized() => initialized = time.GetUtcNow();

    /// <summary>The file's content; <paramref name="reason"/> says what asked for it (<c>init</c>, <c>flush</c>, <c>shutdown</c> or <c>exit</c>).</summary>
    public byte[] Snapshot(string reason, long epoch, string serverName, Counters counters, HostClock clock)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteNumber("format", Format);
            json.WriteString("reason", reason);
            json.WriteNumber("epoch", epoch);
            json.WriteString("server_name", serverName);
            json.WriteString("started_utc", started);
            if (initialized is { } init)
            {
                json.WriteString("initialized_utc", init);
            }
            json.WriteString("saved_utc", time.GetUtcNow());
            json.WriteNumber("last_tick_ms", clock.LastTickMs);
            json.WriteNumber("ticks", Ticks);
            json.WriteNumber("flushes", Flushes);
            json.WriteNumber("archetypes", Archetypes);
            json.WriteNumber("requests", counters.Requests);
            json.WriteNumber("errors", counters.Errors);
            json.WriteNumber("frames_in", counters.FramesIn);
            json.WriteNumber("frames_out", counters.FramesOut);
            json.WriteEndObject();
        }
        return stream.ToArray().Concat(Encoding.ASCII.GetBytes("\n")).ToArray();
    }
}
