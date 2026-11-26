// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/living.c <c>change_abil</c> (the effect of an applied or
// removed item on a player, with its messages), <c>change_attr_value</c>, <c>add_statbonus</c>, <c>remove_statbonus</c> and
// <c>allowed_class</c>.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/living.c:
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
using System.Globalization;

namespace Crossfire.Extension.Gk;

/// <summary>
/// common/living.c: what an item that is applied (or taken off) does to the player. THE ENTRY POINT FOR WAVE 6: <c>apply_special</c>
/// sets or clears <c>FLAG_APPLIED</c> on the item and then calls <see cref="ChangeAbil"/> (which runs <see cref="FixPlayer.Fix"/>
/// itself and tells the player what changed); the strings of the player's life and stats follow at the end of the request.
/// </summary>
internal static class LivingChange
{
    /// <summary>living.c <c>change_attr_value</c>: <paramref name="value"/> (which can be negative) is added to the stat <paramref name="attr"/>, wrapping as a C <c>signed char</c> does.</summary>
    public static void ChangeAttr(Living stats, int attr, int value)
    {
        value = (sbyte)value; // the parameter is a signed char
        if (value == 0)
        {
            return;
        }
        if (attr < 0 || attr > 6)
        {
            return; // C logs "Invalid attribute in change_attr_value" and changes nothing
        }
        LivingRules.SetAttr(stats, attr, CfWorld.GetAttrValue(stats, attr) + value);
    }

    /// <summary>living.c <c>allowed_class</c>: true when every stat of the player is 1 or more.</summary>
    public static bool AllowedClass(CfObject op) =>
        op.Stats.Dex > 0 && op.Stats.Str > 0 && op.Stats.Con > 0 && op.Stats.Int > 0 && op.Stats.Wis > 0 && op.Stats.Pow > 0 && op.Stats.Cha > 0;

    /// <summary>living.c <c>add_statbonus</c>: the stat bonuses of the player's archetype (the class the player chose) are added to its stats and to the stats it rolled.</summary>
    public static void AddStatbonus(CfObject op)
    {
        if (op.Contr is not Player pl || op.Arch == null)
        {
            return;
        }
        pl.EnsureStats();
        ShiftStatbonus(op, pl, 1);
        pl.StoreStats();
    }

    /// <summary>living.c <c>remove_statbonus</c>: the opposite of <see cref="AddStatbonus"/>.</summary>
    public static void RemoveStatbonus(CfObject op)
    {
        if (op.Contr is not Player pl || op.Arch == null)
        {
            return;
        }
        pl.EnsureStats();
        ShiftStatbonus(op, pl, -1);
        pl.StoreStats();
    }

    private static void ShiftStatbonus(CfObject op, Player pl, int sign)
    {
        Living clone = op.Arch!.Clone.Stats;
        for (int i = 0; i < 7; i++)
        {
            int bonus = sign * CfWorld.GetAttrValue(clone, i);
            LivingRules.SetAttr(op.Stats, i, CfWorld.GetAttrValue(op.Stats, i) + bonus);
            LivingRules.SetAttr(pl.OrigStats, i, CfWorld.GetAttrValue(pl.OrigStats, i) + bonus);
        }
    }

    private static readonly int[] WatchedFlags =
    {
        Define.FLAG_LIFESAVE, Define.FLAG_REFL_MISSILE, Define.FLAG_REFL_SPELL, Define.FLAG_FLYING, Define.FLAG_UNDEAD, Define.FLAG_STEALTH,
        Define.FLAG_MAKE_INVIS, Define.FLAG_SEE_IN_DARK, Define.FLAG_XRAYS,
    };

