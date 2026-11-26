// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/c_object.c <c>pick_up_object</c>, the weight test that decides
// whether a player can carry what is given to him. The rest of a trade (the object leaves one player's tree and enters another's) is
// the dispatcher's (Dispatcher.Wave7B.cs).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/c_object.c:
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

/// <summary>The rules of a trade between two players that come from C (docs/mud-phase3.md section 4.5).</summary>
internal static class Trade
{
    /// <summary>
    /// c_object.c <c>pick_up_object</c>'s test: true when <paramref name="receiver"/> cannot carry <paramref name="count"/> of
    /// <paramref name="item"/> (the weight of the objects, and of what a container holds after the container's reduction) on top of
    /// what he carries, by the limit of his Str (<see cref="ItemMatching.WeightLimitOf"/>).
    /// </summary>
    public static bool TooHeavyFor(CfObject receiver, CfObject item, long count)
    {
        long weight = (long)item.Weight * count;
        if (item.Inv != null)
        {
            weight += (long)item.Carrying * (100 - item.Stats.Str) / 100;
        }
        return (long)receiver.Weight + receiver.Carrying + weight > ItemMatching.WeightLimitOf(receiver.Stats.Str);
    }
}
