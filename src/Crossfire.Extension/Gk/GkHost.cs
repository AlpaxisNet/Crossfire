// Not ported from a Crossfire file: the Graal Kingdoms adaptation of the Crossfire host. Graal Kingdoms' archetypes carry nine keys of
// their own that are not Crossfire's; its server added rules for them to the loader, which keeps them as the object's extra lines
// (CfObject.Extra). Names of keys only: no file of Graal Kingdoms' world is here.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

/// <summary>The host of the Crossfire world for Graal Kingdoms' archetypes: <see cref="IsLoaderKey"/> names Graal Kingdoms' own keys.</summary>
internal sealed class GkHost : CrossfireHost
{
    /// <summary>The keys of Graal Kingdoms' archetypes that are not Crossfire's.</summary>
    public static readonly IReadOnlyList<string> OwnKeys = new[]
    {
        "graalnpctype", "graalnpcparam", "flag", "hpbuff", "spbuff", "gracebuff", "castingtime", "restrictaccess", "nopeaceful",
    };

    /// <summary>
    /// The keys this extension itself writes into its level and account files, on top of Crossfire's: the position of a level
    /// object as the host's floats (<c>mudx</c>, <c>mudy</c>), the host's script state (<c>mudblob</c>), the picture a script set
    /// (<c>mudimg</c>), the two numbers Graal Kingdoms keeps with an object (<c>randomvar</c>, <c>marketprice</c>), the account a
    /// player file belongs to (<c>account</c>) and the plain state of a player (docs/extension.md, "The level and account files").
    /// </summary>
    public static readonly IReadOnlyList<string> StateKeys = new[]
    {
        "mudx", "mudy", "mudblob", "mudimg", "randomvar", "marketprice", "account", "mudclass", "mudpeaceful", "mudlastdeath",
        "mudverbose", "mudlevelcap", "mudwccap", "mudaccap", "mudowner",
    };

    private static readonly HashSet<string> Keys = new(OwnKeys.Concat(StateKeys), StringComparer.OrdinalIgnoreCase);

    /// <summary>While a map file is read: the objects that C's <c>load_objects</c> hands to <c>insert_ob_in_map</c>, in file order (a part of a larger object too).</summary>
    public List<CfObject>? Collecting { get; set; }

    /// <summary>The dispatcher that answers what the object model tells the client (lane 4C): the item strings, the lines of text, the fixes.</summary>
    public Dispatcher? Items { get; set; }

    public override bool IsLoaderKey(string key) => Keys.Contains(key);

    public override void EsrvSendItem(CfObject player, CfObject op) => Items?.ItemSent(player, op);

    /// <summary>The string is written whole for every kind of update: the flags of the update say nothing the text does not.</summary>
    public override void EsrvUpdateItem(int flags, CfObject player, CfObject op) => Items?.ItemSent(player, op);

    public override void EsrvDelItem(IPlayerHandle? player, uint tag) => Items?.ItemDeleted(player);

    public override void FixPlayer(CfObject player) => Items?.PlayerFixed(player);

    public override void DrawInfo(CfObject who, string text) => Items?.DrawInfoTo(who, text);

    public override CfObject? FindBestWeaponUsedMatch(CfObject player, string parameters) =>
        Items?.WorldOrNull is { } world ? ItemMatching.FindBestWeaponUsedMatch(world, player, parameters) : null;

    /// <summary>The object model has no squares: the objects that a map file loads are collected in file order (the extension's own level index).</summary>
    public override CfObject? InsertObInMap(CfObject op, IMapHandle map, CfObject? originator, int flag)
    {
        Collecting?.Add(op);
        return op;
    }
}
