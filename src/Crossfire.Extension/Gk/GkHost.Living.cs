// Not ported from a Crossfire file: what the host answers about a player's regeneration and digestion (<c>describe_monster</c> of
// common/item.c asks for them through the world).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal sealed partial class GkHost
{
    public override int PlayerDigestion(IPlayerHandle contr) => contr is Player p ? p.Digestion : 0;

    public override int PlayerGenGrace(IPlayerHandle contr) => contr is Player p ? p.GenGrace : 0;

    public override int PlayerGenSp(IPlayerHandle contr) => contr is Player p ? p.GenSp : 0;

    public override int PlayerGenHp(IPlayerHandle contr) => contr is Player p ? p.GenHp : 0;
}
