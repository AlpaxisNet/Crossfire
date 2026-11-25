// Not ported from a Crossfire file: the frames of the MUD extension protocol (docs/protocol.md sections 2 and 4), written from that
// text alone.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Buffers.Binary;

namespace Crossfire.Extension.Protocol;

/// <summary>The kinds of frames (section 4).</summary>
internal enum FrameKind : byte
{
    Hello = 1,
    Req = 2,
    Reply = 3,
    Query = 4,
    QResult = 5,
    Ping = 6,
    Pong = 7,
    Log = 8,
}

/// <summary>The error codes (section 8).</summary>
internal static class Status
{
    public const int Ok = 0;
    public const int UnknownOp = 1;
    public const int BadArgs = 2;
    public const int BadHandle = 3;
    public const int Unavailable = 4;
    public const int TooLarge = 5;
    public const int Version = 6;
    public const int Busy = 7;
    public const int Internal = 8;
}

internal abstract record Frame
{
    public abstract FrameKind Kind { get; }
}

internal sealed record HelloFrame(Val Hello) : Frame
{
    public override FrameKind Kind => FrameKind.Hello;
}

internal sealed record ReqFrame(ulong ReqId, ulong Op, IReadOnlyList<ulong> Releases, Val Ctx, IReadOnlyList<Val> Args) : Frame
{
    public override FrameKind Kind => FrameKind.Req;
}

internal sealed record ReplyFrame(ulong ReqId, ulong Status, Val Result, IReadOnlyList<Val> Effects, IReadOnlyList<Val> Events) : Frame
{
    public override FrameKind Kind => FrameKind.Reply;
}

internal sealed record QueryFrame(ulong Qid, string Name, IReadOnlyList<Val> Args) : Frame
{
    public override FrameKind Kind => FrameKind.Query;
}

internal sealed record QResultFrame(ulong Qid, ulong Status, Val Result) : Frame
{
    public override FrameKind Kind => FrameKind.QResult;
}

internal sealed record PingFrame(ulong N) : Frame
{
    public override FrameKind Kind => FrameKind.Ping;
}

internal sealed record PongFrame(ulong N) : Frame
{
    public override FrameKind Kind => FrameKind.Pong;
}

internal sealed record LogFrame(ulong Level, string Text) : Frame
{
    public override FrameKind Kind => FrameKind.Log;
}

/// <summary>Frames as bytes: a 4-byte little endian length, then the kind byte and the fields.</summary>
internal static class FrameCodec
{
    /// <summary>The link's maximum until both HELLOs are read (section 2).</summary>
    public const int InitialMaxFrame = 16_777_216;

    /// <summary>The smallest <c>max_frame</c> a HELLO may give.</summary>
    public const int MinMaxFrame = 65_536;

