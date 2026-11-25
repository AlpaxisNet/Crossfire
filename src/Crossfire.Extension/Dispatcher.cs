// Not ported from a Crossfire file: the operations of the Crossfire extension (docs/protocol.md section 7). Everything here runs on
// the worker thread W only: the CfWorld has no locks.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>The outcome of an operation: a status, and the result (a string that says what went wrong when the status is not 0).</summary>
internal readonly record struct Outcome(int Status, Val Result)
{
    public static Outcome Ok(Val result) => new(Protocol.Status.Ok, result);

    public static Outcome Fail(int status, string why) => new(status, Val.Str(why));
}

/// <summary>Hooks of the tests, none of them used in production.</summary>
internal sealed class ExtensionOptions
{
    /// <summary>Called on the worker thread before it handles a request (a test makes the worker slow here).</summary>
    public Action<ulong>? BeforeOperation { get; init; }

    /// <summary>Called on the worker thread right after it took a request from the queue (a test lets a request wait here).</summary>
    public Action<ulong>? AfterDequeue { get; init; }

    /// <summary>The largest tag the extension gives out (a test lowers it to see the answer when the tags are used up).</summary>
    public uint TagLimit { get; init; } = uint.MaxValue;

    /// <summary>How long the process waits for the worker to finish when the connection ends.</summary>
    public TimeSpan WorkerGrace { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How long <c>session.init</c> waits for an older process's lock on the data folder (protocol section 1: 20 s).</summary>
    public TimeSpan LockWait { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>How long <c>session.shutdown</c> with save true waits for the files (the host waits 10 s for its REPLY).</summary>
    public TimeSpan ShutdownSaveTimeout { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>How long the exit waits for the files when the connection ends (the process must be gone within 5 s: the grace and this fit in it).</summary>
    public TimeSpan ExitSaveTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>When the file writer writes.</summary>
    public FileWriterOptions Writer { get; init; } = new();

    /// <summary>The manifest's <c>wants_tick</c>: false in the shipped manifest, a test sets it to send ticks.</summary>
    public bool WantsTick { get; init; }

    /// <summary>How long the worker spins on its queue after a REPLY before it blocks (design section 7).</summary>
    public TimeSpan Spin { get; init; } = TimeSpan.FromTicks(500); // 50 microseconds: a tick of TimeSpan is 100 ns

    /// <summary>The source of the wall-clock times written to the state file.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}

/// <summary>The worker side: one request at a time, in the order they arrive.</summary>
internal sealed class Dispatcher
{
    private readonly HostHello host;
    private readonly Action<int, string> log;
    private readonly ExtensionOptions options;
    private readonly Counters counters;
    private readonly int maxFrame;
    private readonly CancellationToken stopping;
    private readonly SessionState session;
    private readonly HostClock clock = new();
    private DataLock? dataLock;
    private FileWriter? writer;
    private bool saveOnExit = true;
    private bool savedAtShutdown;

    private readonly Dictionary<Archetype, uint> tagOf = new();
    private readonly List<Archetype> archByTag = new();
    private CfWorld? world;
    private int archetypeCount;

    public Dispatcher(HostHello host, int maxFrame, Action<int, string> log, ExtensionOptions options, Counters counters, CancellationToken stopping = default)
    {
        this.stopping = stopping;
        session = new SessionState(options.Time);
        this.host = host;
        this.maxFrame = maxFrame;
        this.log = log;
        this.options = options;
        this.counters = counters;
    }

    /// <summary>True after the REPLY to <c>session.init</c> was made with status 0.</summary>
    public bool Initialized { get; private set; }

    /// <summary>The REPLY to a REQ. A reply that would not fit in the link's maximum becomes <c>too_large</c> (section 4.2).</summary>
    public ReplyFrame Handle(ReqFrame req)
    {
        counters.CountRequest();
        // The releases (req.Releases) are handles of objects the host no longer refers to. Every handle this extension has given out
        // names an archetype, and archetypes are permanent: there is nothing to forget, and dead or unknown handles are ignored.
        Outcome outcome;
        try
        {
            options.BeforeOperation?.Invoke(req.Op);
            outcome = Dispatch(req);
        }
        catch (Exception e)
        {
            log(0, "request " + req.ReqId + " (op " + req.Op + ") failed: " + e.GetType().Name + ": " + e.Message);
            outcome = Outcome.Fail(Status.Internal, "internal error");
        }
        ReplyFrame reply = Reply(req.ReqId, outcome);
        if (outcome.Status != Status.Ok)
        {
            counters.CountError();
        }
        else if (FrameCodec.Encode(reply).Length - 4 > maxFrame)
        {
            counters.CountError();
            reply = Reply(req.ReqId, Outcome.Fail(Status.TooLarge, "the answer does not fit in a frame"));
        }
        return reply;
    }

    private static ReplyFrame Reply(ulong reqid, Outcome o) =>
        new(reqid, (ulong)o.Status, o.Result, Array.Empty<Val>(), Array.Empty<Val>());

    private Outcome Dispatch(ReqFrame req)
    {
        if (req.Op == 1)
        {
            return Init(req.Args);
        }
        if (!Initialized)
        {
            return Outcome.Fail(Status.Unavailable, "session.init has not been answered");
        }
        switch (req.Op)
        {
            case 2 when options.WantsTick:
                return Tick(req.Args);
            case 3:
                return Flush(req.Args);
            case 4:
                return Shutdown(req.Args);
            case 9:
                return Get(req.Args);
            case 10:
                return Set(req.Args);
            case 11:
                return GetMany(req.Args);
            case 12:
                return GetList(req.Args);
            case 13:
                return Invoke(req.Args);
            case 15:
                return req.Args.Count == 0 ? Outcome.Ok(Stats()) : Outcome.Fail(Status.BadArgs, "stats takes no arguments");
            case Manifest.OpFindArchetype:
                return FindArchetype(req.Args, byRealName: false);
            case Manifest.OpFindArchetypeByRealName:
                return FindArchetype(req.Args, byRealName: true);
            default:
                // 2 without wants_tick (the host must not send it), 5 to 8, 14 and 16 (players, levels, objects: later waves),
                // 17 to 63 (reserved) and the manifest's other ids
                return Outcome.Fail(Status.UnknownOp, "unknown operation " + req.Op);
        }
    }

    // ------------------------------------------------------------------ session

    private Outcome Init(IReadOnlyList<Val> args)
    {
        if (args.Count != 0 || Initialized)
        {
            return Outcome.Fail(Status.BadArgs, "session.init takes no arguments and comes once");
        }
        // the lock comes before the world is loaded: an older process may still be saving into this folder (protocol section 1)
        try
        {
            Directory.CreateDirectory(host.DataDir);
            // a session.init that failed after the lock was taken keeps it: a second try must not wait for itself
            dataLock ??= DataLock.Acquire(Path.Combine(host.DataDir, ".lock"), options.LockWait, stopping);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Outcome.Fail(Status.Internal, "the data folder cannot be locked: " + e.Message);
        }
        if (dataLock == null)
        {
            log(1, "the data folder " + host.DataDir + " is locked by another process");
            return Outcome.Fail(Status.Busy, "the data folder is locked by another process");
        }
        writer ??= new FileWriter(host.DataDir, options.Writer, counters, log);
        string? arch = new[] { "Arch", "arch" }.Select(n => Path.Combine(host.WorldDir, n)).FirstOrDefault(Directory.Exists);
        if (arch == null)
        {
            return Outcome.Fail(Status.Internal, "the world has no Arch folder");
        }
        var w = new CfWorld(new GkHost(), (_, _) => { });
        ArchCollection collection = ArchCollector.Collect(arch);
        w.InitArchetypes(collection.Archetypes);

        int count = 0;
        int parts = 0;
        for (Archetype? at = w.FirstArchetype; at != null; at = at.More == null ? at.Next : at.More)
        {
            count++;
            if (at.Head != null)
            {
                parts++;
            }
        }
        world = w;
        archetypeCount = count;
        session.Archetypes = count;
        session.MarkInitialized();
        Initialized = true;
        SubmitSnapshot("init");
        log(2, "loaded " + count + " archetypes (" + parts + " of them parts) from " + collection.Files.Count + " files; "
            + w.LogCounts[(int)LogLevel.Error] + " errors in the data, " + collection.Warnings.Count + " warnings of the collector");
        return Outcome.Ok(Val.Map(("archetypes", Val.Int(count))));
    }

    private Outcome Tick(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || args[0].AsNumberToInt() is not { } nowMs)
        {
            return Outcome.Fail(Status.BadArgs, "session.tick takes now_ms, an int");
        }
        if (!clock.Tick(nowMs))
        {
            return Outcome.Fail(Status.BadArgs, "now_ms is negative or before the last tick's");
        }
        session.Ticks++;
        return Outcome.Ok(Val.Nil);
    }

    private Outcome Flush(IReadOnlyList<Val> args)
    {
        if (args.Count != 0)
        {
            return Outcome.Fail(Status.BadArgs, "session.flush takes no arguments");
        }
        session.Flushes++;
        SubmitSnapshot("flush");
        writer!.Kick(); // the REPLY does not wait for the files
        return Outcome.Ok(Val.Nil);
    }

    private Outcome Shutdown(IReadOnlyList<Val> args)
    {
        // the one argument is `save`, a bool (nil reads as false: section 6.3)
        if (args.Count != 1 || !(args[0].IsNil || args[0].IsBool))
        {
            return Outcome.Fail(Status.BadArgs, "session.shutdown takes one bool");
        }
        if (!args[0].AsBool)
        {
            saveOnExit = false;
            writer!.Discard();
            return Outcome.Ok(Val.Nil);
        }
        SubmitSnapshot("shutdown");
        if (!writer!.WaitWritten(options.ShutdownSaveTimeout))
        {
            return Outcome.Fail(Status.Internal, "the files could not be written");
        }
        savedAtShutdown = true;
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>Gives the file writer a snapshot of everything this process holds (worker only; the writer gets bytes it alone owns).</summary>
    private void SubmitSnapshot(string reason) =>
        writer!.Submit(SessionState.FileName, session.Snapshot(reason, host.Epoch, host.ServerName, counters, clock));

    /// <summary>
    /// The end of the connection (protocol section 1, "Stopping"): saves unless <c>session.shutdown</c> with false came before, and
    /// stops the writer. <paramref name="workerDone"/> is false when the worker did not finish: its state is not read then, and only
    /// what was already handed to the writer is written.
    /// </summary>
    public void Finish(bool workerDone)
    {
        if (writer == null)
        {
            return; // session.init never got as far as the lock: nothing is held, nothing to save
        }
        if (!saveOnExit)
        {
            writer.Discard();
        }
        else if (!savedAtShutdown)
        {
            if (workerDone && Initialized)
            {
                SubmitSnapshot("exit");
            }
            if (!writer.WaitWritten(options.ExitSaveTimeout))
            {
                log(0, "the files were not all written before the exit");
            }
        }
        writer.Stop(TimeSpan.FromMilliseconds(500));
    }

    /// <summary>Gives up the lock on the data folder.</summary>
    public void Release() => dataLock?.Dispose();

    private Val Stats()
    {
        List<(string, Val)> pairs = counters.ToPairs();
        pairs.Add(("archetypes", Val.Int(archetypeCount)));
        pairs.Add(("tags", Val.Int(archByTag.Count)));
        pairs.Add(("ticks", Val.Int(session.Ticks)));
        pairs.Add(("last_tick_ms", Val.Int(clock.LastTickMs)));
        pairs.Add(("flushes", Val.Int(session.Flushes)));
        return Val.Map(pairs.ToArray());
    }

    // ------------------------------------------------------------------ handles

    private Val HandleOf(Archetype at)
    {
        if (!tagOf.TryGetValue(at, out uint tag))
        {
            if ((uint)archByTag.Count >= options.TagLimit)
            {
                throw new TagsUsedUpException();
            }
            archByTag.Add(at);
            tag = (uint)archByTag.Count;
            tagOf[at] = tag;
        }
        return Val.Handle(((ulong)host.Epoch << 32) | tag);
    }

    private sealed class TagsUsedUpException : Exception
    {
    }

    /// <summary>The archetype of a handle, or null for a handle that is of another epoch, has tag 0 or a tag never given out.</summary>
    private Archetype? Resolve(ulong handle)
    {
        uint tag = (uint)handle;
        if ((handle >> 32) != (ulong)host.Epoch || tag == 0 || tag > archByTag.Count)
        {
            return null;
        }
        return archByTag[(int)tag - 1];
    }

    private Outcome WithArchetype(Val handleArg, Func<Archetype, Outcome> then)
    {
        // nil (and any other value that ArgsShapeOk let through) is a nil handle: bad_handle
        Archetype? at = handleArg.Kind == ValKind.Handle ? Resolve(handleArg.AsHandle) : null;
        return at == null ? Outcome.Fail(Status.BadHandle, "dead handle") : then(at);
    }

    // ------------------------------------------------------------------ members

    /// <summary>A list argument: nil reads as the empty list (section 6.3), any other kind but a list is not one.</summary>
    private static bool IsListOrNil(Val v) => v.Kind is ValKind.List or ValKind.Nil;

    private static bool ArgsShapeOk(IReadOnlyList<Val> args, int count, bool handleFirst = true) =>
        args.Count == count && (!handleFirst || args[0].Kind is ValKind.Handle or ValKind.Nil);

    private Outcome Get(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 2) || args[1].AsNumberToInt() is not { } id)
        {
            return Outcome.Fail(Status.BadArgs, "get takes a handle and a member id");
        }
        return WithArchetype(args[0], at =>
            TMudArchMembers.Find(id) is { } m ? Outcome.Ok(m.Read(world!, at)) : Outcome.Fail(Status.BadArgs, "unknown member " + id));
    }

    private Outcome GetMany(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 2) || !IsListOrNil(args[1]))
        {
            return Outcome.Fail(Status.BadArgs, "get_many takes a handle and a list of member ids");
        }
        var ids = new List<long>();
        foreach (Val v in args[1].Items)
        {
            if (v.AsNumberToInt() is not { } id)
            {
                return Outcome.Fail(Status.BadArgs, "a member id is not a number");
            }
            ids.Add(id);
        }
        return WithArchetype(args[0], at =>
        {
            var values = new List<Val>(ids.Count);
            foreach (long id in ids)
            {
                if (TMudArchMembers.Find(id) is not { } m)
                {
                    return Outcome.Fail(Status.BadArgs, "unknown member " + id);
                }
                values.Add(m.Read(world!, at));
            }
            return Outcome.Ok(Val.List(values));
        });
    }

