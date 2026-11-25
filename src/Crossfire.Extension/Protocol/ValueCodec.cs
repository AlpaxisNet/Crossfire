// Not ported from a Crossfire file: the encoding of varints and values (docs/protocol.md section 3), written from that text alone.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Buffers.Binary;
using System.Text;

namespace Crossfire.Extension.Protocol;

/// <summary>A growing byte buffer.</summary>
internal sealed class ByteBuf
{
    private byte[] data = new byte[64];

    public int Length { get; private set; }

    public void Add(byte b)
    {
        Ensure(1);
        data[Length++] = b;
    }

    public void Add(ReadOnlySpan<byte> bytes)
    {
        Ensure(bytes.Length);
        bytes.CopyTo(data.AsSpan(Length));
        Length += bytes.Length;
    }

    public byte[] ToArray() => data.AsSpan(0, Length).ToArray();

    private void Ensure(int more)
    {
        if (Length + more > data.Length)
        {
            Array.Resize(ref data, Math.Max(data.Length * 2, Length + more));
        }
    }
}

/// <summary>Varints (section 3.1) and values (section 3.2): the encoder and the decoder.</summary>
internal static class ValueCodec
{
    /// <summary>Each field's value has depth 16 at most (section 3.2).</summary>
    public const int MaxDepth = 16;

    private static readonly UTF8Encoding Utf8 = new(false, false);

    // ------------------------------------------------------------------ encoding

    public static void WriteVarint(ByteBuf buf, ulong v)
    {
        while (v >= 0x80)
        {
            buf.Add((byte)(v | 0x80));
            v >>= 7;
        }
        buf.Add((byte)v);
    }

    public static ulong Zigzag(long n) => unchecked((ulong)((n << 1) ^ (n >> 63)));

    public static long Unzigzag(ulong u) => unchecked((long)(u >> 1) ^ -(long)(u & 1));

    private static void WriteKey(ByteBuf buf, string key)
    {
        byte[] bytes = Utf8.GetBytes(key);
        WriteVarint(buf, (ulong)bytes.Length);
        buf.Add(bytes);
    }

    public static void Write(ByteBuf buf, Val v)
    {
        buf.Add((byte)v.Kind);
        switch (v.Kind)
        {
            case ValKind.Int:
                WriteVarint(buf, Zigzag(v.AsInt));
                break;
            case ValKind.Float:
                Span<byte> f = stackalloc byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(f, BitConverter.DoubleToInt64Bits(v.AsFloat));
                buf.Add(f);
                break;
            case ValKind.String:
                WriteKey(buf, v.AsString);
                break;
            case ValKind.Handle:
                Span<byte> h = stackalloc byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(h, v.AsHandle);
                buf.Add(h);
                break;
            case ValKind.List:
                WriteVarint(buf, (ulong)v.Items.Count);
                foreach (Val item in v.Items)
                {
                    Write(buf, item);
                }
                break;
            case ValKind.Map:
                WriteVarint(buf, (ulong)v.Pairs.Count);
                foreach (KeyValuePair<string, Val> p in v.Pairs)
                {
                    WriteKey(buf, p.Key);
                    Write(buf, p.Value);
                }
                break;
        }
    }

    // ------------------------------------------------------------------ decoding

    /// <summary>A cursor over the body of a frame; every read that runs past the end is a protocol error.</summary>
    public ref struct Reader
    {
        private readonly ReadOnlySpan<byte> data;

        public Reader(ReadOnlySpan<byte> data)
        {
            this.data = data;
            Position = 0;
        }

        public int Position { get; private set; }

        public int Left => data.Length - Position;

        public byte ReadByte()
        {
            if (Position >= data.Length)
            {
                throw new ProtocolException("the body ends inside a field");
            }
            return data[Position++];
        }

        public ReadOnlySpan<byte> ReadBytes(ulong count)
        {
            if (count > (ulong)Left)
            {
                throw new ProtocolException("a count or byte count is past the end of the body");
            }
            ReadOnlySpan<byte> r = data.Slice(Position, (int)count);
            Position += (int)count;
            return r;
        }

        public ulong ReadVarint()
        {
            ulong result = 0;
            for (int n = 0; n < 10; n++)
            {
                byte b = ReadByte();
                if (n == 9)
                {
                    if ((b & 0x80) != 0)
                    {
                        throw new ProtocolException("a varint is longer than 10 bytes");
                    }
                    if (b > 1)
                    {
                        throw new ProtocolException("a varint does not fit in 64 bits");
                    }
                }
                result |= (ulong)(b & 0x7f) << (7 * n);
                if ((b & 0x80) == 0)
                {
                    return result;
                }
            }
            throw new ProtocolException("a varint is longer than 10 bytes");
        }

        public string ReadText()
        {
            ulong n = ReadVarint();
            return Utf8.GetString(ReadBytes(n));
        }

        /// <summary>A key of a map: its text and its bytes' identity (duplicates are compared before any replacement).</summary>
        public (string Text, string Identity) ReadKey()
        {
            ulong n = ReadVarint();
            ReadOnlySpan<byte> bytes = ReadBytes(n);
            return (Utf8.GetString(bytes), Convert.ToHexString(bytes));
        }

        public Val ReadValue() => ReadValue(1);

        private Val ReadValue(int depth)
        {
            if (depth > MaxDepth)
            {
                throw new ProtocolException("a value is deeper than 16");
            }
            byte tag = ReadByte();
            switch ((ValKind)tag)
            {
                case ValKind.Nil:
                    return Val.Nil;
                case ValKind.False:
                    return Val.False;
                case ValKind.True:
                    return Val.True;
                case ValKind.Int:
                    return Val.Int(Unzigzag(ReadVarint()));
                case ValKind.Float:
                    return Val.Float(BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(8))));
                case ValKind.String:
                    return Val.Str(ReadText());
                case ValKind.Handle:
                    return Val.Handle(BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8)));
                case ValKind.List:
                    {
                        ulong count = ReadVarint();
                        if (count > (ulong)Left)
                        {
                            throw new ProtocolException("a list has more elements than the body has bytes");
                        }
                        var items = new Val[(int)count];
                        for (int k = 0; k < items.Length; k++)
                        {
                            items[k] = ReadValue(depth + 1);
                        }
                        return Val.List(items);
                    }
                case ValKind.Map:
                    {
                        ulong count = ReadVarint();
                        if (count > (ulong)Left)
                        {
                            throw new ProtocolException("a map has more pairs than the body has bytes");
                        }
                        var pairs = new KeyValuePair<string, Val>[(int)count];
                        var seen = new HashSet<string>();
                        for (int k = 0; k < pairs.Length; k++)
                        {
                            (string text, string identity) = ReadKey();
                            if (!seen.Add(identity))
                            {
                                throw new ProtocolException("a map has a duplicate key");
                            }
                            pairs[k] = new KeyValuePair<string, Val>(text, ReadValue(depth + 1));
                        }
                        return Val.Map(pairs);
                    }
                default:
                    throw new ProtocolException("unknown value tag " + tag);
            }
        }
    }

    /// <summary>Decodes a whole value from bytes that hold nothing else.</summary>
    public static Val Decode(ReadOnlySpan<byte> bytes)
    {
        var r = new Reader(bytes);
        Val v = r.ReadValue();
        if (r.Left != 0)
        {
            throw new ProtocolException("bytes after the last field");
        }
        return v;
    }

    public static byte[] Encode(Val v)
    {
        var buf = new ByteBuf();
        Write(buf, v);
        return buf.ToArray();
    }
}
