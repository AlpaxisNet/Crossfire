// Not ported from a Crossfire file: the members of Graal Kingdoms' script type TMudArch (an archetype), read from the archetype's clone
// (the object that Crossfire's object.h calls archt->clone). The table, the C field behind each member and the evidence for it are
// in docs/extension.md; a test keeps the two equal.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Gk;

/// <summary>One member of TMudArch: its id and name in the manifest, its type name, and the way to read it from an archetype.</summary>
internal sealed record ArchMember(int Id, string Name, string TypeName, Func<CfWorld, Archetype, Val> Read);

/// <summary>The type TMudArch (a host object of the manifest, section 6): read-only, stable members of an archetype.</summary>
internal static partial class TMudArchMembers
{
    public const int TypeId = 1;
    public const string TypeName = "TMudArch";
    public const int FirstMemberId = 100;

    private static readonly List<ArchMember> All = Build();

    /// <summary>The methods of the type (the lanes' own, each in the range of its lane: <see cref="ExtendMethods"/>).</summary>
    public static IReadOnlyList<MethodDef> Methods { get; } = BuildMethods();

    private static List<MethodDef> BuildMethods()
    {
        var methods = new List<MethodDef>();
        ExtendMethods(methods);
        return methods;
    }

    static partial void ExtendMethods(List<MethodDef> methods);

    /// <summary>The members in the order of their ids.</summary>
    public static IReadOnlyList<ArchMember> Members => All;

    private static readonly Dictionary<int, ArchMember> ById = All.ToDictionary(m => m.Id);

    /// <summary>The member of that id, or null.</summary>
    public static ArchMember? Find(long id) => id >= int.MinValue && id <= int.MaxValue && ById.TryGetValue((int)id, out ArchMember? m) ? m : null;

