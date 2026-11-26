// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/apply.c <c>check_weapon_power</c>, <c>manual_apply</c> (as the
// switch of <see cref="Apply.DefaultApply"/>, without its plugin hook), <c>player_apply</c> and <c>apply_special</c>: what applying an
// object does to its owner, and the wielding, wearing and readying of objects (docs/mud-phase3.md section 4.4). The command
// <c>apply</c> is in ApplyCommand.cs. The script's part (the events onApplyItem and onApply, the hooks after a state change) is in the
// dispatcher's Dispatcher.Wave6B.cs; where this file calls it, the line says "GK".
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
namespace Crossfire.Extension.Gk;

/// <summary>
/// Applying objects (server/apply.c). The types that another lane of wave 6 owns (containers and keys, food and potions, signs and
/// books) are asked first through <see cref="Dispatcher.ApplyByLane"/>; the rest of <c>manual_apply</c>'s switch is here. What needs
/// code that is not ported (scrolls and spell books, rods and horns, skill items, exits, the improvers) tells the player it is not
/// available yet and changes nothing, so that nothing is used up for nothing (docs/mud-phase3.md section 4.4). What differs from C is in
/// docs/differences.md ("Apply").
/// </summary>
internal sealed partial class Apply
{
    /// <summary>include/define.h <c>AP_APPLY</c>: always apply (never toggle).</summary>
    public const int ApApply = 1;

    /// <summary>include/define.h <c>AP_UNAPPLY</c>: always unapply.</summary>
    public const int ApUnapply = 2;

    /// <summary>include/define.h <c>AP_BASIC_FLAGS</c>.</summary>
    public const int ApBasicFlags = 15;

    /// <summary>include/define.h <c>AP_NO_MERGE</c>: do not merge an unapplied object with others.</summary>
    public const int ApNoMerge = 16;

    /// <summary>include/define.h <c>AP_IGNORE_CURSE</c>: unapply cursed objects too.</summary>
    public const int ApIgnoreCurse = 32;

    /// <summary>The line a player reads for an apply that needs code this extension does not have yet; the object is not used up.</summary>
    public const string NotAvailableYet = "That is not available yet.";

    private readonly Dispatcher d;

    public Apply(Dispatcher dispatcher)
    {
        d = dispatcher;
    }

    private CfWorld World => d.WorldOrNull!;

    private void Say(CfObject who, string text) => World.Host.DrawInfo(who, text);

    /// <summary>Graal Kingdoms' types that are wielded like a weapon (a tool, and the second kind of tool): apply.c has no case for them.</summary>
    public static bool IsWeaponLike(int type) => type is Define.WEAPON or ItemStrings.TypeTool or ItemStrings.TypeTool2;

    /// <summary>Graal Kingdoms' clothes (a mask, legs, a body): worn in a slot of their own, one object per type.</summary>
    public static bool IsClothing(int type) => type is ItemStrings.TypeMask or ItemStrings.TypeLegs or ItemStrings.TypeBody;

    /// <summary>Two objects that compete for one place of the body: the same type, or both wielded like a weapon.</summary>
    private static bool SameSlot(CfObject a, CfObject b) => a.Type == b.Type || (IsWeaponLike(a.Type) && IsWeaponLike(b.Type));

    /// <summary>
    /// apply.c <c>check_weapon_power</c>: true when <paramref name="who"/> is strong enough to use a weapon with
    /// <paramref name="improvs"/> improvements. A player's level is that of his wc experience object.
    /// </summary>
    public bool CheckWeaponPower(CfObject who, int improvs)
    {
        int level = who.Level;
        if (who.Type == Define.PLAYER)
        {
            CfObject? wcObj;
            for (wcObj = who.Inv; wcObj != null; wcObj = wcObj.Below)
            {
                if (wcObj.Type == Define.EXPERIENCE && wcObj.Stats.Str != 0)
                {
                    break;
                }
            }
            if (wcObj == null)
            {
                World.Log(LogLevel.Error, "Error: Player: " + who.Name + " lacks wc experience object\n");
            }
            else
            {
                level = wcObj.Level;
            }
        }
        return improvs <= (level / 5) + 5;
    }

