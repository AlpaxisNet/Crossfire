// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/living.c <c>fix_player</c>, for players: the stats, the hit,
// spell and grace points (wave 4D), the resistances, armour and weapon class, damage, speed, flags and regeneration (wave 5D), and
// what the player may carry.
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
namespace Crossfire.Extension.Gk;

/// <summary>
/// common/living.c <c>fix_player</c>, the one entry point of the extension (<see cref="GkHost.FixPlayer"/> calls it when an item
/// comes into or goes out of a player). For a player whose stats were rolled it is the whole of the C function: the stats come back
/// from <c>orig_stats</c> and the applied items' stat bonuses are added; <c>check_stat_bounds</c>; <c>maxhp</c>, <c>maxsp</c> and
/// <c>maxgrace</c> from the levels; the resistances (protections, vulnerabilities and the strongest potion effect), armour class,
/// weapon class, damage, speed (with the weight carried and the disease), the flags the applied items give, the light, the
/// digestion and regeneration values and <c>weapon_sp</c>; then the weight limit. NOT PORTED: the part for monsters (a monster is
/// left as it is), <c>PLUGINS</c> (<c>current_weapon_script</c>). A player whose stats were never rolled keeps everything but the
/// weight limit (docs/differences.md).
/// </summary>
internal static class FixPlayer
{
    /// <summary>living.c <c>MAX(a, b)</c> for the resistances.</summary>
    private static int Max(int a, int b) => a > b ? a : b;

    /// <summary>fix_player for a player. A player whose stats were never rolled (<see cref="Player.StatsRolled"/>) keeps its stats, life, armour class and speed as they are: only the weight limit is set.</summary>
    public static void Fix(CfWorld world, CfObject op)
    {
        if (op.Type != Define.PLAYER || op.Contr is not Player pl)
        {
            return; // the part for monsters is not ported
        }
        if (pl.StatsRolled)
        {
            FixRolled(world, op, pl);
        }
        // c_object.c: the most the player can pick up, by Str
        pl.WeightLimit = ItemMatching.WeightLimitOf(op.Stats.Str);
    }

