// Not ported from a Crossfire file: the link of the Crossfire extension to the game server (docs/protocol.md sections 1, 2, 4 and 9):
// the handshake, the reader thread X, the worker thread W and the write lock.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Collections.Concurrent;
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
    public bool Send(Frame frame)
    {
        byte[] bytes = FrameCodec.Encode(frame);
        try
        {
            lock (writeLock)
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
        {
            return false;
        }
        counters.CountFrameOut();
        return true;
    }
}

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
            writer.Send(new HelloFrame(Manifest.Hello(Manifest.Build())));
        }
        catch (ProtocolException e)
        {
            Console.Error.WriteLine("protocol error: " + e.Message);
            return ExitProtocol;
        }

        void Log(int level, string text) => writer.Send(new LogFrame((ulong)level, text));

        var state = new LinkState();
        var queue = new BlockingCollection<ReqFrame>();
        var dispatcher = new Dispatcher(host, maxFrame, Log, options, counters);
        var worker = new Thread(() => Work(queue, dispatcher, writer, state)) { IsBackground = true, Name = "crossfire-worker" };
        worker.Start();

        int exit = Read(stream, maxFrame, writer, queue, state, counters, Log);

        // EOF or a protocol error: the state would be saved here (nothing is kept yet), then the process ends
        queue.CompleteAdding();
        if (!worker.Join(options.WorkerGrace))
        {
            Console.Error.WriteLine("the worker did not finish within " + options.WorkerGrace.TotalSeconds + " s");
        }
        return exit;
    }

    /// <summary>The reader thread X: it always reads, answers PING at once and hands requests to the worker.</summary>
    private static int Read(Stream stream, int maxFrame, FrameWriter writer, BlockingCollection<ReqFrame> queue, LinkState state, Counters counters, Action<int, string> log)
    {
        ulong nextReqId = 1;
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
                counters.CountFrameIn();
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
                        queue.Add(req);
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
    private static void Work(BlockingCollection<ReqFrame> queue, Dispatcher dispatcher, FrameWriter writer, LinkState state)
    {
        foreach (ReqFrame req in queue.GetConsumingEnumerable())
        {
            ReplyFrame reply = dispatcher.Handle(req);
            if (req.Op == 4 && reply.Status == Status.Ok)
            {
                state.ShutdownReplied = true; // before the REPLY is written: the host may close and write at once
            }
            state.Outstanding = false; // likewise: the host sends the next REQ as soon as it has read this REPLY
            if (!writer.Send(reply))
            {
                return;
            }
        }
    }
}
