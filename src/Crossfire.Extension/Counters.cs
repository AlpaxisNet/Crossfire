// Not ported from a Crossfire file: the counters of the Crossfire extension (the design's section 8): what `stats` (op 15) returns.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>What the worker knows of one op: it is written and read on the worker thread only.</summary>
internal sealed class OpStats
{
    public long Calls;
    public long HandleTicks;
    public long HandleMaxTicks;
    public long WaitTicks;
    public long WaitMaxTicks;
    public long BytesIn;
    public long BytesOut;
    public long Errors;
}

/// <summary>
/// The counters. Those that the reader thread, the worker and the writer thread all add to are <c>Interlocked</c>; the per-op
/// table and the spin counts are the worker's alone (the <c>stats</c> op runs on the worker too).
/// </summary>
internal sealed class Counters
{
    private long framesIn;
    private long framesOut;
    private long bytesIn;
    private long bytesOut;
    private long pings;
    private long requests;
    private long errors;
    private long writerWrites;
    private long writerFailures;
    private long writerTicks;
    private long writerMaxTicks;
    private long writerFiles;

    private readonly SortedDictionary<ulong, OpStats> ops = new();

    public long FramesIn => Interlocked.Read(ref framesIn);

    public long FramesOut => Interlocked.Read(ref framesOut);

    public long BytesIn => Interlocked.Read(ref bytesIn);

    public long BytesOut => Interlocked.Read(ref bytesOut);

    public long Pings => Interlocked.Read(ref pings);

    public long Requests => Interlocked.Read(ref requests);

    public long Errors => Interlocked.Read(ref errors);

    public long WriterWrites => Interlocked.Read(ref writerWrites);

    public long WriterFailures => Interlocked.Read(ref writerFailures);

    /// <summary>Requests that arrived during the spin after a REPLY (worker only).</summary>
    public long SpinHits { get; set; }

    /// <summary>Spins that ended without a request (worker only).</summary>
    public long SpinMisses { get; set; }

    public void CountFrameIn(long bytes)
    {
        Interlocked.Increment(ref framesIn);
        Interlocked.Add(ref bytesIn, bytes);
    }

    public void CountFrameOut(long bytes)
    {
        Interlocked.Increment(ref framesOut);
        Interlocked.Add(ref bytesOut, bytes);
    }

    public void CountPing() => Interlocked.Increment(ref pings);

    public void CountRequest() => Interlocked.Increment(ref requests);

    public void CountError() => Interlocked.Increment(ref errors);

    /// <summary>The writer thread finished one pass: <paramref name="files"/> files in <paramref name="ticks"/> stopwatch ticks.</summary>
    public void CountWrite(int files, long ticks)
    {
        Interlocked.Increment(ref writerWrites);
        Interlocked.Add(ref writerFiles, files);
        Interlocked.Add(ref writerTicks, ticks);
        long seen;
        while ((seen = Interlocked.Read(ref writerMaxTicks)) < ticks && Interlocked.CompareExchange(ref writerMaxTicks, ticks, seen) != seen)
        {
        }
    }

    public void CountWriteFailure() => Interlocked.Increment(ref writerFailures);

    /// <summary>One handled request (worker only): the time it waited in the queue, the time it took, the bytes of its REQ and REPLY.</summary>
    public void RecordOp(ulong op, long waitTicks, long handleTicks, long bytesInOfReq, long bytesOutOfReply, bool failed)
    {
        if (!ops.TryGetValue(op, out OpStats? s))
        {
            ops[op] = s = new OpStats();
        }
        s.Calls++;
        s.HandleTicks += handleTicks;
        s.HandleMaxTicks = Math.Max(s.HandleMaxTicks, handleTicks);
        s.WaitTicks += waitTicks;
        s.WaitMaxTicks = Math.Max(s.WaitMaxTicks, waitTicks);
        s.BytesIn += bytesInOfReq;
        s.BytesOut += bytesOutOfReply;
        if (failed)
        {
            s.Errors++;
        }
    }

    /// <summary>The name of an op in the stats map: <c>session_init</c>, ..., or <c>unknown_&lt;number&gt;</c> for an op without a name here.</summary>
    public static string OpName(ulong op) => op switch
    {
        1 => "session_init",
        2 => "session_tick",
        3 => "session_flush",
        4 => "session_shutdown",
        5 => "player_login",
        6 => "player_logout",
        7 => "level_load",
        8 => "level_unload",
        9 => "get",
        10 => "set",
        11 => "get_many",
        12 => "get_list",
        13 => "invoke",
        14 => "object_destroyed",
        15 => "stats",
        16 => "objects_blobs",
        Manifest.OpFindArchetype => "findarchetype",
        Manifest.OpFindArchetypeByRealName => "findarchetypebyrealname",
        _ => "unknown_" + op,
    };

    /// <summary>Stopwatch ticks as nanoseconds.</summary>
    public static long Ns(long ticks) => (long)(ticks * (1_000_000_000.0 / Stopwatch.Frequency));

    /// <summary>The counters as the pairs of the <c>stats</c> map (worker only): the totals, the GC, the writer, then the ops that were called.</summary>
    public List<(string, Val)> ToPairs()
    {
        var pairs = new List<(string, Val)>
        {
            ("requests", Val.Int(Requests)),
            ("errors", Val.Int(Errors)),
            ("frames_in", Val.Int(FramesIn)),
            ("frames_out", Val.Int(FramesOut)),
            ("bytes_in", Val.Int(BytesIn)),
            ("bytes_out", Val.Int(BytesOut)),
            ("pings", Val.Int(Pings)),
            ("spin_hits", Val.Int(SpinHits)),
            ("spin_misses", Val.Int(SpinMisses)),
            ("gc_gen0", Val.Int(GC.CollectionCount(0))),
            ("gc_gen1", Val.Int(GC.CollectionCount(1))),
            ("gc_gen2", Val.Int(GC.CollectionCount(2))),
            ("gc_pause_ns", Val.Int((long)(GC.GetTotalPauseDuration().TotalMilliseconds * 1_000_000.0))),
            ("gc_allocated_bytes", Val.Int(GC.GetTotalAllocatedBytes(false))),
            ("writer_writes", Val.Int(WriterWrites)),
            ("writer_files", Val.Int(Interlocked.Read(ref writerFiles))),
            ("writer_failures", Val.Int(WriterFailures)),
            ("writer_ns_total", Val.Int(Ns(Interlocked.Read(ref writerTicks)))),
            ("writer_ns_max", Val.Int(Ns(Interlocked.Read(ref writerMaxTicks)))),
        };
        foreach ((ulong op, OpStats s) in ops)
        {
            string p = "op_" + OpName(op) + "_";
            pairs.Add((p + "calls", Val.Int(s.Calls)));
            pairs.Add((p + "errors", Val.Int(s.Errors)));
            pairs.Add((p + "handle_ns_total", Val.Int(Ns(s.HandleTicks))));
            pairs.Add((p + "handle_ns_max", Val.Int(Ns(s.HandleMaxTicks))));
            pairs.Add((p + "wait_ns_total", Val.Int(Ns(s.WaitTicks))));
            pairs.Add((p + "wait_ns_max", Val.Int(Ns(s.WaitMaxTicks))));
            pairs.Add((p + "bytes_in", Val.Int(s.BytesIn)));
            pairs.Add((p + "bytes_out", Val.Int(s.BytesOut)));
        }
        return pairs;
    }
}