    private static void FixRolled(CfWorld world, CfObject op, Player pl)
    {
        CfObject clone = op.Arch?.Clone ?? new CfObject();
        int light = 0;
        float max = 9;
        float addedSpeed = 0;
        float bonusSpeed = 0;
        float speedReduceFromDisease = 1;
        int weaponWeight = 0;
        int weaponSpeed = 0;
        int bestWc = 0;
        int bestAc = 0;
        int wc;
        int ac;
        var prot = new int[Define.NROFATTACKS];
        var vuln = new int[Define.NROFATTACKS];
        var potionResist = new int[Define.NROFATTACKS];
        CfObject? graceObj = null;
        CfObject? manaObj = null;
        CfObject? wcObj = null;

        for (int i = 0; i < 7; i++)
        {
            LivingRules.SetAttr(op.Stats, i, CfWorld.GetAttrValue(pl.OrigStats, i));
        }
        pl.Encumbrance = 0; // SPELL_ENCUMBRANCE
        if (op.ChosenSkill != null && op.ChosenSkill.ExpObj != null)
        {
            op.ChosenSkill.Level = op.ChosenSkill.ExpObj.Level;
        }
        op.Attacktype = 0;
        pl.Digestion = 0;
        pl.GenHp = 0;
        pl.GenSp = 0;
        pl.GenGrace = 0;
        pl.GenSpArmour = 10;
        op.Slaying = null;
        if (!op.QueryFlag(Define.FLAG_WIZ))
        {
            if (!clone.QueryFlag(Define.FLAG_FLYING))
            {
                op.ClearFlag(Define.FLAG_FLYING);
            }
            op.ClearFlag(Define.FLAG_XRAYS);
            op.ClearFlag(Define.FLAG_MAKE_INVIS);
        }
        op.ClearFlag(Define.FLAG_LIFESAVE);
        op.ClearFlag(Define.FLAG_STEALTH);
        op.ClearFlag(Define.FLAG_BLIND);
        if (!clone.QueryFlag(Define.FLAG_REFL_SPELL))
        {
            op.ClearFlag(Define.FLAG_REFL_SPELL);
        }
        if (!clone.QueryFlag(Define.FLAG_REFL_MISSILE))
        {
            op.ClearFlag(Define.FLAG_REFL_MISSILE);
        }
        if (!clone.QueryFlag(Define.FLAG_UNDEAD))
        {
            op.ClearFlag(Define.FLAG_UNDEAD);
        }
        if (!clone.QueryFlag(Define.FLAG_SEE_IN_DARK))
        {
            op.ClearFlag(Define.FLAG_SEE_IN_DARK);
        }
        op.PathAttuned = clone.PathAttuned;
        op.PathRepelled = clone.PathRepelled;
        op.PathDenied = clone.PathDenied;

        // initializing resistances from the values in the archetype's clone
        Array.Copy(clone.Resist, op.Resist, Define.NROFATTACKS);
        for (int i = 0; i < Define.NROFATTACKS; i++)
        {
            if (op.Resist[i] > 0)
            {
                prot[i] = op.Resist[i];
                vuln[i] = 0;
            }
            else
            {
                vuln[i] = -op.Resist[i];
                prot[i] = 0;
            }
            potionResist[i] = 0;
        }

        wc = clone.Stats.Wc;
        op.Stats.Dam = clone.Stats.Dam;

        if (!op.QueryFlag(Define.FLAG_USE_ARMOUR))
        {
            // for players which cannot use armour, they gain AC -1 per 3 levels, plus a small amount of physical resist
            ac = Math.Max(-10, clone.Stats.Ac - op.Level / 3);
            // C indexes prot[] with AT_PHYSICAL (the bit mask 1, so the magic entry), not ATNR_PHYSICAL: kept as it is
            prot[Define.ATNR_PHYSICAL] += ((100 - prot[(int)Define.AT_PHYSICAL]) * (80 * op.Level / LivingRules.MaxLevel)) / 100;
        }
        else
        {
            ac = clone.Stats.Ac;
        }

        op.Stats.Luck = clone.Stats.Luck;
        op.Speed = clone.Speed;

        for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
        {
            // light sources are never applied, so the check is here
            if (tmp.GlowRadius > light)
            {
                light = tmp.GlowRadius;
            }
            if (!tmp.QueryFlag(Define.FLAG_APPLIED) || tmp.Type == Define.CONTAINER || tmp.Type == Define.CLOSE_CON)
            {
                continue;
            }
            // the meaning of stats in skill or experience objects is different: they link skills to experience; an exception is exp_wis
            if (tmp.Type != Define.EXPERIENCE || tmp.Arch?.Name == "experience_wis")
            {
                if (tmp.Type != Define.POTION)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        LivingChange.ChangeAttr(op.Stats, i, CfWorld.GetAttrValue(tmp.Stats, i));
                    }
                }
                // the items that can change digestion, regeneration, spell point recovery and mana point recovery
                if (tmp.Type == Define.EXPERIENCE || tmp.Type == Define.WEAPON || tmp.Type == Define.ARMOUR || tmp.Type == Define.HELMET
                    || tmp.Type == Define.SHIELD || tmp.Type == Define.RING || tmp.Type == Define.BOOTS || tmp.Type == Define.GLOVES
                    || tmp.Type == Define.AMULET || tmp.Type == Define.GIRDLE || tmp.Type == Define.BRACERS || tmp.Type == Define.CLOAK
                    || tmp.Type == Define.DISEASE || tmp.Type == Define.FORCE)
                {
                    pl.Digestion += tmp.Stats.Food;
                    pl.GenHp += tmp.Stats.Hp;
                    pl.GenSp += tmp.Stats.Sp;
                    pl.GenGrace += tmp.Stats.Grace;
                    // experience objects use last_heal for permanent exp, so don't add those in
                    if (tmp.Type != Define.EXPERIENCE)
                    {
                        pl.GenSpArmour += tmp.LastHeal;
                    }
                }
            }

            if (tmp.Type == Define.SYMPTOM)
            {
                speedReduceFromDisease = (float)(tmp.LastSp / 100.0);
                if (speedReduceFromDisease == 0)
                {
                    speedReduceFromDisease = 1;
                }
            }

