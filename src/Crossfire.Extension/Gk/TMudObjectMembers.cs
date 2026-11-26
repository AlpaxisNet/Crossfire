// Not ported from a Crossfire file: the members and methods of Graal Kingdoms' script type TMudObject (an object in a level), and of
// TMudPlayer (a logged-in player). The table, the C field behind each member and the evidence for it are in docs/extension.md.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Gk;

/// <summary>The type TMudObject: host <c>npc</c>, so its members are on every level NPC that shows an object (protocol 6.2).</summary>
internal static partial class TMudObjectMembers
{
    public const int TypeId = 2;
    public const string TypeName = "TMudObject";
    public const int FirstMemberId = 200;
    public const int MethodCloneMudObject = 300;
    public const int MethodDestroyMudObject = 301;

    /// <summary>The members whose write fires <c>onUpdateObject</c>: every writable member (objects.md 3.2: INFERRED).</summary>
    public static readonly MemberTable<LevelObject> Table = Build();

    private static MemberTable<LevelObject> Build()
    {
        var b = new FieldBuilder<LevelObject>(FirstMemberId, o => o.Ob);

        // names and identity
        b.Str("mudname", o => o.Arch?.Name, null, true);
        b.Str("archname", o => o.Arch?.Name, null, true);
        b.Add("arch", "handle:" + TMudArchMembers.TypeName, false, true,
            (env, lo) => lo.Ob.Arch == null ? Val.Nil : Val.Handle(env.HandleOf(lo.Ob.Arch)), null);
        b.Add("mudid", "int", false, true, (env, lo) => Val.Int(env.TagOf(lo)), null);
        b.Add("fullname", "string", false, false, (env, lo) => Val.Str(FileText.FromBytesText(env.World.QueryName(lo.Ob))), null);
        b.Add("mudfullname", "string", false, false, (env, lo) => Val.Str(FileText.FromBytesText(env.World.QueryShortName(lo.Ob))), null);
        b.Add("icon", "string", false, true, (_, lo) => Val.Str(TMudArchMembers.Icon(lo.Ob.Face?.Name)), null);
        b.Add("img", "string", true, true, (_, lo) => Val.Str(GkLook.ImageOf(lo.Ob)), (_, lo, v) =>
        {
            if (!v.IsNil && v.Kind != ValKind.String)
            {
                return SetResult.Fail("the member takes a string");
            }
            if (FieldBuilder<LevelObject>.HasLineBreak(v.AsString))
            {
                return SetResult.Fail(FieldBuilder<LevelObject>.LineBreakRefused);
            }
            string bytes = FileText.ToBytesText(v.AsString);
            if (bytes.Length == 0)
            {
                lo.Ob.Extra?.Remove("mudimg");
            }
            else
            {
                (lo.Ob.Extra ??= new ExtraKeys()).Set("mudimg", bytes);
            }
            return SetResult.Ok(Val.Str(GkLook.ImageOf(lo.Ob)));
        });
        b.Int("mudtype", o => o.Type, null, 0, 0, true);
        b.ExtraInt("mudgraaltype", "graalnpctype", -1, true, false); // two names of one field: neither is stable, a set of one changes what the host would hold for the other (protocol 6.1)
        b.ExtraInt("npctype", "graalnpctype", -1, true, false);
        b.ExtraStr("mudgraalparam", "graalnpcparam", true, false); // not stable, like npcparam: the same field
        b.ExtraStr("npcparam", "graalnpcparam", true, false);

        // life, stats, names (shared with the player)
        b.SharedLife(stable: true);

        b.ExtraInt("mudhpbuff", "hpbuff", 0, true, true);
        b.ExtraInt("mudspbuff", "spbuff", 0, true, true);
        b.ExtraInt("mudgracebuff", "gracebuff", 0, true, true);
        b.Int("mudquantity", o => o.Nrof, null, 0, 0, false);
        b.Int("mudnrof", o => o.Nrof, null, 0, 0, false);
        b.Int("mudmaterial", o => o.Material, (o, n) => o.Material = (ushort)n, 0, ushort.MaxValue, true);
        b.Add("mudmessage", "list:string", false, true, (_, lo) => TMudArchMembers.Lines(lo.Ob.Msg), null);
        b.Str("value", o => o.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), null, true);
        b.Str("otherarchname", o => o.OtherArch?.Name, null, true);
        b.ExtraInt("castingtime", "castingtime", 0, true, true);
        b.ExtraStr("restrictaccess", "restrictaccess", true, true);
        b.ExtraInt("mudrandomvar", "randomvar", 0, true, true);
        b.ExtraInt("mudmarketprize", "marketprice", 0, true, true);
        b.ExtraBool("nopeaceful", "nopeaceful", true, true);

        // flags of the object
        b.Flag("applied", Define.FLAG_APPLIED, true, false);
        b.Flag("identified", Define.FLAG_IDENTIFIED, true, false);
        b.Flag("cursed", Define.FLAG_CURSED, true, false);
        b.Flag("damned", Define.FLAG_DAMNED, true, false);
        b.Flag("unpaid", Define.FLAG_UNPAID, true, false);
        b.Flag("nopick", Define.FLAG_NO_PICK, true, true);
        b.Flag("startequip", Define.FLAG_STARTEQUIP, true, true);
        b.Flag("ismudmonster", Define.FLAG_MONSTER, false, true);
        b.Add("ismudobject", "bool", false, true, (_, _) => Val.True, null);

