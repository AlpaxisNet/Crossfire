// Not ported from a Crossfire file: what lane 6B of wave 6 (the apply core and wearing) registers in the manifest of the Crossfire extension. The
// lane owns this file; its ids are the ranges of Lane.Wave6B (docs/extension.md, "Lanes and id ranges"). The methods of TMudObject and
// TMudPlayer are in TMudObjectMembers.Wave6B.cs, the code of both in Dispatcher.Wave6B.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;

namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The operation of the function <c>mudapplyinvobject</c>: <c>ctx.player</c> applies an item of his tree.</summary>
    public const int OpMudApplyInvObject = 9100;

    /// <summary>The player event that tells the player's classes that an item is being applied (<c>mudplayers</c> declares it), before the item's own <c>onApply</c>.</summary>
    public const string EventOnApplyItem = "onApplyItem";

    /// <summary>The item event of an apply: the class of the item's type decides; with no function the host runs <c>defaultApply</c> (protocol 11.1).</summary>
    public const string EventOnApply = "onApply";

    public const string EventOnWeaponWielded = "onWeaponWielded";
    public const string EventOnWeaponUnWielded = "onWeaponUnWielded";
    public const string EventOnWandReadied = "onWandReadied";
    public const string EventOnWandUnReadied = "onWandUnReadied";
    public const string EventOnArmorWorn = "onArmorWorn";
    public const string EventOnArmorUnWorn = "onArmorUnWorn";
    public const string EventOnClothWorn = "onClothWorn";
    public const string EventOnClothUnWorn = "onClothUnWorn";

    /// <summary>The functions, types and events of lane 6B; <paramref name="b"/> refuses an id outside <see cref="Lane.Wave6B"/>.</summary>
    private static void RegisterWave6B(ManifestBuilder b)
    {
        // slow: it applies an item (wields, wears, eats ...), so it answers busy before it changes anything and a late reply is applied
        // (docs/mud-phase3.md section 3, rule 10)
        b.AddFunction(Lane.Wave6B, new FunctionDef(OpMudApplyInvObject, "mudapplyinvobject", new[] { "int" }, "bool", Slow: true,
            Handler: (d, args) => d.MudApplyInvObject(args)));

        // every event takes an item or a player as a typed argument, so they are 1.1's
        string player = "handle:" + TMudPlayerMembers.TypeName;
        string item = "handle:" + TMudObjectMembers.TypeName;
        b.AddEvent(Lane.Wave6B, new EventDef(EventOnApplyItem, "player", "sync", new[] { item }, MinMinor: 1));
        b.AddEvent(Lane.Wave6B, new EventDef(EventOnApply, "item", "sync", new[] { player }));
        foreach (string hook in new[]
        {
            EventOnWeaponWielded, EventOnWeaponUnWielded, EventOnWandReadied, EventOnWandUnReadied,
            EventOnArmorWorn, EventOnArmorUnWorn, EventOnClothWorn, EventOnClothUnWorn,
        })
        {
            b.AddEvent(Lane.Wave6B, new EventDef(hook, "player", "sync", new[] { item }, MinMinor: 1));
        }
    }
}