    // ------------------------------------------------------------------ player_apply and manual_apply

    /// <summary>
    /// apply.c <c>player_apply</c>: <paramref name="pl"/> applies <paramref name="op"/>. <paramref name="quiet"/> leaves out the
    /// "I don't know how to apply" and "You must get it first!" lines. Returns what <see cref="ManualApply"/> returned: 1 when the object
    /// was applied, or the script decides (an event was sent), or an error was told. Not ported: the <c>FLAG_WAS_WIZ</c> branch (no
    /// object here was made by a wizard).
    /// </summary>
    public int PlayerApply(CfObject pl, CfObject op, int aflag, bool quiet)
    {
        if (op.Env == null && pl.QueryFlag(Define.FLAG_FLYING))
        {
            // player is flying and applying object not in inventory
            if (!pl.QueryFlag(Define.FLAG_WIZ) && !op.QueryFlag(Define.FLAG_FLYING) && !op.QueryFlag(Define.FLAG_FLY_ON))
            {
                Say(pl, "But you are floating high above the ground!");
                return 0;
            }
        }

        if (pl.Contr is Player contr)
        {
            contr.LastUsed = op;
            contr.LastUsedId = op.Count;
        }

        int tmp = ManualApply(pl, op, aflag);
        if (!quiet)
        {
            Report(pl, op, tmp);
        }
        return tmp;
    }

    // the lines player_apply says for the answer of manual_apply
    private void Report(CfObject pl, CfObject op, int tmp)
    {
        if (tmp == 0)
        {
            Say(pl, "I don't know how to apply the " + World.QueryName(op) + ".");
        }
        else if (tmp == 2)
        {
            Say(pl, "You must get it first!\n");
        }
    }

    /// <summary>
    /// apply.c <c>manual_apply</c>: what applying <paramref name="tmp"/> does for <paramref name="op"/>: 0 when <paramref name="op"/> cannot
    /// apply objects of that type, 1 when it was applied or an error was told, 2 when the object has to be in the inventory. The place of
    /// C's plugin hook is the script's <c>onApply</c> (GK: <see cref="Dispatcher.SendApplyEvent"/>): when the dispatcher sent it, the script
    /// answers and decides (it calls <see cref="DefaultApply"/> through the method <c>defaultApply</c>), and the answer is 1.
    /// </summary>
    public int ManualApply(CfObject op, CfObject tmp, int aflag)
    {
        if (tmp.Head != null)
        {
            tmp = tmp.Head;
        }
        if (Refused(op, tmp) is { } refused)
        {
            return refused;
        }
        // GK: the plugin hook of C. A script that handles onApply decides; with none, the host's fallback runs defaultApply.
        if (d.SendApplyEvent(op, tmp, aflag))
        {
            return 1;
        }
        return Switch(op, tmp, aflag);
    }

    /// <summary>
    /// The switch of <c>manual_apply</c> without the plugin hook: what a script's <c>defaultApply</c> does, with what <c>player_apply</c>
    /// says when the answer is 0 or 2. Returns <c>manual_apply</c>'s answer.
    /// </summary>
    public int DefaultApply(CfObject op, CfObject tmp, int aflag)
    {
        if (tmp.Head != null)
        {
            tmp = tmp.Head;
        }
        int answer = Refused(op, tmp) ?? Switch(op, tmp, aflag);
        Report(op, tmp, answer);
        return answer;
    }

    // the checks that come before the plugin hook in manual_apply; null when none refuses
    private int? Refused(CfObject op, CfObject tmp)
    {
        if (tmp.QueryFlag(Define.FLAG_UNPAID) && !tmp.QueryFlag(Define.FLAG_APPLIED))
        {
            if (op.Type == Define.PLAYER)
            {
                Say(op, "You should pay for it first.");
                return 1;
            }
            return 0; // monsters just skip unpaid items
        }
        // monsters mustn't apply random chests, nor magic_mouths with a counter
        if (op.Type != Define.PLAYER && tmp.Type == Define.TREASURE)
        {
            return 0;
        }
        return null;
    }

