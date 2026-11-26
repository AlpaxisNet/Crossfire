// Not ported from a Crossfire file: the methods of lane 6C of wave 6, food, drink, potions and poison (docs/extension.md, "Methods of the lanes"). The
// lane owns this file; its method ids are in the ranges of Lane.Wave6C.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    private partial Outcome? InvokeWave6C(object target, long method, IReadOnlyList<Val> args) => null;

    private partial int? ApplyWave6C(CfObject op, CfObject tmp, int aflag) => null;
}
