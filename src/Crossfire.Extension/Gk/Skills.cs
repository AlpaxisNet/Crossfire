// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/skill_util.c, the part that links a player's skills to its
// experience objects (<c>init_new_exp_system</c> and what it calls, <c>init_player_exp</c>, <c>link_player_skills</c>,
// <c>link_player_skill</c>, <c>unlink_skill</c>) and include/skillist.h (the table of the skills).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/skill_util.c:
//         Copyright (C) 1992 Frank Tore Johansen
//     include/skillist.h:
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

/// <summary>include/skills.h <c>skill</c>: one row of the skill table.</summary>
internal sealed class SkillEntry
{
    public SkillEntry(string name, int category, int time, long bexp, float lexp, int stat1, int stat2, int stat3)
    {
        Name = name;
        Category = category;
        Time = time;
        Bexp = bexp;
        Lexp = lexp;
        Stat1 = stat1;
        Stat2 = stat2;
        Stat3 = stat3;
    }

    public string Name { get; }

    /// <summary>The experience category (an index of <see cref="SkillTable.ExpCat"/>), or <see cref="SkillTable.ExpNone"/>.</summary>
    public int Category { get; set; }

    public int Time { get; set; }

    public long Bexp { get; set; }

    public float Lexp { get; set; }

    public int Stat1 { get; set; }

    public int Stat2 { get; set; }

    public int Stat3 { get; set; }
}

/// <summary>
/// The skill table of the world and its experience categories: <c>skills[]</c>, <c>exp_cat[]</c> and <c>nrofexpcat</c> as
/// <c>init_new_exp_system</c> leaves them. One per world (<see cref="GkHost.Skills"/>). NOT ported of skill_util.c: everything that
/// uses a skill (<c>do_skill</c>, <c>change_skill</c>, <c>calc_skill_exp</c>, <c>learn_skill</c>, <c>check_skill_to_apply</c>: wave 5D and
/// phase 4) and <c>find_skill_exp_name</c>, which only fills the client's skill experience statistics.
/// </summary>
internal sealed class SkillTable
{
    /// <summary>global.h <c>MAX_EXP_CAT</c>.</summary>
    public const int MaxExpCat = 7;

    /// <summary>global.h <c>EXP_NONE</c>: the "miscellaneous" category, the last one.</summary>
    public const int ExpNone = MaxExpCat - 1;

    /// <summary>living.h <c>NO_STAT_VAL</c>.</summary>
    public const int NoStatVal = 99;

    private const int Dex = Define.DEX;
    private const int IntStat = Define.INT;
    private const int Cha = Define.CHA;
    private const int Str = Define.STR;
    private const int Wis = Define.WIS;
    private const int Pow = Define.POW;
    private const int No = NoStatVal;

    /// <summary>skillist.h <c>skills[]</c>, in the order of skills.h (<c>SK_*</c>).</summary>
    public SkillEntry[] Skills { get; } =
    {
        new("stealing", ExpNone, 0, 0, 0.1f, Dex, IntStat, No),
        new("lockpicking", ExpNone, 0, 50, 1.5f, Dex, IntStat, No),
        new("hiding", ExpNone, 0, 10, 2.5f, Dex, Cha, No),
        new("smithery", ExpNone, 10, 1, 1, IntStat, Str, No),
        new("bowyer", ExpNone, 10, 1, 1, IntStat, Dex, No),
        new("jeweler", ExpNone, 10, 1, 1, IntStat, Dex, Wis),
        new("alchemy", ExpNone, 10, 1, 1, IntStat, Wis, Dex),
        new("thaumaturgy", ExpNone, 10, 1, 1, IntStat, Pow, Wis),
        new("literacy", ExpNone, 10, 1, 1, IntStat, Wis, No),
        new("bargaining", ExpNone, 0, 0, 0, No, No, No),
        new("jumping", ExpNone, 0, 5, 2.5f, Str, Dex, No),
        new("sense magic", ExpNone, 10, 10, 1, Pow, IntStat, No),
        new("oratory", ExpNone, 5, 1, 2, Cha, IntStat, No),
        new("singing", ExpNone, 5, 1, 2, Cha, IntStat, No),
        new("sense curse", ExpNone, 10, 10, 1, Wis, Pow, No),
        new("find traps", ExpNone, 0, 0, 0, No, No, No),
        new("meditation", ExpNone, 10, 0, 0, No, No, No),
        new("punching", ExpNone, 0, 0, 1, Str, Dex, No),
        new("flame touch", ExpNone, 0, 0, 1, Str, Dex, IntStat),
        new("karate", ExpNone, 0, 0, 1, Str, Dex, No),
        new("mountaineer", ExpNone, 0, 0, 0, No, No, No),
        new("woodsman", ExpNone, 0, 1, 1, IntStat, Dex, No),
        new("inscription", ExpNone, 0, 1, 5, Pow, IntStat, No),
        new("melee weapons", ExpNone, 0, 0, 1, Str, Dex, No),
        new("missile weapons", ExpNone, 0, 0, 1, Str, Dex, No),
        new("throwing", ExpNone, 0, 0, 1, Str, Dex, No),
        new("wizardry", ExpNone, 0, 0, 0, Pow, IntStat, Wis),
        new("disarm traps", ExpNone, 0, 1, 0.5f, Dex, IntStat, No),
        new("set traps", ExpNone, 0, 1, 0.5f, IntStat, Dex, No),
        new("use magic item", ExpNone, 0, 0, 0, No, No, No),
        new("praying", ExpNone, 0, 0, 0, Wis, Pow, IntStat),
        new("clawing", ExpNone, 0, 0, 0, Str, Dex, No),
        new("levitation", ExpNone, 0, 0, 0, No, No, No),
    };

