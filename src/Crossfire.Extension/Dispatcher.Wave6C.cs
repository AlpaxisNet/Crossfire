// Not ported from a Crossfire file: the methods of lane 6C of wave 6, food, drink, potions and poison (docs/extension.md, "Methods of the
// lanes"), and the apply of their types for <c>manual_apply</c> ("Apply by lane"). The lane owns this file; its method ids are in the
// ranges of Lane.Wave6C. It has no function and no method of its own: the types are applied by 6B's switch, which asks
// <see cref="ApplyWave6C"/> first.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    private partial Outcome? InvokeWave6C(object target, long method, IReadOnlyList<Val> args) => null;

    /// <summary>
    /// apply.c <c>manual_apply</c>'s cases of DRINK, FOOD, FLESH, POTION and POISON (all return 1: dealt with, or an error was told);
    /// null for any other type. A request that applies an item has checked that the worker is not busy (the functions that call this are
    /// <c>Slow</c> or call <c>CheckBusy()</c> first, mud-phase3.md section 3 rule 10): nothing here changes before that.
    /// </summary>
    private partial int? ApplyWave6C(CfObject op, CfObject tmp, int aflag)
    {
        CfWorld w = world!;
        switch (tmp.Type)
        {
            case Define.DRINK:
            case Define.FOOD:
            case Define.FLESH:
                Consumable.ApplyFood(w, op, tmp, EmitEatFood);
                return 1;
            case Define.POISON:
                Consumable.ApplyPoison(w, op, tmp);
                return 1;
            case Define.POTION:
                tmp.SetFlag(Define.FLAG_APPLIED);
                gk!.Skills?.CheckSkillToApply(w, op, tmp);
                tmp.ClearFlag(Define.FLAG_APPLIED);
                Consumable.ApplyPotion(w, op, tmp);
                return 1;
            default:
                return null;
        }
    }

    /// <summary>
    /// The player event <c>onEatFood(foodarch, food, wascursed)</c> (mud-phase3.md 4.4), after the state changed and the food was used up
    /// (the handle of a food that is gone is nil in the script, as for any dead handle). Only on a link of 1.1 (the event has typed
    /// parameters) and for a player that is logged in.
    /// </summary>
    private void EmitEatFood(CfObject who, Archetype? arch, CfObject food, bool wasCursed)
    {
        if (!Speaks11 || who.Contr is not Player p || manifest.FindEvent(Manifest.EventOnEatFood) == null)
        {
            return;
        }
        EmitPlayerEvent(Manifest.EventOnEatFood, p.Account, new[]
        {
            arch == null ? Val.Nil : Val.Handle(handles.HandleOf(arch)),
            Val.Handle(((IMemberEnv)this).ItemHandleOf(food)),
            Val.Int(wasCursed ? 1 : 0),
        });
    }
}
