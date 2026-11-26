// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/treasure.c, the making of the items of a treasure list.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/treasure.c:
//         Copyright (C) 2000 Mark Wedel
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
namespace Crossfire;

public sealed partial class CfWorld
{
    /// <summary>
    /// treasure.c <c>put_treasure</c> (static): puts a made item where the flags say: on the map square of the creator
    /// (<c>GT_ENVIRONMENT</c>) or into the creator, which a monster then applies (<c>GT_APPLY</c>) and the client of a player is
    /// sent (<c>GT_UPDATE_INV</c>).
    /// </summary>
    private void PutTreasure(CfObject op, CfObject creator, int flags)
    {
        if ((flags & GT.ENVIRONMENT) != 0)
        {
            op.X = creator.X;
            op.Y = creator.Y;
            op.SetFlag(Define.FLAG_OBJ_ORIGINAL);
            if (creator.Map != null)
            {
                Host.InsertObInMap(op, creator.Map, op, Define.INS_NO_MERGE | Define.INS_NO_WALK_ON);
            }
        }
        else
        {
            op = InsertObInOb(op, creator);
            if ((flags & GT.APPLY) != 0 && creator.QueryFlag(Define.FLAG_MONSTER))
            {
                Host.MonsterCheckApply(creator, op);
            }
            CfObject? tmp;
            if ((flags & GT.UPDATE_INV) != 0 && (tmp = IsPlayerInv(creator)) != null)
            {
                Host.EsrvSendItem(tmp, op);
            }
        }
    }

    /// <summary>treasure.c <c>change_treasure</c> (static): the <c>change_name</c>, <c>change_title</c> and <c>change_slaying</c> lines of the list element are put into the made object.</summary>
    private static void ChangeTreasure(Treasure t, CfObject op)
    {
        if (t.ChangeName != null)
        {
            op.Name = t.ChangeName;
            // not great, but better than something that is completely wrong
            op.NamePl = t.ChangeName;
        }
        if (t.ChangeTitle != null)
        {
            op.Title = t.ChangeTitle;
        }
        if (t.ChangeSlaying != null)
        {
            op.Slaying = t.ChangeSlaying;
        }
    }

    // the part of create_all_treasures and create_one_treasure that makes one item of the element
    private void MakeTreasureItem(Treasure t, CfObject op, int flag, int difficulty)
    {
        CfObject tmp = ArchToObject(t.Item)!;
        if (t.Nrof != 0 && tmp.Nrof <= 1)
        {
            tmp.Nrof = (uint)(Random() % t.Nrof + 1);
        }
        FixGeneratedItem(tmp, op, difficulty, t.Magic, flag);
        ChangeTreasure(t, tmp);
        PutTreasure(tmp, op, flag);
    }

    /// <summary>
    /// treasure.c <c>create_all_treasures</c>: makes the items of the element and of the ones after it, each with the chance it has.
    /// An element that names no archetype and no list (C would read through the null pointer) makes nothing.
    /// </summary>
    public void CreateAllTreasures(Treasure t, CfObject op, int flag, int difficulty, int tries)
    {
        if (t.Chance >= 100 || Random() % 100 + 1 < t.Chance)
        {
            if (t.Name != null)
            {
                if (t.Name != "NONE" && difficulty >= t.Magic)
                {
                    CreateTreasure(FindTreasurelist(t.Name), op, flag, difficulty, tries);
                }
            }
            else if (t.Item != null && (t.Item.Clone.Invisible != 0 || (flag & GT.INVISIBLE) == 0))
            {
                MakeTreasureItem(t, op, flag, difficulty);
            }
            if (t.NextYes != null)
            {
                CreateAllTreasures(t.NextYes, op, flag, difficulty, tries);
            }
        }
        else if (t.NextNo != null)
        {
            CreateAllTreasures(t.NextNo, op, flag, difficulty, tries);
        }
        if (t.Next != null)
        {
            CreateAllTreasures(t.Next, op, flag, difficulty, tries);
        }
    }

