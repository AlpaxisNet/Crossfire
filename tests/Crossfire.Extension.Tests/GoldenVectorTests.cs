// Not ported from a Crossfire file: the golden vectors of docs/protocol.md section 13, read from that file itself (its hex blocks are
// parsed here), so that the copy of the text and the code cannot drift.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Text.RegularExpressions;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

public class GoldenVectorTests
{
    private static string DocText()
    {
        for (string? dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string file = Path.Combine(dir, "docs", "protocol.md");
            if (File.Exists(file))
            {
                return File.ReadAllText(file);
            }
        }
        throw new FileNotFoundException("docs/protocol.md");
    }

    private static byte[] Hex(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => Convert.ToByte(t, 16)).ToArray();

    /// <summary>The rows of the table of section 13.1: the label of the value and its bytes.</summary>
    private static Dictionary<string, byte[]> ValueRows()
    {
        string doc = DocText();
        int from = doc.IndexOf("### 13.1", StringComparison.Ordinal);
        int to = doc.IndexOf("### 13.2", StringComparison.Ordinal);
        var rows = new Dictionary<string, byte[]>();
        foreach (Match m in Regex.Matches(doc.Substring(from, to - from), @"^\| (?<label>.+?) \| `(?<hex>[0-9a-f ]+)` \|$", RegexOptions.Multiline))
        {
            rows.Add(m.Groups["label"].Value, Hex(m.Groups["hex"].Value));
        }
        return rows;
    }

    /// <summary>The blocks of section 13.2: the title after "####" and the bytes of the frame.</summary>
    private static Dictionary<string, byte[]> FrameBlocks()
    {
        string doc = DocText();
        int from = doc.IndexOf("### 13.2", StringComparison.Ordinal);
        var blocks = new Dictionary<string, byte[]>();
        foreach (Match m in Regex.Matches(doc.Substring(from), @"^#### (?<title>.+)\n```\n(?<hex>[0-9a-f ]+)\n```", RegexOptions.Multiline))
        {
            blocks.Add(m.Groups["title"].Value, Hex(m.Groups["hex"].Value));
        }
        return blocks;
    }

    private static readonly Dictionary<string, Val> Values = new()
    {
        ["nil"] = Val.Nil,
        ["false"] = Val.False,
        ["true"] = Val.True,
        ["int 0"] = Val.Int(0),
        ["int 1"] = Val.Int(1),
        ["int -1"] = Val.Int(-1),
        ["int 63"] = Val.Int(63),
        ["int -64"] = Val.Int(-64),
        ["int 64"] = Val.Int(64),
        ["int 300"] = Val.Int(300),
        ["int -300"] = Val.Int(-300),
        ["int 9223372036854775807"] = Val.Int(long.MaxValue),
        ["int -9223372036854775808"] = Val.Int(long.MinValue),
        ["float 1.5"] = Val.Float(1.5),
        ["float -0.25"] = Val.Float(-0.25),
        ["string \"\""] = Val.Str(""),
        ["string \"sword\""] = Val.Str("sword"),
        ["string \"Hé\" (UTF-8 c3 a9)"] = Val.Str("Hé"),
        ["handle epoch 1, tag 7"] = Val.Handle(Handle(1, 7)),
        ["list []"] = Val.List(),
        ["list [1, \"a\", nil]"] = Val.List(Val.Int(1), Val.Str("a"), Val.Nil),
        ["map {}"] = Val.Map(),
        ["map {\"a\": 1, \"b\": [true]}"] = Val.Map(("a", Val.Int(1)), ("b", Val.List(Val.True))),
    };

    private static ulong Handle(uint epoch, uint tag) => ((ulong)epoch << 32) | tag;

    private static Val Ctx(string? player = null, string? level = null) =>
        Val.Map(("player", player == null ? Val.Nil : Val.Str(player)), ("level", level == null ? Val.Nil : Val.Str(level)));

    private static Val ExampleManifest() => Val.Map(
        ("functions", Val.List(Val.Map(
            ("id", Val.Int(64)), ("name", Val.Str("findarchetype")), ("params", Val.List(Val.Str("string"))),
            ("ret", Val.Str("handle:TMudArch")), ("slow", Val.False)))),
        ("types", Val.List(Val.Map(
            ("id", Val.Int(1)), ("name", Val.Str("TMudArch")), ("host", Val.Str("object")),
            ("members", Val.List(Val.Map(
                ("id", Val.Int(100)), ("name", Val.Str("mudname")), ("type", Val.Str("string")), ("access", Val.Str("r")),
                ("stable", Val.True)))),
            ("methods", Val.List())))),
        ("events", Val.List()),
        ("wants_tick", Val.False),
        ("limits", Val.Map(("max_list", Val.Int(256)))));

