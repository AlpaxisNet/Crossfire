// Not ported from a Crossfire file: helpers of the tests of objects in levels, levels and accounts: an invented world, and two ways to
// talk to the extension, which the same tests use: straight to the dispatcher in this process, and over a Unix socket.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

/// <summary>The invented world of these tests: archetypes of every class rule, a few script classes and level seeds.</summary>
internal static class ObjectWorld
{
    public const string Arc =
        "Object human_player\nname human\nface human.171\ntype 1\nalive 1\nhp 10\nmaxhp 12\nlevel 1\nStr 2\nweight 70000\nfood 999\nspeed 1\nend\n"
        + "Object banana\nname banana\nname_pl bananas\nface banana.111\ntype 6\nweight 100\nvalue 5\nfood 20\nend\n"
        + "Object sword\nname sword\nname_pl swords\nface sword.111\ntype 15\nweight 3000\ndam 8\nwc 2\nvalue 50\nend\n"
        + "Object altar\nname altar\nface altar.11\ngraalnpctype 2\ngraalnpcparam p1\nend\n"
        + "Object ghost\nname ghost\nface ghost.11\ngraalnpctype 2\nend\n"
        + "Object rat\nname Rat\nface rat.131\nmonster 1\nalive 1\ngraalnpctype 1\nhp 5\nmaxhp 5\nend\n"
        + "Object wolf\nname Wolf\nface wolf.131\nmonster 1\ngraalnpctype 1\nend\n"
        + "Object housedoor\nname house door\nface door.1\ntype 23\nend\n"
        + "Object bag\nname bag\nface bag.11\ntype 122\nend\n"
        + "Object treasure\nname treasure\nface treasure.1\ntype 4\nend\n"
        + "Object treasurechest\nname chest\nface chest.1\nend\n"
        + "Object nofaced\nname plain\nend\n";

    /// <summary>The script classes of the world (files of its scripts folder).</summary>
    public static readonly string[] Classes = { "special_altar", "monsters_rat", "mudobjects", "other", "special_unrelated" };

    /// <summary>A seed level of three objects: floats, a message, script variables, a nested object, a block of an unknown archetype.</summary>
    public const string SeedHall =
        "arch map\nname hall.nw\nwidth 8\nheight 8\noutdoor 1\nend\n"
        + "arch banana\nx 3.328125\ny 1.125\nnrof 3\nflag born=1524297788.081527948\nflag ismudobject=1\nflag spells=\"magic bullet\",\"cause\"\nend\n"
        + "arch sword\nx 0.5\ny 2\nunpaid 1\nmsg\nA fine sword.\nSecond line.\nendmsg\nend\n"
        + "arch bag\nx 7\ny 7\narch banana\nnrof 2\nend\nend\n"
        + "arch notinthisworld\nx 1\ny 1\nend\n";

    /// <summary>Makes a world folder with the archetypes, the classes and the given seeds (file name and text).</summary>
    public static TempFolder Make(params (string Name, string Text)[] seeds)
    {
        TempFolder world = TestWorld.Make(arc: Arc);
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts"));
        foreach (string c in Classes)
        {
            File.WriteAllText(Path.Combine(world.Path, "scripts", c + ".txt"), "// class\n");
        }
        Directory.CreateDirectory(Path.Combine(world.Path, "mudlevels"));
        foreach ((string name, string text) in seeds)
        {
            File.WriteAllText(Path.Combine(world.Path, "mudlevels", name), text);
        }
        return world;
    }

    /// <summary>A seed level of <paramref name="count"/> bananas.</summary>
    public static string Many(int count)
    {
        var sb = new System.Text.StringBuilder("arch map\nname many.nw\nwidth 64\nheight 64\nend\n");
        for (int i = 0; i < count; i++)
        {
            sb.Append("arch banana\nx ").Append(i % 64).Append("\ny ").Append(i / 64).Append("\nnrof ").Append(i + 1).Append("\nend\n");
        }
        return sb.ToString();
    }

    public static string Data(TempFolder world, string relative) => Path.Combine(world.Path, "data", relative);
}

/// <summary>The extension as a host talks to it: a request and its reply, with the context of the running script.</summary>
internal interface IExt : IDisposable
{
    TempFolder World { get; }

    List<LogFrame> Logs { get; }

    ReplyFrame Call(ulong op, IEnumerable<Val>? args = null, string? level = null, string? player = null);

    /// <summary>The host closes the connection: the extension saves and exits.</summary>
    void End();
}

/// <summary>The extension's dispatcher called in this process, with no socket and no threads of the link.</summary>
internal sealed class DirectExt : IExt
{
    private readonly Dispatcher dispatcher;
    private readonly bool ownsWorld;
    private ulong nextReq = 1;
    private bool ended;

    public DirectExt(TempFolder world, ExtensionOptions? options = null, long epoch = 1, bool ownsWorld = true, long maxFrame = 16_777_216)
    {
        World = world;
        this.ownsWorld = ownsWorld;
        options ??= new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) } };
        HostHello hello = HostHello.Parse(ScriptedHost.HostHello(world.Path, epoch, maxFrame: maxFrame));
        dispatcher = new Dispatcher(hello, (int)maxFrame, (level, text) => Logs.Add(new LogFrame((ulong)level, text)), options, new Counters());
        Assert.Equal(0UL, Call(1).Status);
    }

    public TempFolder World { get; }

    public List<LogFrame> Logs { get; } = new();

    public ReplyFrame Call(ulong op, IEnumerable<Val>? args = null, string? level = null, string? player = null)
    {
        Val ctx = Val.Map(("player", player == null ? Val.Nil : Val.Str(player)), ("level", level == null ? Val.Nil : Val.Str(level)));
        return dispatcher.Handle(new ReqFrame(nextReq++, op, Array.Empty<ulong>(), ctx, (args ?? Array.Empty<Val>()).ToArray()));
    }

    public void End()
    {
        if (!ended)
        {
            ended = true;
            dispatcher.Finish(true);
            dispatcher.Release();
        }
    }

    public void Dispose()
    {
        End();
        if (ownsWorld)
        {
            World.Dispose();
        }
    }
}