    /// <summary>treasure.c <c>create_one_treasure</c>: makes one of the items of a <c>treasureone</c> list, chosen by the chances of its elements.</summary>
    public void CreateOneTreasure(TreasureList tl, CfObject op, int flag, int difficulty, int tries)
    {
        int value = Random() % tl.TotalChance;
        Treasure? t;

        if (tries++ > 100)
        {
            return;
        }
        for (t = tl.Items; t != null; t = t.Next)
        {
            value -= t.Chance;
            if (value < 0)
            {
                break;
            }
        }
        if (t == null || value >= 0)
        {
            Log(LogLevel.Error, "create_one_treasure: got null object or not able to find treasure\n");
            throw new CrossfireFatalException("create_one_treasure: abort()");
        }
        if (t.Name != null)
        {
            if (t.Name == "NONE")
            {
                return;
            }
            if (difficulty >= t.Magic)
            {
                CreateTreasure(FindTreasurelist(t.Name), op, flag, difficulty, tries);
            }
            else if (t.Nrof != 0)
            {
                CreateOneTreasure(tl, op, flag, difficulty, tries);
            }
            return;
        }
        if (t.Item != null && (t.Item.Clone.Invisible != 0 || flag != GT.INVISIBLE))
        {
            MakeTreasureItem(t, op, flag, difficulty);
        }
    }

    /// <summary>
    /// treasure.c <c>create_treasure</c>: makes the items of a treasure list into <paramref name="op"/> (or on its square). A list
    /// that does not exist (<see cref="FindTreasurelist"/> gave null; C would read through the null pointer) makes nothing.
    /// <paramref name="tries"/> counts the list transitions.
    /// </summary>
    public void CreateTreasure(TreasureList? t, CfObject op, int flag, int difficulty, int tries)
    {
        if (t == null || tries++ > 100)
        {
            return;
        }
        if (t.TotalChance != 0)
        {
            CreateOneTreasure(t, op, flag, difficulty, tries);
        }
        else if (t.Items != null)
        {
            CreateAllTreasures(t.Items, op, flag, difficulty, tries);
        }
    }

    /// <summary>
    /// treasure.c <c>fix_generated_item</c>, the part phase 3 needs: what is done after an item of a list was made, for the flag
    /// <c>GT_STARTEQUIP</c> (a single item that is not a container, money or thrown gets the flag STARTEQUIP, anything else but
    /// money has its value cleared) and, unless <c>GT_ENVIRONMENT</c>, <see cref="FixFleshItem"/>. NOT ported (docs/differences.md):
    /// the random magic (<c>set_magic</c>, so <paramref name="maxMagic"/> is not used), the artifacts and the properties that the
    /// type switch gives rings, amulets, wands, rods, scrolls, books, spellbooks, potions, runes and cursed weapons and armour: such
    /// an item is as its archetype is.
    /// </summary>
    public void FixGeneratedItem(CfObject op, CfObject? creator, int difficulty, int maxMagic, int flags)
    {
        if (creator == null || creator.Type == op.Type)
        {
            creator = op; // safety & to prevent polymorphed objects giving attributes
        }
        if ((flags & GT.STARTEQUIP) != 0)
        {
            if (op.Nrof < 2 && op.Type != Define.CONTAINER && op.Type != Define.MONEY && !op.QueryFlag(Define.FLAG_IS_THROWN))
            {
                op.SetFlag(Define.FLAG_STARTEQUIP);
            }
            else if (op.Type != Define.MONEY)
            {
                op.Value = 0;
            }
        }
        if ((flags & GT.ENVIRONMENT) == 0)
        {
            FixFleshItem(op, creator);
        }
    }

    /// <summary>
    /// treasure.c <c>fix_flesh_item</c>: flesh made from a creature is named after it and gets its weight, level and part of its
    /// resistances; flesh of a creature that bites with poison is poisonous.
    /// </summary>
    public void FixFleshItem(CfObject item, CfObject? donor)
    {
        if (item.Type == Define.FLESH && donor != null)
        {
            // change the name
            item.Name = donor.Name + "'s " + item.Name;
            item.NamePl = donor.Name + "'s " + item.NamePl;

            // weight is FLESH weight/100 * donor
            if ((item.Weight = unchecked((int)(((double)item.Weight / 100.0) * donor.Weight))) == 0)
            {
                item.Weight = 1;
            }

            // value is multiplied by level of donor
            item.Value *= Isqrt(donor.Level * 2);

            // food value
            item.Stats.Food += (short)(donor.Stats.Hp / 100 + donor.Stats.Con);

            // flesh items inherit some abilities of donor, but not full effect
            for (int i = 0; i < Define.NROFATTACKS; i++)
            {
                item.Resist[i] = (sbyte)(donor.Resist[i] / 2);
            }

            // item inherits donor's level (important for quezals)
            item.Level = donor.Level;

            // if donor has some attacktypes, the flesh is poisonous
            if ((donor.Attacktype & Define.AT_POISON) != 0)
            {
                item.Type = Define.POISON;
            }
            if ((donor.Attacktype & Define.AT_ACID) != 0)
            {
                item.Stats.Hp = (short)(-1 * item.Stats.Food);
            }
            item.SetFlag(Define.FLAG_NO_STEAL);
        }
    }
}