    private Outcome Set(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 3) || args[1].AsNumberToInt() is not { } id)
        {
            return Outcome.Fail(Status.BadArgs, "set takes a handle, a member id and a value");
        }
        // every member of TMudArch has access "r"
        return WithArchetype(args[0], _ => Outcome.Fail(Status.BadArgs,
            TMudArchMembers.Find(id) == null ? "unknown member " + id : "member " + id + " is read-only"));
    }

    private Outcome GetList(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 3) || args[1].AsNumberToInt() is not { } id || !IsListOrNil(args[2]))
        {
            return Outcome.Fail(Status.BadArgs, "get_list takes a handle, a member id and a list of field ids");
        }
        // no member of TMudArch has the type list:handle:<type>
        return WithArchetype(args[0], _ => Outcome.Fail(Status.BadArgs,
            TMudArchMembers.Find(id) == null ? "unknown member " + id : "member " + id + " is not a list of handles"));
    }

    private Outcome Invoke(IReadOnlyList<Val> args)
    {
        if (!ArgsShapeOk(args, 3) || args[1].AsNumberToInt() == null || !IsListOrNil(args[2]))
        {
            return Outcome.Fail(Status.BadArgs, "invoke takes a handle, a method id and a list");
        }
        // TMudArch has no methods yet
        return WithArchetype(args[0], _ => Outcome.Fail(Status.BadArgs, "unknown method"));
    }

    // ------------------------------------------------------------------ the manifest's functions

    private Outcome FindArchetype(IReadOnlyList<Val> args, bool byRealName)
    {
        if (args.Count != 1 || !(args[0].IsNil || args[0].Kind == ValKind.String))
        {
            return Outcome.Fail(Status.BadArgs, "the function takes one string");
        }
        string name = args[0].AsString;
        Archetype? at = byRealName ? world!.FindArchetypeByObjectName(name) : world!.FindArchetype(name);
        if (at == null)
        {
            return Outcome.Ok(Val.Nil);
        }
        try
        {
            return Outcome.Ok(HandleOf(at));
        }
        catch (TagsUsedUpException)
        {
            return Outcome.Fail(Status.Internal, "the tags of this epoch are used up");
        }
    }
}