    /// <summary>The frame of each block of section 13.2, built from the values the text gives.</summary>
    private static Dictionary<string, Frame> Frames() => new Dictionary<string, Frame>
    {
        ["HELLO from the GameServer"] = new HelloFrame(Val.Map(
            ("proto_major", Val.Int(1)), ("proto_minor", Val.Int(0)), ("role", Val.Str("gameserver")),
            ("name", Val.Str("Alpaxis GameServer")), ("version", Val.Str("1.0")), ("server_name", Val.Str("Test")),
            ("epoch", Val.Int(1)), ("data_dir", Val.Str("/srv/mud-data/Test")), ("world_dir", Val.Str("/srv/servers/Test")),
            ("max_frame", Val.Int(16777216)), ("call_timeout_ms", Val.Int(500)))),
        ["HELLO from the extension"] = new HelloFrame(Manifest.Hello(ExampleManifest())),
        ["REQ 1: session.init"] = new ReqFrame(1, 1, Array.Empty<ulong>(), Ctx(), Array.Empty<Val>()),
        ["REPLY 1: session.init done, 3 archetypes"] = new ReplyFrame(1, 0, Val.Map(("archetypes", Val.Int(3))), Array.Empty<Val>(), Array.Empty<Val>()),
        ["REQ 2: findarchetype(\"sword\") for player gs1player in main.nw, releasing nothing"] =
            new ReqFrame(2, 64, Array.Empty<ulong>(), Ctx("gs1player", "main.nw"), new[] { Val.Str("sword") }),
        ["REPLY 2: the handle (epoch 1, tag 7)"] = new ReplyFrame(2, 0, Val.Handle(Handle(1, 7)), Array.Empty<Val>(), Array.Empty<Val>()),
        ["REQ 3: get member 100 of that handle, releasing handle (1, 5)"] =
            new ReqFrame(3, 9, new[] { Handle(1, 5) }, Ctx(), new[] { Val.Handle(Handle(1, 7)), Val.Int(100) }),
        ["REPLY 3: \"sword\""] = new ReplyFrame(3, 0, Val.Str("sword"), Array.Empty<Val>(), Array.Empty<Val>()),
        ["REPLY 4: error 3 bad_handle"] = new ReplyFrame(4, 3, Val.Str("dead handle"), Array.Empty<Val>(), Array.Empty<Val>()),
        ["REPLY 5: an effect and an event"] = new ReplyFrame(5, 0, Val.Nil,
            new[] { Val.Map(("k", Val.Str("client.text")), ("player", Val.Str("gs1player")), ("text", Val.Str("You pick up a sword."))) },
            new[] { Val.Map(("name", Val.Str("onUpdateObject")), ("target", Val.Str("object")), ("handle", Val.Handle(Handle(1, 7))), ("args", Val.List())) }),
        ["QUERY 1: level.loaded(\"main.nw\")"] = new QueryFrame(1, "level.loaded", new[] { Val.Str("main.nw") }),
        ["QRESULT 1: true"] = new QResultFrame(1, 0, Val.True),
        ["PING 9"] = new PingFrame(9),
        ["PONG 9"] = new PongFrame(9),
        ["LOG level 1: \"loaded 3 archetypes\""] = new LogFrame(1, "loaded 3 archetypes"),
    }.Concat(Frames11()).ToDictionary(p => p.Key, p => p.Value);

    private static Val Str(params string[] items) => Val.List(items.Select(Val.Str));