    private static List<ArchMember> Build()
    {
        var list = new List<ArchMember>();
        int next = FirstMemberId;

        void Add(string name, string type, Func<CfWorld, Archetype, Val> read) => list.Add(new ArchMember(next++, name, type, read));
        void Str(string name, Func<CfWorld, Archetype, string> f) => Add(name, "string", (w, a) => Val.Str(f(w, a)));
        void Int(string name, Func<CfObject, long> f) => Add(name, "int", (_, a) => Val.Int(f(a.Clone)));
        void Bool(string name, int flag) => Add(name, "bool", (_, a) => Val.Bool(a.Clone.QueryFlag(flag)));
        void ExtraInt(string name, string key, long absent = 0) =>
            Add(name, "int", (_, a) => Val.Int(a.Clone.Extra?.Get(key) is { } t ? CStr.Atoi(t) : absent));
        void ExtraStr(string name, string key) => Add(name, "string", (_, a) => Val.Str(a.Clone.Extra?.Get(key) ?? ""));

        // the names (objects.md 4.5, 5.5, 5.6): the archetype's name, and the clone's name in its three forms
        Str("mudname", (_, a) => a.Name ?? "");
        Str("archname", (_, a) => a.Name ?? "");
        Str("mudrealname", (_, a) => a.Clone.Name ?? "");
        Str("fullname", (w, a) => w.QueryName(a.Clone));
        Str("mudfullname", (w, a) => w.QueryShortName(a.Clone));
        Str("icon", (_, a) => Icon(a.Clone.Face?.Name));

        // plain fields: mud + the Crossfire field
        Int("mudtype", o => o.Type);
        Int("mudlevel", o => o.Level);
        Int("mudhp", o => o.Stats.Hp);
        Int("mudmaxhp", o => o.Stats.Maxhp);
        Int("mudsp", o => o.Stats.Sp);
        Int("mudmaxsp", o => o.Stats.Maxsp);
        Int("mudgrace", o => o.Stats.Grace);
        Int("mudmaxgrace", o => o.Stats.Maxgrace);
        ExtraInt("mudhpbuff", "hpbuff");
        ExtraInt("mudspbuff", "spbuff");
        ExtraInt("mudgracebuff", "gracebuff");
        Int("mudac", o => o.Stats.Ac);
        Int("mudwc", o => o.Stats.Wc);
        Int("muddamage", o => o.Stats.Dam);
        Int("mudattacktype", o => o.Attacktype);
        Add("mudspeed", "float", (_, a) => Val.Float(double.Parse(a.Clone.Speed.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)));
        Int("mudexp", o => o.Stats.Exp);
        Int("mudfood", o => o.Stats.Food);
        Int("mudweight", o => o.Weight);
        Int("mudcarrying", o => o.Carrying);
        Int("mudquantity", o => o.Nrof);
        Int("mudnrof", o => o.Nrof);
        Int("mudmaterial", o => o.Material);
        Int("mudmagic", o => o.Magic);
        Int("mudluck", o => o.Stats.Luck);
        Int("mudstrength", o => o.Stats.Str);
        Int("muddexterity", o => o.Stats.Dex);
        Int("mudconstitution", o => o.Stats.Con);
        Int("mudwisdom", o => o.Stats.Wis);
        Int("mudintelligence", o => o.Stats.Int);
        Int("mudpower", o => o.Stats.Pow);
        Int("mudcharm", o => o.Stats.Cha);
        Int("mudcharisma", o => o.Stats.Cha);
        Str("mudtitle", (_, a) => a.Clone.Title ?? "");
        Str("mudslaying", (_, a) => a.Clone.Slaying ?? "");
        Str("mudrace", (_, a) => a.Clone.Race ?? "");
        Add("mudmessage", "list:string", (_, a) => Lines(a.Clone.Msg));
        Str("value", (_, a) => a.Clone.Value.ToString(CultureInfo.InvariantCulture));
        Str("otherarchname", (_, a) => a.Clone.OtherArch?.Name ?? "");

        // Graal Kingdoms' own keys (kept by the loader as extra lines of the clone)
        ExtraInt("mudgraaltype", "graalnpctype", -1);
        ExtraInt("npctype", "graalnpctype", -1);
        ExtraStr("mudgraalparam", "graalnpcparam");
        ExtraStr("npcparam", "graalnpcparam");
        ExtraInt("castingtime", "castingtime");
        ExtraStr("restrictaccess", "restrictaccess");
        Add("nopeaceful", "bool", (_, a) => Val.Bool(a.Clone.Extra?.Get("nopeaceful") is { } t && CStr.Atoi(t) != 0));

        // flags
        Bool("applied", Define.FLAG_APPLIED);
        Bool("identified", Define.FLAG_IDENTIFIED);
        Bool("cursed", Define.FLAG_CURSED);
        Bool("damned", Define.FLAG_DAMNED);
        Bool("unpaid", Define.FLAG_UNPAID);
        Bool("nopick", Define.FLAG_NO_PICK);
        Bool("nodamage", Define.FLAG_NO_DAMAGE);
        Bool("alive", Define.FLAG_ALIVE);
        Bool("undead", Define.FLAG_UNDEAD);
        Bool("scared", Define.FLAG_SCARED);
        Bool("flying", Define.FLAG_FLYING);
        Bool("confused", Define.FLAG_CONFUSED);
        Bool("blind", Define.FLAG_BLIND);
        Bool("startequip", Define.FLAG_STARTEQUIP);
        Bool("ismudmonster", Define.FLAG_MONSTER);
        Add("ismudobject", "bool", (_, _) => Val.True);
        return list;
    }

    /// <summary>
    /// The icon of a face (objects.md 4.5): "mud_" + the face without a trailing ".&lt;digits&gt;" + ".png"; a face that already ends
    /// ".png" is used as it is. An archetype without a face (or with Crossfire's blank or empty face) has no icon: "". Whether a
    /// picture of that name exists is the host's folder of icons, which the extension does not see.
    /// </summary>
    public static string Icon(string? face)
    {
        if (string.IsNullOrEmpty(face) || face == FaceTable.Define_BLANK_FACE_NAME || face == FaceTable.Define_EMPTY_FACE_NAME)
        {
            return "";
        }
        if (face.EndsWith(".png", StringComparison.Ordinal))
        {
            return face;
        }
        int dot = face.LastIndexOf('.');
        if (dot > 0 && dot < face.Length - 1 && face.Skip(dot + 1).All(char.IsAsciiDigit))
        {
            face = face.Substring(0, dot);
        }
        return "mud_" + face + ".png";
    }

    /// <summary>The lines of an archetype's message: split at newlines, the empty piece after the last newline dropped.</summary>
    public static Val Lines(string? msg)
    {
        if (msg == null)
        {
            return Val.List();
        }
        string[] parts = msg.Split('\n');
        int count = parts[^1].Length == 0 ? parts.Length - 1 : parts.Length;
        return Val.List(parts.Take(count).Select(Val.Str));
    }
}
