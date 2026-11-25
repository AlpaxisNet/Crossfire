// Not ported from a Crossfire file: helpers of the tests. A scripted host: the game server's side of the protocol, written from
// docs/protocol.md, over a real Unix socket to the extension running in this process.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Net.Sockets;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

/// <summary>A folder in the temp folder, removed when it is disposed.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cfx" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>The invented archetypes of the tests: three objects (one of two parts, one with Graal Kingdoms' own keys), 4 blocks.</summary>
internal static class TestWorld
{
    public const string Arc =
        "Object sword\nname sword\nname_pl swords\nface sword.111\ntype 15\nweight 3000\ndam 8\nwc 2\nvalue 50\nend\n"
        + "Object house\nname house\nface house.1\nmsg\nIt is a house.\nWelcome.\nendmsg\nhp 5\nmaxhp 9\nlevel 3\nspeed 0.1\nend\n"
        + "Object house_wing\nface house.2\nx 1\ny 0\nMore\nend\n"
        + "Object goblin\nname Goblin Chief\nface gobchief.131\nmonster 1\nalive 1\ngraalnpctype 1\ngraalnpcparam gob.png\nhpbuff 4\nnopeaceful 1\nend\n";

    public const int Blocks = 4;

    /// <summary>A world folder with an Arch folder (or another name) holding <see cref="Arc"/>.</summary>
    public static TempFolder Make(string archFolder = "Arch", string arc = Arc)
    {
        var world = new TempFolder();
        string dir = System.IO.Path.Combine(world.Path, archFolder, "Test");
        Directory.CreateDirectory(dir);
        File.WriteAllText(System.IO.Path.Combine(dir, "test.arc"), arc);
        return world;
    }
}

internal sealed class ScriptedHost : IDisposable
{
    private readonly TempFolder socketFolder = new();
    private readonly Socket hostSocket;
    private readonly NetworkStream stream;
    private readonly Task<int> extension;
    private ulong nextReqId = 1;

    /// <summary>The frames read so far that were LOG frames (they may come before any REPLY).</summary>
    public List<LogFrame> Logs { get; } = new();

    private ScriptedHost(ExtensionOptions? options)
    {
        string path = System.IO.Path.Combine(socketFolder.Path, "s");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        client.Connect(new UnixDomainSocketEndPoint(path));
        hostSocket = listener.Accept();
        hostSocket.ReceiveTimeout = 10_000;
        stream = new NetworkStream(hostSocket, ownsSocket: true);
        var extensionStream = new NetworkStream(client, ownsSocket: true);
        extension = Task.Run(() =>
        {
            using (extensionStream)
            {
                return ExtensionRunner.Run(extensionStream, options);
            }
        });
    }

    /// <summary>The host's HELLO map of the golden vectors (docs/protocol.md section 13.2) with the given changes.</summary>
    public static Val HostHello(string worldDir, long epoch = 1, long protoMajor = 1, long maxFrame = 16_777_216, params string[] without)
    {
        var pairs = new List<(string, Val)>
        {
            ("proto_major", Val.Int(protoMajor)),
            ("proto_minor", Val.Int(0)),
            ("role", Val.Str("gameserver")),
            ("name", Val.Str("Alpaxis GameServer")),
            ("version", Val.Str("1.0")),
            ("server_name", Val.Str("Test")),
            ("epoch", Val.Int(epoch)),
            ("data_dir", Val.Str(System.IO.Path.Combine(worldDir, "data"))),
            ("world_dir", Val.Str(worldDir)),
            ("max_frame", Val.Int(maxFrame)),
            ("call_timeout_ms", Val.Int(500)),
        };
        return Val.Map(pairs.Where(p => !without.Contains(p.Item1)).ToArray());
    }

    /// <summary>Starts the extension and sends the host's HELLO; the extension's HELLO is not read yet.</summary>
    public static ScriptedHost Start(Val hello, ExtensionOptions? options = null)
    {
        var host = new ScriptedHost(options);
        host.Send(new HelloFrame(hello));
        return host;
    }

    /// <summary>Starts the extension and sends nothing.</summary>
    public static ScriptedHost StartWithoutHello() => new(null);

    /// <summary>Starts the extension, does the handshake and returns the host with the extension's HELLO in <paramref name="extensionHello"/>.</summary>
    public static ScriptedHost Handshake(string worldDir, out Val extensionHello, ExtensionOptions? options = null, long epoch = 1, long maxFrame = 16_777_216)
    {
        ScriptedHost host = Start(HostHello(worldDir, epoch, 1, maxFrame), options);
        extensionHello = ((HelloFrame)host.Read()!).Hello;
        return host;
    }

    public void Send(Frame frame)
    {
        byte[] bytes = FrameCodec.Encode(frame);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    public void SendRaw(byte[] bytes)
    {
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    /// <summary>The next frame, or null at the end of the connection. LOG frames are kept in <see cref="Logs"/> and skipped.</summary>
    public Frame? Read()
    {
        for (; ; )
        {
            Frame? f = FrameCodec.Read(stream, FrameCodec.InitialMaxFrame);
            if (f is LogFrame log)
            {
                Logs.Add(log);
                continue;
            }
            return f;
        }
    }

    public static Val NoCtx() => Val.Map(("player", Val.Nil), ("level", Val.Nil));

    /// <summary>Sends a REQ with the next reqid without reading its REPLY.</summary>
    public ulong SendReq(ulong op, IEnumerable<Val>? args = null, IEnumerable<ulong>? releases = null)
    {
        ulong id = nextReqId++;
        Send(new ReqFrame(id, op, (releases ?? Array.Empty<ulong>()).ToArray(), NoCtx(), (args ?? Array.Empty<Val>()).ToArray()));
        return id;
    }

    public ReplyFrame ReadReply(ulong expectedReqId)
    {
        var reply = (ReplyFrame)Read()!;
        Assert.Equal(expectedReqId, reply.ReqId);
        return reply;
    }

    /// <summary>A REQ and its REPLY.</summary>
    public ReplyFrame Call(ulong op, IEnumerable<Val>? args = null, IEnumerable<ulong>? releases = null) =>
        ReadReply(SendReq(op, args, releases));

    public ReplyFrame Init() => Call(1);

    /// <summary>The host closes its end: the extension reads EOF.</summary>
    public void CloseHostSide() => hostSocket.Shutdown(SocketShutdown.Both);

    /// <summary>The exit status of the extension, which must come within the time the protocol allows (5 s).</summary>
    public int WaitExit()
    {
        Assert.True(extension.Wait(TimeSpan.FromSeconds(5)), "the extension did not exit within 5 s");
        return extension.Result;
    }

    public void Dispose()
    {
        stream.Dispose();
        try
        {
            extension.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }
        socketFolder.Dispose();
    }
}