    /// <summary>The frames of section 13.3, protocol 1.1's: the host's HELLO of minor 1, a 1.1 extension's HELLO (an item event, a ref parameter, player_classes), gmap.layout, a set of a ref member, a REPLY with caller.setprop and two item events with a fallback each, and the two player queries.</summary>
    private static Dictionary<string, Frame> Frames11()
    {
        Val manifest = Val.Map(
            ("functions", Val.List()),
            ("types", Val.List(
                Val.Map(
                    ("id", Val.Int(1)), ("name", Val.Str("TMudObject")), ("host", Val.Str("object")),
                    ("members", Val.List(Val.Map(
                        ("id", Val.Int(100)), ("name", Val.Str("mudowner")), ("type", Val.Str("ref")), ("access", Val.Str("rw")), ("stable", Val.False)))),
                    ("methods", Val.List(Val.Map(
                        ("id", Val.Int(200)), ("name", Val.Str("defaultapply")), ("params", Val.List()), ("ret", Val.Str("void")))))),
                Val.Map(("id", Val.Int(2)), ("name", Val.Str("TMudPlayer")), ("host", Val.Str("player")), ("members", Val.List()), ("methods", Val.List())))),
            ("events", Val.List(
                Val.Map(("name", Val.Str("onApply")), ("target", Val.Str("item")), ("mode", Val.Str("sync")), ("params", Str("handle:TMudPlayer", "int"))),
                Val.Map(("name", Val.Str("onOwnerChanged")), ("target", Val.Str("object")), ("mode", Val.Str("deferred")), ("params", Str("ref"))))),
            ("wants_tick", Val.False),
            ("limits", Val.Map(("max_list", Val.Int(256)))),
            ("player_classes", Str("mudplayers")));
        Val effect = Val.Map(("k", Val.Str("caller.setprop")), ("name", Val.Str("this.weapon1")), ("value", Val.Str("sword")));
        Val withEffect = Val.Map(
            ("name", Val.Str("onApply")), ("target", Val.Str("item")), ("handle", Val.Handle(Handle(1, 7))), ("type", Val.Str("TMudObject")),
            ("classes", Str("specialitem")), ("args", Val.List(Val.Handle(Handle(1, 9)), Val.Int(3))),
            ("fallback", Val.Map(("k", Val.Str("client.text")), ("player", Val.Str("gs1player")), ("text", Val.Str("You cannot use that.")))));
        Val withInvoke = Val.Map(
            ("name", Val.Str("onApply")), ("target", Val.Str("item")), ("handle", Val.Handle(Handle(1, 8))), ("type", Val.Str("TMudObject")),
            ("classes", Val.List()), ("args", Val.List(Val.Handle(Handle(1, 9)), Val.Int(0))),
            ("fallback", Val.Map(("invoke", Val.Str("defaultapply")), ("handle", Val.Handle(Handle(1, 8))), ("args", Val.List()))));
        return new Dictionary<string, Frame>
        {
            ["HELLO 1.1 from the GameServer (1.1: proto_minor 1)"] = new HelloFrame(Val.Map(
                ("proto_major", Val.Int(1)), ("proto_minor", Val.Int(1)), ("role", Val.Str("gameserver")),
                ("name", Val.Str("Alpaxis GameServer")), ("version", Val.Str("1.1")), ("server_name", Val.Str("Test")),
                ("epoch", Val.Int(1)), ("data_dir", Val.Str("/srv/mud-data/Test")), ("world_dir", Val.Str("/srv/servers/Test")),
                ("max_frame", Val.Int(16777216)), ("call_timeout_ms", Val.Int(500)))),
            ["HELLO 1.1 from the extension (1.1: an item event, a ref parameter, player_classes)"] = new HelloFrame(Val.Map(
                ("proto_major", Val.Int(1)), ("proto_minor", Val.Int(1)), ("role", Val.Str("extension")), ("name", Val.Str("crossfire")),
                ("version", Val.Str("0.2.0")), ("features", Val.List()), ("manifest", manifest))),
            ["REQ 6 (1.1): gmap.layout of a 2 by 2 gmap with an empty cell"] = new ReqFrame(6, 17, Array.Empty<ulong>(), Ctx(),
                new[] { Val.Str("world.gmap"), Val.Int(2), Val.Int(2), Str("a.nw", "b.nw", "c.nw", ""), Val.Int(64) }),
            ["REPLY 6 (1.1): gmap.layout done"] = new ReplyFrame(6, 0, Val.Nil, Array.Empty<Val>(), Array.Empty<Val>()),
            ["REQ 7 (1.1): set member 100 (a ref) to the text gs1player"] = new ReqFrame(7, 10, Array.Empty<ulong>(), Ctx("gs1player", "main.nw"),
                new[] { Val.Handle(Handle(1, 7)), Val.Int(100), Val.Str("gs1player") }),
            ["REPLY 7 (1.1): caller.setprop, an item event with an effect fallback, an item event with an invoke fallback"] =
                new ReplyFrame(7, 0, Val.Nil, new[] { effect }, new[] { withEffect, withInvoke }),
            ["QUERY 2 (1.1): player.pos for gs1player"] = new QueryFrame(2, "player.pos", new[] { Val.Str("gs1player") }),
            ["QRESULT 2 (1.1): main.nw, 12.5, 30.25"] = new QResultFrame(2, 0, Val.List(Val.Str("main.nw"), Val.Float(12.5), Val.Float(30.25))),
            ["QUERY 3 (1.1): player.at for gs1player, index 1"] = new QueryFrame(3, "player.at", new[] { Val.Str("gs1player"), Val.Int(1) }),
            ["QRESULT 3 (1.1): gs2player"] = new QResultFrame(3, 0, Val.Str("gs2player")),
        };
    }

