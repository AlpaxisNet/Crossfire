// Not ported from a Crossfire file: the manifest of the Crossfire extension (docs/protocol.md section 6) and its HELLO (section 4.1).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>
/// The manifest and the HELLO that carries it. The manifest is collected, not written in one place: the core registers what the
/// first waves built (<c>RegisterCore</c>), and each lane of the later waves registers its own functions, types and events in a
/// partial file of its own (Gk/Manifest.Items.cs and the others), through the <see cref="ManifestBuilder"/> that
/// <see cref="Collect"/> hands to every lane's method. The ids each lane may use are in <see cref="Lane"/> and docs/extension.md.
/// </summary>
internal static partial class Manifest
{
    public const string ExtensionName = "crossfire";
    public const string ExtensionVersion = "0.1.0";

    /// <summary>The highest <c>proto_minor</c> this extension speaks (protocol section 4.1); it sends it in every HELLO.</summary>
    public const int ProtoMinor = 1;

    public const int OpFindArchetype = 64;
    public const int OpFindArchetypeByRealName = 65;

    public const int OpMudAddObject = 66;
    public const int OpMudAddSpecialObject = 67;

    public const string EventOnUpdateObject = "onUpdateObject";
    public const string EventOnMudDestroy = "onMudDestroy";
    public const string EventOnLoadedMudAccount = "onLoadedMudAccount";

    private static Val Member(ArchMember m) => Val.Map(
        ("id", Val.Int(m.Id)),
        ("name", Val.Str(m.Name)),
        ("type", Val.Str(m.TypeName)),
        ("access", Val.Str("r")),
        ("stable", Val.True));

    /// <summary>The link's minor version (protocol section 4.1): the smaller of the two sides' <c>proto_minor</c>.</summary>
    public static int LinkMinorOf(long hostProtoMinor) => (int)Math.Clamp(Math.Min(hostProtoMinor, ProtoMinor), 0, ProtoMinor);

    /// <summary>
    /// Collects the manifest of a link of the given minor version: the core, then each lane in the order of <see cref="Lane.All"/>,
    /// then <paramref name="extra"/> (a hook of the tests).
    /// </summary>
    public static ManifestBuilder Collect(int linkMinor, Action<ManifestBuilder>? extra = null)
    {
        var b = new ManifestBuilder(linkMinor);
        RegisterCore(b);
        RegisterItems(b);
        RegisterStart(b);
        RegisterGmaps(b);
        RegisterWave5A(b);
        RegisterWave5B(b);
        RegisterWave5C(b);
        RegisterWave5D(b);
        extra?.Invoke(b);
        return b;
    }

    /// <summary>The manifest map of a link of the given minor version; <c>wants_tick</c> is false unless asked.</summary>
    public static Val Build(bool wantsTick = false, int linkMinor = 0, Action<ManifestBuilder>? extra = null) => Collect(linkMinor, extra).ToVal(wantsTick);

    private static void RegisterCore(ManifestBuilder b)
    {
        b.AddFunction(Lane.Core, new FunctionDef(OpFindArchetype, "findarchetype", new[] { "string" }, "handle:" + TMudArchMembers.TypeName));
        b.AddFunction(Lane.Core, new FunctionDef(OpFindArchetypeByRealName, "findarchetypebyrealname", new[] { "string" }, "handle:" + TMudArchMembers.TypeName));
        b.AddFunction(Lane.Core, new FunctionDef(OpMudAddObject, "mudaddobject", new[] { "float", "float", "string" }, "handle:" + TMudObjectMembers.TypeName));
        b.AddFunction(Lane.Core, new FunctionDef(OpMudAddSpecialObject, "mudaddspecialobject", new[] { "float", "float", "int", "string", "string" }, "handle:" + TMudObjectMembers.TypeName));
        b.AddType(Lane.Core, new TypeDef(TMudArchMembers.TypeId, TMudArchMembers.TypeName, Val.Map(
            ("id", Val.Int(TMudArchMembers.TypeId)),
            ("name", Val.Str(TMudArchMembers.TypeName)),
            ("host", Val.Str("object")),
            ("members", Val.List(TMudArchMembers.Members.Select(Member))),
            ("methods", Val.List()))));
        b.AddType(Lane.Core, new TypeDef(TMudObjectMembers.TypeId, TMudObjectMembers.TypeName, TMudObjectMembers.Table.ToManifest()));
        b.AddType(Lane.Core, new TypeDef(TMudPlayerMembers.TypeId, TMudPlayerMembers.TypeName, TMudPlayerMembers.Table.ToManifest()));
        b.AddEvent(Lane.Core, new EventDef(EventOnUpdateObject, "object"));
        b.AddEvent(Lane.Core, new EventDef(EventOnMudDestroy, "object"));
        b.AddEvent(Lane.Core, new EventDef(EventOnLoadedMudAccount, "player"));
    }

    /// <summary>The extension's HELLO map (section 4.1), the keys in the order the text lists them; <c>max_frame</c> is left out.</summary>
    public static Val Hello(Val manifest, int protoMinor = 0) => Val.Map(
        ("proto_major", Val.Int(1)),
        ("proto_minor", Val.Int(protoMinor)),
        ("role", Val.Str("extension")),
        ("name", Val.Str(ExtensionName)),
        ("version", Val.Str(ExtensionVersion)),
        ("features", Val.List()),
        ("manifest", manifest));
}

/// <summary>What the extension reads from the host's HELLO (section 4.1).</summary>
internal sealed record HostHello(long Epoch, string DataDir, string WorldDir, long MaxFrame, long CallTimeoutMs, string ServerName, long ProtoMinor = 0)
{
    /// <summary>The link's minor version (section 4.1): the smaller of the host's <c>proto_minor</c> and this extension's.</summary>
    public int LinkMinor => Manifest.LinkMinorOf(ProtoMinor);

    /// <summary>The proto_major of a HELLO map, or null when it has none or it is not a number.</summary>
    public static long? ProtoMajor(Val hello) => hello.Get("proto_major")?.AsNumberToInt();

    /// <summary>Checks the required keys and their types, as section 3.3 asks; a fault is a protocol error.</summary>
    public static HostHello Parse(Val hello)
    {
        long Number(string key) => hello.Get(key)?.AsNumberToInt() ?? throw new ProtocolException("the host's HELLO lacks the number " + key);
        string Text(string key) => hello.Get(key) is { Kind: ValKind.String } s ? s.AsString : throw new ProtocolException("the host's HELLO lacks the string " + key);

        Number("proto_major");
        long protoMinor = Number("proto_minor");
        Text("role");
        Text("name");
        Text("version");
        string serverName = Text("server_name");
        long epoch = Number("epoch");
        string dataDir = Text("data_dir");
        string worldDir = Text("world_dir");
        long maxFrame = Number("max_frame");
        long timeout = Number("call_timeout_ms");
        if (epoch < 1 || epoch > uint.MaxValue)
        {
            throw new ProtocolException("the host's epoch is out of range");
        }
        if (maxFrame < FrameCodec.MinMaxFrame)
        {
            throw new ProtocolException("the host's max_frame is below 65,536");
        }
        return new HostHello(epoch, dataDir, worldDir, maxFrame, timeout, serverName, protoMinor);
    }
}
