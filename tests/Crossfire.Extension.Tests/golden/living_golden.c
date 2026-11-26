/*
 * Golden values for the tests of the rest of fix_player (tests/Crossfire.Extension.Tests/LivingTests.cs).
 *
 * This is the arithmetic of Crossfire's common/living.c fix_player (version 8b7ee7f90) for a player, with the same declarations and
 * expressions as the C (float, int, double conversions as written there), compiled with gcc on x86-64 (SSE, FLT_EVAL_METHOD 0).
 * The object model, the flags and the life points are left out: a scenario is the player's level, stats and the applied items'
 * numbers. Build and run:  gcc -O0 -o living_golden living_golden.c -lm && ./living_golden
 * Every line is "<scenario> ac wc dam speed weapon_sp light resist[0..2]"; the test file holds the same numbers.
 *
 * CrossFire, A Multiplayer game for X-windows, common/living.c:
 *     Copyright (C) 2000 Mark Wedel, Copyright (C) 1992 Frank Tore Johansen
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation; either version 2 of the License, or (at your option) any later version.
 */
#include <stdio.h>
#include <string.h>

#define MAXLEVEL 110
#define NROFATTACKS 25
#define MAX(a,b) ((a)>(b)?(a):(b))

enum { T_NONE, T_WEAPON, T_ARMOUR, T_HELMET, T_SHIELD, T_RING, T_SYMPTOM, T_FORCE, T_EXPERIENCE, T_SKILL, T_POTION_EFFECT };

static int dex_bonus[31]={-4,-3,-2,-2,-1,-1,-1,0,0,0,0,0,0,0,1,1,1,2,2,2,3,3,3,4,4,4,5,5,6,6,7};
static float speed_bonus[31]={-0.4,-0.4,-0.3,-0.3,-0.2,-0.2,-0.2,-0.1,-0.1,-0.1,-0.05,0,0,0,0.05,0.1,0.1,0.2,0.3,0.4,0.5,0.6,0.8,1.0,1.2,1.4,1.6,1.8,2.0,2.5,3.0};
static int dam_bonus[31]={-2,-2,-2,-1,-1,-1,0,0,0,0,0,0,1,1,1,2,2,2,3,3,3,4,4,5,5,6,6,7,8,10,15};
static int thaco_bonus[31]={-2,-2,-1,-1,0,0,0,0,0,0,0,0,0,0,1,1,1,2,2,2,3,3,3,4,4,5,5,6,7,8,10};
static int max_carry[31]={2,4,7,11,16,22,29,37,46,56,67,79,92,106,121,137,154,172,191,211,232,254,277,301,326,352,400,450,500,600,1000};

struct item { int type, wc, ac, dam, magic, last_sp, weight, exp, glow; int resist[NROFATTACKS]; int chosen; int level; };

struct scen {
    const char *name; int level, str, dex, carrying, braced, use_armour, base_ac, base_wc, base_dam, base_speed_x100;
    int resist[NROFATTACKS]; int wc_obj_level; int nitems; struct item it[6];
};