/// <summary>The extension over a Unix socket (the scripted host of the other tests), in this process.</summary>
internal sealed class SocketExt : IExt
{
    private readonly ScriptedHost host;
    private readonly bool ownsWorld;
    private bool ended;

    public SocketExt(TempFolder world, ExtensionOptions? options = null, long epoch = 1, bool ownsWorld = true, long maxFrame = 16_777_216)
    {
        World = world;
        this.ownsWorld = ownsWorld;
        options ??= new ExtensionOptions { Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) } };
        host = ScriptedHost.Handshake(world.Path, out _, options, epoch, maxFrame);
        Assert.Equal(0UL, host.Init().Status);
    }

    public TempFolder World { get; }

    public List<LogFrame> Logs => host.Logs;

    public ReplyFrame Call(ulong op, IEnumerable<Val>? args = null, string? level = null, string? player = null) =>
        host.Call(op, args, null, level, player);

    public void End()
    {
        if (!ended)
        {
            ended = true;
            host.CloseHostSide();
            Assert.Equal(0, host.WaitExit());
        }
    }

    public void Dispose()
    {
        End();
        host.Dispose();
        if (ownsWorld)
        {
            World.Dispose();
        }
    }
}

/// <summary>Reading what a reply says.</summary>
internal static class Say
{
    public static Val S(string s) => Val.Str(s);

    public static Val N(double d) => Val.Float(d);

    public static Val H(ulong h) => Val.Handle(h);

    public static int Obj(string member) => TMudObjectMembers.Table.FindByName(member)?.Id ?? throw new ArgumentException(member);

    public static int Ply(string member) => TMudPlayerMembers.Table.FindByName(member)?.Id ?? throw new ArgumentException(member);

    public static int Arch(string member) => TMudArchMembers.Members.Single(m => m.Name == member).Id;

    public static ulong Ok(ReplyFrame r)
    {
        Assert.Equal(0UL, r.Status);
        return r.Result.Kind == ValKind.Handle ? r.Result.AsHandle : 0;
    }

    public static IReadOnlyList<Val> Effects(ReplyFrame r, string k) => r.Effects.Where(e => e.Get("k")!.AsString == k).ToList();

    public static ulong ArchHandle(IExt ext, string name)
    {
        ReplyFrame r = ext.Call(Manifest.OpFindArchetype, new[] { S(name) });
        Assert.Equal(0UL, r.Status);
        return r.Result.AsHandle;
    }

    /// <summary>Loads a level page by page; returns every npc.place effect and the pages' sizes.</summary>
    public static (List<Val> Places, List<int> Pages) LoadAll(IExt ext, string level)
    {
        var places = new List<Val>();
        var pages = new List<int>();
        Val cursor = Val.Nil;
        for (int i = 0; i < 100; i++)
        {
            ReplyFrame r = ext.Call(7, new[] { S(level), cursor }, level: level);
            Assert.Equal(0UL, r.Status);
            Assert.All(r.Effects, e => Assert.Equal("npc.place", e.Get("k")!.AsString));
            places.AddRange(r.Effects);
            pages.Add(r.Effects.Count);
            if (!r.Result.Get("more")!.AsBool)
            {
                Assert.True(r.Result.Get("cursor")!.IsNil);
                return (places, pages);
            }
            cursor = r.Result.Get("cursor")!;
        }
        throw new InvalidOperationException("a level.load that does not end");
    }

    public static Val Get(IExt ext, ulong handle, int member)
    {
        ReplyFrame r = ext.Call(9, new[] { H(handle), Val.Int(member) });
        Assert.Equal(0UL, r.Status);
        return r.Result;
    }

    public static ReplyFrame Set(IExt ext, ulong handle, int member, Val value) => ext.Call(10, new[] { H(handle), Val.Int(member), value });

    /// <summary>Waits until a file exists under the data folder (the writer writes within a moment).</summary>
    public static string WaitFile(TempFolder world, string relative, int milliseconds = 5000)
    {
        string path = ObjectWorld.Data(world, relative);
        for (int i = 0; i < milliseconds / 10; i++)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path, System.Text.Encoding.Latin1);
            }
            Thread.Sleep(10);
        }
        throw new FileNotFoundException(path);
    }

    /// <summary>Asks for a flush and waits until the file has the text.</summary>
    public static string Flushed(IExt ext, string relative, Func<string, bool>? ready = null)
    {
        Assert.Equal(0UL, ext.Call(3).Status);
        for (int i = 0; i < 500; i++)
        {
            string path = ObjectWorld.Data(ext.World, relative);
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path, System.Text.Encoding.Latin1);
                if (ready == null || ready(text))
                {
                    return text;
                }
            }
            Thread.Sleep(10);
        }
        throw new FileNotFoundException(relative);
    }
}
