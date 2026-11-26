// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/c_object.c <c>command_apply</c> and apply.c
// <c>player_apply_below</c>, the command that applies an object by name (docs/mud-phase3.md sections 4.4 and 4.6). The apply itself is
// in Apply.cs.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/c_object.c:
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
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

internal sealed partial class Apply
{
    /// <summary>
    /// c_object.c <c>command_apply</c>: <paramref name="op"/> applies what <paramref name="parameters"/> names (an optional <c>-a</c> to
    /// always apply or <c>-u</c> to always unapply, then the name); without parameters the object below the player is applied
    /// (<see cref="PlayerApplyBelow"/>). Always 0.
    /// </summary>
    public int CommandApply(CfObject op, string? parameters)
    {
        if (parameters == null)
        {
            PlayerApplyBelow(op);
            return 0;
        }
        int aflag = 0;
        int at = 0;
        while (at < parameters.Length && parameters[at] == ' ')
        {
            at++;
        }
        if (string.CompareOrdinal(parameters, at, "-a ", 0, 3) == 0)
        {
            aflag = ApApply;
            at += 3;
        }
        if (string.CompareOrdinal(parameters, at, "-u ", 0, 3) == 0)
        {
            aflag = ApUnapply;
            at += 3;
        }
        while (at < parameters.Length && parameters[at] == ' ')
        {
            at++;
        }
        string name = parameters.Substring(at);
        CfObject? inv = ItemMatching.FindBestObjectMatch(World, op, name);
        if (inv != null)
        {
            PlayerApply(op, inv, aflag, quiet: false);
        }
        else
        {
            Say(op, "Could not find any match to the " + name + ".");
        }
        return 0;
    }

    /// <summary>
    /// apply.c <c>player_apply_below</c>: applies the object <paramref name="pl"/> has below him: with an open container, its top
    /// objects; otherwise the objects of the square, and the object model has no squares (what lies on the ground is a level object of
    /// the extension, which a script applies with <c>mudapplyobject</c>), so there is nothing to apply.
    /// </summary>
    public void PlayerApplyBelow(CfObject pl)
    {
        // If using a container, set the starting item to be the top item in the container.
        CfObject? tmp = pl.Container?.Inv;
        CfObject? next;
        int floors = 0;
        for (; tmp != null; tmp = next)
        {
            next = tmp.Below;
            if (tmp.QueryFlag(Define.FLAG_IS_FLOOR))
            {
                floors++;
            }
            else if (floors > 0)
            {
                return; // process only floor objects after first floor object
            }
            if (tmp.Invisible == 0 || tmp.QueryFlag(Define.FLAG_WALK_ON) || tmp.QueryFlag(Define.FLAG_FLY_ON))
            {
                if (PlayerApply(pl, tmp, 0, quiet: true) == 1)
                {
                    return;
                }
            }
            if (floors >= 2)
            {
                return; // process at most two floor objects
            }
        }
    }
}
