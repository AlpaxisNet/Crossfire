/*
 * Golden values for the tests of the consumables (tests/Crossfire.Extension.Tests/ConsumableTests.cs).
 *
 * This is the arithmetic of Crossfire's server/apply.c (version 8b7ee7f90): apply_food, eat_special_food, apply_poison and the
 * chance of dragon_eat_flesh, with the same declarations and expressions as the C (short stats, int and double conversions as
 * written there), compiled with gcc on x86-64. The object model, the messages and the dice are left out: a scenario is the numbers
 * of the eater and of the meal. Build and run:  gcc -O0 -o consumable_golden consumable_golden.c -lm && ./consumable_golden
 * Lines: "food <name> food hp", "special <name> hp sp", "poison food", "dragon <name> chance threshold" (threshold is
 * (int)(chance*100), what RANDOM()%10000 is compared with), "levels <name> hp[1] hp[2] changed" for the improvement potion.
 *
 * CrossFire, A Multiplayer game for X-windows, server/apply.c:
 *     Copyright (C) 2001 Mark Wedel & Crossfire Development Team, Copyright (C) 1992 Frank Tore Johansen
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation; either version 2 of the License, or (at your option) any later version.
 */
#include <stdio.h>
#include <math.h>

#define MIN(a,b) ((a)<(b)?(a):(b))

struct st { short food, hp, maxhp, sp; };

/* the numbers of apply_food's normal (not dragon, not cursed) case */
static void food(const char *name, short pfood, short php, short pmaxhp, short mfood)
{
    struct st op = { pfood, php, pmaxhp, 0 };
    int capacity_remaining;
    capacity_remaining = 999 - op.food;
    op.food += mfood;
    if (capacity_remaining < mfood)
        op.hp += capacity_remaining / 50;
    else
        op.hp += mfood / 50;
    if (op.hp > op.maxhp)
        op.hp = op.maxhp;
    if (op.food > 999)
        op.food = 999;
    printf("food %s %d %d\n", name, op.food, op.hp);
}

/* eat_special_food's hp and sp lines, cursed or not (the poison hit does no damage) */
static void special(const char *name, short php, short psp, short fhp, short fsp, int cursed)
{
    struct st who = { 0, php, 0, psp };
    if (fhp != 0) {
        if (!cursed)
            who.hp += fhp;
    }
    if (fsp != 0) {
        if (cursed) {
            who.sp -= fsp;
            if (who.sp < 0)
                who.sp = 0;
        } else
            who.sp += fsp;
    }
    printf("special %s %d %d\n", name, who.hp, who.sp);
}

/* dragon_eat_flesh: the chance of one resistance (levels, the skin's resistance, the meal's resistance, the ability focus) */
static void dragon(const char *name, int oplevel, int meallevel, int skinresist, int mealresist, int focus)
{
    double chance, bonus, mbonus;
    int MAXLEVEL = 110;
    bonus = (MAXLEVEL - oplevel) * 30. / ((double)MAXLEVEL);
    if (focus)
        bonus += 5;
    mbonus = oplevel * 20. / ((double)MAXLEVEL);
    chance = (((double)MIN(oplevel + bonus, meallevel + bonus + mbonus)) * 100. / ((double)MAXLEVEL)) - skinresist;
    if (chance >= 0.)
        chance += 1.;
    else
        chance = (chance < -12) ? 0. : 1. / pow(2., -chance);
    chance *= ((double)(MIN(mealresist, 50))) / 50.;
    if (focus)
        chance = MIN(100., chance * 2);
    printf("dragon %s %.6f %d\n", name, chance, (int)(chance * 100));
}

/* the improvement potion: the first level whose points are below the potion's (cursed: not 1) is set */
static void levels(const char *name, int level, int cursed)
{
    int hp[11] = { 0, 9, 7, 8, 8, 8, 8, 8, 8, 8, 8 }, sp[11] = { 0, 6, 6, 3, 3, 3, 3, 3, 3, 3, 3 }, gr[11] = { 0, 3, 3, 2, 2, 2, 2, 2, 2, 2, 2 };
    int i;
    for (i = 1; i < MIN(11, level); i++) {
        if (cursed) {
            if (hp[i] != 1) { hp[i] = 1; break; }
            if (sp[i] != 1) { sp[i] = 1; break; }
            if (gr[i] != 1) { gr[i] = 1; break; }
        } else {
            if (hp[i] < 9) { hp[i] = 9; break; }
            if (sp[i] < 6) { sp[i] = 6; break; }
            if (gr[i] < 3) { gr[i] = 3; break; }
        }
    }
    printf("levels %s %d %d %d\n", name, hp[1], hp[2], i < MIN(11, level));
}

int main(void)
{
    food("normal", 500, 10, 20, 100);        /* the stomach has room */
    food("overfull", 950, 10, 20, 200);       /* capacity_remaining 49 < 200: hp += 0, food clamps at 999 */
    food("full_hp_clamp", 900, 19, 20, 400);  /* capacity_remaining 99 < 400: hp += 1 */
    food("hp_cap", 0, 18, 20, 600);           /* 600/50 = 12: clamped to maxhp */
    special("heal", 5, 3, 7, 0, 0);
    special("poisonous", 5, 3, -4, 0, 0);
    special("mana", 5, 3, 0, 6, 0);
    special("drain", 5, 3, 0, 6, 1);
    special("cursed_hp", 5, 3, 9, 0, 1);
    {
        short food = 999;
        food -= food / 4;
        printf("poison %d\n", food);
        food = 7;
        food -= food / 4;
        printf("poison %d\n", food);
    }
    dragon("focus_low", 10, 40, 0, 50, 1);
    dragon("plain_low", 10, 40, 0, 25, 0);
    dragon("skin_high", 60, 20, 80, 50, 0);   /* chance < -12 */
    dragon("skin_near", 60, 20, 30, 50, 0);   /* -12 <= chance < 0 */
    dragon("level_high", 100, 100, 0, 50, 0);
    levels("first", 6, 0);
    levels("cursed", 6, 1);
    levels("level2", 2, 0);
    return 0;
}