    /// <summary>
    /// living.c <c>change_abil</c>: <paramref name="tmp"/> was applied (<c>FLAG_APPLIED</c> is set: flag 1) or taken off (flag -1) by the
    /// player <paramref name="op"/>. A potion changes the rolled stats (and the stats) first; the attack types and spell paths of an
    /// item that goes are taken away; <see cref="FixPlayer.Fix"/> works out everything else; then the player is told, line by line
    /// through <c>DrawInfo</c>, what the item changed (hands glowing, flying, blindness, luck, hit points, the resistances, the
    /// stats). True when anything was changed. The calling function checks that the item can be applied at all.
    /// NOT PORTED: <c>check_walk_on</c> of a player that floats down (the object model has no squares).
    /// </summary>
    public static int ChangeAbil(CfWorld world, CfObject op, CfObject tmp)
    {
        int flag = tmp.QueryFlag(Define.FLAG_APPLIED) ? 1 : -1;
        int success = 0;
        int potionMax = 0;

        // what the player was like before it was changed (C copies the whole object; only the flags and the resistances are compared)
        var before = new bool[WatchedFlags.Length];
        for (int n = 0; n < WatchedFlags.Length; n++)
        {
            before[n] = op.QueryFlag(WatchedFlags[n]);
        }
        short[] refResist = (short[])op.Resist.Clone();
        bool Was(int f) => before[Array.IndexOf(WatchedFlags, f)];

        Player? pl = op.Type == Define.PLAYER ? op.Contr as Player : null;
        if (op.Type == Define.PLAYER && pl != null && tmp.Type == Define.POTION)
        {
            pl.EnsureStats();
            for (int j = 0; j < 7; j++)
            {
                int i = CfWorld.GetAttrValue(pl.OrigStats, j);
                // see whether the stats are within the limits such that this can be applied
                if (i + flag * CfWorld.GetAttrValue(tmp.Stats, j) <= 20 + tmp.Stats.Sp + CfWorld.GetAttrValue(op.Arch?.Clone.Stats ?? new Living(), j) && i > 0)
                {
                    ChangeAttr(pl.OrigStats, j, flag * CfWorld.GetAttrValue(tmp.Stats, j));
                    tmp.Stats.Sp = 0; // fix it up for super potions
                }
                else
                {
                    potionMax = 1; // the potion is useless: the player has already hit the natural maximum
                }
            }
            pl.StoreStats(); // the rolled stats are kept with the account
            // this also ups the character's normal stats (fix_player probably works them out again)
            for (int j = 0; j < 7; j++)
            {
                ChangeAttr(op.Stats, j, flag * CfWorld.GetAttrValue(tmp.Stats, j));
            }
            LivingRules.CheckStatBounds(op.Stats);
        }

        // reset the attributes that fix_player does not reset since it does not search everything to set them
        if (flag == -1)
        {
            op.Attacktype &= ~tmp.Attacktype;
            op.PathAttuned &= ~tmp.PathAttuned;
            op.PathRepelled &= ~tmp.PathRepelled;
            op.PathDenied &= ~tmp.PathDenied;
        }

        // op can have any attribute because of several items: so fix_player, which is called from here
        FixPlayer.Fix(world, op);
        pl?.Touch(); // the strings of the stats are written at the end of the request

        void Say(string text) => world.Host.DrawInfo(op, text);

        if ((tmp.Attacktype & Define.AT_CONFUSION) != 0)
        {
            success = 1;
            Say(flag > 0 ? "Your hands begin to glow red." : "Your hands stop glowing red.");
        }
        if (op.QueryFlag(Define.FLAG_LIFESAVE) != Was(Define.FLAG_LIFESAVE))
        {
            success = 1;
            Say(flag > 0 ? "You feel very protected." : "You don't feel protected anymore.");
        }
        if (op.QueryFlag(Define.FLAG_REFL_MISSILE) != Was(Define.FLAG_REFL_MISSILE))
        {
            success = 1;
            Say(flag > 0 ? "A magic force shimmers around you." : "The magic force fades away.");
        }
        if (op.QueryFlag(Define.FLAG_REFL_SPELL) != Was(Define.FLAG_REFL_SPELL))
        {
            success = 1;
            Say(flag > 0 ? "You feel more safe now, somehow." : "Suddenly you feel less safe, somehow.");
        }
        if (tmp.QueryFlag(Define.FLAG_FLYING))
        {
            success = 1;
            if (flag > 0)
            {
                // if the player was already flying it is now flying higher
                if (op.QueryFlag(Define.FLAG_FLYING) == Was(Define.FLAG_FLYING))
                {
                    Say("You float a little higher in the air.");
                }
                else
                {
                    Say("You start to float in the air!.");
                    op.SetFlag(Define.FLAG_FLYING);
                    if (op.Speed > 1)
                    {
                        op.Speed = 1;
                    }
                }
            }
            else if (op.QueryFlag(Define.FLAG_FLYING) == Was(Define.FLAG_FLYING))
            {
                Say("You float a little lower in the air.");
            }
            else
            {
                Say("You float down to the ground.");
            }
        }

        // becoming undead is special: only those that were not originally undead may change their status
        if (op.Arch == null || !op.Arch.Clone.QueryFlag(Define.FLAG_UNDEAD))
        {
            if (op.QueryFlag(Define.FLAG_UNDEAD) != Was(Define.FLAG_UNDEAD))
            {
                success = 1;
                if (flag > 0)
                {
                    op.Race = "undead";
                    Say("Your lifeforce drains away!");
                }
                else
                {
                    op.Race = op.Arch?.Clone.Race;
                    Say("Your lifeforce returns!");
                }
            }
        }

        if (op.QueryFlag(Define.FLAG_STEALTH) != Was(Define.FLAG_STEALTH))
        {
            success = 1;
            Say(flag > 0 ? "You walk more quietly." : "You walk more noisily.");
        }
        if (op.QueryFlag(Define.FLAG_MAKE_INVIS) != Was(Define.FLAG_MAKE_INVIS))
        {
            success = 1;
            Say(flag > 0 ? "You become transparent." : "You can see yourself.");
        }
        // blinded: you can tell if more blinded since a blinded player has minimal vision
        if (tmp.QueryFlag(Define.FLAG_BLIND))
        {
            success = 1;
            if (flag > 0)
            {
                if (op.QueryFlag(Define.FLAG_WIZ))
                {
                    Say("Your mortal self is blinded.");
                }
                else
                {
                    Say("You are blinded.");
                    op.SetFlag(Define.FLAG_BLIND);
                    LosAgain(pl);
                }
            }
            else if (op.QueryFlag(Define.FLAG_WIZ))
            {
                Say("Your mortal self can now see again.");
            }
            else
            {
                Say("Your vision returns.");
                op.ClearFlag(Define.FLAG_BLIND);
                LosAgain(pl);
            }
        }

        if (op.QueryFlag(Define.FLAG_SEE_IN_DARK) != Was(Define.FLAG_SEE_IN_DARK))
        {
            success = 1;
            Say(flag > 0 ? "Your vision is better in the dark." : "You see less well in the dark.");
            LosAgain(pl);
        }

        if (op.QueryFlag(Define.FLAG_XRAYS) != Was(Define.FLAG_XRAYS))
        {
            success = 1;
            if (flag > 0)
            {
                if (op.QueryFlag(Define.FLAG_WIZ))
                {
                    Say("Your vision becomes a little clearer.");
                }
                else
                {
                    Say("Everything becomes transparent.");
                    LosAgain(pl);
                }
            }
            else if (op.QueryFlag(Define.FLAG_WIZ))
            {
                Say("Your vision becomes a bit out of focus.");
            }
            else
            {
                Say("Everything suddenly looks very solid.");
                LosAgain(pl);
            }
        }
        if (tmp.Stats.Luck != 0)
        {
            success = 1;
            Say(flag > 0 ? "You feel more lucky." : "You feel less lucky.");
        }
        if (tmp.Stats.Hp != 0 && op.Type == Define.PLAYER)
        {
            success = 1;
            Say(flag * tmp.Stats.Hp > 0 ? "You feel much more healthy!" : "You feel much less healthy!");
        }
        if (tmp.Stats.Sp != 0 && op.Type == Define.PLAYER && tmp.Type != Define.SKILL)
        {
            success = 1;
            Say(flag * tmp.Stats.Sp > 0 ? "You feel one with the powers of magic!" : "You suddenly feel very mundane.");
        }
        // for the future when artifacts set this
        if (tmp.Stats.Grace != 0 && op.Type == Define.PLAYER)
        {
            success = 1;
            Say(flag * tmp.Stats.Grace > 0 ? "You feel closer to your god!" : "You suddenly feel less holy.");
        }
        if (tmp.Stats.Food != 0 && op.Type == Define.PLAYER)
        {
            success = 1;
            Say(tmp.Stats.Food * flag > 0 ? "You feel your digestion slowing down." : "You feel your digestion speeding up.");
        }

        // messages for changed resistance
        for (int i = 0; i < Define.NROFATTACKS; i++)
        {
            if (i == Define.ATNR_PHYSICAL)
            {
                continue; // don't display about armour
            }
            if (op.Resist[i] != refResist[i])
            {
                success = 1;
                string verb = op.Resist[i] > refResist[i] ? "rises" : "drops";
                Say("Your resistance to " + Tables.ChangeResistMsg[i] + " " + verb + " to " + op.Resist[i].ToString(CultureInfo.InvariantCulture) + "%.");
            }
        }

        if (tmp.Type != Define.EXPERIENCE && potionMax == 0)
        {
            for (int j = 0; j < 7; j++)
            {
                int i = CfWorld.GetAttrValue(tmp.Stats, j);
                if (i != 0)
                {
                    success = 1;
                    Say(i * flag > 0 ? LivingTables.GainMsg[j] : LivingTables.LoseMsg[j]);
                }
            }
        }
        return success;
    }

    // C: if(op->type==PLAYER) op->contr->do_los=1
    private static void LosAgain(Player? pl)
    {
        if (pl != null)
        {
            pl.DoLos = true;
        }
    }
}
