// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): the tables and messages of common/living.c that <c>fix_player</c> and
// <c>change_abil</c> need (the bonus tables of Dex and Str, the weight a player carries before it slows down, the messages of a
// change of a stat).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/living.c:
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
namespace Crossfire.Extension.Gk;

/// <summary>common/living.c: the static tables of <c>fix_player</c> (indexed by a stat, 0 to 30) and the messages of <c>change_abil</c>.</summary>
internal static class LivingTables
{
    /// <summary>living.c <c>dex_bonus</c>: the armour class bonus of Dex.</summary>
    public static readonly int[] DexBonus =
    {
        -4, -3, -2, -2, -1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 6, 6, 7,
    };

    /// <summary>living.c <c>speed_bonus</c> (a float table, Dex is its stat).</summary>
    public static readonly float[] SpeedBonus =
    {
        -0.4f, -0.4f, -0.3f, -0.3f, -0.2f, -0.2f, -0.2f, -0.1f, -0.1f, -0.1f, -0.05f, 0f, 0f, 0f,
        0.05f, 0.1f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.8f, 1.0f, 1.2f, 1.4f,
        1.6f, 1.8f, 2.0f, 2.5f, 3.0f,
    };

    /// <summary>living.c <c>dam_bonus</c>: the damage bonus of Str.</summary>
    public static readonly int[] DamBonus =
    {
        -2, -2, -2, -1, -1, -1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5, 5, 6, 6, 7, 8, 10, 15,
    };

    /// <summary>living.c <c>thaco_bonus</c>: the weapon class bonus of Str.</summary>
    public static readonly int[] ThacoBonus =
    {
        -2, -2, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5, 5, 6, 7, 8, 10,
    };

    /// <summary>living.c <c>max_carry</c>: the kilograms a player may carry before the extra speed penalty starts.</summary>
    public static readonly int[] MaxCarry =
    {
        2, 4, 7, 11, 16, 22, 29, 37, 46, 56, 67, 79, 92, 106, 121, 137, 154, 172, 191, 211, 232, 254, 277,
        301, 326, 352, 400, 450, 500, 600, 1000,
    };

    /// <summary>living.c <c>gain_msg</c>: the message of a stat that an applied item raises.</summary>
    public static readonly string[] GainMsg =
    {
        "You feel stronger.", "You feel more agile.", "You feel healthy.", "You feel wiser.", "You seem to look better.",
        "You feel smarter.", "You feel more potent.",
    };

    /// <summary>living.c <c>lose_msg</c>: the message of a stat that an applied item lowers.</summary>
    public static readonly string[] LoseMsg =
    {
        "You feel weaker!", "You feel clumsy!", "You feel less healthy!", "You lose some of your memory!", "You look ugly!",
        "You feel stupid!", "You feel less potent!",
    };
}
