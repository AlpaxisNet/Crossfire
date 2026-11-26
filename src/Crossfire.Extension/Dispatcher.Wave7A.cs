// Not ported from a Crossfire file: the methods of lane 7A of wave 7, the mudcommand table (docs/extension.md, "Methods of the lanes"). The
// lane owns this file; its method ids are in the ranges of Lane.Wave7A.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    private partial Outcome? InvokeWave7A(object target, long method, IReadOnlyList<Val> args) => null;
}
