// Not ported from a Crossfire file: what the Graal Kingdoms adaptation of the Crossfire host does for the start path (lane 4D): the
// world's skill table and <c>fix_player</c>.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal sealed partial class GkHost
{
    /// <summary>The world the host belongs to (set by <c>session.init</c>; the calls of C that need it come through here).</summary>
    public CfWorld? World { get; set; }

    /// <summary>The skills and experience categories of the world (<c>skills[]</c>, <c>exp_cat[]</c>), made when the archetypes were read.</summary>
    public SkillTable? Skills { get; set; }

    /// <summary>common/living.c <c>fix_player</c>, the part of it the extension has (<see cref="Gk.FixPlayer"/>).</summary>
    public override void FixPlayer(CfObject player)
    {
        if (World != null)
        {
            Gk.FixPlayer.Fix(World, player);
        }
    }
}