static void run(const struct scen *s)
{
    int i, light = 0;
    float f, max = 9, added_speed = 0, bonus_speed = 0, speed_reduce_from_disease = 1;
    float M, W, s_, D, K, S, M2;
    int weapon_weight = 0, weapon_speed = 0;
    int best_wc = 0, best_ac = 0, wc = 0, ac = 0;
    int prot[NROFATTACKS], vuln[NROFATTACKS], potion_resist[NROFATTACKS], resist[NROFATTACKS];
    int dam, level = s->level, Str = s->str, Dex = s->dex;
    float speed;

    memcpy(resist, s->resist, sizeof(resist));
    for (i = 0; i < NROFATTACKS; i++) {
        if (resist[i] > 0) prot[i] = resist[i], vuln[i] = 0;
        else vuln[i] = -(resist[i]), prot[i] = 0;
        potion_resist[i] = 0;
    }
    wc = s->base_wc;
    dam = s->base_dam;
    if (!s->use_armour) {
        ac = MAX(-10, s->base_ac - level / 3);
        prot[0] += ((100 - prot[1]) * (80 * level / MAXLEVEL)) / 100; /* AT_PHYSICAL is 1 in C: prot[1] */
    } else ac = s->base_ac;
    speed = s->base_speed_x100 / 100.0;
    for (int n = 0; n < s->nitems; n++) {
        const struct item *tmp = &s->it[n];
        if (tmp->glow > light) light = tmp->glow;
        if (tmp->type == T_SYMPTOM) {
            speed_reduce_from_disease = tmp->last_sp / 100.0;
            if (speed_reduce_from_disease == 0) speed_reduce_from_disease = 1;
        }
        for (i = 0; i < NROFATTACKS; i++) {
            if (tmp->type == T_POTION_EFFECT && tmp->resist[i]) {
                if (potion_resist[i]) potion_resist[i] = MAX(potion_resist[i], tmp->resist[i]);
                else potion_resist[i] = tmp->resist[i];
            }
            else if (tmp->resist[i] > 0) prot[i] += ((100 - prot[i]) * tmp->resist[i]) / 100;
            else if (tmp->resist[i] < 0) vuln[i] += ((100 - vuln[i]) * (-tmp->resist[i])) / 100;
        }
        if (tmp->exp && tmp->type != T_EXPERIENCE) {
            if (tmp->exp > 0) {
                added_speed += (float)tmp->exp / 3.0;
                bonus_speed += 1.0 + (float)tmp->exp / 3.0;
            } else
                added_speed += (float)tmp->exp;
        }
        switch (tmp->type) {
        case T_SKILL:
            if (tmp->chosen) {
                wc -= tmp->wc;
                if (tmp->dam > 0) {
                    weapon_speed = (int)tmp->last_sp;
                    if (weapon_speed < 0) weapon_speed = 0;
                    weapon_weight = tmp->weight;
                    dam += tmp->dam * (1 + (tmp->level / 9)); /* the level of chosen_skill, the skill object */
                    if (tmp->magic) dam += tmp->magic;
                }
                if (tmp->wc) wc -= (tmp->wc + tmp->magic);
                if (tmp->ac) ac -= (tmp->ac + tmp->magic);
            }
            break;
        case T_SHIELD: case T_RING: case T_HELMET:
            if (tmp->wc) wc -= (tmp->wc + tmp->magic);
            if (tmp->dam) dam += (tmp->dam + tmp->magic);
            if (tmp->ac) ac -= (tmp->ac + tmp->magic);
            break;
        case T_WEAPON:
            wc -= (tmp->wc + tmp->magic);
            if (tmp->ac && tmp->ac + tmp->magic > 0) ac -= tmp->ac + tmp->magic;
            dam += (tmp->dam + tmp->magic);
            weapon_weight = tmp->weight;
            weapon_speed = ((int)tmp->last_sp * 2 - tmp->magic) / 2;
            if (weapon_speed < 0) weapon_speed = 0;
            break;
        case T_ARMOUR: case T_FORCE:
            if (tmp->wc) {
                if (best_wc < tmp->wc + tmp->magic) { wc += best_wc; best_wc = tmp->wc + tmp->magic; }
                else wc += tmp->wc + tmp->magic;
            }
            if (tmp->ac) {
                if (best_ac < tmp->ac + tmp->magic) { ac += best_ac; best_ac = tmp->ac + tmp->magic; }
                else ac += tmp->ac + tmp->magic;
            }
            if (tmp->wc) wc -= (tmp->wc + tmp->magic);
            if (tmp->ac) ac -= (tmp->ac + tmp->magic);
            if (tmp->last_sp && tmp->last_sp / 10.0 < max) max = tmp->last_sp / 10.0;
            break;
        }
    }
    for (i = 0; i < NROFATTACKS; i++) {
        resist[i] = prot[i] - vuln[i];
        if (potion_resist[i] && ((potion_resist[i] > resist[i]) || (potion_resist[i] < 0)))
            resist[i] = potion_resist[i];
    }
    if (s->braced) ac += 2; else ac -= dex_bonus[Dex];
    if (s->wc_obj_level > 1) {
        int j;
        wc -= (s->wc_obj_level + thaco_bonus[Str]);
        for (j = 1; j < s->wc_obj_level; j++) {
            if (!(j % 6)) wc--;
            if (!(j % 4) && !(dam_bonus[Str] < 0)) dam += (1 + (dam_bonus[Str] / 5));
        }
    } else
        wc -= (level + thaco_bonus[Str]);
    dam += dam_bonus[Str];
    if (s->braced) wc += 4;
    if (dam < 1) dam = 1;
    speed = 1.0 + speed_bonus[Dex];
    if (added_speed >= 0) speed += added_speed / 10.0;
    else speed /= (float)(1.0 - added_speed);
    if (speed > max) speed = max;
    f = (s->carrying / 1000) - max_carry[Str];
    if (f > 0) speed = speed / (1.0 + f / max_carry[Str]);
    speed += bonus_speed / 10.0;
    speed = speed * speed_reduce_from_disease;
    if (speed < 0.01) speed = 0.01;
    M = (max_carry[Str] - 121) / 121.0;
    M2 = max_carry[Str] / 100.0;
    W = weapon_weight / 20000.0;
    s_ = 2 - weapon_speed / 10.0;
    D = (Dex - 14) / 14.0;
    K = 1 + M / 3.0 - W / (3 * M2) + speed / 5.0 + D / 2.0;
    K *= (4 + level) / (float)(6 + level) * 1.2;
    if (K <= 0) K = 0.01;
    S = speed / (K * s_);
    if (wc > 120) wc = 120; else if (wc < -120) wc = -120;
    if (ac > 120) ac = 120; else if (ac < -120) ac = -120;
    printf("%s ac=%d wc=%d dam=%d speed=%.9g weapon_sp=%.9g glow=%d res0=%d res1=%d res2=%d res4=%d\n", s->name, ac, wc, dam, speed, S, light, resist[0], resist[1], resist[2], resist[4]);
}