            // positive and negative protections are counted separately (prot, vuln); the resistance of potions is special: the
            // bigger value of several potion effects is taken
            if (tmp.Type != Define.POTION)
            {
                for (int i = 0; i < Define.NROFATTACKS; i++)
                {
                    if (tmp.Arch?.Name == "force" && tmp.Resist[i] != 0 && tmp.Type == Define.POTION_EFFECT)
                    {
                        potionResist[i] = potionResist[i] != 0 ? Max(potionResist[i], tmp.Resist[i]) : tmp.Resist[i];
                    }
                    else if (tmp.Resist[i] > 0)
                    {
                        prot[i] += ((100 - prot[i]) * tmp.Resist[i]) / 100;
                    }
                    else if (tmp.Resist[i] < 0)
                    {
                        vuln[i] += ((100 - vuln[i]) * -tmp.Resist[i]) / 100;
                    }
                }
            }

            if (tmp.Type != Define.BOW && tmp.Type != Define.SYMPTOM)
            {
                op.Attacktype |= tmp.Attacktype;
            }
            op.PathAttuned |= tmp.PathAttuned;
            op.PathRepelled |= tmp.PathRepelled;
            op.PathDenied |= tmp.PathDenied;
            op.Stats.Luck = (sbyte)(op.Stats.Luck + tmp.Stats.Luck);
            if (tmp.QueryFlag(Define.FLAG_LIFESAVE))
            {
                op.SetFlag(Define.FLAG_LIFESAVE);
            }
            if (tmp.QueryFlag(Define.FLAG_REFL_SPELL))
            {
                op.SetFlag(Define.FLAG_REFL_SPELL);
            }
            if (tmp.QueryFlag(Define.FLAG_REFL_MISSILE))
            {
                op.SetFlag(Define.FLAG_REFL_MISSILE);
            }
            if (tmp.QueryFlag(Define.FLAG_STEALTH))
            {
                op.SetFlag(Define.FLAG_STEALTH);
            }
            if (tmp.QueryFlag(Define.FLAG_UNDEAD) && !clone.QueryFlag(Define.FLAG_UNDEAD))
            {
                op.SetFlag(Define.FLAG_UNDEAD);
            }
            if (tmp.QueryFlag(Define.FLAG_MAKE_INVIS))
            {
                op.SetFlag(Define.FLAG_MAKE_INVIS);
                op.Invisible = 1;
            }
            if (tmp.QueryFlag(Define.FLAG_FLYING))
            {
                op.SetFlag(Define.FLAG_FLYING);
                if (!op.QueryFlag(Define.FLAG_WIZ))
                {
                    max = 1;
                }
            }
            if (tmp.QueryFlag(Define.FLAG_XRAYS))
            {
                op.SetFlag(Define.FLAG_XRAYS);
            }
            if (tmp.QueryFlag(Define.FLAG_BLIND))
            {
                op.SetFlag(Define.FLAG_BLIND);
            }
            if (tmp.QueryFlag(Define.FLAG_SEE_IN_DARK))
            {
                op.SetFlag(Define.FLAG_SEE_IN_DARK);
            }

            if (tmp.Stats.Exp != 0 && tmp.Type != Define.EXPERIENCE)
            {
                if (tmp.Stats.Exp > 0)
                {
                    addedSpeed = (float)(addedSpeed + (float)tmp.Stats.Exp / 3.0);
                    bonusSpeed = (float)(bonusSpeed + (1.0 + (float)tmp.Stats.Exp / 3.0));
                }
                else
                {
                    addedSpeed += tmp.Stats.Exp;
                }
            }

