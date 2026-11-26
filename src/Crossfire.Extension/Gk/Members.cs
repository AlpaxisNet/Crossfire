// Not ported from a Crossfire file: the way the Crossfire extension describes the members of its script types (the types of the
// manifest, docs/protocol.md section 6) and reads and writes them on its objects.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Gk;

/// <summary>What a member needs from the extension besides its target: the world, and the handles of objects it names.</summary>
internal interface IMemberEnv
{
    CfWorld World { get; }

    /// <summary>The handle of an archetype, a level object or a player (a tag is given out at the first call).</summary>
    ulong HandleOf(object target);

    /// <summary>The tag of an object, the low half of its handle.</summary>
    uint TagOf(object target);

    /// <summary>The handle of an object of an inventory (or one inside an object that holds objects): its tag is given at the first call (lane 4C).</summary>
    ulong ItemHandleOf(CfObject item);

    /// <summary>The object (or player, or archetype) a handle names, null for a dead or unknown one.</summary>
    object? Resolve(ulong handle);

    /// <summary>The handle the extension gave an online player (null for an account that is not logged in).</summary>
    ulong? PlayerHandleOf(string account);

    /// <summary>The object an item handle names, found inside the tree of <paramref name="player"/> only (null for any other handle).</summary>
    CfObject? ItemOfPlayer(PlayerState player, ulong handle);

    /// <summary>The in-memory owner of an object that is not an account (a level object).</summary>
    object? OwnerOf(CfObject item);

    /// <summary>Sets the in-memory owner of an object (null clears it).</summary>
    void SetOwnerOf(CfObject item, object? owner);
}

/// <summary>The outcome of a write: the value that was stored (it may differ from the one given: a number is clamped), or why it was refused.</summary>
internal readonly record struct SetResult(Val? Stored, string? Error)
{
    public static SetResult Ok(Val stored) => new(stored, null);

    public static SetResult Fail(string why) => new(null, why);
}

/// <summary>One member of a script type of the manifest, for targets of type <typeparamref name="T"/>.</summary>
internal sealed class ObjMember<T>
{
    public ObjMember(int id, string name, string typeName, bool writable, bool stable, Func<IMemberEnv, T, Val> read, Func<IMemberEnv, T, Val, SetResult>? write, string? clientName = null, int minMinor = 0, Func<IMemberEnv, T, IReadOnlyList<CfObject>>? items = null)
    {
        MinMinor = minMinor;
        Items = items;
        Id = id;
        Name = name;
        TypeName = typeName;
        Writable = writable;
        Stable = stable;
        Read = read;
        Write = write;
        ClientName = clientName;
    }

    public int Id { get; }

    public string Name { get; }

    public string TypeName { get; }

    public bool Writable { get; }

    public bool Stable { get; }

    public Func<IMemberEnv, T, Val> Read { get; }

    public Func<IMemberEnv, T, Val, SetResult>? Write { get; }

    /// <summary>For a member of a player: the client string (the whole flag name) that shows its value to the client, or null.</summary>
    public string? ClientName { get; }

    /// <summary>The protocol minor version the member needs: a member of 1 (the type <c>ref</c>) is not in the manifest of a 1.0 link.</summary>
    public int MinMinor { get; }

    /// <summary>For a member of a type <c>list:handle:T</c>: the objects of the list, so that <c>get_list</c> can give rows (null for any other member).</summary>
    public Func<IMemberEnv, T, IReadOnlyList<CfObject>>? Items { get; }
}

/// <summary>One method of a script type.</summary>
internal sealed record MethodDef(int Id, string Name, string[] Params, string Ret);

/// <summary>A script type of the manifest and its members.</summary>
internal sealed class MemberTable<T>
{
    private readonly Dictionary<int, ObjMember<T>> byId = new();

    public MemberTable(int typeId, string typeName, string host, IReadOnlyList<ObjMember<T>> members, IReadOnlyList<MethodDef> methods)
    {
        TypeId = typeId;
        TypeName = typeName;
        Host = host;
        Members = members;
        Methods = methods;
        foreach (ObjMember<T> m in members)
        {
            byId[m.Id] = m;
        }
    }

    public int TypeId { get; }

    public string TypeName { get; }

    public string Host { get; }

    public IReadOnlyList<ObjMember<T>> Members { get; }

    public IReadOnlyList<MethodDef> Methods { get; }

    public ObjMember<T>? Find(long id) => id >= int.MinValue && id <= int.MaxValue && byId.TryGetValue((int)id, out ObjMember<T>? m) ? m : null;

