// Not ported from a Crossfire file: the manifest of the Crossfire extension (docs/protocol.md section 6) and its HELLO (section 4.1).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>The manifest and the HELLO that carries it.</summary>
internal static class Manifest
{
    public const string ExtensionName = "crossfire";
    public const string ExtensionVersion = "0.1.0";

    public const int OpFindArchetype = 64;
    public const int OpFindArchetypeByRealName = 65;

    private static Val Function(int id, string name) => Val.Map(
        ("id", Val.Int(id)),
        ("name", Val.Str(name)),
        ("params", Val.List(Val.Str("string"))),
        ("ret", Val.Str("handle:" + TMudArchMembers.TypeName)),
        ("slow", Val.False));

    private static Val Member(ArchMember m) => Val.Map(
        ("id", Val.Int(m.Id)),
        ("name", Val.Str(m.Name)),
        ("type", Val.Str(m.TypeName)),
        ("access", Val.Str("r")),
        ("stable", Val.True));

    /// <summary>The manifest map: the two functions and the type TMudArch with its stable read-only members.</summary>
    public static Val Build() => Val.Map(
        ("functions", Val.List(Function(OpFindArchetype, "findarchetype"), Function(OpFindArchetypeByRealName, "findarchetypebyrealname"))),
        ("types", Val.List(Val.Map(
            ("id", Val.Int(TMudArchMembers.TypeId)),
            ("name", Val.Str(TMudArchMembers.TypeName)),
            ("host", Val.Str("object")),
            ("members", Val.List(TMudArchMembers.Members.Select(Member))),
            ("methods", Val.List())))),
        ("events", Val.List()),
        ("wants_tick", Val.False),
        ("limits", Val.Map(("max_list", Val.Int(256)))));

    /// <summary>The extension's HELLO map (section 4.1), the keys in the order the text lists them; <c>max_frame</c> is left out.</summary>
    public static Val Hello(Val manifest) => Val.Map(
        ("proto_major", Val.Int(1)),
        ("proto_minor", Val.Int(0)),
        ("role", Val.Str("extension")),
        ("name", Val.Str(ExtensionName)),
        ("version", Val.Str(ExtensionVersion)),
        ("features", Val.List()),
        ("manifest", manifest));
}

/// <summary>What the extension reads from the host's HELLO (section 4.1).</summary>
internal sealed record HostHello(long Epoch, string DataDir, string WorldDir, long MaxFrame, long CallTimeoutMs, string ServerName)
{
    /// <summary>The proto_major of a HELLO map, or null when it has none or it is not a number.</summary>
    public static long? ProtoMajor(Val hello) => hello.Get("proto_major")?.AsNumberToInt();

    /// <summary>Checks the required keys and their types, as section 3.3 asks; a fault is a protocol error.</summary>
    public static HostHello Parse(Val hello)
    {
        long Number(string key) => hello.Get(key)?.AsNumberToInt() ?? throw new ProtocolException("the host's HELLO lacks the number " + key);
        string Text(string key) => hello.Get(key) is { Kind: ValKind.String } s ? s.AsString : throw new ProtocolException("the host's HELLO lacks the string " + key);

        Number("proto_major");
        Number("proto_minor");
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
        return new HostHello(epoch, dataDir, worldDir, maxFrame, timeout, serverName);
    }
}