    /// <summary>global.h <c>exp_cat[]</c>: one experience object for each experience archetype of the world, in the order of the archetypes.</summary>
    public List<CfObject> ExpCat { get; } = new();

    /// <summary>global.h <c>nrofexpcat</c>.</summary>
    public int NrOfExpCat => ExpCat.Count;

    /// <summary>
    /// skill_util.c <c>init_new_exp_system</c>: finds the experience objects among the archetypes (<c>init_exp_obj</c>), links the skills
    /// to them by the stats they share (<c>link_skills_to_exp</c>) and applies the text of the <c>skill_params</c> file
    /// (<c>read_skill_params</c>). C exits when the world has no experience archetype or more than <see cref="MaxExpCat"/>; the
    /// port logs the error and goes on (with none, or with the first ones) (docs/differences.md).
    /// </summary>
    public static SkillTable Create(CfWorld world, string skillParams)
    {
        var table = new SkillTable();
        table.InitExpObj(world);
        table.LinkSkillsToExp(world);
        table.ReadSkillParams(world, skillParams);
        return table;
    }

    private void InitExpObj(CfWorld world)
    {
        for (Archetype? at = world.FirstArchetype; at != null; at = at.Next)
        {
            if (at.Clone.Type != Define.EXPERIENCE)
            {
                continue;
            }
            if (ExpCat.Count == MaxExpCat)
            {
                world.Log(LogLevel.Error, "Aborting! Reached limit of available experience\ncategories. Need to increase value of MAX_EXP_CAT.\n");
                break;
            }
            CfObject exp = world.GetObject();
            exp.Level = 1;
            exp.Stats.Exp = 0;
            world.CopyObject(at.Clone, exp);
            ExpCat.Add(exp);
        }
        if (ExpCat.Count == 0)
        {
            world.Log(LogLevel.Error, "Aborting! No experience objects found in archetypes.\n");
        }
    }

    /// <summary>skill_util.c <c>check_link</c>: true when the stat is set in the experience object.</summary>
    private static bool CheckLink(CfWorld world, int stat, CfObject exp)
    {
        switch (stat)
        {
            case Define.STR:
                return exp.Stats.Str != 0;
            case Define.CON:
                return exp.Stats.Con != 0;
            case Define.DEX:
                return exp.Stats.Dex != 0;
            case Define.INT:
                return exp.Stats.Int != 0;
            case Define.WIS:
                return exp.Stats.Wis != 0;
            case Define.POW:
                return exp.Stats.Pow != 0;
            case Define.CHA:
                return exp.Stats.Cha != 0;
            case NoStatVal:
                return false;
            default:
                world.Log(LogLevel.Error, "Aborting! Tried to link skill with unknown stat!\n");
                throw new CrossfireFatalException("check_link: unknown stat");
        }
    }

