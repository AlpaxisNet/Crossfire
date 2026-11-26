// Not ported from a Crossfire file: the registration point of the Crossfire extension's manifest (docs/protocol.md section 6) and the
// id ranges of the lanes that fill it (docs/extension.md, "Lanes and id ranges").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Text.RegularExpressions;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>
/// A lane of the work on the extension (a part of it that one agent owns) and the ids it may use: the ids of its functions (the
/// operation numbers), of its types, and of the members and methods of every type (those ids are unique across the whole manifest).
/// A lane's block of member ids is split by who the members belong to: the first 500 for <c>TMudObject</c>, the next 250 for
/// <c>TMudPlayer</c>, the last 250 for the lane's own new types.
/// </summary>
internal sealed record Lane(string Name, int FirstFunction, int LastFunction, int FirstType, int LastType, int FirstMember, int LastMember)
{
    /// <summary>What the first waves built: functions 64 to 999 (64 to 67 are used), types 1 to 9 (1 to 3), members and methods 1 to 999.</summary>
    public static readonly Lane Core = new("core", 64, 999, 1, 9, 1, 999);

    /// <summary>4C: items, strings, inventory members.</summary>
    public static readonly Lane Items = new("4C", 1000, 1999, 10, 19, 1000, 1999);

    /// <summary>4D: the start path (accounts, class, stats).</summary>
    public static readonly Lane Start = new("4D", 2000, 2999, 20, 29, 2000, 2999);

    /// <summary>4G: gmaps, if any.</summary>
    public static readonly Lane Gmaps = new("4G", 3000, 3999, 30, 39, 3000, 3999);

    /// <summary>5A of wave 5.</summary>
    public static readonly Lane Wave5A = new("5A", 4000, 4999, 40, 49, 4000, 4999);

    /// <summary>5B of wave 5.</summary>
    public static readonly Lane Wave5B = new("5B", 5000, 5999, 50, 59, 5000, 5999);

    /// <summary>5C of wave 5.</summary>
    public static readonly Lane Wave5C = new("5C", 6000, 6999, 60, 69, 6000, 6999);

    /// <summary>5D of wave 5.</summary>
    public static readonly Lane Wave5D = new("5D", 7000, 7999, 70, 79, 7000, 7999);

    /// <summary>Every lane, the core first.</summary>
    public static readonly IReadOnlyList<Lane> All = new[] { Core, Items, Start, Gmaps, Wave5A, Wave5B, Wave5C, Wave5D };

    /// <summary>The first member id of the lane's members of <c>TMudObject</c> (500 ids).</summary>
    public int ObjectMembersFirst => FirstMember;

    /// <summary>The last member id of the lane's members and methods of <c>TMudObject</c>.</summary>
    public int ObjectMembersLast => FirstMember + 499;

    /// <summary>The first member id of the lane's members of <c>TMudPlayer</c> (250 ids).</summary>
    public int PlayerMembersFirst => FirstMember + 500;

    /// <summary>The last member id of the lane's members and methods of <c>TMudPlayer</c>.</summary>
    public int PlayerMembersLast => FirstMember + 749;

    /// <summary>The first member id of the lane's own new types (250 ids).</summary>
    public int OwnTypesMembersFirst => FirstMember + 750;

    public bool OwnsFunction(long id) => id >= FirstFunction && id <= LastFunction;

    public bool OwnsType(long id) => id >= FirstType && id <= LastType;

    public bool OwnsMember(long id) => id >= FirstMember && id <= LastMember;

    /// <summary>The lane that owns a member or method id, or null.</summary>
    public static Lane? OfMember(long id) => All.FirstOrDefault(l => l.OwnsMember(id));
}

