// Not ported from a Crossfire file: what lane 6A of wave 6 (containers, sacks and keys) registers in the manifest of the Crossfire extension. The
// lane owns this file; its ids are the ranges of Lane.Wave6A (docs/extension.md, "Lanes and id ranges").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The operation of the function <c>mudmoveinvobject</c>: <c>ctx.player</c> moves an item of his tree into a container of it or out of one.</summary>
    public const int OpMudMoveInvObject = 8000;

    /// <summary>The functions, types and events of lane 6A; <paramref name="b"/> refuses an id outside <see cref="Lane.Wave6A"/>.</summary>
    private static void RegisterWave6A(ManifestBuilder b)
    {
        // slow: it moves an item, so it answers busy before it changes anything and a late reply is applied (docs/mud-phase3.md section 3, rule 10)
        b.AddFunction(Lane.Wave6A, new FunctionDef(OpMudMoveInvObject, "mudmoveinvobject", new[] { "int", "int", "int" }, "bool",
            Slow: true, Handler: (d, args) => d.MudMoveInvObject(args)));
    }
}
