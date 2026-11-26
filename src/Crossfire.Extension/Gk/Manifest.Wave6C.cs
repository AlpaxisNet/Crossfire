// Not ported from a Crossfire file: what lane 6C of wave 6 (food, drink, potions and poison) registers in the manifest of the Crossfire extension. The
// lane owns this file; its ids are the ranges of Lane.Wave6C (docs/extension.md, "Lanes and id ranges").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;

namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The player event of eating or drinking, after the state changed (mud-phase3.md 4.4); the class <c>mudplayers</c> of Graal Kingdoms defines it.</summary>
    public const string EventOnEatFood = "onEatFood";

    /// <summary>The functions, types and events of lane 6C; <paramref name="b"/> refuses an id outside <see cref="Lane.Wave6C"/>.</summary>
    private static void RegisterWave6C(ManifestBuilder b)
    {
        // onEatFood(foodarch, food, wascursed): the archetype of the food, the food (nil once used up) and 1 when it was cursed
        b.AddEvent(Lane.Wave6C, new EventDef(EventOnEatFood, "player", "sync",
            new[] { "handle:" + TMudArchMembers.TypeName, "handle:" + TMudObjectMembers.TypeName, "int" }, MinMinor: 1));
    }
}
