// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/loader.l.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/loader.l:
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
using System.Text;

namespace Crossfire;

public sealed partial class CfWorld
{
    private static void SaveLong(StringBuilder buf, string name, long n) => buf.Append(name).Append(' ').Append(n).Append('\n');

    private static void SaveDouble(StringBuilder buf, string name, double v) => buf.Append(name).Append(' ').Append(CStr.Fmt(v)).Append('\n');

    /// <summary>
    /// loader.l <c>get_ob_diff</c>: the lines (<c>key value</c>, each with a newline) of all variables that are different in the two
    /// objects, taking the values from <paramref name="op"/>. Used to dump objects (op2 the empty archetype's clone) or to save them
    /// (op2 the object's own archetype). Null when there is no difference. The lines that the port keeps in
    /// <see cref="CfObject.Extra"/> come last, those that the archetype does not have.
    /// </summary>
    public string? GetObDiff(CfObject op, CfObject op2)
    {
        var buf = new StringBuilder();
        // C keeps buf2 in a static; an event index without a name appends what it held last. The port starts empty.
        string buf2 = "";

        if (op.Name != null && op.Name != op2.Name)
        {
            buf.Append("name ").Append(op.Name).Append('\n');
        }
        if (op.NamePl != null && op.NamePl != op2.NamePl)
        {
            buf.Append("name_pl ").Append(op.NamePl).Append('\n');
        }
        if (op.Title != null && op.Title != op2.Title)
        {
            buf.Append("title ").Append(op.Title).Append('\n');
        }
        if (op.Race != null && op.Race != op2.Race)
        {
            buf.Append("race ").Append(op.Race).Append('\n');
        }
        if (op.Slaying != null && op.Slaying != op2.Slaying)
        {
            buf.Append("slaying ").Append(op.Slaying).Append('\n');
        }
        if (op.Msg != null && op.Msg != op2.Msg)
        {
            buf.Append("msg\n");
            buf.Append(op.Msg);
            buf.Append("endmsg\n");
        }
        if (op.OtherArch != op2.OtherArch && op.OtherArch != null && op.OtherArch.Name != null)
        {
            buf.Append("other_arch ").Append(op.OtherArch.Name).Append('\n');
        }
        if (op.Face != op2.Face)
        {
            buf.Append("face ").Append(op.Face?.Name).Append('\n');
        }
        for (int i = 1; i <= Define.NR_EVENTS; i++)
        {
            string? en = EventKeyword[i];
            if (op.EventHook[i] != null && op.EventHook[i] != op2.EventHook[i])
            {
                if (en != null)
                {
                    buf2 = "event_" + en + " " + op.EventHook[i] + "\n";
                }
                buf.Append(buf2);
            }
            if (op.EventPlugin[i] != null && op.EventPlugin[i] != op2.EventPlugin[i])
            {
                if (en != null)
                {
                    buf2 = "event_" + en + "_plugin " + op.EventPlugin[i] + "\n";
                }
                buf.Append(buf2);
            }
            if (op.EventOptions[i] != null && op.EventOptions[i] != op2.EventOptions[i])
            {
                if (en != null)
                {
                    buf2 = "event_" + en + "_options " + op.EventOptions[i] + "\n";
                }
                buf.Append(buf2);
            }
        }
        if (op.AnimationId != op2.AnimationId)
        {
            if (op.AnimationId != 0)
            {
                buf2 = "animation " + Animations[op.AnimationId].Name + "\n";
                if (!op.QueryFlag(Define.FLAG_ANIMATE))
                {
                    buf.Append(buf2);
                    buf2 = "is_animated 0\n";
                }
            }
            else
            {
                buf2 = "animation NONE\n";
            }
            buf.Append(buf2);
        }
        if (op.Stats.Str != op2.Stats.Str)
        {
            SaveLong(buf, "Str", op.Stats.Str);
        }
        if (op.Stats.Dex != op2.Stats.Dex)
        {
            SaveLong(buf, "Dex", op.Stats.Dex);
        }
        if (op.Stats.Con != op2.Stats.Con)
        {
            SaveLong(buf, "Con", op.Stats.Con);
        }
        if (op.Stats.Wis != op2.Stats.Wis)
        {
            SaveLong(buf, "Wis", op.Stats.Wis);
        }
        if (op.Stats.Pow != op2.Stats.Pow)
        {
            SaveLong(buf, "Pow", op.Stats.Pow);
        }
        if (op.Stats.Cha != op2.Stats.Cha)
        {
            SaveLong(buf, "Cha", op.Stats.Cha);
        }
        if (op.Stats.Int != op2.Stats.Int)
        {
            SaveLong(buf, "Int", op.Stats.Int);
        }
        if (op.Stats.Hp != op2.Stats.Hp)
        {
            SaveLong(buf, "hp", op.Stats.Hp);
        }
        if (op.Stats.Maxhp != op2.Stats.Maxhp)
        {
            SaveLong(buf, "maxhp", op.Stats.Maxhp);
        }
        if (op.Stats.Sp != op2.Stats.Sp)
        {
            SaveLong(buf, "sp", op.Stats.Sp);
        }
        if (op.Stats.Maxsp != op2.Stats.Maxsp)
        {
            SaveLong(buf, "maxsp", op.Stats.Maxsp);
        }
        if (op.Stats.Grace != op2.Stats.Grace)
        {
            SaveLong(buf, "grace", op.Stats.Grace);
        }
        if (op.Stats.Maxgrace != op2.Stats.Maxgrace)
        {
            SaveLong(buf, "maxgrace", op.Stats.Maxgrace);
        }
        if (op.Stats.Exp != op2.Stats.Exp)
        {
            SaveLong(buf, "exp", op.Stats.Exp);
        }
        if (op.Expmul != op2.Expmul)
        {
            SaveDouble(buf, "expmul", op.Expmul);
        }
        if (op.Stats.Food != op2.Stats.Food)
        {
            SaveLong(buf, "food", op.Stats.Food);
        }
        if (op.Stats.Dam != op2.Stats.Dam)
        {
            SaveLong(buf, "dam", op.Stats.Dam);
        }
        if (op.Stats.Luck != op2.Stats.Luck)
        {
            SaveLong(buf, "luck", op.Stats.Luck);
        }
        if (op.Stats.Wc != op2.Stats.Wc)
        {
            SaveLong(buf, "wc", op.Stats.Wc);
        }
        if (op.Stats.Ac != op2.Stats.Ac)
        {
            SaveLong(buf, "ac", op.Stats.Ac);
        }
        if (op.X != op2.X)
        {
            SaveLong(buf, "x", op.X);
        }
        if (op.Y != op2.Y)
        {
            SaveLong(buf, "y", op.Y);
        }
        if (op.Speed != op2.Speed)
        {
            buf.Append("speed ").Append(CStr.Fmt(op.Speed)).Append('\n');
        }
        if (op.Speed > 0 && op.SpeedLeft != op2.SpeedLeft)
        {
            buf.Append("speed_left ").Append(CStr.Fmt(op.SpeedLeft)).Append('\n');
        }
        if (op.MoveStatus != op2.MoveStatus)
        {
            SaveLong(buf, "move_state", op.MoveStatus);
        }
        if (op.MoveType != op2.MoveType)
        {
            SaveLong(buf, "attack_movement", op.MoveType);
        }
        if (op.Nrof != op2.Nrof)
        {
            SaveLong(buf, "nrof", op.Nrof);
        }
        if (op.Level != op2.Level)
        {
            SaveLong(buf, "level", op.Level);
        }
        if (op.Direction != op2.Direction)
        {
            SaveLong(buf, "direction", op.Direction);
        }
        if (op.Type != op2.Type)
        {
            SaveLong(buf, "type", op.Type);
        }
        if (op.Attacktype != op2.Attacktype)
        {
            SaveLong(buf, "attacktype", op.Attacktype);
        }

        for (int tmp = 0; tmp < Define.NROFATTACKS; tmp++)
        {
            if (op.Resist[tmp] != op2.Resist[tmp])
            {
                buf.Append("resist_").Append(Tables.ResistSave[tmp]).Append(' ').Append(op.Resist[tmp]).Append('\n');
            }
        }

        if (op.PathAttuned != op2.PathAttuned)
        {
            SaveLong(buf, "path_attuned", op.PathAttuned);
        }
        if (op.PathRepelled != op2.PathRepelled)
        {
            SaveLong(buf, "path_repelled", op.PathRepelled);
        }
        if (op.PathDenied != op2.PathDenied)
        {
            SaveLong(buf, "path_denied", op.PathDenied);
        }
        if (op.Material != op2.Material)
        {
            SaveLong(buf, "material", op.Material);
        }
        if (op.Value != op2.Value)
        {
            SaveLong(buf, "value", op.Value);
        }
        if (op.Carrying != op2.Carrying)
        {
            SaveLong(buf, "carrying", op.Carrying);
        }
        if (op.Weight != op2.Weight)
        {
            SaveLong(buf, "weight", op.Weight);
        }
        if (op.Invisible != op2.Invisible)
        {
            SaveLong(buf, "invisible", op.Invisible);
        }
        if (op.State != op2.State)
        {
            SaveLong(buf, "state", op.State);
        }
        if (op.Magic != op2.Magic)
        {
            SaveLong(buf, "magic", op.Magic);
        }
        if (op.LastHeal != op2.LastHeal)
        {
            SaveLong(buf, "last_heal", op.LastHeal);
        }
        if (op.LastSp != op2.LastSp)
        {
            SaveLong(buf, "last_sp", op.LastSp);
        }
        if (op.LastGrace != op2.LastGrace)
        {
            SaveLong(buf, "last_grace", op.LastGrace);
        }
        if (op.LastEat != op2.LastEat)
        {
            SaveLong(buf, "last_eat", op.LastEat);
        }
        int linked;
        if (op.QueryFlag(Define.FLAG_IS_LINKED) && (linked = Host.GetButtonValue(op)) != 0)
        {
            SaveLong(buf, "connected", linked);
        }
        if (op.GlowRadius != op2.GlowRadius)
        {
            SaveLong(buf, "glow_radius", op.GlowRadius);
        }
        if (op.Randomitems != op2.Randomitems)
        {
            buf.Append("randomitems ").Append(op.Randomitems != null ? op.Randomitems.Name : "none").Append('\n');
        }

        // Eneq(@csd.uu.se): Handles run_away and pick_up
        if (op.RunAway != op2.RunAway)
        {
            SaveLong(buf, "run_away", op.RunAway);
        }
        if (op.PickUp != op2.PickUp)
        {
            SaveLong(buf, "pick_up", op.PickUp);
        }
        if (op.WeightLimit != op2.WeightLimit)
        {
            SaveLong(buf, "container", op.WeightLimit);
        }

        // Vick (@bern.docs.uu.se) @921107 -> Handle 'will_apply' & 'random_movement'.
        if (op.WillApply != op2.WillApply)
        {
            SaveLong(buf, "will_apply", op.WillApply);
        }

        if (op.CurrentWeaponScript != op2.CurrentWeaponScript)
        {
            buf.Append("current_weapon_script ").Append(op.CurrentWeaponScript).Append('\n');
        }

        // Mol(@meryl.csd.uu.se) 921108  Handle can_apply
        if (op.CanApply != op2.CanApply)
        {
            SaveLong(buf, "can_apply", op.CanApply);
        }

        if (op.Weapontype != 0 && op.Weapontype != op2.Weapontype)
        {
            buf.Append("weapontype ").Append(unchecked((int)op.Weapontype)).Append('\n');
        }
        if (op.Elevation != 0 && op.Elevation != op2.Elevation)
        {
            buf.Append("elevation ").Append(op.Elevation).Append('\n');
        }
        if (op.ClientType != 0 && op.ClientType != op2.ClientType)
        {
            buf.Append("client_type ").Append(op.ClientType).Append('\n');
        }

        for (int tmp = 0; tmp <= Define.NUM_FLAGS; tmp++)
        {
            (int flag, string? keyword) = Define.FlagLinks[tmp];
            if (flag != -1 && op.QueryFlag(flag) != op2.QueryFlag(flag))
            {
                if (flag == Define.FLAG_SLOW_MOVE)
                {
                    buf.Append("slow_move ").Append(CStr.Fmt(op.Stats.Exp / 1000.0)).Append('\n');
                }
                else
                {
                    buf.Append(keyword).Append(op.QueryFlag(flag) ? " 1\n" : " 0\n");
                }
            }
        }

        // Not in C: the lines of the archetype text that the loader does not know.
        if (op.Extra != null)
        {
            foreach (KeyValuePair<string, string> e in op.Extra.Entries)
            {
                bool inArch = false;
                if (op2.Extra != null)
                {
                    foreach (KeyValuePair<string, string> o in op2.Extra.Entries)
                    {
                        if (string.Equals(o.Key, e.Key, StringComparison.OrdinalIgnoreCase) && o.Value == e.Value)
                        {
                            inArch = true;
                            break;
                        }
                    }
                }
                if (!inArch)
                {
                    buf.Append(e.Key).Append(' ').Append(e.Value).Append('\n');
                }
            }
        }

        if (buf.Length == 0)
        {
            return null;
        }
        return buf.ToString();
    }