    /// <summary>The frame with its length prefix.</summary>
    public static byte[] Encode(Frame frame)
    {
        var body = new ByteBuf();
        body.Add((byte)frame.Kind);
        switch (frame)
        {
            case HelloFrame h:
                ValueCodec.Write(body, h.Hello);
                break;
            case ReqFrame r:
                ValueCodec.WriteVarint(body, r.ReqId);
                ValueCodec.WriteVarint(body, r.Op);
                ValueCodec.Write(body, Val.List(r.Releases.Select(Val.Handle)));
                ValueCodec.Write(body, r.Ctx);
                ValueCodec.Write(body, Val.List(r.Args));
                break;
            case ReplyFrame r:
                ValueCodec.WriteVarint(body, r.ReqId);
                ValueCodec.WriteVarint(body, r.Status);
                ValueCodec.Write(body, r.Result);
                ValueCodec.Write(body, Val.List(r.Effects));
                ValueCodec.Write(body, Val.List(r.Events));
                ValueCodec.Write(body, Val.Nil);
                break;
            case QueryFrame q:
                ValueCodec.WriteVarint(body, q.Qid);
                ValueCodec.Write(body, Val.Str(q.Name));
                ValueCodec.Write(body, Val.List(q.Args));
                break;
            case QResultFrame q:
                ValueCodec.WriteVarint(body, q.Qid);
                ValueCodec.WriteVarint(body, q.Status);
                ValueCodec.Write(body, q.Result);
                break;
            case PingFrame p:
                ValueCodec.WriteVarint(body, p.N);
                break;
            case PongFrame p:
                ValueCodec.WriteVarint(body, p.N);
                break;
            case LogFrame l:
                ValueCodec.WriteVarint(body, l.Level);
                ValueCodec.Write(body, Val.Str(l.Text));
                break;
        }
        byte[] result = new byte[4 + body.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)body.Length);
        body.ToArray().CopyTo(result, 4);
        return result;
    }

    /// <summary>The frame of a body (the bytes after the length), or a <see cref="ProtocolException"/>.</summary>
    public static Frame Decode(ReadOnlySpan<byte> body)
    {
        var r = new ValueCodec.Reader(body);
        byte kind = r.ReadByte();
        Frame frame;
        switch ((FrameKind)kind)
        {
            case FrameKind.Hello:
                frame = new HelloFrame(Expect(ref r, ValKind.Map, "hello"));
                break;
            case FrameKind.Req:
                {
                    ulong reqid = r.ReadVarint();
                    ulong op = r.ReadVarint();
                    Val releases = Expect(ref r, ValKind.List, "releases");
                    if (releases.Items.Any(h => h.Kind != ValKind.Handle))
                    {
                        throw new ProtocolException("releases holds a value that is not a handle");
                    }
                    Val ctx = Expect(ref r, ValKind.Map, "ctx");
                    Val args = Expect(ref r, ValKind.List, "args");
                    frame = new ReqFrame(reqid, op, releases.Items.Select(h => h.AsHandle).ToArray(), ctx, args.Items);
                    break;
                }
            case FrameKind.Reply:
                {
                    ulong reqid = r.ReadVarint();
                    ulong status = r.ReadVarint();
                    Val result = r.ReadValue();
                    Val effects = Expect(ref r, ValKind.List, "effects");
                    Val events = Expect(ref r, ValKind.List, "events");
                    Val touched = r.ReadValue();
                    if (!touched.IsNil)
                    {
                        throw new ProtocolException("touched is not nil");
                    }
                    frame = new ReplyFrame(reqid, status, result, effects.Items, events.Items);
                    break;
                }
            case FrameKind.Query:
                {
                    ulong qid = r.ReadVarint();
                    Val name = Expect(ref r, ValKind.String, "name");
                    Val args = Expect(ref r, ValKind.List, "args");
                    frame = new QueryFrame(qid, name.AsString, args.Items);
                    break;
                }
            case FrameKind.QResult:
                {
                    ulong qid = r.ReadVarint();
                    ulong status = r.ReadVarint();
                    frame = new QResultFrame(qid, status, r.ReadValue());
                    break;
                }
            case FrameKind.Ping:
                frame = new PingFrame(r.ReadVarint());
                break;
            case FrameKind.Pong:
                frame = new PongFrame(r.ReadVarint());
                break;
            case FrameKind.Log:
                {
                    ulong level = r.ReadVarint();
                    frame = new LogFrame(level, Expect(ref r, ValKind.String, "text").AsString);
                    break;
                }
            default:
                throw new ProtocolException("unknown frame kind " + kind);
        }
        if (r.Left != 0)
        {
            throw new ProtocolException("bytes after the last field of a frame");
        }
        return frame;
    }

    private static Val Expect(ref ValueCodec.Reader r, ValKind kind, string field)
    {
        Val v = r.ReadValue();
        if (v.Kind != kind)
        {
            throw new ProtocolException("the field " + field + " is not of type " + kind);
        }
        return v;
    }

    /// <summary>
    /// Reads one frame. Null when the stream ends (before the frame, or inside it: the connection is over either way). A length of 0
    /// or above <paramref name="maxFrame"/> is a protocol error, found before any body is read.
    /// </summary>
    public static Frame? Read(Stream stream, int maxFrame)
    {
        byte[] head = new byte[4];
        if (!ReadFull(stream, head))
        {
            return null;
        }
        uint n = BinaryPrimitives.ReadUInt32LittleEndian(head);
        if (n < 1 || n > (uint)maxFrame)
        {
            throw new ProtocolException("a frame length of " + n + " is out of range");
        }
        byte[] body = new byte[n];
        if (!ReadFull(stream, body))
        {
            return null;
        }
        return Decode(body);
    }

    private static bool ReadFull(Stream stream, byte[] buffer)
    {
        int got = 0;
        while (got < buffer.Length)
        {
            int k;
            try
            {
                k = stream.Read(buffer, got, buffer.Length - got);
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            if (k == 0)
            {
                return false;
            }
            got += k;
        }
        return true;
    }
}
