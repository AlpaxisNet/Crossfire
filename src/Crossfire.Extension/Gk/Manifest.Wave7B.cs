// Not ported from a Crossfire file: what lane 7B of wave 7 (the account completion and trade) registers in the manifest of the Crossfire extension. The
// lane owns this file; its ids are the ranges of Lane.Wave7B (docs/extension.md, "Lanes and id ranges").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The operation of the function <c>mudexamineinvobject2</c>: <c>ctx.player</c> examines an item of another player's tree (the player is named by his index in <c>level.players</c>).</summary>
    public const int OpMudExamineInvObject2 = 13000;

    /// <summary>The operation of the function <c>mudtransferobject</c>: <c>ctx.player</c> gives an item of his tree (or part of a stack) to another player.</summary>
    public const int OpMudTransferObject = 13001;

    /// <summary>The functions, types and events of lane 7B; <paramref name="b"/> refuses an id outside <see cref="Lane.Wave7B"/>.</summary>
    private static void RegisterWave7B(ManifestBuilder b)
    {
        // both ask the host's query player.at, which a link of 1.0 does not have: below 1.1 the functions are not in the manifest
        b.AddFunction(Lane.Wave7B, new FunctionDef(OpMudExamineInvObject2, "mudexamineinvobject2", new[] { "int", "int" }, "bool",
            Handler: (d, args) => d.MudExamineInvObject2(args), MinMinor: 1));
        // slow: it moves an item between two accounts, so it answers busy before it changes anything and a late reply is applied (docs/mud-phase3.md section 3, rule 10)
        b.AddFunction(Lane.Wave7B, new FunctionDef(OpMudTransferObject, "mudtransferobject", new[] { "int", "int", "int", "int" }, "bool",
            Slow: true, Handler: (d, args) => d.MudTransferObject(args), MinMinor: 1));
    }
}