    /// <summary>
    /// loader.l <c>save_object</c>: dumps an object (and its inventory) as text: "arch name", what differs from the archetype,
    /// the inventory as nested blocks, "end". If bit 0 of <paramref name="flag"/> is set unpaid objects are saved. If bit 1 is set the
    /// object is not removed after it is saved (all the callers set it); without it the object and its inventory are removed and freed.
    /// An object that has an owner is not saved.
    /// </summary>
    public void SaveObject(StringBuilder fp, CfObject op, int flag)
    {
        // Even if the object does have an owner, it would seem that we should still save it.
        if (op.Owner != null)
        {
            return;
        }

        // If it is unpaid and we don't want to save those, just return.
        if ((flag & 1) == 0 && op.QueryFlag(Define.FLAG_UNPAID))
        {
            return;
        }

        Archetype at = op.Arch ?? EmptyArchetypeOrBlank();
        fp.Append("arch ").Append(at.Name).Append('\n');

        string? cp = GetObDiff(op, at.Clone);
        if (cp != null)
        {
            fp.Append(cp);
        }

        // Eneq(@csd.uu.se): Added this to allow containers being saved with contents
        CfObject? old = null;
        CfObject? tmp;

        if ((flag & 2) != 0)
        {
            for (tmp = op.Inv; tmp != null; tmp = tmp.Below)
            {
                SaveObject(fp, tmp, flag);
            }
        }
        else
        {
            // Slightly different logic because tmp/op will be removed by the save_object we call. So we just keep looking at op->inv
            // until there is nothing left. In theory, the variable old should not be needed, as recursive loops shouldn't happen.
            while ((tmp = op.Inv) != null)
            {
                if (old == tmp)
                {
                    Log(LogLevel.Error, " Recursive loop in inventory\n");
                    break;
                }
                SaveObject(fp, tmp, flag);
                old = tmp;
            }
        }

        if ((flag & 2) == 0)
        {
            RemoveOb(op);
            FreeObject(op);
        }

        fp.Append("end\n");
    }
}
