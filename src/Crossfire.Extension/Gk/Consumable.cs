// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): what happens when a player eats, drinks or is poisoned by an object,
// server/apply.c <c>apply_food</c>, <c>apply_potion</c>, <c>apply_poison</c>, <c>eat_special_food</c> and <c>dragon_eat_flesh</c>
// (lane 6C of docs/mud-phase3.md).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/apply.c:
//         Copyright (C) 2001 Mark Wedel & Crossfire Development Team
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
/// server/apply.c: the consumables. Every line C writes with <c>new_draw_info</c> goes to <see cref="CfWorld.Host"/>'s
/// <c>DrawInfo</c>; <c>fix_player</c> is the host's <c>FixPlayer</c>; an object that is used up goes through
/// <see cref="CfWorld.DecreaseObNr"/> (C's <c>decrease_ob</c>), which tells the client. Texts are C's bytes.
/// WHAT IS NOT PORTED (differences.md "Consumables"): <c>cast_spell</c> (a potion that casts a spell), <c>drain_stat</c> (a cursed
/// potion of restoration), <c>cast_change_attr</c> (food that changes stats or protects) and the protection potion's timed force,
/// because the extension has no spells and no tick: such a potion says that it is not available and is NOT used up (39 of Graal
/// Kingdoms' 51 potion archetypes would be eaten for nothing). <c>hit_player</c> of an attack of type poison does no damage in C
/// (the attack type is an effect: <c>dam = 0</c>) and only calls <c>poison_player</c>, which needs the tick: it is left out.
/// </summary>
internal static class Consumable
{
    /// <summary>The highest food level (999) a player can hold; C writes the number in <c>apply_food</c> and <c>dragon_eat_flesh</c>.</summary>
    public const int FullStomach = 999;

    /// <summary>What the player is told when the effect of an object needs code the extension does not have; the object is not used up.</summary>
    public const string NotAvailable = "That is not available yet.";

    /// <summary>living.c <c>restore_msg</c>.</summary>
    public static readonly string[] RestoreMsg =
    {
        "You feel your strength return.",
        "You feel your agility return.",
        "You feel your health return.",
        "You feel your wisdom return.",
        "You feel your charisma return.",
        "You feel your memory return.",
        "You feel your spirits return.",
    };

    /// <summary>What a player's eating tells the scripts (the event onEatFood): the player, the archetype of the food, the food, and whether it was cursed.</summary>
    internal delegate void Eaten(CfObject who, Archetype? arch, CfObject food, bool wasCursed);

    // eat_special_food's sp_type[]: the attack numbers that a spell protects against (-1 in C for the others)
    private static readonly bool[] Protectable = BuildProtectable();

    private static bool[] BuildProtectable()
    {
        var t = new bool[Define.NROFATTACKS];
        foreach (int i in new[] { 0, 1, 2, 3, 4, 5, 7, 10, 11, 12, 15, 16 })
        {
            t[i] = true;
        }
        return t;
    }

    private static void Draw(CfWorld w, CfObject op, string text) => w.Host.DrawInfo(op, text);

    private static void Touch(CfObject op) => (op.Contr as Player)?.Touch();

    private static short S(int v) => unchecked((short)v);

