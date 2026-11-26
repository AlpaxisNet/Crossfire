// Not ported from a Crossfire file: what lane 5A of wave 5 (pick up and drop) registers in the manifest of the Crossfire extension. The
// lane owns this file; its ids are the ranges of Lane.Wave5A (docs/extension.md, "Lanes and id ranges"). The methods of TMudObject are in
// TMudObjectMembers.Wave5A.cs, the code of both in Dispatcher.Wave5A.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The operation of the function <c>muddropinvobject</c>: <c>ctx.player</c> drops an item of his tree at a place of the request's level.</summary>
    public const int OpMudDropInvObject = 4100;

    /// <summary>The functions, types and events of lane 5A; <paramref name="b"/> refuses an id outside <see cref="Lane.Wave5A"/>.</summary>
    private static void RegisterWave5A(ManifestBuilder b)
    {
        // slow: it moves an item, so it answers busy before it changes anything and a late reply is applied (docs/mud-phase3.md section 3, rule 10)
        b.AddFunction(Lane.Wave5A, new FunctionDef(OpMudDropInvObject, "muddropinvobject", new[] { "int", "float", "float", "int" }, "bool",
            Slow: true, Handler: (d, args) => d.MudDropInvObject(args)));
    }
}
