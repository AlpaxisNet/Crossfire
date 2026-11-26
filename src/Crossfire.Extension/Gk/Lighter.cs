// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): lighting an object with a lighter, server/apply.c <c>apply_lighter</c>,
// and the saving throw of an object against fire that it calls, server/attack.c <c>did_make_save_item</c> and <c>save_throw_object</c>
// (docs/mud-phase3.md section 4.4, lane 6D).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/apply.c:
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//
//     server/attack.c:
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
// ---- end of notices ----
namespace Crossfire.Extension.Gk;

/// <summary>
/// The lighter (server/apply.c <c>apply_lighter</c>). The object to light is the player's marked object, which is always an object of
/// his inventory, and an inventory has no map: of <c>save_throw_object</c> only the path of a fire attack on an object that has an
/// environment is ported (<see cref="SaveThrowObjectFire"/>); the branches for the other attacks and for objects on a map are not
/// reached from here. Items the port makes are made by the dispatcher (a <c>newItem</c> function: identified at creation).
/// </summary>
internal static class Lighter
{
    /// <summary>The line of a player who has marked nothing.</summary>
    public const string NeedMark = "You need to mark a lightable object.";

    /// <summary>common/utils.c <c>rndm</c>: a number from <paramref name="min"/> to <paramref name="max"/>; <paramref name="min"/> for an empty range.</summary>
    private static int Rndm(CfWorld w, int min, int max)
    {
        int diff = max - min + 1;
        if (max < 1 || diff < 1)
        {
            return min;
        }
        return w.Random() % diff + min;
    }

    /// <summary>
    /// apply.c <c>apply_lighter</c>: <paramref name="who"/> lights his marked object with <paramref name="lighter"/>. A lighter with
    /// charges (<c>last_eat</c> set and <c>stats.food</c> the charges) uses one up; of a stack of such lighters one is split off and
    /// loses the charge. Returns true when the lighter or the inventory changed (so that the player's file is written).
    /// </summary>
    public static bool ApplyLighter(CfWorld w, CfObject who, CfObject lighter, Func<string, CfObject> newItem)
    {
        CfObject? item = ItemMatching.FindMarkedObject(who);
        if (item == null)
        {
            /* nothing to light */
            w.Host.DrawInfo(who, NeedMark);
            return false;
        }
        bool changed = false;
        if (lighter.LastEat != 0 && lighter.Stats.Food != 0)
        {
            /* lighter gets used up */
            /* Split multiple lighters if they're being used up.  Otherwise one charge from each would be used up.  --DAMN */
            if (lighter.Nrof > 1)
            {
                CfObject oneLighter = w.GetObject();
                w.CopyObject(lighter, oneLighter);
                lighter.Nrof -= 1; // C does not change the weight carried here: the next fix_weight makes it right
                oneLighter.Nrof = 1;
                oneLighter.Stats.Food--;
                w.Host.EsrvSendItem(who, lighter);
                oneLighter = w.InsertObInOb(oneLighter, who);
                w.Host.EsrvSendItem(who, oneLighter);
            }
            else
            {
                lighter.Stats.Food--;
            }
            changed = true;
        }
        else if (lighter.LastEat != 0)
        {
            /* no charges left in lighter */
            w.Host.DrawInfo(who, "You attempt to light the " + item.Name + " with a used up " + lighter.Name + ".");
            return false;
        }
        /* Perhaps we should split what we are trying to light on fire? I can't see many times when you would want to light multiple
         * objects at once. */
        uint nrof = item.Nrof;
        uint count = item.Count;
        /* If the item is destroyed, we don't have a valid pointer to the name object, so make a copy so the message we print out makes
         * some sense. */
        string itemName = item.Name ?? "";
        bool isPlayerEnv = CfWorld.IsPlayerInv(item) == who;

        SaveThrowObjectFire(w, item, who, newItem);
        /* Change to check count and not freed, since the object pointer may have gotten recycled */
        if (nrof != item.Nrof || CfWorld.WasDestroyed(item, count))
        {
            w.Host.DrawInfo(who, "You light the " + itemName + " with the " + lighter.Name + ".");
            /* Need to update the player so that the players glow radius gets changed. */
            if (isPlayerEnv)
            {
                w.Host.FixPlayer(who);
            }
        }
        else
        {
            w.Host.DrawInfo(who, "You attempt to light the " + item.Name + " with the " + lighter.Name + " and fail.");
        }
        return changed || nrof != item.Nrof || CfWorld.WasDestroyed(item, count);
    }