    private int Switch(CfObject op, CfObject tmp, int aflag)
    {
        // GK: the types of the other lanes of wave 6 (containers and keys, food and potions, signs and books, ...)
        if (d.ApplyByLane(op, tmp, aflag) is { } byLane)
        {
            return byLane;
        }
        switch (tmp.Type)
        {
            case Define.CF_HANDLE:
            case Define.TRIGGER:
                // buttons and levers need the map's links (button.c), which the object model has no squares for; Graal Kingdoms has
                // one trigger archetype and no handle: applying them does nothing (docs/differences.md)
                return 1;

            case Define.EXIT:
                if (op.Type != Define.PLAYER)
                {
                    return 0;
                }
                return NotAvailable(op, tmp); // enter_exit is a change of level: the host's, not the extension's

            case Define.SKILLSCROLL:
            case Define.SPELLBOOK:
                return op.Type == Define.PLAYER ? NotAvailable(op, tmp) : 0;

            case Define.SCROLL:
            case Define.SAVEBED:
            case Define.ARMOUR_IMPROVER:
            case Define.WEAPON_IMPROVER:
            case Define.CLOCK:
            case Define.MENU:
            case Define.POWER_CRYSTAL:
                return NotAvailable(op, tmp);

            case Define.ROD:
            case Define.HORN:
            case Define.SKILL:
                // readying a rod or a horn, or a skill tool, belongs with their spells and skills (phase 4); apply_special has the cases
                return NotAvailable(op, tmp);

            case Define.WEAPON:
            case Define.ARMOUR:
            case Define.BOOTS:
            case Define.GLOVES:
            case Define.AMULET:
            case Define.GIRDLE:
            case Define.BRACERS:
            case Define.SHIELD:
            case Define.HELMET:
            case Define.RING:
            case Define.CLOAK:
            case Define.WAND:
            case Define.BOW:
            case ItemStrings.TypeTool: // Graal Kingdoms' own types (docs/mud-phase3.md section 4.4)
            case ItemStrings.TypeTool2:
            case ItemStrings.TypeMask:
            case ItemStrings.TypeLegs:
            case ItemStrings.TypeBody:
                if (tmp.Env != op)
                {
                    return 2; // not in inventory
                }
                ApplySpecial(op, tmp, aflag);
                return 1;

            case 160: // furniture and house doors: housing is not part of phase 3
            case 161:
                Say(op, "You can't apply that.");
                return 1;

            default:
                return 0;
        }
    }

    private int NotAvailable(CfObject op, CfObject tmp)
    {
        Say(op, NotAvailableYet);
        return 1; // told: nothing was used
    }

    // ------------------------------------------------------------------ apply_special