    /// <summary>apply.c <c>apply_food</c>: the object is food, flesh or a drink. <paramref name="eaten"/> is told after the object is used up, for a player.</summary>
    public static void ApplyFood(CfWorld w, CfObject op, CfObject tmp, Eaten? eaten = null)
    {
        Archetype? arch = tmp.Arch;
        bool wasCursed = tmp.QueryFlag(Define.FLAG_CURSED);
        if (op.Type != Define.PLAYER)
        {
            op.Stats.Hp = op.Stats.Maxhp;
        }
        else if (tmp.Type == Define.FLESH && CfWorld.IsDragonPl(op) != 0 && DragonEatFlesh(w, op, tmp) != 0)
        {
            // check if this is a dragon (player), eating some flesh: it has eaten
        }
        else
        {
            // usual case - no dragon meal
            if (op.Stats.Food + tmp.Stats.Food > FullStomach)
            {
                Draw(w, op, tmp.Type == Define.FOOD || tmp.Type == Define.FLESH
                    ? "You feel full, but what a waste of food!"
                    : "Most of the drink goes down your face not your throat!");
            }

            if (!tmp.QueryFlag(Define.FLAG_CURSED))
            {
                string buf;
                if (CfWorld.IsDragonPl(op) == 0)
                {
                    // eating message for normal players
                    buf = tmp.Type == Define.DRINK
                        ? "Ahhh...that " + tmp.Name + " tasted good."
                        : "The " + tmp.Name + " tasted " + (tmp.Type == Define.FLESH ? "terrible!" : "good.");
                }
                else
                {
                    // eating message for dragon players
                    buf = "The " + tmp.Name + " tasted terrible!";
                }

                Draw(w, op, buf);
                int capacityRemaining = FullStomach - op.Stats.Food;
                op.Stats.Food = S(op.Stats.Food + tmp.Stats.Food);
                op.Stats.Hp = S(op.Stats.Hp + (capacityRemaining < tmp.Stats.Food ? capacityRemaining / 50 : tmp.Stats.Food / 50));
                if (op.Stats.Hp > op.Stats.Maxhp)
                {
                    op.Stats.Hp = op.Stats.Maxhp;
                }

                if (op.Stats.Food > FullStomach)
                {
                    op.Stats.Food = FullStomach;
                }
            }

            // special food hack -b.t.
            if (!string.IsNullOrEmpty(tmp.Title) || tmp.QueryFlag(Define.FLAG_CURSED))
            {
                EatSpecialFood(w, op, tmp);
            }
        }

        w.DecreaseObNr(tmp, 1);
        Touch(op);
        if (op.Type == Define.PLAYER)
        {
            eaten?.Invoke(op, arch, tmp, wasCursed);
        }
    }

    /// <summary>
    /// apply.c <c>eat_special_food</c>: food that changes the eater's stats or protects him (<c>cast_change_attr</c>, not ported: the
    /// player is told once that it does not work) and food that changes hit points or spell points (ported, without a limit as in C).
    /// </summary>
    public static void EatSpecialFood(CfWorld w, CfObject who, CfObject food)
    {
        // check if food modifies stats of the eater (STR, DEX, CON, CHA)
        bool needsSpell = CfWorld.GetAttrValue(food.Stats, Define.STR) != 0 || CfWorld.GetAttrValue(food.Stats, Define.DEX) != 0
            || CfWorld.GetAttrValue(food.Stats, Define.CON) != 0 || CfWorld.GetAttrValue(food.Stats, Define.CHA) != 0;

        // check if we can protect the eater
        for (int i = 0; i < Define.NROFATTACKS; i++)
        {
            if (food.Resist[i] > 0 && Protectable[i])
            {
                needsSpell = true;
            }
        }

        if (needsSpell)
        {
            Draw(w, who, NotAvailable);
        }

        // check for hp, sp change
        if (food.Stats.Hp != 0)
        {
            if (food.QueryFlag(Define.FLAG_CURSED))
            {
                // hit_player(who, food->stats.hp, food, AT_POISON): damage 0, see the class's note
                Draw(w, who, "Eck!...that was poisonous!");
            }
            else
            {
                Draw(w, who, food.Stats.Hp > 0 ? "You begin to feel better." : "Eck!...that was poisonous!");
                who.Stats.Hp = S(who.Stats.Hp + food.Stats.Hp);
            }
        }

        if (food.Stats.Sp != 0)
        {
            if (food.QueryFlag(Define.FLAG_CURSED))
            {
                Draw(w, who, "You are drained of mana!");
                who.Stats.Sp = S(who.Stats.Sp - food.Stats.Sp);
                if (who.Stats.Sp < 0)
                {
                    who.Stats.Sp = 0;
                }
            }
            else
            {
                Draw(w, who, "You feel a rush of magical energy!");
                who.Stats.Sp = S(who.Stats.Sp + food.Stats.Sp);
                // place limit on max sp from food?
            }
        }
    }