    public ObjMember<T>? FindByName(string name) => Members.FirstOrDefault(m => m.Name == name);

    public MethodDef? FindMethod(long id) => Methods.FirstOrDefault(m => m.Id == id);

    /// <summary>The type as the manifest lists it (section 6).</summary>
    /// <summary>The type as the manifest map lists it; for a link below <c>MinMinor</c> of a member (a <c>ref</c>) the member is left out.</summary>
    public Val ToManifest(int linkMinor = int.MaxValue) => Val.Map(
        ("id", Val.Int(TypeId)),
        ("name", Val.Str(TypeName)),
        ("host", Val.Str(Host)),
        ("members", Val.List(Members.Where(m => m.MinMinor <= linkMinor).Select(m => Val.Map(
            ("id", Val.Int(m.Id)),
            ("name", Val.Str(m.Name)),
            ("type", Val.Str(m.TypeName)),
            ("access", Val.Str(m.Writable ? "rw" : "r")),
            ("stable", Val.Bool(m.Stable)))))),
        ("methods", Val.List(Methods.Select(m => Val.Map(
            ("id", Val.Int(m.Id)),
            ("name", Val.Str(m.Name)),
            ("params", Val.List(m.Params.Select(Val.Str))),
            ("ret", Val.Str(m.Ret)))))));
}

/// <summary>
/// Makes the members that are plain fields of a Crossfire object (the same for a level object and a player's object): an integer
/// field with its range, a text, a flag, and the lines Graal Kingdoms' loader keeps in <see cref="CfObject.Extra"/>.
/// </summary>
internal sealed class FieldBuilder<T>
{
    private readonly List<ObjMember<T>> list = new();
    private readonly Func<T, CfObject> of;
    private int next;
    private int last = int.MaxValue;

    public FieldBuilder(int firstId, Func<T, CfObject> of)
    {
        next = firstId;
        this.of = of;
    }

    public List<ObjMember<T>> Members => list;

    public int NextId => next;

    /// <summary>
    /// Continues the numbering at <paramref name="first"/> and refuses an id above <paramref name="lastId"/>: a lane adds its members
    /// with the range its <see cref="Lane"/> gives for this type (<see cref="Lane.ObjectMembersFirst"/> and the like).
    /// </summary>
    public void UseRange(int first, int lastId)
    {
        next = first;
        last = lastId;
    }

    /// <summary>The next id for a method of the type (methods and members share the numbering).</summary>
    public int NextMethodId() => TakeId();

    private int TakeId() => next <= last ? next++ : throw new InvalidOperationException("the lane's range of member ids is used up");

    public void Add(string name, string type, bool writable, bool stable, Func<IMemberEnv, T, Val> read, Func<IMemberEnv, T, Val, SetResult>? write, string? client = null, int minMinor = 0, Func<IMemberEnv, T, IReadOnlyList<CfObject>>? items = null) =>
        list.Add(new ObjMember<T>(TakeId(), name, type, writable, stable, read, write, client, minMinor, items));

    /// <summary>A number field: <paramref name="min"/> and <paramref name="max"/> are the range of the C type (a value outside is clamped).</summary>
    public void Int(string name, Func<CfObject, long> get, Action<CfObject, long>? set, long min, long max, bool stable, string? client = null) =>
        Add(name, "int", set != null, stable, (_, t) => Val.Int(get(of(t))), set == null ? null : (_, t, v) =>
        {
            long n;
            if (v.IsNil)
            {
                n = 0;
            }
            else if (v.AsNumberToInt() is { } given)
            {
                n = given;
            }
            else
            {
                return SetResult.Fail("the member takes a number");
            }
            CfObject ob = of(t);
            set(ob, Math.Clamp(n, min, max));
            return SetResult.Ok(Val.Int(get(ob)));
        }, client);

    /// <summary>A field that is a float in C.</summary>
    public void Float(string name, Func<CfObject, double> get, Action<CfObject, double>? set, bool stable, string? client = null) =>
        Add(name, "float", set != null, stable, (_, t) => Val.Float(get(of(t))), set == null ? null : (_, t, v) =>
        {
            double d;
            if (v.IsNil)
            {
                d = 0;
            }
            else if (v.Kind == ValKind.Int)
            {
                d = v.AsInt;
            }
            else if (v.Kind == ValKind.Float)
            {
                d = double.IsNaN(v.AsFloat) ? 0 : v.AsFloat;
            }
            else
            {
                return SetResult.Fail("the member takes a number");
            }
            CfObject ob = of(t);
            set(ob, d);
            return SetResult.Ok(Val.Float(get(ob)));
        }, client);

