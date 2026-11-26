// Not ported from a Crossfire file: what lane 4G (gmaps, if any) registers in the manifest of the Crossfire extension. The lane owns
// this file; its ids are the ranges of Lane.Gmaps (docs/extension.md, "Lanes and id ranges").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The functions, types and events of lane 4G; <paramref name="b"/> refuses an id outside <see cref="Lane.Gmaps"/>.</summary>
    private static void RegisterGmaps(ManifestBuilder b)
    {
    }
}