    /// <summary>
    /// apply.c <c>dragon_eat_flesh</c>: a dragon (a player of a race "dragon" that has the forces <c>dragon_skin_force</c> and
    /// <c>dragon_ability_force</c>) eats flesh; its resistances may improve. 1 when the dragon ate it, 0 when it does not work.
    /// </summary>
    public static int DragonEatFlesh(CfWorld w, CfObject op, CfObject meal)
    {
        CfObject? skin = null;
        CfObject? abil = null;
        double maxchance = 0;
        var winner = new int[Define.NROFATTACKS];
        int winners = 0;
        const int MaxLevel = 110; // this really sucks! MAXLEVEL is only defined in common/living

        // let's make sure and doublecheck the parameters
        if (meal.Type != Define.FLESH || CfWorld.IsDragonPl(op) == 0)
        {
            return 0;
        }

        // now grab the 'dragon_skin'- and 'dragon_ability'-forces from the player's inventory
        for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Type == Define.FORCE && tmp.Arch != null)
            {
                if (tmp.Arch.Name == "dragon_skin_force")
                {
                    skin = tmp;
                }
                else if (tmp.Arch.Name == "dragon_ability_force")
                {
                    abil = tmp;
                }
            }
        }

        // if either skin or ability are missing, this is an old player which is not to be considered a dragon -> bail out
        if (skin == null || abil == null)
        {
            return 0;
        }

        // now start by filling stomache and health, according to food-value
        if ((FullStomach - op.Stats.Food) < meal.Stats.Food)
        {
            op.Stats.Hp = S(op.Stats.Hp + (FullStomach - op.Stats.Food) / 50);
        }
        else
        {
            op.Stats.Hp = S(op.Stats.Hp + meal.Stats.Food / 50);
        }

        if (op.Stats.Hp > op.Stats.Maxhp)
        {
            op.Stats.Hp = op.Stats.Maxhp;
        }

        op.Stats.Food = S(Math.Min(FullStomach, op.Stats.Food + meal.Stats.Food));

        // on to the interesting part: chances for adding resistance
        for (int i = 0; i < Define.NROFATTACKS; i++)
        {
            if (meal.Resist[i] > 0 && CfWorld.AtnrIsDragonEnabled(i) != 0)
            {
                // got positive resistance, now calculate improvement chance (0-100)

                // this bonus makes resistance increase easier at lower levels
                double bonus = (MaxLevel - op.Level) * 30.0 / MaxLevel;
                if (i == abil.Stats.Exp)
                {
                    bonus += 5; // additional bonus for resistance of ability-focus
                }

                // monster bonus increases with level, because high-level flesh is too rare
                double mbonus = op.Level * 20.0 / MaxLevel;

                double chance = (Math.Min(op.Level + bonus, meal.Level + bonus + mbonus) * 100.0 / MaxLevel) - skin.Resist[i];

                if (chance >= 0.0)
                {
                    chance += 1.0;
                }
                else
                {
                    chance = chance < -12 ? 0.0 : 1.0 / Math.Pow(2.0, -chance);
                }

                // chance is proportional to amount of resistance (max. 50)
                chance *= Math.Min((int)meal.Resist[i], 50) / 50.0;

                // doubled chance for resistance of ability-focus
                if (i == abil.Stats.Exp)
                {
                    chance = Math.Min(100.0, chance * 2);
                }

                // now make the throw and save all winners (Don't insert luck bonus here!)
                if (w.Random() % 10000 < (int)(chance * 100))
                {
                    winner[winners] = i;
                    winners++;
                }

                if (chance > maxchance)
                {
                    maxchance = chance;
                }
            }
        }

        // print message according to maxchance
        string buf;
        if (maxchance > 50.0)
        {
            buf = "Hmm! The " + meal.Name + " tasted delicious!";
        }
        else if (maxchance > 10.0)
        {
            buf = "The " + meal.Name + " tasted very good.";
        }
        else if (maxchance > 1.0)
        {
            buf = "The " + meal.Name + " tasted good.";
        }
        else if (maxchance > 0.0001)
        {
            buf = "The " + meal.Name + " had a boring taste.";
        }
        else if (meal.LastEat > 0 && CfWorld.AtnrIsDragonEnabled(meal.LastEat) != 0)
        {
            buf = "The " + meal.Name + " tasted strange.";
        }
        else
        {
            buf = "The " + meal.Name + " had no taste.";
        }

        Draw(w, op, buf);

        // now choose a winner if we have any
        int win = -1;
        if (winners > 0)
        {
            win = winner[w.Random() % winners];
        }

        if (win >= 0 && win < Define.NROFATTACKS && skin.Resist[win] < 95)
        {
            // resistance increased!
            skin.Resist[win]++;
            w.Host.FixPlayer(op);
            Draw(w, op, "Your skin is now more resistant to " + Tables.ChangeResistMsg[win] + "!");
        }

        // if this flesh contains a new ability focus, we mark it into the ability_force and it will take effect on next level
        if (meal.LastEat > 0 && CfWorld.AtnrIsDragonEnabled(meal.LastEat) != 0 && meal.LastEat != abil.LastEat)
        {
            abil.LastEat = meal.LastEat; // write: last_eat <new attnr focus>

            if (meal.LastEat != abil.Stats.Exp)
            {
                Draw(w, op, "Your metabolism prepares to focus on " + Tables.ChangeResistMsg[meal.LastEat] + "!");
                Draw(w, op, "The change will happen at level " + (abil.Level + 1).ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                Draw(w, op, "Your metabolism will continue to focus on " + Tables.ChangeResistMsg[meal.LastEat] + ".");
                abil.LastEat = 0;
            }
        }

        return 1;
    }

    /// <summary>apply.c <c>apply_poison</c>: a poisonous drink; the eater loses a quarter of his food level.</summary>
    public static void ApplyPoison(CfWorld w, CfObject op, CfObject tmp)
    {
        if (op.Type == Define.PLAYER)
        {
            Draw(w, op, "Yech!  That tasted poisonous!");
        }

        // if (tmp->stats.hp > 0) hit_player(op, tmp->stats.hp, tmp, AT_POISON): damage 0 in C, poison_player is not ported
        op.Stats.Food = S(op.Stats.Food - op.Stats.Food / 4);
        w.DecreaseObNr(tmp, 1);
        Touch(op);
    }

    /// <summary>
    /// apply.c <c>apply_potion</c>: 1 when the potion was dealt with (whether or not it is used up: a potion whose effect needs a spell,
    /// a cursed potion of restoration and a protection potion say <see cref="NotAvailable"/> and stay in the pack), 0 when the
    /// archetype <c>depletion</c> is missing. The skill check of <c>manual_apply</c> is the caller's.
    /// </summary>
    public static int ApplyPotion(CfWorld w, CfObject op, CfObject tmp)
    {
        bool got = false;
        int i;

        if (op.Type == Define.PLAYER && !tmp.QueryFlag(Define.FLAG_IDENTIFIED))
        {
            w.Identify(tmp);
        }

        bool cursed = tmp.QueryFlag(Define.FLAG_CURSED) || tmp.QueryFlag(Define.FLAG_DAMNED);

        // only players get this
        if (op.Type == Define.PLAYER && (tmp.Attacktype & Define.AT_DEPLETE) != 0)
        {
            // potion of restoration
            if (cursed)
            {
                // drain_stat(op) is not ported
                Draw(w, op, NotAvailable);
                return 1;
            }

            Archetype? at = w.FindArchetype("depletion");
            if (at == null)
            {
                w.Log(LogLevel.Error, "Could not find archetype depletion");
                return 0;
            }

            CfObject? depl = CfWorld.PresentArchInOb(at, op);
            if (depl != null)
            {
                for (i = 0; i < 7; i++)
                {
                    if (CfWorld.GetAttrValue(depl.Stats, i) != 0)
                    {
                        Draw(w, op, RestoreMsg[i]);
                    }
                }

                w.RemoveOb(depl);
                w.FreeObject(depl);
                w.Host.FixPlayer(op);
            }
            else
            {
                Draw(w, op, "You feel a great loss...");
            }

            w.DecreaseObNr(tmp, 1);
            Touch(op);
            return 1;
        }

        // only players get this
        if (op.Type == Define.PLAYER && (tmp.Attacktype & Define.AT_GODPOWER) != 0)
        {
            // improvement potion
            var pl = (Player)op.Contr!;
            pl.EnsureStats();
            int limit = Math.Min(11, (int)op.Level);
            for (i = 1; i < limit; i++)
            {
                if (cursed)
                {
                    if (pl.LevHp[i] != 1)
                    {
                        pl.LevHp[i] = 1;
                        break;
                    }

                    if (pl.LevSp[i] != 1)
                    {
                        pl.LevSp[i] = 1;
                        break;
                    }

                    if (pl.LevGrace[i] != 1)
                    {
                        pl.LevGrace[i] = 1;
                        break;
                    }
                }
                else
                {
                    if (pl.LevHp[i] < 9)
                    {
                        pl.LevHp[i] = 9;
                        break;
                    }

                    if (pl.LevSp[i] < 6)
                    {
                        pl.LevSp[i] = 6;
                        break;
                    }

                    if (pl.LevGrace[i] < 3)
                    {
                        pl.LevGrace[i] = 3;
                        break;
                    }
                }
            }

            // just makes checking easier
            if (i < limit)
            {
                got = true;
                pl.StoreStats(); // the points of the levels are kept with the account
            }

            if (!cursed)
            {
                if (got)
                {
                    w.Host.FixPlayer(op);
                    Draw(w, op, "The Gods smile upon you and remake you");
                    Draw(w, op, "a little more in their image.");
                    Draw(w, op, "You feel a little more perfect.");
                }
                else
                {
                    Draw(w, op, "The potion had no effect - you are already perfect");
                }
            }
            else if (got)
            {
                // cursed potion
                w.Host.FixPlayer(op);
                Draw(w, op, "The Gods are angry and punish you.");
            }
            else
            {
                Draw(w, op, "You are fortunate that you are so pathetic.");
            }

            w.DecreaseObNr(tmp, 1);
            Touch(op);
            return 1;
        }

        // a potion that casts a spell: healing, restore spellpoint (power potion) and heroism all fit into this category
        if (tmp.Stats.Sp != 0)
        {
            // cast_spell(op, tmp, ..., spellPotion) is not ported
            Draw(w, op, NotAvailable);
            return 1;
        }

        // deal with protection potions: the force that C inserts counts down in the tick, which the extension does not have
        for (i = 0; i < Define.NROFATTACKS; i++)
        {
            if (tmp.Resist[i] != 0)
            {
                Draw(w, op, NotAvailable);
                return 1;
            }
        }

        // only thing left are the stat potions
        if (op.Type == Define.PLAYER)
        {
            if (cursed && tmp.Value != 0)
            {
                tmp.ClearFlag(Define.FLAG_APPLIED);
            }
            else
            {
                tmp.SetFlag(Define.FLAG_APPLIED);
            }

            if (LivingChange.ChangeAbil(w, op, tmp) == 0)
            {
                Draw(w, op, "Nothing happened.");
            }
        }

        // CLEAR_FLAG is so that if the character has other potions that were grouped with the one consumed, his stat will not be
        // raised by them. fix_player just clears up all the stats.
        tmp.ClearFlag(Define.FLAG_APPLIED);
        w.Host.FixPlayer(op);
        w.DecreaseObNr(tmp, 1);
        Touch(op);
        return 1;
    }
}