    // skill_util.c link_skills_to_exp: the last experience object that has the skill's first stat wins; one that has the second or the
    // third stat only counts while the skill has none
    private void LinkSkillsToExp(CfWorld world)
    {
        foreach (SkillEntry skill in Skills)
        {
            for (int j = 0; j < NrOfExpCat; j++)
            {
                if (CheckLink(world, skill.Stat1, ExpCat[j]))
                {
                    skill.Category = j;
                }
                else if (skill.Category == ExpNone && CheckLink(world, skill.Stat2, ExpCat[j]))
                {
                    skill.Category = j;
                }
                else if (skill.Category == ExpNone && CheckLink(world, skill.Stat3, ExpCat[j]))
                {
                    skill.Category = j;
                }
                else if (j == NrOfExpCat || skill.Stat1 == NoStatVal)
                {
                    skill.Category = ExpNone;
                }
            }
        }
    }

    /// <summary>skill_util.c <c>lookup_skill_by_name</c>: the index of the first skill whose name the text starts with (the line's newline stays), -1 when none.</summary>
    public int LookupSkillByName(string name)
    {
        for (int nr = 0; nr < Skills.Length; nr++)
        {
            if (name.Length >= Skills[nr].Name.Length && string.CompareOrdinal(name, 0, Skills[nr].Name, 0, Skills[nr].Name.Length) == 0)
            {
                return nr;
            }
        }
        return -1;
    }

    // skill_util.c read_skill_params: pairs of lines, a name and "category time bexp lexp stat1 stat2 stat3"; a line that starts with # is
    // skipped alone, and a name that no skill has is skipped without its numbers line, which then is read as a name
    private void ReadSkillParams(CfWorld world, string text)
    {
        var fp = new LoadSource(text);
        int cat = 0;
        int time = 0;
        int bexp = 0;
        float lexp = 0;
        int stat1 = 0;
        int stat2 = 0;
        int stat3 = 0;
        string? name;
        while ((name = Fgets(fp, 255)) != null)
        {
            if (name.Length > 0 && name[0] == '#')
            {
                continue;
            }
            int index = LookupSkillByName(name);
            if (index == -1)
            {
                world.Log(LogLevel.Error, "\nskill_params has unrecognized skill: " + name);
                continue;
            }
            string? attrib = Fgets(fp, 255);
            if (attrib == null)
            {
                break; // fgets leaves the buffer as it was: the values of the last line are set again, which changes nothing
            }
            Sscanf(attrib, ref cat, ref time, ref bexp, ref lexp, ref stat1, ref stat2, ref stat3);
            SkillEntry skill = Skills[index];
            skill.Category = cat;
            skill.Time = time;
            skill.Bexp = bexp;
            skill.Lexp = lexp;
            skill.Stat1 = stat1;
            skill.Stat2 = stat2;
            skill.Stat3 = stat3;
        }
    }

    // fgets(buf, size, fp) on a LoadSource
    private static string? Fgets(LoadSource fp, int size)
    {
        if (fp.AtEnd)
        {
            return null;
        }
        int nl = fp.Text.IndexOf('\n', fp.Pos);
        int end = nl >= 0 ? nl + 1 : fp.Text.Length;
        if (end - fp.Pos > size - 1)
        {
            end = fp.Pos + size - 1;
        }
        string line = fp.Text.Substring(fp.Pos, end - fp.Pos);
        fp.Pos = end;
        return line;
    }

