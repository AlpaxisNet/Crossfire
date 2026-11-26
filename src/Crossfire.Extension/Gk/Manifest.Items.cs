// Not ported from a Crossfire file: what lane 4C (items, strings and inventory members) registers in the manifest of the Crossfire
// extension. The lane owns this file; its ids are the ranges of Lane.Items (docs/extension.md, "Lanes and id ranges").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The player event of a line of text for the player (C: <c>new_draw_info</c>); the class <c>mudplayers</c> of Graal Kingdoms defines it.</summary>
    public const string EventOnRPGMessage = "onRPGMessage";

    /// <summary>The functions, types and events of lane 4C; <paramref name="b"/> refuses an id outside <see cref="Lane.Items"/>.</summary>
    private static void RegisterItems(ManifestBuilder b)
    {
        // a line of text is the player event onRPGMessage(text) with the client.text effect as its fallback; on a 1.0 link, which has no
        // fallback, the effect alone is sent (Dispatcher.DrawInfoTo)
        b.AddEvent(Lane.Items, new EventDef(EventOnRPGMessage, "player", "sync", new[] { "string" }, MinMinor: 1));
        b.AddPlayerClass("mudplayers");
    }
}