        var methods = new List<MethodDef>
        {
            new MethodDef(MethodCloneMudObject, "cloneMudObject", new[] { "float", "float" }, "handle:" + TypeName),
            new MethodDef(MethodDestroyMudObject, "destroymudobject", Array.Empty<string>(), "void"),
        };
        // the lanes' members and methods: each in the range of its lane (Lane.ObjectMembersFirst), in the order of Lane.All
        Begin(b, Lane.Items);
        ExtendItems(b, methods);
        Begin(b, Lane.Start);
        ExtendStart(b, methods);
        Begin(b, Lane.Gmaps);
        ExtendGmaps(b, methods);
        Begin(b, Lane.Wave5A);
        ExtendWave5A(b, methods);
        Begin(b, Lane.Wave5B);
        ExtendWave5B(b, methods);
        Begin(b, Lane.Wave5C);
        ExtendWave5C(b, methods);
        Begin(b, Lane.Wave5D);
        ExtendWave5D(b, methods);
        Begin(b, Lane.Wave6A);
        ExtendWave6A(b, methods);
        Begin(b, Lane.Wave6B);
        ExtendWave6B(b, methods);
        Begin(b, Lane.Wave6C);
        ExtendWave6C(b, methods);
        Begin(b, Lane.Wave6D);
        ExtendWave6D(b, methods);
        Begin(b, Lane.Wave7A);
        ExtendWave7A(b, methods);
        Begin(b, Lane.Wave7B);
        ExtendWave7B(b, methods);
        return new MemberTable<LevelObject>(TypeId, TypeName, "npc", b.Members, methods);
    }

    private static void Begin(FieldBuilder<LevelObject> b, Lane lane) => b.UseRange(lane.ObjectMembersFirst, lane.ObjectMembersLast);

    // A lane implements its method in a partial file of its own (Gk/TMudObjectMembers.Items.cs and so on): it adds members with b and
    // methods to the list, with ids from b.NextMethodId(). A method that is not implemented adds nothing.
    static partial void ExtendItems(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendStart(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendGmaps(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave5A(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave5B(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave5C(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave5D(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave6A(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave6B(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave6C(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave6D(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave7A(FieldBuilder<LevelObject> b, List<MethodDef> methods);

    static partial void ExtendWave7B(FieldBuilder<LevelObject> b, List<MethodDef> methods);
}

/// <summary>The type TMudPlayer: host <c>player</c>, so its members are on the player's object of the host (protocol 6.2).</summary>
internal static partial class TMudPlayerMembers
{
    public const int TypeId = 3;
    public const string TypeName = "TMudPlayer";
    public const int FirstMemberId = 500;

    public static readonly MemberTable<PlayerState> Table = Build();

    private static MemberTable<PlayerState> Build()
    {
        var b = new FieldBuilder<PlayerState>(FirstMemberId, p => p.Ob);
        b.Add("mudid", "int", false, true, (env, p) => Val.Int(env.TagOf(p)), null);
        b.SharedLife(stable: false);
        b.ExtraStr("mudclass", "mudclass", true, false);
        b.Bool("peaceful", Commands.IsPeaceful, (ob, v) => (ob.Extra ??= new ExtraKeys()).Set(Commands.PeacefulKey, v ? "1" : "0"), false); // C: a new player is peaceful (get_player); an account without the line is too
        b.ExtraInt("lastdeath", "mudlastdeath", 0, true, false, long.MinValue, long.MaxValue);
        b.ExtraInt("mudverbose", "mudverbose", 0, true, false, sbyte.MinValue, sbyte.MaxValue); // a level, as C's char `listen`; GK compares it with 5 and 10
        b.ExtraInt("levelcap", "mudlevelcap", 0, true, false);
        b.ExtraInt("wccap", "mudwccap", 0, true, false);
        b.ExtraInt("accap", "mudaccap", 0, true, false);
        b.Add("ismudobject", "bool", false, true, (_, _) => Val.True, null);
        var methods = new List<MethodDef>();
        // the lanes' members and methods of a player: each in the range of its lane (Lane.PlayerMembersFirst)
        Begin(b, Lane.Items);
        ExtendItems(b, methods);
        Begin(b, Lane.Start);
        ExtendStart(b, methods);
        Begin(b, Lane.Gmaps);
        ExtendGmaps(b, methods);
        Begin(b, Lane.Wave5A);
        ExtendWave5A(b, methods);
        Begin(b, Lane.Wave5B);
        ExtendWave5B(b, methods);
        Begin(b, Lane.Wave5C);
        ExtendWave5C(b, methods);
        Begin(b, Lane.Wave5D);
        ExtendWave5D(b, methods);
        Begin(b, Lane.Wave6A);
        ExtendWave6A(b, methods);
        Begin(b, Lane.Wave6B);
        ExtendWave6B(b, methods);
        Begin(b, Lane.Wave6C);
        ExtendWave6C(b, methods);
        Begin(b, Lane.Wave6D);
        ExtendWave6D(b, methods);
        Begin(b, Lane.Wave7A);
        ExtendWave7A(b, methods);
        Begin(b, Lane.Wave7B);
        ExtendWave7B(b, methods);
        return new MemberTable<PlayerState>(TypeId, TypeName, "player", b.Members, methods);
    }

    private static void Begin(FieldBuilder<PlayerState> b, Lane lane) => b.UseRange(lane.PlayerMembersFirst, lane.PlayerMembersLast);

    // As for TMudObject (Gk/TMudPlayerMembers.Items.cs and so on).
    static partial void ExtendItems(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendStart(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendGmaps(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave5A(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave5B(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave5C(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave5D(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave6A(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave6B(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave6C(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave6D(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave7A(FieldBuilder<PlayerState> b, List<MethodDef> methods);

    static partial void ExtendWave7B(FieldBuilder<PlayerState> b, List<MethodDef> methods);
}
