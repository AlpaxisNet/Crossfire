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

    private static readonly HashSet<string> Keys = new(OwnKeys, StringComparer.OrdinalIgnoreCase);

    public override bool IsLoaderKey(string key) => Keys.Contains(key);
}
