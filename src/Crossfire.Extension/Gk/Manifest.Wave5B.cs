// Not ported from a Crossfire file: what lane 5B of wave 5 (the inventory functions, docs/mud-phase3.md section 4.2) registers in the
// manifest of the Crossfire extension. The lane owns this file; its ids are the ranges of Lane.Wave5B (docs/extension.md, "Lanes and
// id ranges"). The code of the functions is in Dispatcher.Wave5B.cs, the methods of the types in Gk/TMudObjectMembers.Wave5B.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;

namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The operation of <c>mudaddinvobject(count, archetype)</c>.</summary>
    public const int OpMudAddInvObject = 5000;

    /// <summary>The operation of <c>mudremoveinvobject(count, archetype)</c>.</summary>
    public const int OpMudRemoveInvObject = 5001;

    /// <summary>The operation of <c>mudcountinvobject(archetype, deep)</c>.</summary>
    public const int OpMudCountInvObject = 5002;

    /// <summary>The operation of <c>mudfindinvobject(text)</c>.</summary>
    public const int OpMudFindInvObject = 5003;

    /// <summary>The operation of <c>mudresetinvobject(id)</c>.</summary>
    public const int OpMudResetInvObject = 5004;

    /// <summary>The operation of <c>mudorderinventory(ids)</c>.</summary>
    public const int OpMudOrderInventory = 5005;

    /// <summary>The operation of <c>findMudObject(id)</c>.</summary>
    public const int OpFindMudObject = 5006;

    /// <summary>The operation of <c>mudhaveskill(name)</c>.</summary>
    public const int OpMudHaveSkill = 5007;

    /// <summary>The operation of <c>mudskilllevel(name)</c>.</summary>
    public const int OpMudSkillLevel = 5008;

    /// <summary>The operations of <c>mudlistweapons</c>, <c>mudlistskills</c>, <c>mudlistspells</c> and <c>mudlistequipment</c>: they write into the calling NPC (1.1).</summary>
    public const int OpMudListWeapons = 5009, OpMudListSkills = 5010, OpMudListSpells = 5011, OpMudListEquipment = 5012;

    /// <summary>
    /// The functions of lane 5B; <paramref name="b"/> refuses an id outside <see cref="Lane.Wave5B"/>. The ones that add, remove or
    /// change items are slow: they answer busy, before anything changes, while the writer is saving (docs/mud-phase3.md section 3,
    /// rule 10). The four lists write with the effect <c>caller.setprop</c>, which a 1.0 link does not have: below 1.1 they are not in
    /// the manifest, and the host's script has no such function.
    /// </summary>
    private static void RegisterWave5B(ManifestBuilder b)
    {
        string item = "handle:" + TMudObjectMembers.TypeName;
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudAddInvObject, "mudaddinvobject", new[] { "int", "string" }, "bool", Slow: true, Handler: (d, a) => d.MudAddInvObject(a)));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudRemoveInvObject, "mudremoveinvobject", new[] { "int", "string" }, "bool", Slow: true, Handler: (d, a) => d.MudRemoveInvObject(a)));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudCountInvObject, "mudcountinvobject", new[] { "string", "bool" }, "int", Handler: (d, a) => d.MudCountInvObject(a)));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudFindInvObject, "mudfindinvobject", new[] { "string" }, "int", Handler: (d, a) => d.MudFindInvObject(a)));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudResetInvObject, "mudresetinvobject", new[] { "int" }, "bool", Slow: true, Handler: (d, a) => d.MudResetInvObject(a)));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudOrderInventory, "mudorderinventory", new[] { "any" }, "void", Handler: (d, a) => d.MudOrderInventory(a)));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpFindMudObject, "findMudObject", new[] { "int" }, item, Handler: (d, a) => d.FindMudObject(a)));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudHaveSkill, "mudhaveskill", new[] { "string" }, "bool", Handler: (d, a) => d.MudHaveSkill(a)));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudSkillLevel, "mudskilllevel", new[] { "string" }, "int", Handler: (d, a) => d.MudSkillLevel(a)));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudListWeapons, "mudlistweapons", Array.Empty<string>(), "void", Handler: (d, a) => d.MudListWeapons(a), MinMinor: 1));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudListSkills, "mudlistskills", Array.Empty<string>(), "void", Handler: (d, a) => d.MudListSkills(a), MinMinor: 1));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudListSpells, "mudlistspells", Array.Empty<string>(), "void", Handler: (d, a) => d.MudListSpells(a), MinMinor: 1));
        b.AddFunction(Lane.Wave5B, new FunctionDef(OpMudListEquipment, "mudlistequipment", Array.Empty<string>(), "void", Handler: (d, a) => d.MudListEquipment(a), MinMinor: 1));
    }
}