    /// <summary>
    /// attack.c <c>did_make_save_item</c> for an attack of type <c>AT_FIRE</c>: true when the object kept its form. Each material of the
    /// object saves or not by <c>material[i].save</c>, the object's magic and its resistance to fire; an attack that is too weak
    /// (the originator's <c>stats.dam</c>) saves once more per material.
    /// </summary>
    public static bool DidMakeSaveItemFire(CfWorld w, CfObject op, CfObject originator)
    {
        int saves = 0;
        int materials = 0;
        const int number = Define.ATNR_FIRE;

        /* If the object is immune, no effect */
        if (op.Resist[number] == 100)
        {
            return true;
        }

        for (int i = 0; i < Define.NROFMATERIALS; i++)
        {
            if ((op.Material & (1 << i)) != 0)
            {
                materials++;
                if (Rndm(w, 1, 20) >= Tables.Material[i].Save[number] - op.Magic - op.Resist[number] / 100)
                {
                    saves++;
                }
                /* if the attack is too weak */
                if ((20 - Tables.Material[i].Save[number]) / 3 > originator.Stats.Dam)
                {
                    saves++;
                }
            }
        }
        if (saves == materials || materials == 0)
        {
            return true;
        }
        if (saves == 0 || Rndm(w, 1, materials) > saves)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// attack.c <c>save_throw_object(op, AT_FIRE, originator)</c> for an object in an inventory (<c>op.Env</c> is set, <c>op.Map</c> is
    /// not: C's <c>stop_item</c> returns the object as it is). An object that fails its throw and glows (<c>glow_radius</c>, with an
    /// <c>other_arch</c>) becomes one of its <c>other_arch</c>; any other burns: a stack loses a random number of its objects (0 to
    /// nrof - 1), a single object is destroyed, and a <c>burnout</c> comes into the same environment.
    /// </summary>
    public static void SaveThrowObjectFire(CfWorld w, CfObject op, CfObject originator, Func<string, CfObject> newItem)
    {
        if (DidMakeSaveItemFire(w, op, originator))
        {
            return;
        }
        CfObject? env = op.Env;
        if (env == null)
        {
            // an object on a map is not lit by a lighter of an inventory
            w.Log(LogLevel.Error, "BUG: save_throw_object(): an object without an environment is not ported\n");
            return;
        }

        /* Hacked the following so that type LIGHTER will work. Also, objects which are potenital "lights" that are hit by flame/elect
         * attacks will be set to glow. "lights" are any object with +/- glow_radius and an "other_arch" to change to. (and please note
         * that we cant fail our save and reach this function if the object doesnt contain a material that can burn. So forget
         * lighting magical swords on fire with this!) -b.t. */
        if (op.OtherArch != null && op.GlowRadius != 0)
        {
            string arch = op.OtherArch.Name!;
            w.DecreaseObNr(op, 1);
            CfObject made = newItem(arch);
            w.InsertObInOb(made, env);
            if (env.Contr != null)
            {
                w.Host.EsrvSendItem(env, made);
            }
            return;
        }
        if (op.Nrof > 1)
        {
            w.DecreaseObNr(op, Rndm(w, 0, (int)op.Nrof - 1));
        }
        else
        {
            CfObject? tmp = CfWorld.IsPlayerInv(op.Env);
            if (tmp != null)
            {
                w.Host.EsrvDelItem(tmp.Contr, op.Count);
                w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, tmp, tmp);
            }
            if (!op.QueryFlag(Define.FLAG_REMOVED))
            {
                w.RemoveOb(op);
            }
            w.FreeObject(op);
        }
        w.InsertObInOb(newItem("burnout"), env);
    }
}