    /// <summary>
    /// apply.c <c>apply_special</c>: <paramref name="who"/> wields, wears or readies <paramref name="op"/> (or takes it off); 0 when it
    /// was done, 1 when it was not. <paramref name="aflags"/> is 0 (toggle), <see cref="ApApply"/> or <see cref="ApUnapply"/> with
    /// <see cref="ApNoMerge"/> and <see cref="ApIgnoreCurse"/> as options. It does not check for unpaid objects. GK: Graal Kingdoms'
    /// tools (162, 163) are wielded in the place of the weapon, and its clothes (164 to 166) are worn in a slot per type, like armour
    /// that anyone may wear. After a change, <see cref="Dispatcher.SendApplyHook"/> tells the player's script.
    /// </summary>
    public int ApplySpecial(CfObject? who, CfObject op, int aflags)
    {
        CfWorld w = World;
        int basicFlag = aflags & ApBasicFlags;
        string buf = "";
        CfObject? tmp;
        bool i;

        if (who == null)
        {
            w.Log(LogLevel.Error, "apply_special() from object without environment.\n");
            return 1;
        }
        if (op.Env != who)
        {
            return 1; // op is not in inventory
        }

        if (op.QueryFlag(Define.FLAG_APPLIED))
        {
            // always apply, so no reason to unapply
            if (basicFlag == ApApply)
            {
                return 0;
            }
            if ((aflags & ApIgnoreCurse) == 0 && (op.QueryFlag(Define.FLAG_CURSED) || op.QueryFlag(Define.FLAG_DAMNED)))
            {
                Say(who, "No matter how hard you try, you just can't\nremove " + w.QueryName(op) + ".");
                return 1;
            }
            op.ClearFlag(Define.FLAG_APPLIED);
            switch (op.Type)
            {
                case Define.WEAPON:
                case ItemStrings.TypeTool:
                case ItemStrings.TypeTool2:
                    LivingChange.ChangeAbil(w, who, op);
                    // 'unready' melee weapons skill if it is current skill
                    if (op.Type == Define.WEAPON)
                    {
                        d.SkillTable?.CheckSkillToApply(w, who, op);
                    }
                    if (who.QueryFlag(Define.FLAG_READY_WEAPON))
                    {
                        who.ClearFlag(Define.FLAG_READY_WEAPON);
                    }
                    who.CurrentWeaponScript = null;
                    who.CurrentWeapon = null;
                    buf = "You unwield " + w.QueryName(op) + ".";
                    break;

                case Define.SKILL: // allows objects to impart skills
                    if (op != who.ChosenSkill)
                    {
                        w.Log(LogLevel.Error, "BUG: apply_special(): applied skill is not chosen skill\n");
                    }
                    if (who.Type == Define.PLAYER && who.Contr is Player pl)
                    {
                        pl.ShootType = 0; // range_none
                        if (op.Invisible == 0)
                        {
                            // its a tool, need to unlink it
                            SkillTable.UnlinkSkill(w, op);
                            Say(who, "You stop using the " + w.QueryName(op) + ".");
                            Say(who, "You can no longer use the skill: " + SkillName(op) + ".");
                        }
                    }
                    LivingChange.ChangeAbil(w, who, op);
                    who.ChosenSkill = null;
                    who.ClearFlag(Define.FLAG_READY_SKILL);
                    buf = "";
                    break;

                case Define.ARMOUR:
                case Define.HELMET:
                case Define.SHIELD:
                case Define.RING:
                case Define.BOOTS:
                case Define.GLOVES:
                case Define.AMULET:
                case Define.GIRDLE:
                case Define.BRACERS:
                case Define.CLOAK:
                case ItemStrings.TypeMask:
                case ItemStrings.TypeLegs:
                case ItemStrings.TypeBody:
                    LivingChange.ChangeAbil(w, who, op);
                    buf = "You unwear " + w.QueryName(op) + ".";
                    break;

                case Define.BOW:
                case Define.WAND:
                case Define.ROD:
                case Define.HORN:
                    d.SkillTable?.CheckSkillToApply(w, who, op);
                    buf = "You unready " + w.QueryName(op) + ".";
                    if (who.Type == Define.PLAYER)
                    {
                        if (who.Contr is Player pl2)
                        {
                            pl2.ShootType = 0; // range_none
                        }
                    }
                    else
                    {
                        switch (op.Type)
                        {
                            case Define.WAND:
                                who.ClearFlag(Define.FLAG_READY_WAND);
                                break;
                            case Define.ROD:
                                who.ClearFlag(Define.FLAG_READY_ROD);
                                break;
                            case Define.HORN:
                                who.ClearFlag(Define.FLAG_READY_HORN);
                                break;
                            case Define.BOW:
                                who.ClearFlag(Define.FLAG_READY_BOW);
                                break;
                        }
                    }
                    break;

                default:
                    buf = "You unapply " + w.QueryName(op) + ".";
                    break;
            }
            if (buf.Length > 0)
            {
                Say(who, buf);
            }
            w.Host.FixPlayer(who);

            if ((aflags & ApNoMerge) == 0)
            {
                uint delTag = op.Count;
                tmp = w.MergeOb(op, null);
                if (who.Type == Define.PLAYER)
                {
                    if (tmp != null)
                    {
                        // it was merged
                        w.Host.EsrvDelItem(who.Contr, delTag);
                        op = tmp;
                    }
                    w.Host.EsrvSendItem(who, op);
                }
            }

            d.SendApplyHook(who, op, applied: false); // GK: after the state is changed (and the stack it merged into is known)
            return 0;
        }

        if (basicFlag == ApUnapply)
        {
            return 0;
        }
        i = false;
        // This goes through and checks to see if the player already has something of that type applied - if so, unapply it.
        for (tmp = who.Inv; tmp != null; tmp = tmp.Below)
        {
            if (SameSlot(tmp, op) && tmp.QueryFlag(Define.FLAG_APPLIED) && tmp != op)
            {
                if (tmp.Type == Define.RING && !i)
                {
                    i = true;
                }
                else if (ApplySpecial(who, tmp, 0) != 0)
                {
                    return 1;
                }
            }
        }
        if (op.Nrof > 1)
        {
            tmp = w.GetSplitOb(op, (int)(op.Nrof - 1));
        }
        else
        {
            tmp = null;
        }
        switch (op.Type)
        {
            case Define.WEAPON:
            case ItemStrings.TypeTool:
            case ItemStrings.TypeTool2:
            {
                if (!who.QueryFlag(Define.FLAG_USE_WEAPON))
                {
                    buf = "You can't use " + w.QueryName(op) + ".";
                    if (tmp != null)
                    {
                        w.InsertObInOb(tmp, who);
                    }
                    Say(who, buf);
                    return 1;
                }
                if (!CheckWeaponPower(who, op.LastEat))
                {
                    Say(who, "That weapon is too powerful for you to use.");
                    Say(who, "It would consume your soul!.");
                    if (tmp != null)
                    {
                        w.InsertObInOb(tmp, who);
                    }
                    return 1;
                }
                if (op.Level != 0 && who.Name != null && !(op.Name ?? "").StartsWith(who.Name, StringComparison.Ordinal))
                {
                    // if the weapon does not have the name as the character, can't use it.
                    // (Ragnarok's sword attempted to be used by Foo: won't work)
                    Say(who, "The weapon does not recognize you as its owner.");
                    if (tmp != null)
                    {
                        w.InsertObInOb(tmp, who);
                    }
                    return 1;
                }

                op.SetFlag(Define.FLAG_APPLIED);

                // check for melee weapons skill, alter player status. Note that we need to call this *before* change_abil
                // (GK: a tool needs no skill: check_skill_to_apply knows the types of Crossfire only)
                if (op.Type == Define.WEAPON && (d.SkillTable == null || d.SkillTable.CheckSkillToApply(w, who, op) == 0))
                {
                    // C loses the part of the stack it split off here; it goes back (docs/differences.md)
                    if (tmp != null)
                    {
                        w.InsertObInOb(tmp, who);
                    }
                    return 1;
                }
                if (!who.QueryFlag(Define.FLAG_READY_WEAPON))
                {
                    who.SetFlag(Define.FLAG_READY_WEAPON);
                }

                LivingChange.ChangeAbil(w, who, op);
                // GROS's current_weapon_script (EVENT_ATTACK of the plugins) is not ported: there are no plugins here
                who.CurrentWeapon = op;
                buf = "You wield " + w.QueryName(op) + ".";
                break;
            }

            case Define.ARMOUR:
            case Define.HELMET:
            case Define.SHIELD:
            case Define.BOOTS:
            case Define.GLOVES:
            case Define.GIRDLE:
            case Define.BRACERS:
            case Define.CLOAK:
                if (!who.QueryFlag(Define.FLAG_USE_ARMOUR))
                {
                    buf = "You can't use " + w.QueryName(op) + ".";
                    Say(who, buf);
                    if (tmp != null)
                    {
                        w.InsertObInOb(tmp, who);
                    }
                    return 1;
                }
                // C falls through into the ring and the amulet
                op.SetFlag(Define.FLAG_APPLIED);
                LivingChange.ChangeAbil(w, who, op);
                buf = "You wear " + w.QueryName(op) + ".";
                break;

            case Define.RING:
            case Define.AMULET:
            case ItemStrings.TypeMask: // GK: clothes need no use_armour: they are not armour
            case ItemStrings.TypeLegs:
            case ItemStrings.TypeBody:
                op.SetFlag(Define.FLAG_APPLIED);
                LivingChange.ChangeAbil(w, who, op);
                buf = "You wear " + w.QueryName(op) + ".";
                break;

            // this part is needed for skill-tools
            case Define.SKILL:
                if (who.ChosenSkill != null)
                {
                    w.Log(LogLevel.Error, "BUG: apply_special(): can't apply two skills\n");
                    return 1;
                }
                if (who.Type == Define.PLAYER && who.Contr is Player plSkill)
                {
                    plSkill.ShootType = SkillTable.RangeSkill;
                    if (op.Invisible == 0)
                    {
                        // for tools
                        if (op.ExpObj != null)
                        {
                            w.Log(LogLevel.Error, "BUG: apply_special(SKILL): found unapplied tool with experience object\n");
                        }
                        else
                        {
                            d.SkillTable?.LinkPlayerSkill(who, op);
                        }
                        Say(who, "You ready " + w.QueryName(op) + ".");
                        Say(who, "You can now use the skill: " + SkillName(op) + ".");
                    }
                    else
                    {
                        Say(who, "Readied skill: " + SkillName(op) + ".");
                    }
                }
                op.SetFlag(Define.FLAG_APPLIED);
                LivingChange.ChangeAbil(w, who, op);
                who.ChosenSkill = op;
                who.SetFlag(Define.FLAG_READY_SKILL);
                buf = "";
                break;

            case Define.WAND:
            case Define.ROD:
            case Define.HORN:
            case Define.BOW:
                // check for skill, alter player status
                op.SetFlag(Define.FLAG_APPLIED);
                if (d.SkillTable == null || d.SkillTable.CheckSkillToApply(w, who, op) == 0)
                {
                    // C loses the part of the stack it split off here; it goes back (docs/differences.md)
                    if (tmp != null)
                    {
                        w.InsertObInOb(tmp, who);
                    }
                    return 1;
                }
                Say(who, "You ready " + w.QueryName(op) + ".");
                if (who.Type == Define.PLAYER)
                {
                    // the shoot type was set by check_skill_to_apply; chosen_item_spell and known_spell are the casting's (phase 4)
                    if (op.Type == Define.BOW)
                    {
                        Say(who, "You will now fire " + (op.Race ?? "nothing") + " with " + w.QueryName(op) + ".");
                    }
                }
                else
                {
                    switch (op.Type)
                    {
                        case Define.WAND:
                            who.SetFlag(Define.FLAG_READY_WAND);
                            break;
                        case Define.ROD:
                            who.SetFlag(Define.FLAG_READY_ROD);
                            break;
                        case Define.HORN:
                            who.SetFlag(Define.FLAG_READY_HORN);
                            break;
                        case Define.BOW:
                            who.SetFlag(Define.FLAG_READY_BOW);
                            break;
                    }
                }
                break;

            default:
                buf = "You apply " + w.QueryName(op) + ".";
                break;
        }
        if (!op.QueryFlag(Define.FLAG_APPLIED))
        {
            op.SetFlag(Define.FLAG_APPLIED);
        }
        if (buf.Length > 0)
        {
            Say(who, buf);
        }
        if (tmp != null)
        {
            tmp = w.InsertObInOb(tmp, who);
        }
        w.Host.FixPlayer(who);
        if (op.Type != Define.WAND && who.Type == Define.PLAYER)
        {
            op.SetFlag(Define.FLAG_BEEN_APPLIED);
        }
        if (op.QueryFlag(Define.FLAG_CURSED) || op.QueryFlag(Define.FLAG_DAMNED))
        {
            if (who.Type == Define.PLAYER)
            {
                Say(who, "Oops, it feels deadly cold!");
                op.SetFlag(Define.FLAG_KNOWN_CURSED);
            }
        }
        if (who.Type == Define.PLAYER)
        {
            // if multiple objects were applied, update both slots
            if (tmp != null)
            {
                w.Host.EsrvSendItem(who, tmp);
            }
            w.Host.EsrvSendItem(who, op);
        }
        d.SendApplyHook(who, op, applied: true); // GK: after applied is set
        return 0;
    }

    // skills[op->stats.sp].name
    private string SkillName(CfObject op)
    {
        SkillTable? table = d.SkillTable;
        int sp = op.Stats.Sp;
        return table != null && sp >= 0 && sp < table.Skills.Length ? table.Skills[sp].Name : "";
    }
}