    /// <summary>Why a text with a line break is refused: the object is saved as lines, and a break in a value would write lines of its own into the file.</summary>
    public const string LineBreakRefused = "the member takes a text on one line (no newline or carriage return)";

    /// <summary>Whether a text holds a newline or a carriage return.</summary>
    public static bool HasLineBreak(string text) => text.AsSpan().IndexOfAny('\n', '\r') >= 0;

    /// <summary>A text field (the text of a file holds the bytes of UTF-8, one char per byte: <see cref="FileText"/>).</summary>
    public void Str(string name, Func<CfObject, string?> get, Action<CfObject, string>? set, bool stable, string? client = null) =>
        Add(name, "string", set != null, stable, (_, t) => Val.Str(FileText.FromBytesText(get(of(t)) ?? "")), set == null ? null : (_, t, v) =>
        {
            if (!v.IsNil && v.Kind != ValKind.String)
            {
                return SetResult.Fail("the member takes a string");
            }
            if (HasLineBreak(v.AsString))
            {
                return SetResult.Fail(LineBreakRefused);
            }
            CfObject ob = of(t);
            set(ob, FileText.ToBytesText(v.AsString));
            return SetResult.Ok(Val.Str(FileText.FromBytesText(get(ob) ?? "")));
        }, client);

    /// <summary>A flag of the object (<c>QUERY_FLAG</c>).</summary>
    public void Flag(string name, int flag, bool writable, bool stable) =>
        Bool(name, ob => ob.QueryFlag(flag), writable ? (ob, b) => ob.SetOrClearFlag(flag, b ? 1 : 0) : null, stable);

    /// <summary>A bool member.</summary>
    public void Bool(string name, Func<CfObject, bool> get, Action<CfObject, bool>? set, bool stable) =>
        Add(name, "bool", set != null, stable, (_, t) => Val.Bool(get(of(t))), set == null ? null : (_, t, v) =>
        {
            if (!v.IsNil && !v.IsBool)
            {
                return SetResult.Fail("the member takes a bool");
            }
            CfObject ob = of(t);
            set(ob, v.AsBool);
            return SetResult.Ok(Val.Bool(get(ob)));
        });

    /// <summary>A number kept as a line of the object's extra keys (Graal Kingdoms' own keys); <paramref name="absent"/> is what a read gives when there is no line.</summary>
    public void ExtraInt(string name, string key, long absent, bool writable, bool stable, long min = int.MinValue, long max = int.MaxValue, string? client = null) =>
        Int(name, ob => ob.Extra?.Get(key) is { } t ? CStr.Atoi(t) : absent, writable ? (ob, n) => (ob.Extra ??= new ExtraKeys()).Set(key, n.ToString(CultureInfo.InvariantCulture)) : null, min, max, stable, client);

    /// <summary>A text kept as an extra line.</summary>
    public void ExtraStr(string name, string key, bool writable, bool stable) =>
        Str(name, ob => ob.Extra?.Get(key), writable ? (ob, s) =>
        {
            if (s.Length == 0)
            {
                ob.Extra?.Remove(key);
            }
            else
            {
                (ob.Extra ??= new ExtraKeys()).Set(key, s);
            }
        }
        : null, stable);

    /// <summary>A bool kept as an extra line (1 or 0).</summary>
    public void ExtraBool(string name, string key, bool writable, bool stable) =>
        Bool(name, ob => ob.Extra?.Get(key) is { } t && CStr.Atoi(t) != 0, writable ? (ob, b) => (ob.Extra ??= new ExtraKeys()).Set(key, b ? "1" : "0") : null, stable);

