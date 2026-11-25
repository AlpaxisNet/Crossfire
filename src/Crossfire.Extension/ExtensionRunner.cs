// Not ported from a Crossfire file: the link of the Crossfire extension to the game server (docs/protocol.md sections 1, 2, 4 and 9):
// the handshake, the reader thread X, the worker thread W and the write lock.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Collections.Concurrent;
using System.Diagnostics;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>Writes frames whole, one at a time, whichever thread sends them (section 2).</summary>
internal sealed class FrameWriter
{
    private readonly Stream stream;
    private readonly Counters counters;
    private readonly object writeLock = new();

    public FrameWriter(Stream stream, Counters counters)
    {
        this.stream = stream;
        this.counters = counters;
    }

    /// <summary>Sends a frame; false when the connection is gone.</summary>
    public bool Send(Frame frame) => Send(frame, out _);

    /// <summary>Sends a frame; false when the connection is gone. <paramref name="bytes"/> is the size of the frame on the link.</summary>
    public bool Send(Frame frame, out int bytes)
    {
        byte[] encoded = FrameCodec.Encode(frame);
        bytes = encoded.Length;
        try
        {
            lock (writeLock)
            {
                stream.Write(encoded, 0, encoded.Length);
                stream.Flush();
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
        {
            return false;
        }
        counters.CountFrameOut(encoded.Length);
        return true;
    }
}

/// <summary>Counts the bytes read from the host (the reader thread's alone), so that each frame's size on the link is known.</summary>
internal sealed class ReadCountingStream : Stream
{
    private readonly Stream inner;

    public ReadCountingStream(Stream inner)
    {
        this.inner = inner;
    }

    public long BytesRead { get; private set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int n = inner.Read(buffer, offset, count);
        BytesRead += n;
        return n;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>A request in the queue between the reader and the worker: when it was queued and how big it was on the link.</summary>
internal readonly record struct Queued(ReqFrame Req, long Stamp, long Bytes);

/// <summary>The extension's side of one connection, from the host's HELLO to the end of the connection.</summary>
internal static class ExtensionRunner
{
    /// <summary>The connection ended (EOF) or the host shut the extension down.</summary>
    public const int ExitOk = 0;

    /// <summary>The host's <c>proto_major</c> is not one this extension speaks: no HELLO is sent.</summary>
    public const int ExitVersion = 2;

    /// <summary>A protocol error, or no HELLO.</summary>
    public const int ExitProtocol = 3;

    private sealed class LinkState
    {
        public volatile bool Outstanding;
        public volatile bool ShutdownReplied;
    }

    /// <summary>Runs the extension on an open connection and returns the exit status of the process.</summary>
    public static int Run(Stream stream, ExtensionOptions? options = null)
    {
        options ??= new ExtensionOptions();
        var counters = new Counters();
        var writer = new FrameWriter(stream, counters);

        HostHello host;
        int maxFrame;
        try
        {
            Frame? first = FrameCodec.Read(stream, FrameCodec.InitialMaxFrame);
            if (first == null)
            {
                Console.Error.WriteLine("the connection ended before the host's HELLO");
                return ExitProtocol;
            }
            if (first is not HelloFrame hello)
            {
                throw new ProtocolException("the first frame is not a HELLO");
            }
            long? major = HostHello.ProtoMajor(hello.Hello);
            if (major != null && major != 1)
            {
                Console.Error.WriteLine("the host speaks protocol " + major + ", this extension speaks 1");
                return ExitVersion;
            }
            host = HostHello.Parse(hello.Hello);
            maxFrame = (int)Math.Min(FrameCodec.InitialMaxFrame, host.MaxFrame);
            writer.Send(new HelloFrame(Manifest.Hello(Manifest.Build(options.WantsTick))));
        }
        catch (ProtocolException e)
        {
            Console.Error.WriteLine("protocol error: " + e.Message);
            return ExitProtocol;
        }

        void Log(int level, string text) => writer.Send(new LogFrame((ulong)level, text));

        var state = new LinkState();
        var queue = new BlockingCollection<Queued>();
        using var stopping = new CancellationTokenSource();
        var dispatcher = new Dispatcher(host, maxFrame, Log, options, counters, stopping.Token);
        var worker = new Thread(() => Work(queue, dispatcher, writer, state, counters, options)) { IsBackground = true, Name = "crossfire-worker" };
        worker.Start();

        var counting = new ReadCountingStream(stream);
        int exit = Read(counting, maxFrame, writer, queue, state, counters, Log);

        // EOF or a protocol error: the state is saved (unless session.shutdown said false), then the process ends (protocol section 1)
        stopping.Cancel();
        queue.CompleteAdding();
        bool workerDone = worker.Join(options.WorkerGrace);
        if (!workerDone)
        {
            Console.Error.WriteLine("the worker did not finish within " + options.WorkerGrace.TotalSeconds + " s");
        }
        dispatcher.Finish(workerDone);
        dispatcher.Release();
        return exit;
    }

    /// <summary>The reader thread X: it always reads, answers PING at once and hands requests to the worker.</summary>
    private static int Read(ReadCountingStream stream, int maxFrame, FrameWriter writer, BlockingCollection<Queued> queue, LinkState state, Counters counters, Action<int, string> log)
    {
        ulong nextReqId = 1;
        long before = stream.BytesRead;
        for (; ; )
        {
            Frame? frame;
            try
            {
                frame = FrameCodec.Read(stream, maxFrame);
                if (frame == null)
                {
                    return ExitOk;
                }
                counters.CountFrameIn(stream.BytesRead - before);
                long size = stream.BytesRead - before;
                before = stream.BytesRead;
                switch (frame)
                {
                    case PingFrame ping:
                        counters.CountPing();
                        if (!writer.Send(new PongFrame(ping.N)))
                        {
                            return ExitOk;
                        }
                        break;
                    case PongFrame:
                        break; // the extension sends no PING in this version: a PONG it did not send is ignored
                    case ReqFrame req:
                        if (state.ShutdownReplied)
                        {
                            throw new ProtocolException("a REQ after the REPLY to session.shutdown");
                        }
                        if (state.Outstanding)
                        {
                            throw new ProtocolException("a REQ while the previous one is outstanding");
                        }
                        if (req.ReqId != nextReqId)
                        {
                            throw new ProtocolException("reqid " + req.ReqId + " where " + nextReqId + " was expected");
                        }
                        nextReqId++;
                        state.Outstanding = true;
                        queue.Add(new Queued(req, Stopwatch.GetTimestamp(), size));
                        break;
                    default:
                        throw new ProtocolException("a " + frame.Kind + " frame where the host may not send one");
                }
            }
            catch (ProtocolException e)
            {
                log(0, "protocol error: " + e.Message);
                Console.Error.WriteLine("protocol error: " + e.Message);
                return ExitProtocol;
            }
        }
    }

    /// <summary>The worker W: the only thread that touches the world.</summary>
    private static void Work(BlockingCollection<Queued> queue, Dispatcher dispatcher, FrameWriter writer, LinkState state, Counters counters, ExtensionOptions options)
    {
        long spin = (long)(options.Spin.TotalSeconds * Stopwatch.Frequency);
        bool afterReply = false;
        for (; ; )
        {
            if (!queue.TryTake(out Queued item))
            {
                // A chain of requests stays hot when the worker spins a moment before it sleeps (design section 7); it costs CPU only
                // while a chain is active, since it spins only after a REPLY.
                if (afterReply && spin > 0)
                {
                    bool hit = SpinTake(queue, spin, out item);
                    if (hit)
                    {
                        counters.SpinHits++;
                    }
                    else
                    {
                        counters.SpinMisses++;
                    }
                    if (!hit && !queue.TryTake(out item, Timeout.Infinite))
                    {
                        return;
                    }
                }
                else if (!queue.TryTake(out item, Timeout.Infinite))
                {
                    return;
                }
            }
            ReqFrame req = item.Req;
            options.AfterDequeue?.Invoke(req.Op);
            long started = Stopwatch.GetTimestamp();
            ReplyFrame reply = dispatcher.Handle(req);
            long handled = Stopwatch.GetTimestamp();
            if (req.Op == 4 && reply.Status == Status.Ok)
            {
                state.ShutdownReplied = true; // before the REPLY is written: the host may close and write at once
            }
            state.Outstanding = false; // likewise: the host sends the next REQ as soon as it has read this REPLY
            bool sent = writer.Send(reply, out int replyBytes);
            counters.RecordOp(req.Op, started - item.Stamp, handled - started, item.Bytes, replyBytes, reply.Status != Status.Ok);
            if (!sent)
            {
                return;
            }
            afterReply = true;
        }
    }

    private static bool SpinTake(BlockingCollection<Queued> queue, long spinTicks, out Queued item)
    {
        long end = Stopwatch.GetTimestamp() + spinTicks;
        do
        {
            if (queue.TryTake(out item))
            {
                return true;
            }
            Thread.SpinWait(8);
        }
        while (Stopwatch.GetTimestamp() < end);
        item = default;
        return false;
    }
}