int main(void)
{
    static struct scen a[12];
    int n = 0, i;
    /* bare: level 1, Str 14, Dex 12, can use armour, speed 1 */
    a[n] = (struct scen){ "bare", 1, 14, 12, 0, 0, 1, 0, 0, 0, 100 }; n++;
    /* sword and mail: sword wc 3 dam 8 magic 1 speed 5 weight 3000; mail ac 5 */
    a[n] = (struct scen){ "armed", 1, 14, 12, 23000, 0, 1, 0, 0, 0, 100, {0}, 0, 2,
        { { T_WEAPON, 3, 0, 8, 1, 5, 3000 }, { T_ARMOUR, 0, 5, 0, 0, 0, 20000 } } }; n++;
    /* armour slowing the player: last_sp 4 gives max 0.4; the helmet's glow radius 2 is the light */
    a[n] = (struct scen){ "slow_armour", 3, 16, 20, 0, 0, 1, 0, 0, 0, 100, {0}, 0, 2,
        { { T_ARMOUR, 0, 3, 0, 0, 4, 30000 }, { T_HELMET, 0, 1, 0, 0, 0, 2000, 0, 2 } } }; n++;
    /* too much to carry: Str 10 carries 67 kg, this one 100 */
    a[n] = (struct scen){ "overloaded", 5, 10, 14, 100000, 0, 1, 0, 0, 0, 100 }; n++;
    /* speed from the exp of rings: +3 and +6 */
    a[n] = (struct scen){ "speedy", 5, 12, 16, 0, 0, 1, 0, 0, 0, 100, {0}, 0, 2,
        { { T_RING, 0, 0, 0, 0, 0, 100, 3 }, { T_RING, 0, 0, 0, 0, 0, 100, 6 } } }; n++;
    a[n] = (struct scen){ "slowed", 5, 12, 16, 0, 0, 1, 0, 0, 0, 100, {0}, 0, 1,
        { { T_RING, 0, 0, 0, 0, 0, 100, -2 } } }; n++;
    /* resistances: the archetype has fire 20 (index 2) and cold -30 (index 4); armour 50 magic (1) and 40 fire, a ring -25 fire, a potion effect 60 fire */
    a[n] = (struct scen){ "resist", 1, 14, 12, 0, 0, 1, 0, 0, 0, 100, { 0, 0, 20, 0, -30 }, 0, 3,
        { { T_ARMOUR, 0, 2, 0, 0, 0, 1000, 0, 0, { 0, 50, 40 } }, { T_RING, 0, 0, 0, 0, 0, 100, 0, 0, { 0, 0, -25 } },
          { T_POTION_EFFECT, 0, 0, 0, 0, 0, 100, 0, 0, { 0, 0, 60 } } } }; n++;
    /* a player that cannot use armour: level 20, base ac 1, magic resistance 10 */
    a[n] = (struct scen){ "noarmour", 20, 14, 12, 0, 0, 0, 1, 0, 0, 100, { 0, 10 } }; n++;
    a[n] = (struct scen){ "braced", 1, 14, 12, 0, 1, 1, 0, 0, 0, 100 }; n++;
    /* an experience object (Str set) of level 13 */
    a[n] = (struct scen){ "fighter", 14, 22, 12, 0, 0, 1, 0, 0, 0, 100, {0}, 13 }; n++;
    a[n] = (struct scen){ "diseased", 2, 14, 14, 0, 0, 1, 0, 0, 0, 100, {0}, 0, 1, { { T_SYMPTOM, 0, 0, 0, 0, 50 } } }; n++;
    /* the chosen skill is a weapon: wc 2 ac 1 dam 3 magic 2 speed 6 weight 4000, the skill object has level 18 */
    a[n] = (struct scen){ "skill", 18, 14, 12, 0, 0, 1, 0, 0, 0, 100, {0}, 0, 1,
        { { T_SKILL, 2, 1, 3, 2, 6, 4000, 0, 0, {0}, 1, 18 } } }; n++;
    for (i = 0; i < n; i++) run(&a[i]);
    return 0;
}