            switch (tmp.Type)
            {
                // The experience objects that matter: the one whose Str is set controls the weapon class bonus of the player, the
                // ones with Pow and Wis set the spell points and the grace (hp_obj, set by Con, does nothing in this version)
                case Define.EXPERIENCE:
                    if (tmp.Stats.Str != 0 && wcObj == null)
                    {
                        wcObj = tmp;
                    }
                    if (tmp.Stats.Pow != 0 && manaObj == null)
                    {
                        manaObj = tmp;
                    }
                    if (tmp.Stats.Wis != 0 && graceObj == null)
                    {
                        graceObj = tmp;
                    }
                    break;

                case Define.SKILL: // skills modifying the character: only the skill the player has chosen
                    if (tmp == op.ChosenSkill)
                    {
                        wc -= tmp.Stats.Wc;
                        if (tmp.Stats.Dam > 0)
                        {
                            // the skill is a 'weapon'
                            if (!op.QueryFlag(Define.FLAG_READY_WEAPON))
                            {
                                weaponSpeed = tmp.LastSp; // WEAPON_SPEED
                            }
                            if (weaponSpeed < 0)
                            {
                                weaponSpeed = 0;
                            }
                            weaponWeight = tmp.Weight;
                            op.Stats.Dam = (short)(op.Stats.Dam + tmp.Stats.Dam * (1 + (op.ChosenSkill!.Level / 9)));
                            if (tmp.Magic != 0)
                            {
                                op.Stats.Dam = (short)(op.Stats.Dam + tmp.Magic);
                            }
                        }
                        if (tmp.Stats.Wc != 0)
                        {
                            wc -= tmp.Stats.Wc + tmp.Magic;
                        }
                        if (tmp.Slaying != null)
                        {
                            op.Slaying = tmp.Slaying;
                        }
                        if (tmp.Stats.Ac != 0)
                        {
                            ac -= tmp.Stats.Ac + tmp.Magic;
                        }
                        pl.Encumbrance += 3 * tmp.Weight / 1000;
                    }
                    break;

                case Define.SHIELD:
                case Define.RING:
                case Define.AMULET:
                case Define.GIRDLE:
                case Define.HELMET:
                case Define.BOOTS:
                case Define.GLOVES:
                case Define.CLOAK:
                    if (tmp.Type == Define.SHIELD)
                    {
                        pl.Encumbrance += tmp.Weight / 2000;
                    }
                    if (tmp.Stats.Wc != 0)
                    {
                        wc -= tmp.Stats.Wc + tmp.Magic;
                    }
                    if (tmp.Stats.Dam != 0)
                    {
                        op.Stats.Dam = (short)(op.Stats.Dam + tmp.Stats.Dam + tmp.Magic);
                    }
                    if (tmp.Stats.Ac != 0)
                    {
                        ac -= tmp.Stats.Ac + tmp.Magic;
                    }
                    break;

                case Define.WEAPON:
                    wc -= tmp.Stats.Wc + tmp.Magic;
                    if (tmp.Stats.Ac != 0 && tmp.Stats.Ac + tmp.Magic > 0)
                    {
                        ac -= tmp.Stats.Ac + tmp.Magic;
                    }
                    op.Stats.Dam = (short)(op.Stats.Dam + tmp.Stats.Dam + tmp.Magic);
                    weaponWeight = tmp.Weight;
                    weaponSpeed = (tmp.LastSp * 2 - tmp.Magic) / 2; // WEAPON_SPEED(tmp)*2-magic, halved
                    if (weaponSpeed < 0)
                    {
                        weaponSpeed = 0;
                    }
                    if (tmp.Slaying != null)
                    {
                        op.Slaying = tmp.Slaying;
                    }
                    op.CurrentWeapon = tmp;
                    pl.Encumbrance += 3 * tmp.Weight / 1000;
                    break;

                case Define.ARMOUR: // only the best of these three are used
                case Define.BRACERS:
                case Define.FORCE:
                    if (tmp.Type == Define.ARMOUR)
                    {
                        pl.Encumbrance += tmp.Weight / 1000;
                    }
                    if (tmp.Stats.Wc != 0)
                    {
                        if (bestWc < tmp.Stats.Wc + tmp.Magic)
                        {
                            wc += bestWc;
                            bestWc = tmp.Stats.Wc + tmp.Magic;
                        }
                        else
                        {
                            wc += tmp.Stats.Wc + tmp.Magic;
                        }
                    }
                    if (tmp.Stats.Ac != 0)
                    {
                        if (bestAc < tmp.Stats.Ac + tmp.Magic)
                        {
                            ac += bestAc; // remove the last bonus
                            bestAc = tmp.Stats.Ac + tmp.Magic;
                        }
                        else
                        {
                            ac += tmp.Stats.Ac + tmp.Magic; // to nullify the effect below
                        }
                    }
                    if (tmp.Stats.Wc != 0)
                    {
                        wc -= tmp.Stats.Wc + tmp.Magic;
                    }
                    if (tmp.Stats.Ac != 0)
                    {
                        ac -= tmp.Stats.Ac + tmp.Magic;
                    }
                    if (tmp.LastSp != 0 && tmp.LastSp / 10.0 < max) // ARMOUR_SPEED
                    {
                        max = (float)(tmp.LastSp / 10.0);
                    }
                    break;
            }
        }

        // total resistance = total protections - total vulnerabilities; a potion in effect that gives more (or a cursed one that
        // gives a vulnerability) replaces it
        for (int i = 0; i < Define.NROFATTACKS; i++)
        {
            op.Resist[i] = (short)(prot[i] - vuln[i]);
            if (potionResist[i] != 0 && (potionResist[i] > op.Resist[i] || potionResist[i] < 0))
            {
                op.Resist[i] = (short)potionResist[i];
            }
        }

        LivingRules.CheckStatBounds(op.Stats);
        Life(op, pl, manaObj, graceObj);

        if (pl.Braced)
        {
            ac += 2;
        }
        else
        {
            ac -= LivingTables.DexBonus[op.Stats.Dex];
        }

        // the weapon class bonuses depend on the player's level in the relevant experience object (wc_obj), not on the general level
        if (wcObj != null && wcObj.Level > 1)
        {
            wc -= wcObj.Level + LivingTables.ThacoBonus[op.Stats.Str];
            for (int i = 1; i < wcObj.Level; i++)
            {
                // additional wc every 6 levels
                if (i % 6 == 0)
                {
                    wc--;
                }
                // additional dam every 4 levels
                if (i % 4 == 0 && !(LivingTables.DamBonus[op.Stats.Str] < 0))
                {
                    op.Stats.Dam = (short)(op.Stats.Dam + (1 + (LivingTables.DamBonus[op.Stats.Str] / 5)));
                }
            }
        }
        else
        {
            wc -= op.Level + LivingTables.ThacoBonus[op.Stats.Str];
        }
        op.Stats.Dam = (short)(op.Stats.Dam + LivingTables.DamBonus[op.Stats.Str]);
        if (pl.Braced)
        {
            wc += 4;
        }
        if (op.Stats.Dam < 1)
        {
            op.Stats.Dam = 1;
        }
        op.Speed = (float)(1.0 + LivingTables.SpeedBonus[op.Stats.Dex]);
        if (pl.Searching)
        {
            op.Speed -= 1; // SEARCH_ITEMS
        }
        if (op.Attacktype == 0)
        {
            op.Attacktype = clone.Attacktype;
        }

        if (addedSpeed >= 0)
        {
            op.Speed = (float)(op.Speed + addedSpeed / 10.0);
        }
        else
        {
            op.Speed /= (float)(1.0 - addedSpeed); // something wrong here...
        }
        if (op.Speed > max)
        {
            op.Speed = max;
        }

        // f is the kilograms above (positive) or below (negative) what the player can carry before it is slowed down
        float f = (op.Carrying / 1000) - LivingTables.MaxCarry[op.Stats.Str];
        if (f > 0)
        {
            op.Speed = (float)(op.Speed / (1.0 + f / LivingTables.MaxCarry[op.Stats.Str]));
        }

        op.Speed = (float)(op.Speed + bonusSpeed / 10.0); // not affected by limits
        // a lower limit on speed: with 0.01 the player moves once in about 12 seconds
        op.Speed *= speedReduceFromDisease;
        if (op.Speed < 0.01)
        {
            op.Speed = (float)0.01;
        }

        // the formula of vidarl@ifi.uio.no for the speed of a fighting player (float variables as in C)
        float m = (float)((LivingTables.MaxCarry[op.Stats.Str] - 121) / 121.0);
        float m2 = (float)(LivingTables.MaxCarry[op.Stats.Str] / 100.0);
        float w = (float)(weaponWeight / 20000.0);
        float s = (float)(2 - weaponSpeed / 10.0);
        float d = (float)((op.Stats.Dex - 14) / 14.0);
        float k = (float)(1 + m / 3.0 - w / (3 * m2) + op.Speed / 5.0 + d / 2.0);
        k = (float)(k * ((4 + op.Level) / (float)(6 + op.Level) * 1.2));
        if (k <= 0)
        {
            k = (float)0.01;
        }
        pl.WeaponSp = op.Speed / (k * s);

        // prevent overflows of wc and ac: the best you can get is ABS(120)
        if (wc > 120)
        {
            wc = 120;
        }
        else if (wc < -120)
        {
            wc = -120;
        }
        op.Stats.Wc = (sbyte)wc;
        if (ac > 120)
        {
            ac = 120;
        }
        else if (ac < -120)
        {
            ac = -120;
        }
        op.Stats.Ac = (sbyte)ac;

        op.GlowRadius = (short)light;
        world.UpdateObSpeed(op);
    }

    // fix_player's awarding of hp, sp and grace
    private static void Life(CfObject op, Player pl, CfObject? manaObj, CfObject? graceObj)
    {
        int plLevel = op.Level;
        if (plLevel < 1)
        {
            plLevel = 1; // safety, we should always get 1 levels worth of hp!
        }
        int con = LivingRules.ConBonus[op.Stats.Con];
        int maxhp = 0;
        for (int i = 1; i <= plLevel && i <= 10; i++)
        {
            int j = pl.LevHp[i] + con / 2;
            if (i % 2 != 0 && con % 2 != 0)
            {
                if (con > 0)
                {
                    j++;
                }
                else
                {
                    j--;
                }
            }
            maxhp += j > 1 ? j : 1;
        }
        for (int i = 11; i <= op.Level; i++)
        {
            maxhp += 2;
        }
        op.Stats.Maxhp = (short)maxhp;
        if (op.Stats.Hp > op.Stats.Maxhp)
        {
            op.Stats.Hp = op.Stats.Maxhp;
        }

        // sp gain is controlled by the level of the player's relevant experience object (manaObj)
        if (manaObj == null || manaObj.Level == 0)
        {
            manaObj = op;
        }
        int pow = LivingRules.SpBonus[op.Stats.Pow];
        int maxsp = 0;
        for (int i = 1; i <= manaObj.Level && i <= 10; i++)
        {
            int j = pl.LevSp[i] + pow / 2;
            if (i % 2 != 0 && pow % 2 != 0)
            {
                if (pow > 0)
                {
                    j++;
                }
                else
                {
                    j--;
                }
            }
            maxsp += j > 1 ? j : 1;
        }
        for (int i = 11; i <= manaObj.Level; i++)
        {
            maxsp += 2;
        }
        op.Stats.Maxsp = (short)maxsp;
        // characters can get their sp supercharged via rune of transferrance
        if (op.Stats.Sp > op.Stats.Maxsp * 2)
        {
            op.Stats.Sp = (short)(op.Stats.Maxsp * 2);
        }

        // maxgrace depends on both Wis and Pow; it is kept in a float so that the divisions do not make big jumps from level to level
        if (graceObj == null || graceObj.Level == 0)
        {
            graceObj = op;
        }
        float gp = LivingRules.GraceBonus[op.Stats.Pow];
        float gw = LivingRules.GraceBonus[op.Stats.Wis];
        float spTmp = 0.0f;
        for (int i = 1; i <= graceObj.Level && i <= 10; i++)
        {
            float graceTmp = (float)(pl.LevGrace[i] + (gp + gw) / 8.0);
            // changed the starting grace for playability
            if (i < 2)
            {
                graceTmp = (float)(graceTmp + (1.0 + (gp + gw) / 4.0));
            }
            if (graceTmp < 1.0f)
            {
                graceTmp = 1.0f;
            }
            spTmp += graceTmp;
        }
        int maxgrace = (int)spTmp;
        // one grace point per level after 11
        for (int i = 11; i <= graceObj.Level; i++)
        {
            maxgrace += 2;
        }
        op.Stats.Maxgrace = (short)maxgrace;
    }
}