/// <summary>
/// A function of the manifest. <paramref name="Handler"/> is what the dispatcher runs for the operation <paramref name="Id"/>
/// (a lane's function; the core's four are dispatched by the switch of the dispatcher and have none); <paramref name="Slow"/> is the
/// manifest's <c>slow</c> (the host allows it a longer deadline) and also makes the dispatcher answer <c>busy</c>, before the handler
/// runs, while <see cref="Dispatcher.Busy"/> is set (docs/mud-phase3.md section 3, rule 10); <paramref name="MinMinor"/> is the link's
/// minor version the function needs (below it the function is not in the manifest and its operation is <c>unknown_op</c>).
/// </summary>
internal sealed record FunctionDef(int Id, string Name, string[] Params, string Ret, bool Slow = false, Func<Dispatcher, IReadOnlyList<Val>, Outcome>? Handler = null, int MinMinor = 0);

/// <summary>A type of the manifest as the manifest map lists it (<c>MemberTable.ToManifest</c> makes one).</summary>
internal sealed record TypeDef(int Id, string Name, Val Map, int MinMinor = 0);

/// <summary>
/// An event of the manifest. A target <c>item</c> is a 1.1 target (<paramref name="MinMinor"/> is then at least 1); <paramref name="Params"/>
/// is a 1.1 key (the host reads it by the 1.0 rules below 1.1, so it is left out there).
/// </summary>
internal sealed record EventDef(string Name, string Target, string Mode = "sync", string[]? Params = null, int MinMinor = 0);

/// <summary>
/// Collects the manifest of one link (protocol section 6). The core and every lane register through it; it refuses (an
/// <see cref="InvalidOperationException"/>: a fault of the program, found by the first test that builds the manifest) an id outside
/// the lane's range, a duplicate id or name, a type name that is not one, and a 1.1 feature on a link below 1.1 that is not marked as
/// one. What needs a higher minor version than the link has is left out of the manifest and of the dispatcher (section 4.1).
/// </summary>
internal sealed class ManifestBuilder
{
    private static readonly Regex Name = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly HashSet<string> Scalar = new(StringComparer.Ordinal) { "int", "float", "bool", "string", "any", "void", "ref" };

    private readonly List<FunctionDef> functions = new();
    private readonly List<TypeDef> types = new();
    private readonly List<EventDef> events = new();
    private readonly List<string> playerClasses = new();
    private readonly Dictionary<int, FunctionDef> functionById = new();
    private readonly HashSet<string> functionNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> typeIds = new();
    private readonly HashSet<string> typeNames = new(StringComparer.Ordinal);
    private readonly HashSet<long> memberIds = new();
    private readonly Dictionary<string, EventDef> eventByName = new(StringComparer.Ordinal);

    public ManifestBuilder(int linkMinor)
    {
        LinkMinor = linkMinor;
    }

    /// <summary>The link's minor version: what a lane may register depends on it.</summary>
    public int LinkMinor { get; }

    /// <summary>True when the link speaks 1.1 or later.</summary>
    public bool Speaks11 => LinkMinor >= 1;

    /// <summary>The functions that are in this link's manifest, in the order they were registered.</summary>
    public IReadOnlyList<FunctionDef> Functions => functions;

    /// <summary>The function of an operation number that is in this link's manifest, or null.</summary>
    public FunctionDef? FindFunction(long op) => op >= int.MinValue && op <= int.MaxValue && functionById.TryGetValue((int)op, out FunctionDef? f) ? f : null;

    /// <summary>The names of the types that are in this link's manifest.</summary>
    public IReadOnlyCollection<string> TypeNames => typeNames;

    /// <summary>Every event a lane registered, also one that this link's minor version leaves out of the manifest (<see cref="EventDef.MinMinor"/>).</summary>
    public EventDef? FindEvent(string name) => eventByName.TryGetValue(name, out EventDef? e) ? e : null;

    /// <summary>Registers a function; ignored when the link's minor version is below <see cref="FunctionDef.MinMinor"/>.</summary>
    public void AddFunction(Lane lane, FunctionDef f)
    {
        if (!lane.OwnsFunction(f.Id))
        {
            throw new InvalidOperationException("function " + f.Name + ": id " + f.Id + " is outside the range of lane " + lane.Name);
        }
        if (functionById.ContainsKey(f.Id) || !functionNames.Add(f.Name))
        {
            throw new InvalidOperationException("function " + f.Name + " (" + f.Id + "): id or name already registered");
        }
        CheckName(f.Name);
        foreach (string p in f.Params.Append(f.Ret))
        {
            CheckTypeSyntax(p, f.MinMinor);
        }
        if (LinkMinor < f.MinMinor)
        {
            functionNames.Remove(f.Name);
            return;
        }
        functions.Add(f);
        functionById[f.Id] = f;
    }