    // sscanf(line, "%d %d %d %f %d %d %d", ...): each number that reads is stored, the first that does not stops the scan
    private static void Sscanf(string line, ref int cat, ref int time, ref int bexp, ref float lexp, ref int stat1, ref int stat2, ref int stat3)
    {
        string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int n = 0;
        if (!Int(words, ref n, ref cat) || !Int(words, ref n, ref time) || !Int(words, ref n, ref bexp))
        {
            return;
        }
        if (n >= words.Length || !float.TryParse(words[n], NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
        {
            return;
        }
        lexp = f;
        n++;
        if (!Int(words, ref n, ref stat1) || !Int(words, ref n, ref stat2))
        {
            return;
        }
        Int(words, ref n, ref stat3);
    }

    private static bool Int(string[] words, ref int n, ref int value)
    {
        if (n < words.Length && int.TryParse(words[n], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v))
        {
            value = v;
            n++;
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ the player's side

    /// <summary>
    /// skill_util.c <c>init_player_exp</c>: gives the player one experience object of each category it lacks (a new player gets all of
    /// them, each with its share of the player's experience), sets them applied, and makes the player's experience their sum. Returns
    /// false for a non-player. C's loop for a player that has some but not all of the objects reads past its array and adds copies
    /// without end; the port adds each missing object once.
    /// </summary>
    public bool InitPlayerExp(CfWorld world, CfObject pl)
    {
        if (pl.Type != Define.PLAYER)
        {
            world.Log(LogLevel.Error, "init_player_exp(): called non-player " + pl.Name + ".\n");
            return false;
        }
        var expOb = new List<CfObject>();
        for (CfObject? tmp = pl.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Type == Define.EXPERIENCE)
            {
                if (expOb.Count == MaxExpCat)
                {
                    return false;
                }
                expOb.Add(tmp);
            }
        }
        // no experience objects: the situation of a new player or of a file from before the skills
        if (expOb.Count == 0)
        {
            for (int j = 0; j < NrOfExpCat; j++)
            {
                CfObject tmp = world.GetObject();
                world.CopyObject(ExpCat[j], tmp);
                tmp = world.InsertObInOb(tmp, pl);
                tmp.Stats.Exp = pl.Stats.Exp / NrOfExpCat;
                expOb.Add(tmp);
                world.Host.EsrvSendItem(pl, tmp);
            }
        }
        else if (expOb.Count != NrOfExpCat)
        {
            // the situation of an old save file
            if (expOb.Count < NrOfExpCat)
            {
                world.Log(LogLevel.Error, "init_player_exp(): player has too few experience objects.\n");
            }
            if (expOb.Count > NrOfExpCat)
            {
                world.Log(LogLevel.Error, "init_player_exp(): player has too many experience objects.\n");
            }
            for (int j = 0; j < NrOfExpCat; j++)
            {
                if (expOb.Any(e => e.Name == ExpCat[j].Name))
                {
                    continue;
                }
                CfObject tmp = world.GetObject();
                world.CopyObject(ExpCat[j], tmp);
                tmp = world.InsertObInOb(tmp, pl);
                expOb.Add(tmp);
                world.Host.EsrvSendItem(pl, tmp);
            }
        }

        // the experience objects are applied (fix_player needs it), and the score of the player is the sum of the valid ones
        pl.Stats.Exp = 0;
        foreach (CfObject exp in expOb)
        {
            if (!exp.QueryFlag(Define.FLAG_APPLIED))
            {
                exp.SetFlag(Define.FLAG_APPLIED);
            }
            pl.Stats.Exp += exp.Stats.Exp;
            LivingRules.PlayerLvlAdj(world, null, exp);
        }
        return true;
    }

    /// <summary>
    /// skill_util.c <c>link_player_skills</c>: every skill of the player is unapplied and linked to the experience object of its
    /// category (<see cref="CfObject.ExpObj"/>); the player's experience is made the sum of its experience objects'. A player with
    /// the wrong number of experience objects gets them first (<see cref="InitPlayerExp"/>). C then calls itself again, which for
    /// more experience objects than categories never ends: the port gives up with an error. <c>fix_player</c> is for the caller.
    /// </summary>
    public bool LinkPlayerSkills(CfWorld world, CfObject pl, bool again = false)
    {
        var skillObs = new List<CfObject>();
        var expObs = new List<CfObject>();

        // we're going to unapply all skills
        pl.ChosenSkill = null;
        pl.ClearFlag(Define.FLAG_READY_SKILL);

        // first find all exp and skill objects
        for (CfObject? tmp = pl.Inv; tmp != null && skillObs.Count < 100; tmp = tmp.Below)
        {
            if (tmp.Type == Define.EXPERIENCE)
            {
                expObs.Add(tmp);
                tmp.Nrof = 1; // to handle multiple instances
            }
            else if (tmp.Type == Define.SKILL)
            {
                // for startup, lets unapply all skills
                tmp.ClearFlag(Define.FLAG_APPLIED);
                if (tmp.Invisible != 0)
                {
                    tmp.Nrof = 1; // to handle multiple instances
                    skillObs.Add(tmp);
                }
            }
        }
        if (expObs.Count != NrOfExpCat)
        {
            world.Log(LogLevel.Error, "link_player_skills() - player has bad number of exp obj\n");
            if (again || !InitPlayerExp(world, pl))
            {
                world.Log(LogLevel.Error, "link_player_skills() - failed to correct problem.\n");
                return false;
            }
            return LinkPlayerSkills(world, pl, true);
        }

        // ok, link the associated skills to exp objects
        foreach (CfObject skill in skillObs)
        {
            int cat = CategoryOf(skill);
            if (cat == ExpNone)
            {
                continue;
            }
            skill.ExpObj = expObs.FirstOrDefault(e => e.Name == ExpCat[cat].Name);
        }

        // the score (total experience) of the player is the sum of all valid experience objects
        pl.Stats.Exp = 0;
        foreach (CfObject exp in expObs)
        {
            if (!exp.QueryFlag(Define.FLAG_APPLIED))
            {
                exp.SetFlag(Define.FLAG_APPLIED);
            }
            pl.Stats.Exp += exp.Stats.Exp;
            LivingRules.PlayerLvlAdj(world, null, exp);
        }
        return true;
    }

    // skills[skillop->stats.sp].category; a skill number outside the table (C reads whatever lies after it) is in no category
    private int CategoryOf(CfObject skill)
    {
        int sp = skill.Stats.Sp;
        if (sp < 0 || sp >= Skills.Length)
        {
            return ExpNone;
        }
        int cat = Skills[sp].Category;
        return cat >= 0 && cat < NrOfExpCat ? cat : ExpNone;
    }

    /// <summary>
    /// skill_util.c <c>link_player_skill</c>: links a skill to the experience object of its category in the player's inventory when it is
    /// applied or learned. True when the skill has a category, false for a miscellaneous skill (which has no experience object).
    /// </summary>
    public bool LinkPlayerSkill(CfObject pl, CfObject skillop)
    {
        int cat = CategoryOf(skillop);
        if (cat != ExpNone)
        {
            // the skill has an exp object: find the right one in the player's inventory
            for (CfObject? tmp = pl.Inv; tmp != null; tmp = tmp.Below)
            {
                if (tmp.Type == Define.EXPERIENCE && tmp.Name == ExpCat[cat].Name)
                {
                    skillop.ExpObj = tmp;
                    break;
                }
            }
            return true;
        }
        skillop.ExpObj = null;
        return false;
    }

    /// <summary>skill_util.c <c>unlink_skill</c>: the skill no longer has an experience object. A skill that is not in a player's inventory is an error.</summary>
    public static void UnlinkSkill(CfWorld world, CfObject? skillop)
    {
        CfObject? op = skillop?.Env;
        if (skillop == null || op == null || op.Type != Define.PLAYER)
        {
            world.Log(LogLevel.Error, "Error: unlink_skill() called for non-player!\n");
            return;
        }
        skillop.ExpObj = null;
    }

    // ------------------------------------------------------------------ change_skill and check_skill_to_apply (wave 5D)

    /// <summary>include/player.h <c>rangetype</c> <c>range_bow</c>.</summary>
    public const int RangeBow = 1;

    /// <summary>include/player.h <c>range_wand</c>.</summary>
    public const int RangeWand = 3;

    /// <summary>include/player.h <c>range_rod</c>.</summary>
    public const int RangeRod = 4;

    /// <summary>include/player.h <c>range_scroll</c>.</summary>
    public const int RangeScroll = 5;

    /// <summary>include/player.h <c>range_horn</c>.</summary>
    public const int RangeHorn = 6;

    /// <summary>include/player.h <c>range_skill</c>.</summary>
    public const int RangeSkill = 7;

    /// <summary>
    /// <c>apply_special(who, item, flag)</c> of apply.c (<c>flag</c> is <c>AP_APPLY</c> 1 or <c>AP_UNAPPLY</c> 2): 0 when the item was
    /// applied or taken off, 1 when it was not. Wave 6B sets it when it ports <c>apply_special</c>; until then (null)
    /// <see cref="ChangeSkill"/> cannot apply a skill that is not already the chosen one and says it could not.
    /// </summary>
    public Func<CfObject, CfObject, int, int>? ApplySpecial { get; set; }

    /// <summary>
    /// skill_util.c <c>change_skill</c>: the player <paramref name="who"/> readies the skill number <paramref name="skIndex"/> (or, with
    /// -1, none: the chosen skill is taken off). 1 when the skill is ready, else 0 (with the message "You have no knowledge of ..."
    /// when the player has no such skill).
    /// </summary>
    public int ChangeSkill(CfWorld world, CfObject who, int skIndex)
    {
        if (who.ChosenSkill != null && who.ChosenSkill.Stats.Sp == skIndex)
        {
            // optimization for changing skill to current skill
            if (who.Type == Define.PLAYER && who.Contr is Player current)
            {
                current.ShootType = RangeSkill;
            }
            return 1;
        }
        CfObject? tmp;
        if (skIndex >= 0 && skIndex < Skills.Length && (tmp = CfWorld.FindSkill(who, skIndex)) != null)
        {
            if (Apply(world, who, tmp, 1) != 0)
            {
                world.Log(LogLevel.Error, "BUG: change_skill(): can't apply new skill\n");
                return 0;
            }
            return 1;
        }
        if (who.ChosenSkill != null && Apply(world, who, who.ChosenSkill, 2) != 0)
        {
            world.Log(LogLevel.Error, "BUG: change_skill(): can't unapply old skill\n");
        }
        if (skIndex >= 0 && skIndex < Skills.Length)
        {
            world.Host.DrawInfo(who, "You have no knowledge of " + Skills[skIndex].Name + ".");
        }
        return 0;
    }

    private int Apply(CfWorld world, CfObject who, CfObject item, int flag)
    {
        if (ApplySpecial == null)
        {
            world.Log(LogLevel.Error, "apply_special is not available: the skill " + item.Name + " is not changed\n");
            return 1;
        }
        return ApplySpecial(who, item, flag);
    }

    /// <summary>
    /// skill_util.c <c>check_skill_to_apply</c>: when a player applies (or takes off) an item that needs a skill (a weapon, a bow, a
    /// potion, scroll, rod, wand or horn), the skill is checked and made the player's chosen skill, and the shoot type is set. 1 when
    /// the skill exists and could be readied; 0 for an item that needs none, when the item was taken off, and when the player
    /// cannot use it (then "You can't use that!" and the item's <c>FLAG_APPLIED</c> is cleared). Always 1 for a monster. C's note: call it
    /// after <c>FLAG_APPLIED</c> is set or cleared and before <see cref="LivingChange.ChangeAbil"/>.
    /// </summary>
    public int CheckSkillToApply(CfWorld world, CfObject who, CfObject item)
    {
        if (who.Type != Define.PLAYER)
        {
            return 1; // this function is only for players
        }
        int skill;
        int shoottype;
        // first figure out the required skill and the ending shoottype from the item
        switch (item.Type)
        {
            case Define.WEAPON:
                skill = Define.SK_MELEE_WEAPON;
                shoottype = RangeSkill;
                break;
            case Define.BOW:
                skill = Define.SK_MISSILE_WEAPON;
                shoottype = RangeBow;
                break;
            case Define.POTION:
            case Define.SCROLL:
                skill = Define.SK_USE_MAGIC_ITEM; // not literacy!
                shoottype = RangeScroll;
                break;
            case Define.ROD:
                skill = Define.SK_USE_MAGIC_ITEM;
                shoottype = RangeRod;
                break;
            case Define.WAND:
                skill = Define.SK_USE_MAGIC_ITEM;
                shoottype = RangeWand;
                break;
            case Define.HORN:
                skill = Define.SK_USE_MAGIC_ITEM;
                shoottype = RangeHorn;
                break;
            default:
                world.Log(LogLevel.Debug, "Warning: bad call of check_skill_to_apply()\nNo skill exists for item: " + item.Name + "\n");
                return 0;
        }

        // now alter the player's status appropriately
        if (!item.QueryFlag(Define.FLAG_APPLIED))
        {
            // we are trying to unapply
            if (who.ChosenSkill != null && who.ChosenSkill.Stats.Sp == skill)
            {
                ChangeSkill(world, who, -1);
            }
            return 0;
        }
        // we are trying to apply
        if (who.ChosenSkill == null || who.ChosenSkill.Stats.Sp != skill)
        {
            if (ChangeSkill(world, who, skill) == 0)
            {
                world.Host.DrawInfo(who, "You can't use that!");
                item.ClearFlag(Define.FLAG_APPLIED);
                return 0;
            }
        }
        if (who.Contr is Player pl)
        {
            pl.ShootType = shoottype;
        }
        return 1;
    }
}
