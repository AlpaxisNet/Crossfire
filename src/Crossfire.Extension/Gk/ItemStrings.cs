// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): socket/item.c <c>query_flags</c>, with the text of the item strings of
// Graal Kingdoms' inventory window (docs/mud-phase3.md section 5) built from what <c>esrv_send_item</c> sends.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     socket/item.c:
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
using System.Globalization;
using System.Text;

namespace Crossfire.Extension.Gk;

/// <summary>
/// The flag word and the text of an item string. <see cref="QueryFlags"/> is the C function; <see cref="Flags"/> is what the string
/// carries (Graal Kingdoms' types 162 to 166 and <c>F_NOPICK</c> added); <see cref="Text"/> is the seven comma fields.
/// </summary>
internal static class ItemStrings
{
    /// <summary>newclient.h: the low nibble of the flag word (<c>a_none</c> .. <c>a_applied</c>).</summary>
    public const uint ANone = 0, AReadied = 1, AWielded = 2, AWorn = 3, AActive = 4, AApplied = 5;

    /// <summary>newclient.h: <c>F_APPLIED</c>, the mask of the low nibble.</summary>
    public const uint FApplied = 0x000F;

    /// <summary>newclient.h: <c>F_UNPAID</c>, <c>F_MAGIC</c>, <c>F_CURSED</c>, <c>F_DAMNED</c>, <c>F_OPEN</c>, <c>F_NOPICK</c>, <c>F_LOCKED</c>.</summary>
    public const uint FUnpaid = 0x0200, FMagic = 0x0400, FCursed = 0x0800, FDamned = 0x1000, FOpen = 0x2000, FNoPick = 0x4000, FLocked = 0x8000;

    /// <summary>Graal Kingdoms' own object types that carry an applied state (not in Crossfire's define.h; names of types only).</summary>
    public const int TypeTool = 162, TypeTool2 = 163, TypeMask = 164, TypeLegs = 165, TypeBody = 166;

    /// <summary>The prefix of the item strings' flag names: the whole name is this and the item's id.</summary>
    public const string ItemPrefix = "clientr.mud_item";

    public const string ContainerName = "clientr.mud_container";

    public const string WeightName = "clientr.mud_weight";

    public const string WeightLimitName = "clientr.mud_weight_lim";

    /// <summary>socket/item.c <c>query_flags</c>: the flags the client is sent for an object.</summary>
    public static uint QueryFlags(CfObject op)
    {
        uint flags = 0;

        if (op.QueryFlag(Define.FLAG_APPLIED))
        {
            switch (op.Type)
            {
                case Define.BOW:
                case Define.WAND:
                case Define.ROD:
                case Define.HORN:
                    flags = AReadied;
                    break;
                case Define.WEAPON:
                    flags = AWielded;
                    break;
                case Define.SKILL:
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
                    flags = AWorn;
                    break;
                case Define.CONTAINER:
                    flags = AActive;
                    break;
                default:
                    flags = AApplied;
                    break;
            }
        }
        if (op.Type == Define.CONTAINER && ((op.Env != null && op.Env.Container == op) || (op.Env == null && op.QueryFlag(Define.FLAG_APPLIED))))
        {
            flags |= FOpen;
        }

        if (op.QueryFlag(Define.FLAG_KNOWN_CURSED))
        {
            if (op.QueryFlag(Define.FLAG_DAMNED))
            {
                flags |= FDamned;
            }
            else if (op.QueryFlag(Define.FLAG_CURSED))
            {
                flags |= FCursed;
            }
        }
        if (op.QueryFlag(Define.FLAG_KNOWN_MAGICAL) && !op.QueryFlag(Define.FLAG_IDENTIFIED))
        {
            flags |= FMagic;
        }
        if (op.QueryFlag(Define.FLAG_UNPAID))
        {
            flags |= FUnpaid;
        }
        if (op.QueryFlag(Define.FLAG_INV_LOCKED))
        {
            flags |= FLocked;
        }
        return flags;
    }

    /// <summary>
    /// The flag word of the item string: <see cref="QueryFlags"/>, with the applied state Graal Kingdoms' types need to be drawn on
    /// the paper doll (an applied 162 or 163 is wielded, an applied 164 to 166 is worn: C would give them "applied", which draws
    /// nothing, mud-phase3.md section 5, fact 3b), and <c>F_NOPICK</c> for an object with <c>FLAG_NO_PICK</c> (C ORs it in only in
    /// <c>esrv_send_inventory</c>; here a send and an update alike).
    /// </summary>
    public static uint Flags(CfObject op)
    {
        uint flags = QueryFlags(op);
        if (op.QueryFlag(Define.FLAG_APPLIED) && (flags & FApplied) == AApplied)
        {
            if (op.Type is TypeTool or TypeTool2)
            {
                flags = (flags & ~FApplied) | AWielded;
            }
            else if (op.Type is TypeMask or TypeLegs or TypeBody)
            {
                flags = (flags & ~FApplied) | AWorn;
            }
        }
        if (op.QueryFlag(Define.FLAG_NO_PICK))
        {
            flags |= FNoPick;
        }
        return flags;
    }

    /// <summary>socket/item.c <c>WEIGHT</c>, for the whole stack (Graal Kingdoms' client does not multiply, mud-phase3.md fact 3a): grams.</summary>
    public static long StackWeight(CfObject op) => (long)Math.Max(1u, op.Nrof) * op.Weight + op.Carrying;

    /// <summary>
    /// The seven comma fields of <c>clientr.mud_item&lt;id&gt;</c> (docs/mud-phase3.md section 5): the short name, the picture, the type,
    /// the quantity, the weight of the whole stack in grams, the flag word and 1 inside the open container.
    /// </summary>
    public static string Text(CfWorld world, CfObject op, bool inContainer)
    {
        var sb = new StringBuilder();
        sb.Append(FileText.FromBytesText(world.QueryShortName(op))).Append(',');
        sb.Append(TMudArchMembers.Icon(op.Face?.Name)).Append(',');
        sb.Append(op.Type.ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append(op.Nrof.ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append(StackWeight(op).ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append(Flags(op).ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append(inContainer ? '1' : '0');
        return sb.ToString();
    }

    /// <summary>The text of <c>clientr.mud_container</c>: the id, the name and the picture of the container the player has open.</summary>
    public static string ContainerText(CfWorld world, uint id, CfObject container) =>
        id.ToString(CultureInfo.InvariantCulture) + "," + FileText.FromBytesText(world.QueryShortName(container)) + "," + TMudArchMembers.Icon(container.Face?.Name);
}