    /// <summary>Registers a type with its members and methods; ignored when the link's minor version is below <see cref="TypeDef.MinMinor"/>.</summary>
    public void AddType(Lane lane, TypeDef t)
    {
        if (!lane.OwnsType(t.Id))
        {
            throw new InvalidOperationException("type " + t.Name + ": id " + t.Id + " is outside the range of lane " + lane.Name);
        }
        if (t.Map.Get("id")?.AsNumberToInt() != t.Id || t.Map.Get("name") is not { Kind: ValKind.String } mapName || mapName.AsString != t.Name)
        {
            throw new InvalidOperationException("type " + t.Name + ": the id or name of its map differs from the registered ones");
        }
        if (!typeIds.Add(t.Id) || !typeNames.Add(t.Name))
        {
            throw new InvalidOperationException("type " + t.Name + " (" + t.Id + "): id or name already registered");
        }
        CheckName(t.Name);
        var ids = new List<long>();
        foreach (Val m in t.Map.Get("members")!.Items.Concat(t.Map.Get("methods")!.Items))
        {
            long id = m.Get("id")!.AsInt;
            // a type of the core (TMudObject, TMudPlayer) also holds the members and methods that the lanes add to it, each in its own range
            if (!lane.OwnsMember(id) && !(lane == Lane.Core && Lane.OfMember(id) != null))
            {
                throw new InvalidOperationException("type " + t.Name + ": member or method id " + id + " is outside the range of lane " + lane.Name);
            }
            if (memberIds.Contains(id) || ids.Contains(id))
            {
                throw new InvalidOperationException("type " + t.Name + ": member or method id " + id + " is used twice");
            }
            ids.Add(id);
            if (m.Get("type") is { } type)
            {
                CheckTypeSyntax(type.AsString, t.MinMinor);
            }
            foreach (Val p in m.Get("params")?.Items ?? Array.Empty<Val>())
            {
                CheckTypeSyntax(p.AsString, t.MinMinor);
            }
            if (m.Get("ret") is { } ret)
            {
                CheckTypeSyntax(ret.AsString, t.MinMinor);
            }
        }
        if (LinkMinor < t.MinMinor)
        {
            typeIds.Remove(t.Id);
            typeNames.Remove(t.Name);
            return;
        }
        foreach (long id in ids)
        {
            memberIds.Add(id);
        }
        types.Add(t);
    }

    /// <summary>Registers an event; one whose target is <c>item</c> needs a link of 1.1 (it is left out of the manifest below that).</summary>
    public void AddEvent(Lane lane, EventDef e)
    {
        CheckName(e.Name);
        if (e.Target is not ("object" or "player" or "global" or "item") || e.Mode is not ("sync" or "deferred"))
        {
            throw new InvalidOperationException("event " + e.Name + ": target " + e.Target + " or mode " + e.Mode + " is not one of the protocol's");
        }
        if (eventByName.ContainsKey(e.Name))
        {
            throw new InvalidOperationException("event " + e.Name + " is already registered");
        }
        if (e.Target == "item" && e.MinMinor < 1)
        {
            e = e with { MinMinor = 1 };
        }
        foreach (string p in e.Params ?? Array.Empty<string>())
        {
            CheckTypeSyntax(p, Math.Max(1, e.MinMinor));
        }
        eventByName[e.Name] = e;
        if (LinkMinor >= e.MinMinor)
        {
            events.Add(e);
        }
    }

    /// <summary>Adds a script class that the host joins to every player it binds (1.1: ignored below it).</summary>
    public void AddPlayerClass(string name)
    {
        CheckName(name);
        if (Speaks11 && !playerClasses.Contains(name))
        {
            playerClasses.Add(name);
        }
    }

