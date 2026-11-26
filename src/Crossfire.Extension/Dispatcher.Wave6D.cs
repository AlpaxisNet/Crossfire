// Lane 6D of wave 6: signs, books, lighters, treasure chests, and the types 160 and 161 of Graal Kingdoms (docs/mud-phase3.md section 4.4;
// docs/extension.md "Signs, books, lighters and chests"). The apply of each type is the lane's case of C's <c>manual_apply</c> switch
// (server/apply.c), asked by lane 6B through <see cref="ApplyByLane"/>; the ported text of a sign, a book and a lighter is Gk/Readable.cs and
// Gk/Lighter.cs. This file ports <c>apply_treasure</c> (and what C's <c>auto_apply</c> does to a chest), where the contents of a chest
// come out onto the ground of the player's square, which here is a place of a level of the extension (as a drop's, Gk/PickupDrop.cs).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/apply.c:
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
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    /// <summary>Graal Kingdoms' type 160 (furniture): housing is not part of phase 3, so it cannot be applied.</summary>
    internal const int TypeFurniture = 160;

    /// <summary>Graal Kingdoms' type 161 (house doors): housing is not part of phase 3, so it cannot be applied.</summary>
    internal const int TypeHouseDoor = 161;

    /// <summary>The line of an apply of a type of housing.</summary>
    internal const string CannotApplyThat = "You can't apply that.";

    /// <summary>The line of a chest that holds nothing (apply.c <c>apply_treasure</c>).</summary>
    internal const string ChestWasEmpty = "The chest was empty.";

    private partial Outcome? InvokeWave6D(object target, long method, IReadOnlyList<Val> args) => null;

    /// <summary>
    /// The apply of the types of lane 6D (C's <c>manual_apply</c> cases): <c>SIGN</c>, <c>BOOK</c> (a player only), <c>LIGHTER</c> (a
    /// player only), <c>TREASURE</c> (a player only), and Graal Kingdoms' 160 and 161 ("You can't apply that."); 1 when the object was
    /// applied or a line was told, 0 for a monster that cannot apply the type, null for any other type. The caller (lane 6B's entry
    /// points) has answered <c>busy</c> before anything changed.
    /// </summary>
    private partial int? ApplyWave6D(CfObject op, CfObject tmp, int aflag)
    {
        CfWorld w = world!;
        switch (tmp.Type)
        {
            case Define.SIGN:
                if (Readable.ApplySign(w, op, tmp))
                {
                    NoteChanged(tmp);
                }
                return 1;

            case Define.BOOK:
                if (op.Type != Define.PLAYER)
                {
                    return 0;
                }
                Readable.ApplyBook(w, gk?.Skills, op, tmp);
                return 1;

            case Define.LIGHTER:
                if (op.Type != Define.PLAYER)
                {
                    return 0;
                }
                if (Lighter.ApplyLighter(w, op, tmp, NewItemNamed) && op.Contr is Player lighting)
                {
                    lighting.State.Dirty = true;
                    AskForWholeWindow(lighting.State);
                }
                return 1;

            case Define.TREASURE:
                /* monsters mustn't apply random chests, nor magic_mouths with a counter */
                if (op.Type != Define.PLAYER)
                {
                    return 0;
                }
                ApplyTreasure(op, tmp);
                return 1;

            case TypeFurniture:
            case TypeHouseDoor:
                w.Host.DrawInfo(op, CannotApplyThat);
                return 1;

            default:
                return null;
        }
    }

    /// <summary>A new item of the archetype of that name (identified at creation); the singularity of C's <c>get_archetype</c> when there is none.</summary>
    private CfObject NewItemNamed(string name) => world!.FindArchetype(name) is { } at ? NewItem(at) : world.CreateSingularity(name);

    /// <summary>An object of a level or an inventory changed in a way that its file has to be written: the level, or the account of the player who holds it.</summary>
    private static void NoteChanged(CfObject ob)
    {
        if (ob.Env == null)
        {
            if (ob.Map is Level level)
            {
                level.Dirty = true;
            }
        }
        else if (CfWorld.IsPlayerInv(ob.Env)?.Contr is Player holder)
        {
            holder.State.Dirty = true;
        }
    }

    /// <summary>The object of a level that is the face of <paramref name="ob"/> (an object on the ground), or null.</summary>
    private static LevelObject? GroundOf(CfObject ob) => ob.Env == null && ob.Map is Level level ? level.Objects.Find(lo => lo.Ob == ob) : null;

    /// <summary>
    /// What C's <c>fix_auto_apply</c> does to a chest when its map is loaded for the first time (<c>create_treasure(randomitems)</c>,
    /// <c>stats.hp</c> times), done at the first apply of a chest that no map load has filled (one made by <see cref="NewItem"/>:
    /// Alpaxis's choice, docs/differences.md). The things made are identified, as every item of the engine is.
    /// </summary>
    private void FillChest(CfObject chest)
    {
        if (chest.Inv != null || chest.Randomitems == null || chest.QueryFlag(Define.FLAG_AUTO_APPLY))
        {
            return;
        }
        int difficulty = chest.Map is Level level ? level.Header.Difficulty : 14;
        while (chest.Stats.Hp-- > 0)
        {
            world!.CreateTreasure(chest.Randomitems, chest, 0, difficulty, 0);
        }
        for (CfObject? c = chest.Inv; c != null; c = c.Below)
        {
            c.SetFlag(Define.FLAG_IDENTIFIED);
        }
    }

    /// <summary>
    /// apply.c <c>apply_treasure</c>: the contents of the chest <paramref name="tmp"/> come out one by one ("You find X in the chest.")
    /// onto the square of the player <paramref name="op"/>, and the empty chest goes (one of a stack). An empty chest says so and goes.
    /// The square is the player's feet (<c>player.pos</c>, as a drop asks for it); when the extension holds no level there (or the
    /// host did not answer) nothing changes and the player is told "You can't drop that here.". C's spring of a trap found in the chest
    /// (<c>RUNE</c>, phase 4) is not ported.
    /// </summary>
    private void ApplyTreasure(CfObject op, CfObject tmp)
    {
        CfWorld w = world!;
        var contr = (Player)op.Contr!;
        PlayerState ps = contr.State;
        LevelObject? chestFace = GroundOf(tmp);
        FillChest(tmp);

        CfObject? treas = tmp.Inv;
        if (treas == null)
        {
            w.Host.DrawInfo(op, ChestWasEmpty);
            DecreaseChest(ps, tmp, chestFace);
            return;
        }

        // the place is known before anything changes: a chest that cannot be emptied stays as it is
        DropSite feet = FeetOf(ps);
        DropPlace? place = feet.Resolve();
        if (place == null)
        {
            w.Host.DrawInfo(op, Pickup.CannotDropHere);
            return;
        }

        uint tmpTag = tmp.Count;
        uint opTag = op.Count;
        do
        {
            w.RemoveOb(treas);
            w.Host.DrawInfo(op, "You find " + w.QueryName(treas) + " in the chest.");
            InsertObInMap(treas, place);
            if (CfWorld.WasDestroyed(op, opTag) || CfWorld.WasDestroyed(tmp, tmpTag))
            {
                break;
            }
        }
        while ((treas = tmp.Inv) != null);

        if (!CfWorld.WasDestroyed(tmp, tmpTag) && tmp.Inv == null)
        {
            DecreaseChest(ps, tmp, chestFace);
        }

        // the contents crossed from a file to the level's: the source is written first
        if (chestFace == null)
        {
            SubmitMove(null, ps, place.Level, null);
            AskForWholeWindow(ps);
        }
        else if (chestFace.Level != place.Level)
        {
            SubmitMove(chestFace.Level, null, place.Level, null);
        }
    }

    /// <summary>
    /// <c>decrease_ob(chest)</c>: one chest of the stack goes, and the last one leaves its place (the player's tree, with its tag, or
    /// the level, with its face) and is freed.
    /// </summary>
    private void DecreaseChest(PlayerState ps, CfObject chest, LevelObject? face)
    {
        if (chest.Nrof > 1)
        {
            world!.DecreaseObNr(chest, 1);
            if (face != null)
            {
                GroundChanged(face);
            }
            else
            {
                ps.Dirty = true;
            }
        }
        else if (face != null)
        {
            DestroyObject(face, removeNpc: true);
        }
        else
        {
            RemoveStack(ps, chest);
            ps.Dirty = true;
        }
    }
}
