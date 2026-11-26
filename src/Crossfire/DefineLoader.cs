// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): include/loader.h, include/map.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     include/loader.h:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/map.h:
//         Copyright (C) 2001 Mark Wedel
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
namespace Crossfire;

public static partial class Define
{
    // loader.h: what lex_load returns

    /// <summary>loader.h LL_IGNORED: the object was ignored (an Object line without a name).</summary>
    public const int LL_IGNORED = -1;
    /// <summary>loader.h LL_EOF: the end of the text was reached.</summary>
    public const int LL_EOF = 0;
    /// <summary>loader.h LL_MORE: the block ended and it was a further part of a multipart object.</summary>
    public const int LL_MORE = 1;
    /// <summary>loader.h LL_NORMAL: the block ended.</summary>
    public const int LL_NORMAL = 2;

    // loader.h: the buffer state of load_object

    /// <summary>loader.h LO_REPEAT: reading from the same buffer as the last call.</summary>
    public const int LO_REPEAT = 0;
    /// <summary>loader.h LO_LINEMODE: the file is multi-purpose (treasures, artifacts): lines are scanned one by one.</summary>
    public const int LO_LINEMODE = 1;
    /// <summary>loader.h LO_NEWFILE: the first read from a file: the buffers are reset.</summary>
    public const int LO_NEWFILE = 2;
    /// <summary>loader.h LO_NOREAD: reset the buffers, do not read.</summary>
    public const int LO_NOREAD = 3;

    /// <summary>map.h MAP_STYLE: the map is a random map style (loader.l speed).</summary>
    public const int MAP_STYLE = 0x8;

    /// <summary>map.h MAP_FLUSH.</summary>
    public const int MAP_FLUSH = 0x1;
    /// <summary>map.h MAP_PLAYER_UNIQUE: the map file is one a player's unique items were saved in (no name changes, no original flag).</summary>
    public const int MAP_PLAYER_UNIQUE = 0x2;
    /// <summary>map.h MAP_BLOCK.</summary>
    public const int MAP_BLOCK = 0x4;
    /// <summary>map.h MAP_OVERLAY: the file is an overlay of a map.</summary>
    public const int MAP_OVERLAY = 0x10;
}
