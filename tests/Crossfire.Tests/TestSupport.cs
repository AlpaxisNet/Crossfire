namespace Crossfire.Tests;

/// <summary>Not ported from a Crossfire file: helpers of the tests (a world, its log, small archetype texts).</summary>
internal sealed class TestHost : CrossfireHost
{
    public List<string> Calls { get; } = new();
    public HashSet<string>? KnownLists { get; set; }

    public override void FixPlayer(CfObject player) => Calls.Add("fix:" + player.Name);

    public override void RemoveObFromMap(CfObject op) => Calls.Add("removemap:" + op.Name);

    public override CfObject? InsertObInMap(CfObject op, IMapHandle map, CfObject? originator, int flag)
    {
        Calls.Add("insertmap:" + op.Name);
        return op;
    }

    public override bool MapInMemory(IMapHandle map) => true;

    public override CfObject? FindPlayerWithContainer(CfObject container) => PlayerWithContainer;

    public CfObject? PlayerWithContainer { get; set; }

    public override void EsrvSendItem(CfObject player, CfObject op) => Calls.Add("send:" + op.Name);

    public override void EsrvDelItem(IPlayerHandle? player, uint tag) => Calls.Add("del:" + tag);

    public override void EsrvUpdateItem(int flags, CfObject player, CfObject op) => Calls.Add("update:" + flags);

    public override void AddButtonLink(CfObject op, IMapHandle? map, int value) => Calls.Add("button:" + value);

    public override TreasureList? FindTreasureList(string name) =>
        KnownLists == null || KnownLists.Contains(name) ? base.FindTreasureList(name) : null;
}

internal sealed class FakePlayer : IPlayerHandle
{
}

internal sealed class FakeMap : IMapHandle
{
}

internal static class T
{
    /// <summary>An archetype file's first object: init_archetypes looks for it.</summary>
    public const string Empty = "Object empty_archetype\nend\n";

    public static CfWorld World(out List<string> log, CrossfireHost? host = null)
    {
        var l = new List<string>();
        log = l;
        var w = new CfWorld(host, (level, msg) => l.Add(level + ": " + msg));
        // a repeatable RANDOM(): 0, 1, 2, ...
        int n = 0;
        w.Random = () => n++;
        return w;
    }

    public static CfWorld WorldWith(string arc, out List<string> log, CrossfireHost? host = null)
    {
        CfWorld w = World(out log, host);
        w.InitArchetypes(Empty + arc);
        return w;
    }

    public static CfObject Obj(CfWorld w, string name, int weight = 0, uint nrof = 0, byte type = 0)
    {
        CfObject o = w.GetObject();
        o.Name = name;
        o.NamePl = name;
        o.Weight = weight;
        o.Nrof = nrof;
        o.Type = type;
        return o;
    }

    public static string TempDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "cf-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }
}