    public static IEnumerable<object[]> ValueLabels() => Values.Keys.Select(k => new object[] { k });

    public static IEnumerable<object[]> FrameTitles() => Frames().Keys.Select(k => new object[] { k });

    [Fact]
    public void ValueRows_Given_the_text_When_compared_with_the_tests_Then_every_row_has_a_value_and_no_value_lacks_a_row()
    {
        // Arrange
        Dictionary<string, byte[]> rows = ValueRows();

        // Act
        string[] inDoc = rows.Keys.Order().ToArray();
        string[] inTest = Values.Keys.Order().ToArray();

        // Assert
        Assert.Equal(inTest, inDoc);
    }

    [Fact]
    public void FrameBlocks_Given_the_text_When_compared_with_the_tests_Then_every_block_has_a_frame_and_no_frame_lacks_a_block()
    {
        // Arrange
        Dictionary<string, byte[]> blocks = FrameBlocks();

        // Act
        string[] inDoc = blocks.Keys.Order().ToArray();
        string[] inTest = Frames().Keys.Order().ToArray();

        // Assert
        Assert.Equal(inTest, inDoc);
    }

    [Theory]
    [MemberData(nameof(ValueLabels))]
    public void Encode_Given_a_value_of_section_13_1_When_encoded_Then_the_bytes_of_the_text_come_out(string label)
    {
        // Arrange
        byte[] expected = ValueRows()[label];

        // Act
        byte[] actual = ValueCodec.Encode(Values[label]);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ValueLabels))]
    public void Decode_Given_the_bytes_of_section_13_1_When_decoded_Then_the_value_of_the_text_comes_out(string label)
    {
        // Arrange
        byte[] bytes = ValueRows()[label];

        // Act
        Val decoded = ValueCodec.Decode(bytes);

        // Assert
        Assert.True(Val.Same(Values[label], decoded), label + " decoded as " + decoded);
    }

    [Theory]
    [MemberData(nameof(FrameTitles))]
    public void Encode_Given_a_frame_of_section_13_2_When_encoded_Then_the_bytes_of_the_text_come_out(string title)
    {
        // Arrange
        byte[] expected = FrameBlocks()[title];

        // Act
        byte[] actual = FrameCodec.Encode(Frames()[title]);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(FrameTitles))]
    public void Decode_Given_a_frame_of_section_13_2_When_decoded_and_encoded_again_Then_the_bytes_are_the_same_and_the_kind_is_right(string title)
    {
        // Arrange
        byte[] bytes = FrameBlocks()[title];
        using var stream = new MemoryStream(bytes);

        // Act
        Frame? frame = FrameCodec.Read(stream, FrameCodec.InitialMaxFrame);

        // Assert
        Assert.NotNull(frame);
        Assert.Equal(Frames()[title].Kind, frame!.Kind);
        Assert.Equal(bytes, FrameCodec.Encode(frame));
    }

    [Fact]
    public void Decode_Given_req_3_When_decoded_Then_its_fields_are_the_ones_of_the_text()
    {
        // Arrange
        using var stream = new MemoryStream(FrameBlocks()["REQ 3: get member 100 of that handle, releasing handle (1, 5)"]);

        // Act
        var req = (ReqFrame)FrameCodec.Read(stream, FrameCodec.InitialMaxFrame)!;

        // Assert
        Assert.Equal(3UL, req.ReqId);
        Assert.Equal(9UL, req.Op);
        Assert.Equal(new[] { Handle(1, 5) }, req.Releases);
        Assert.Equal(Handle(1, 7), req.Args[0].AsHandle);
        Assert.Equal(100L, req.Args[1].AsInt);
        Assert.True(req.Ctx.Get("player")!.IsNil);
    }
}
