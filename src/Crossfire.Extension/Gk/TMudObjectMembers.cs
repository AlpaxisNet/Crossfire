// Not ported from a Crossfire file: the members and methods of Graal Kingdoms' script type TMudObject (an object in a level), and of
// TMudPlayer (a logged-in player). The table, the C field behind each member and the evidence for it are in docs/extension.md.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Gk;

/// <summary>The type TMudObject: host <c>npc</c>, so its members are on every level NPC that shows an object (protocol 6.2).</summary>
internal static class TMudObjectMembers
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
        b.ExtraInt("mudgraaltype", "graalnpctype", -1, true, true);
        b.ExtraInt("npctype", "graalnpctype", -1, true, false); // an alias of mudgraaltype: only one of the two may be stable (protocol 6.1)
        b.ExtraStr("mudgraalparam", "graalnpcparam", true, true);
        b.ExtraStr("npcparam", "graalnpcparam", true, false); // an alias of mudgraalparam

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

        var methods = new[]
        {
            new MethodDef(MethodCloneMudObject, "cloneMudObject", new[] { "float", "float" }, "handle:" + TypeName),
            new MethodDef(MethodDestroyMudObject, "destroymudobject", Array.Empty<string>(), "void"),
        };
        return new MemberTable<LevelObject>(TypeId, TypeName, "npc", b.Members, methods);
    }
}

/// <summary>The type TMudPlayer: host <c>player</c>, so its members are on the player's object of the host (protocol 6.2).</summary>
internal static class TMudPlayerMembers
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
        b.ExtraBool("peaceful", "mudpeaceful", true, false);
        b.ExtraInt("lastdeath", "mudlastdeath", 0, true, false, long.MinValue, long.MaxValue);
        b.ExtraBool("mudverbose", "mudverbose", true, false);
        b.ExtraInt("levelcap", "mudlevelcap", 0, true, false);
        b.ExtraInt("wccap", "mudwccap", 0, true, false);
        b.ExtraInt("accap", "mudaccap", 0, true, false);
        b.Add("ismudobject", "bool", false, true, (_, _) => Val.True, null);
        return new MemberTable<PlayerState>(TypeId, TypeName, "player", b.Members, Array.Empty<MethodDef>());
    }
}
