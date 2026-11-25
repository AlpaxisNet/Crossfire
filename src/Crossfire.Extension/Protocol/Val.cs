// Not ported from a Crossfire file: the values of the MUD extension protocol (docs/protocol.md section 3.2), written from that text alone.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Protocol;

/// <summary>The tags of a value (docs/protocol.md section 3.2).</summary>
internal enum ValKind : byte
{
    Nil = 0,
    False = 1,
    True = 2,
    Int = 3,
    Float = 4,
    String = 5,
    Handle = 6,
    List = 7,
    Map = 8,
}

/// <summary>A protocol value: nil, a bool, an int, a float, a string, a handle, a list or a map (the keys of a map in the order given).</summary>
internal sealed class Val
{
    public static readonly Val Nil = new(ValKind.Nil);
    public static readonly Val False = new(ValKind.False);
    public static readonly Val True = new(ValKind.True);

    private readonly long i;
    private readonly double f;
    private readonly string? s;
    private readonly IReadOnlyList<Val>? list;
    private readonly IReadOnlyList<KeyValuePair<string, Val>>? map;

    private Val(ValKind kind) => Kind = kind;

    private Val(ValKind kind, long i, double f, string? s, IReadOnlyList<Val>? list, IReadOnlyList<KeyValuePair<string, Val>>? map)
    {
        Kind = kind;
        this.i = i;
        this.f = f;
        this.s = s;
        this.list = list;
        this.map = map;
    }

    public ValKind Kind { get; }

    public static Val Bool(bool b) => b ? True : False;

    public static Val Int(long n) => new(ValKind.Int, n, 0, null, null, null);

    public static Val Float(double d) => new(ValKind.Float, 0, d, null, null, null);

    public static Val Str(string text) => new(ValKind.String, 0, 0, text, null, null);

    /// <summary>A handle: the epoch in the high 32 bits and the tag in the low 32 bits (section 5).</summary>
    public static Val Handle(ulong h) => new(ValKind.Handle, unchecked((long)h), 0, null, null, null);

    public static Val List(IEnumerable<Val> items) => new(ValKind.List, 0, 0, null, items.ToArray(), null);

    public static Val List(params Val[] items) => new(ValKind.List, 0, 0, null, items, null);

    public static Val Map(IEnumerable<KeyValuePair<string, Val>> pairs) => new(ValKind.Map, 0, 0, null, null, pairs.ToArray());

    public static Val Map(params (string Key, Val Value)[] pairs) =>
        new(ValKind.Map, 0, 0, null, null, pairs.Select(p => new KeyValuePair<string, Val>(p.Key, p.Value)).ToArray());

    public bool IsNil => Kind == ValKind.Nil;

    public bool IsBool => Kind is ValKind.False or ValKind.True;

    public bool AsBool => Kind == ValKind.True;

    public long AsInt => i;

    public double AsFloat => f;

    public string AsString => s ?? "";

    public ulong AsHandle => unchecked((ulong)i);

    public IReadOnlyList<Val> Items => list ?? Array.Empty<Val>();

    public IReadOnlyList<KeyValuePair<string, Val>> Pairs => map ?? Array.Empty<KeyValuePair<string, Val>>();

    /// <summary>The value of a key of a map, or null when the key is not there (or this is not a map).</summary>
    public Val? Get(string key)
    {
        foreach (KeyValuePair<string, Val> p in Pairs)
        {
            if (p.Key == key)
            {
                return p.Value;
            }
        }
        return null;
    }

    /// <summary>
    /// A number as section 3.2 reads it: an int, or a float truncated toward zero and clamped to the 64-bit range, NaN giving 0.
    /// Null for any other type.
    /// </summary>
    public long? AsNumberToInt()
    {
        if (Kind == ValKind.Int)
        {
            return i;
        }
        if (Kind == ValKind.Float)
        {
            if (double.IsNaN(f))
            {
                return 0;
            }
            if (f >= 9223372036854775808.0)
            {
                return long.MaxValue;
            }
            if (f <= -9223372036854775808.0)
            {
                return long.MinValue;
            }
            return (long)f;
        }
        return null;
    }

    /// <summary>Equality as section 6 compares manifests: lists in order, maps without regard to the order of their keys.</summary>
    public static bool Same(Val a, Val b)
    {
        if (a.Kind != b.Kind)
        {
            return false;
        }
        switch (a.Kind)
        {
            case ValKind.Int:
            case ValKind.Handle:
                return a.i == b.i;
            case ValKind.Float:
                return BitConverter.DoubleToInt64Bits(a.f) == BitConverter.DoubleToInt64Bits(b.f);
            case ValKind.String:
                return a.AsString == b.AsString;
            case ValKind.List:
                return a.Items.Count == b.Items.Count && a.Items.Zip(b.Items).All(p => Same(p.First, p.Second));
            case ValKind.Map:
                return a.Pairs.Count == b.Pairs.Count && a.Pairs.All(p => b.Get(p.Key) is { } o && Same(p.Value, o));
            default:
                return true;
        }
    }

    public override string ToString() => Kind switch
    {
        ValKind.Nil => "nil",
        ValKind.False => "false",
        ValKind.True => "true",
        ValKind.Int => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ValKind.Float => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        ValKind.String => "\"" + s + "\"",
        ValKind.Handle => "handle:" + AsHandle.ToString("x", System.Globalization.CultureInfo.InvariantCulture),
        ValKind.List => "[" + string.Join(", ", Items) + "]",
        _ => "{" + string.Join(", ", Pairs.Select(p => p.Key + ": " + p.Value)) + "}",
    };
}

/// <summary>A breach of a "must" about the bytes or the order of frames (docs/protocol.md section 9).</summary>
internal sealed class ProtocolException : Exception
{
    public ProtocolException(string message) : base(message)
    {
    }
}
