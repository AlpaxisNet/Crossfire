// Not ported from a Crossfire file: what lane 7A of wave 7 (the mudcommand table) registers in the manifest of the Crossfire extension. The
// lane owns this file; its ids are the ranges of Lane.Wave7A (docs/extension.md, "Lanes and id ranges"). The code is in
// Dispatcher.Wave7A.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The operation of the function <c>mudcommand</c>: <c>ctx.player</c> runs a command (docs/mud-phase3.md section 4.6).</summary>
    public const int OpMudCommand = 12000;

    /// <summary>The functions, types and events of lane 7A; <paramref name="b"/> refuses an id outside <see cref="Lane.Wave7A"/>.</summary>
    private static void RegisterWave7A(ManifestBuilder b)
    {
        // slow: drop, dropall, take and save change items and the account, so the function answers busy before it runs and a late reply
        // is applied (docs/mud-phase3.md section 3, rule 10)
        b.AddFunction(Lane.Wave7A, new FunctionDef(OpMudCommand, "mudcommand", new[] { "string", "string" }, "void", Slow: true,
            Handler: (d, args) => d.MudCommand(args)));
    }
}