    /// <summary>The plain fields that a level object and a player's object share.</summary>
    public void SharedLife(bool stable)
    {
        Int("mudlevel", o => o.Level, (o, n) => o.Level = (short)n, short.MinValue, short.MaxValue, false, "clientr.mud_level");
        Int("mudhp", o => o.Stats.Hp, (o, n) => o.Stats.Hp = (short)n, short.MinValue, short.MaxValue, false, "clientr.mud_hp");
        Int("mudmaxhp", o => o.Stats.Maxhp, (o, n) => o.Stats.Maxhp = (short)n, short.MinValue, short.MaxValue, false, "clientr.mud_maxhp");
        Int("mudsp", o => o.Stats.Sp, (o, n) => o.Stats.Sp = (short)n, short.MinValue, short.MaxValue, false, "clientr.mud_sp");
        Int("mudmaxsp", o => o.Stats.Maxsp, (o, n) => o.Stats.Maxsp = (short)n, short.MinValue, short.MaxValue, false, "clientr.mud_maxsp");
        Int("mudgrace", o => o.Stats.Grace, (o, n) => o.Stats.Grace = (short)n, short.MinValue, short.MaxValue, false, "clientr.mud_grace");
        Int("mudmaxgrace", o => o.Stats.Maxgrace, (o, n) => o.Stats.Maxgrace = (short)n, short.MinValue, short.MaxValue, false, "clientr.mud_maxgrace");
        Int("mudexp", o => o.Stats.Exp, (o, n) => o.Stats.Exp = (int)n, int.MinValue, int.MaxValue, false, "clientr.mud_exp");
        Int("mudfood", o => o.Stats.Food, (o, n) => o.Stats.Food = (short)n, short.MinValue, short.MaxValue, false, "clientr.mud_food");
        Int("mudac", o => o.Stats.Ac, (o, n) => o.Stats.Ac = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, stable, "clientr.mud_ac");
        Int("mudwc", o => o.Stats.Wc, (o, n) => o.Stats.Wc = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, stable, "clientr.mud_wc");
        Int("muddamage", o => o.Stats.Dam, (o, n) => o.Stats.Dam = (short)n, short.MinValue, short.MaxValue, stable, "clientr.mud_dam");
        Int("mudattacktype", o => o.Attacktype, (o, n) => o.Attacktype = unchecked((uint)n), 0, uint.MaxValue, stable);
        Float("mudspeed", o => double.Parse(o.Speed.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), (o, d) => o.Speed = (float)d, false, "clientr.mud_speed");
        Int("mudweight", o => o.Weight, null, 0, 0, false); // the player's string clientr.mud_weight is written by the flush (Dispatcher.Items.cs)
        Int("mudcarrying", o => o.Carrying, null, 0, 0, false);
        Int("mudmagic", o => o.Magic, (o, n) => o.Magic = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, false);
        Int("mudluck", o => o.Stats.Luck, (o, n) => o.Stats.Luck = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, stable, "clientr.mud_luck");
        Int("mudstrength", o => o.Stats.Str, (o, n) => o.Stats.Str = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, stable, "clientr.mud_str");
        Int("muddexterity", o => o.Stats.Dex, (o, n) => o.Stats.Dex = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, stable, "clientr.mud_dex");
        Int("mudconstitution", o => o.Stats.Con, (o, n) => o.Stats.Con = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, stable, "clientr.mud_con");
        Int("mudwisdom", o => o.Stats.Wis, (o, n) => o.Stats.Wis = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, stable, "clientr.mud_wis");
        Int("mudintelligence", o => o.Stats.Int, (o, n) => o.Stats.Int = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, stable, "clientr.mud_int");
        Int("mudpower", o => o.Stats.Pow, (o, n) => o.Stats.Pow = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, stable, "clientr.mud_pow");
        Int("mudcharm", o => o.Stats.Cha, (o, n) => o.Stats.Cha = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, false, "clientr.mud_cha"); // not stable, like mudcharisma: the same field
        Int("mudcharisma", o => o.Stats.Cha, (o, n) => o.Stats.Cha = (sbyte)n, sbyte.MinValue, sbyte.MaxValue, false, "clientr.mud_cha");
        Str("mudrealname", o => o.Name, (o, s) => o.Name = s, stable);
        Str("mudtitle", o => o.Title, (o, s) => o.Title = s.Length == 0 ? null : s, stable);
        Str("mudslaying", o => o.Slaying, (o, s) => o.Slaying = s.Length == 0 ? null : s, stable);
        Str("mudrace", o => o.Race, (o, s) => o.Race = s.Length == 0 ? null : s, stable);
        Flag("nodamage", Define.FLAG_NO_DAMAGE, true, stable);
        Flag("alive", Define.FLAG_ALIVE, true, false);
        Flag("undead", Define.FLAG_UNDEAD, true, false);
        Flag("scared", Define.FLAG_SCARED, true, false);
        Flag("flying", Define.FLAG_FLYING, true, false);
        Flag("confused", Define.FLAG_CONFUSED, true, false);
        Flag("blind", Define.FLAG_BLIND, true, false);
    }
}