    /// <summary>The manifest map of section 6 for this link.</summary>
    public Val ToVal(bool wantsTick = false)
    {
        foreach (TypeDef t in types)
        {
            foreach (Val m in t.Map.Get("members")!.Items.Concat(t.Map.Get("methods")!.Items))
            {
                CheckReferences(m.Get("type")?.AsString);
                foreach (Val p in m.Get("params")?.Items ?? Array.Empty<Val>())
                {
                    CheckReferences(p.AsString);
                }
                CheckReferences(m.Get("ret")?.AsString);
            }
        }
        foreach (FunctionDef f in functions)
        {
            foreach (string p in f.Params.Append(f.Ret))
            {
                CheckReferences(p);
            }
        }
        var pairs = new List<(string, Val)>
        {
            ("functions", Val.List(functions.Select(f => Val.Map(
                ("id", Val.Int(f.Id)),
                ("name", Val.Str(f.Name)),
                ("params", Val.List(f.Params.Select(Val.Str))),
                ("ret", Val.Str(f.Ret)),
                ("slow", Val.Bool(f.Slow)))))),
            ("types", Val.List(types.Select(t => t.Map))),
            ("events", Val.List(events.Select(EventVal))),
            ("wants_tick", Val.Bool(wantsTick)),
            ("limits", Val.Map(("max_list", Val.Int(256)))),
        };
        if (playerClasses.Count > 0)
        {
            pairs.Add(("player_classes", Val.List(playerClasses.Select(Val.Str))));
        }
        return Val.Map(pairs.ToArray());
    }

    private Val EventVal(EventDef e) => e.Params != null && Speaks11
        ? Val.Map(("name", Val.Str(e.Name)), ("target", Val.Str(e.Target)), ("mode", Val.Str(e.Mode)), ("params", Val.List(e.Params.Select(Val.Str))))
        : Val.Map(("name", Val.Str(e.Name)), ("target", Val.Str(e.Target)), ("mode", Val.Str(e.Mode)));

    private static void CheckName(string name)
    {
        if (!Name.IsMatch(name) || name.Length > 64)
        {
            throw new InvalidOperationException("the name " + name + " is not a name of the manifest");
        }
    }

    /// <summary>A type name's syntax (section 6); <c>ref</c> is a 1.1 name, so whoever uses it must say <paramref name="minMinor"/> 1.</summary>
    private static void CheckTypeSyntax(string type, int minMinor)
    {
        if (type.StartsWith("list:", StringComparison.Ordinal))
        {
            if (type == "list:void")
            {
                throw new InvalidOperationException("list:void is not a type name");
            }
            CheckTypeSyntax(type.Substring(5), minMinor);
        }
        else if (type.StartsWith("handle:", StringComparison.Ordinal))
        {
            CheckName(type.Substring(7));
        }
        else if (type.StartsWith("ref:", StringComparison.Ordinal))
        {
            CheckName(type.Substring(4));
            RequireOneOne(type, minMinor);
        }
        else if (type == "list" || Scalar.Contains(type))
        {
            if (type == "ref")
            {
                RequireOneOne(type, minMinor);
            }
        }
        else
        {
            throw new InvalidOperationException("the type name " + type + " is not one of the protocol's");
        }
    }

    private static void RequireOneOne(string type, int minMinor)
    {
        if (minMinor < 1)
        {
            throw new InvalidOperationException("the type name " + type + " is a 1.1 name: register its function, type or event with MinMinor 1");
        }
    }

    private void CheckReferences(string? type)
    {
        while (type != null && type.StartsWith("list:", StringComparison.Ordinal))
        {
            type = type.Substring(5);
        }
        if (type != null && (type.StartsWith("handle:", StringComparison.Ordinal) || type.StartsWith("ref:", StringComparison.Ordinal)))
        {
            string target = type.Substring(type.IndexOf(':') + 1);
            if (!typeNames.Contains(target))
            {
                throw new InvalidOperationException("the type name " + type + " names a type that is not in the manifest");
            }
        }
    }
}
