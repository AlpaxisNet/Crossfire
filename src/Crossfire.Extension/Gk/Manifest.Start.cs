// Not ported from a Crossfire file: what lane 4D (the start path) registers in the manifest of the Crossfire extension. The lane owns
// this file; its ids are the ranges of Lane.Start (docs/extension.md, "Lanes and id ranges"). The methods of TMudPlayer are in
// AccountMethods.cs, their code in Dispatcher.Start.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The operation of the function <c>mudresetplayer</c>: the player of the request's context starts again (docs/mud-phase3.md section 4.1).</summary>
    public const int OpMudResetPlayer = 2000;

    /// <summary>The operation of the function <c>mudrollstats</c>.</summary>
    public const int OpMudRollStats = 2001;

    /// <summary>The functions, types and events of lane 4D; <paramref name="b"/> refuses an id outside <see cref="Lane.Start"/>.</summary>
    private static void RegisterStart(ManifestBuilder b)
    {
        // global functions: the actor is the player of the request's context (a script that runs for the player, not a method of it)
        b.AddFunction(Lane.Start, new FunctionDef(OpMudResetPlayer, "mudresetplayer", Array.Empty<string>(), "void", Handler: (d, args) => d.MudResetPlayerOfContext(args)));
        b.AddFunction(Lane.Start, new FunctionDef(OpMudRollStats, "mudrollstats", Array.Empty<string>(), "void", Handler: (d, args) => d.MudRollStatsOfContext(args)));
    }
}
