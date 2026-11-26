// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): the inventory functions of Graal Kingdoms' library, group A of
// docs/mud-phase3.md section 4.2, as far as they are C: the weight test of a pick up and the search of an archetype in an inventory.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/c_object.c (pick_up_object: the weight test):
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     common/object.c (present_arch_in_ob):
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     server/skill_util.c (the experience object's level, show_skills):
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
/// What the inventory functions of lane 5B need that does not touch the dispatcher: the weight test, counting and searching
/// by archetype, name or title, the skills of a player, and the lists the quick menu draws. The functions that change a tree
/// are the dispatcher's (<c>Dispatcher.Wave5B.cs</c>), which keeps tags, files and the client's strings right.
/// </summary>
internal static class Inventory
{
    /// <summary>The most one call adds: a count above it is refused (the sum of the weights stays inside an int).</summary>
    public const int MaxAdd = 100_000_000;

    /// <summary>The most entries a list written for the quick menu has (a request's effects are at most 4,096).</summary>
    public const int MaxListed = 1000;

    /// <summary>How many objects a stack stands for: an object without <c>nrof</c> is one (c_object.c <c>pick_up_object</c>: <c>tmp_nrof</c>).</summary>
    public static long StackCount(CfObject op) => op.Nrof == 0 ? 1 : op.Nrof;

    /// <summary>
    /// c_object.c <c>pick_up_object</c>: whether <paramref name="weight"/> more grams would be more than the player can carry:
    /// his own weight and what he carries, plus the new weight, against <c>weight_limit[Str]</c>.
    /// </summary>
    public static bool TooHeavy(CfObject pl, long weight) =>
        (long)pl.Weight + pl.Carrying + weight > ItemMatching.WeightLimitOf(pl.Stats.Str);

    /// <summary>object.c <c>present_arch_in_ob</c> for every stack: the objects of the top level of <paramref name="holder"/> made from <paramref name="at"/>, in order.</summary>
    public static List<CfObject> PresentArch(CfObject holder, Archetype at)
    {
        var list = new List<CfObject>();
        for (CfObject? c = holder.Inv; c != null; c = c.Below)
        {
            if (c.Arch == at)
            {
                list.Add(c);
            }
        }
        return list;
    }

    /// <summary>The sum of the objects of an archetype in an inventory; <paramref name="deep"/> goes into the containers too.</summary>
    public static long CountArch(CfObject holder, Archetype at, bool deep)
    {
        long sum = 0;
        for (CfObject? c = holder.Inv; c != null; c = c.Below)
        {
            if (c.Arch == at)
            {
                sum += StackCount(c);
            }
            if (deep && c.Inv != null)
            {
                sum += CountArch(c, at, true);
            }
        }
        return sum;
    }

    /// <summary>
    /// c_object.c <c>find_best_object_match</c> over the inventory of <paramref name="holder"/>; with <paramref name="deep"/> over the
    /// containers inside too (the top level first, so that on a tie the first of the top level wins).
    /// </summary>
    public static CfObject? FindBest(CfWorld world, CfObject holder, string text, bool deep)
    {
        if (!deep)
        {
            return ItemMatching.FindBestObjectMatch(world, holder, text);
        }
        CfObject? best = null;
        int matchVal = 0;
        Scan(holder);
        return best;

        void Scan(CfObject at)
        {
            for (CfObject? tmp = at.Inv; tmp != null; tmp = tmp.Below)
            {
                if (tmp.Invisible != 0)
                {
                    continue;
                }
                int value;
                if ((value = ItemMatching.ItemMatchedString(world, holder, tmp, text)) > matchVal)
                {
                    matchVal = value;
                    best = tmp;
                }
            }
            for (CfObject? tmp = at.Inv; tmp != null; tmp = tmp.Below)
            {
                if (tmp.Invisible == 0 && tmp.Inv != null)
                {
                    Scan(tmp);
                }
            }
        }
    }

    /// <summary>
    /// <c>findinvobjectbytitle</c> (an Alpaxis rule: Graal Kingdoms' source of the function is not known): the first visible object whose
    /// <c>title</c> is the text (letters in any case), else the first whose name and title, as "name title", are.
    /// </summary>
    public static CfObject? FindByTitle(CfObject holder, string text, bool deep)
    {
        if (text.Length == 0)
        {
            return null;
        }
        return Find(holder, c => c.Title != null && ItemMatching.StrCaseCmp(c.Title, text) == 0)
            ?? Find(holder, c => c.Title != null && ItemMatching.StrCaseCmp(c.Name + " " + c.Title, text) == 0);

        CfObject? Find(CfObject at, Func<CfObject, bool> test)
        {
            for (CfObject? c = at.Inv; c != null; c = c.Below)
            {
                if (c.Invisible == 0 && test(c))
                {
                    return c;
                }
            }
            if (deep)
            {
                for (CfObject? c = at.Inv; c != null; c = c.Below)
                {
                    if (c.Invisible == 0 && c.Inv != null && Find(c, test) is { } inner)
                    {
                        return inner;
                    }
                }
            }
            return null;
        }
    }

    /// <summary>True when <paramref name="item"/> is inside <paramref name="holder"/>: one level down, or at any depth with <paramref name="deep"/>.</summary>
    public static bool IsInside(CfObject item, CfObject holder, bool deep)
    {
        if (item.Env == holder)
        {
            return true;
        }
        if (deep)
        {
            for (CfObject? c = item.Env; c != null; c = c.Env)
            {
                if (c == holder)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ skills

    /// <summary>The skill object (type 43) of a player by its name (letters in any case), or null; the top level of the tree is looked at, where skills are.</summary>
    public static CfObject? SkillOf(CfObject pl, string name)
    {
        string wanted = name.Trim();
        if (wanted.Length == 0)
        {
            return null;
        }
        for (CfObject? c = pl.Inv; c != null; c = c.Below)
        {
            if (c.Type == Define.SKILL && ItemMatching.StrCaseCmp(c.Name, wanted) == 0)
            {
                return c;
            }
        }
        return null;
    }

    /// <summary>
    /// The level of a skill: that of its experience object, which is always right (skill_util.c <c>show_skills</c>: the level of the
    /// skill object itself is only updated when the skill is changed to), else the skill object's own.
    /// </summary>
    public static int SkillLevel(CfObject skill) => skill.ExpObj is { } exp ? exp.Level : skill.Level;

    // ------------------------------------------------------------------ the lists of the quick menu

    /// <summary>The types that the quick menu lists as weapons: weapons, bows and the two tool types of Graal Kingdoms that are wielded (docs/mud-phase3.md section 4.4).</summary>
    public static bool IsWeapon(CfObject op) => op.Type is Define.WEAPON or Define.BOW or ItemStrings.TypeTool or ItemStrings.TypeTool2;

    /// <summary>"name,icon" of the visible objects of the player's top level that pass <paramref name="pick"/>, in the order of the inventory.</summary>
    public static List<string> ListItems(CfWorld world, CfObject pl, Func<CfObject, bool> pick)
    {
        var list = new List<string>();
        for (CfObject? c = pl.Inv; c != null && list.Count < MaxListed; c = c.Below)
        {
            if (c.Invisible == 0 && pick(c))
            {
                list.Add(ItemStrings.DisplayName(world, c) + "," + TMudArchMembers.Icon(c.Face?.Name));
            }
        }
        return list;
    }

    /// <summary>"name,icon" of the player's skills (type 43 objects, which are invisible).</summary>
    public static List<string> ListSkills(CfWorld world, CfObject pl)
    {
        var list = new List<string>();
        for (CfObject? c = pl.Inv; c != null && list.Count < MaxListed; c = c.Below)
        {
            if (c.Type == Define.SKILL)
            {
                list.Add(FileText.FromBytesText(c.Name ?? "").Replace(',', ';') + "," + TMudArchMembers.Icon(c.Face?.Name));
            }
        }
        return list;
    }

    /// <summary>The names of the spell paths, comma separated (spellist.h <c>spellpathnames</c>): what <c>mudlistspells</c> writes as <c>spellpaths</c>.</summary>
    public static string SpellPaths() => string.Join(",", Tables.Spellpathnames);

    // ------------------------------------------------------------------ order

    /// <summary>Links the inventory of <paramref name="holder"/> in a new order: the objects of <paramref name="first"/> at the head, in that order, then the others as they were.</summary>
    public static void Reorder(CfObject holder, IReadOnlyList<CfObject> first)
    {
        var order = new List<CfObject>(first);
        var seen = new HashSet<CfObject>(first, ReferenceEqualityComparer.Instance);
        for (CfObject? c = holder.Inv; c != null; c = c.Below)
        {
            if (!seen.Contains(c))
            {
                order.Add(c);
            }
        }
        CfObject? above = null;
        foreach (CfObject c in order)
        {
            c.Above = above;
            c.Below = null;
            if (above != null)
            {
                above.Below = c;
            }
            above = c;
        }
        holder.Inv = order.Count == 0 ? null : order[0];
    }
}
