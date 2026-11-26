// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/map.c (the map file's header and object list), include/map.h,
// server/player.c (get_player_archetype).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/map.c:
//         Copyright (C) 2001 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     include/map.h:
//         Copyright (C) 2001 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     server/player.c:
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
using System.Text;

namespace Crossfire;

/// <summary>
/// The fields of include/map.h <c>mapdef</c> that the header of a map file sets and writes (common/map.c <c>load_map_header</c>,
/// <c>new_save_map</c>). The squares, the players and the timers of the map are not ported: a host that holds a map keeps them.
/// The defaults are those of <c>get_linked_map</c>.
/// </summary>
public sealed class MapHeader
{
    /// <summary>map.h: name, the name of the map.</summary>
    public string? Name;
    /// <summary>map.h: msg, the message of the map.</summary>
    public string? Msg;
    /// <summary>map.h: reset_timeout, seconds before the map is reset (header keys <c>weight</c> and <c>reset_timeout</c>).</summary>
    public int ResetTimeout = 7200;
    /// <summary>map.h: timeout, swapping delay (header keys <c>value</c> and <c>swap_time</c> set this one).</summary>
    public int Timeout = 300;
    /// <summary>map.h: swap_time, the countdown that <c>new_save_map</c> writes as <c>swap_time</c> (the loader never sets it).</summary>
    public int SwapTime;
    /// <summary>map.h: difficulty (header keys <c>level</c> and <c>difficulty</c>).</summary>
    public int Difficulty;
    /// <summary>map.h: darkness (header keys <c>invisible</c> and <c>darkness</c>).</summary>
    public int Darkness;
    /// <summary>map.h: fixed_resettime (header keys <c>stand_still</c> and <c>fixed_resettime</c>).</summary>
    public int FixedResettime;
    /// <summary>map.h: width (header keys <c>x</c> and <c>width</c>).</summary>
    public int Width = 16;
    /// <summary>map.h: height (header keys <c>y</c> and <c>height</c>).</summary>
    public int Height = 16;
    /// <summary>map.h: enter_x (header keys <c>hp</c> and <c>enter_x</c>).</summary>
    public int EnterX = 1;
    /// <summary>map.h: enter_y (header keys <c>sp</c> and <c>enter_y</c>).</summary>
    public int EnterY = 1;
    /// <summary>map.h: unique.</summary>
    public int Unique;
    /// <summary>map.h: outdoor.</summary>
    public int Outdoor;
    /// <summary>map.h: temp.</summary>
    public int Temp;
    /// <summary>map.h: pressure.</summary>
    public int Pressure;
    /// <summary>map.h: humid.</summary>
    public int Humid;
    /// <summary>map.h: windspeed.</summary>
    public int Windspeed;
    /// <summary>map.h: winddir.</summary>
    public int Winddir;
    /// <summary>map.h: sky.</summary>
    public int Sky;
    /// <summary>map.h: tile_path[4], the neighbouring maps (header keys <c>tile_path_1</c> to <c>tile_path_4</c>).</summary>
    public string?[] TilePath = new string?[4];
}

public sealed partial class CfWorld
{
    /// <summary>
    /// C <c>fgets(buf, size, fp)</c> on a <see cref="LoadSource"/>: the next line with its newline, at most <paramref name="size"/> - 1
    /// characters (a longer line comes in pieces); null at the end of the text.
    /// </summary>
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

    /// <summary>
    /// map.c <c>load_map_header</c>: reads the header block of a map file (<c>arch map</c> up to <c>end</c>) into <paramref name="m"/>.
    /// Returns 0 on success and 1 when the text ends before the <c>end</c> line, as C does. The tile paths are kept as written: C
    /// checks that the file exists and otherwise tries the path relative to the map's own, which needs the file system (docs/differences.md).
    /// </summary>
    public int LoadMapHeader(LoadSource fp, MapHeader m)
    {
        string key = "";
        var msgbuf = new StringBuilder();
        string? buf;
        while ((buf = Fgets(fp, Define.HUGE_BUF - 1)) != null)
        {
            int k = 0;
            while (k < buf.Length && IsSpace(buf[k]))
            {
                k++;
            }
            if (k >= buf.Length)
            {
                key = "";
                continue; // empty line
            }
            string? value;
            int sp = buf.IndexOf(' ', k);
            if (sp < 0)
            {
                // no value: the key runs to the newline
                int nlPos = buf.IndexOf('\n', k);
                key = nlPos < 0 ? buf.Substring(k) : buf.Substring(k, nlPos - k);
                value = null;
            }
            else
            {
                key = buf.Substring(k, sp - k);
                int v = sp + 1;
                while (v < buf.Length && IsSpace(buf[v]))
                {
                    v++;
                }
                value = buf.Substring(v);
            }
            // value keeps its newline, as C's does; the rules that store text cut it there
            string text = value == null ? "" : (value.IndexOf('\n') is var nl and >= 0 ? value.Substring(0, nl) : value);
            int num = CStr.Atoi(value ?? "");

            if (key == "arch")
            {
                // This is an oddity, but not something we care about much.
                if (value != "map\n")
                {
                    Log(LogLevel.Error, "loading map and got a non 'arch map' line(" + key + " " + (value ?? "(null)") + ")?\n");
                }
            }
            else if (key == "name")
            {
                m.Name = text;
            }
            else if (key == "msg")
            {
                string? line;
                while ((line = Fgets(fp, Define.HUGE_BUF - 1)) != null)
                {
                    if (line == "endmsg\n")
                    {
                        break;
                    }
                    msgbuf.Append(line);
                }
                // There are lots of maps that have empty messages (msg/endmsg with nothing between): the empty message is not kept.
                if (msgbuf.Length != 0)
                {
                    m.Msg = msgbuf.ToString();
                }
            }
            else if (key == "hp" || key == "enter_x")
            {
                m.EnterX = num;
            }
            else if (key == "sp" || key == "enter_y")
            {
                m.EnterY = num;
            }
            else if (key == "x" || key == "width")
            {
                m.Width = num;
            }
            else if (key == "y" || key == "height")
            {
                m.Height = num;
            }
            else if (key == "weight" || key == "reset_timeout")
            {
                m.ResetTimeout = num;
            }
            else if (key == "value" || key == "swap_time")
            {
                m.Timeout = num;
            }
            else if (key == "level" || key == "difficulty")
            {
                m.Difficulty = num;
            }
            else if (key == "invisible" || key == "darkness")
            {
                m.Darkness = num;
            }
            else if (key == "stand_still" || key == "fixed_resettime")
            {
                m.FixedResettime = num;
            }
            else if (key == "unique")
            {
                m.Unique = num;
            }
            else if (key == "outdoor")
            {
                m.Outdoor = num;
            }
            else if (key == "temp")
            {
                m.Temp = num;
            }
            else if (key == "pressure")
            {
                m.Pressure = num;
            }
            else if (key == "humid")
            {
                m.Humid = num;
            }
            else if (key == "windspeed")
            {
                m.Windspeed = num;
            }
            else if (key == "winddir")
            {
                m.Winddir = num;
            }
            else if (key == "sky")
            {
                m.Sky = num;
            }
            else if (key.StartsWith("tile_path_", StringComparison.Ordinal))
            {
                int tile = CStr.Atoi(key.Substring(10));
                if (tile < 1 || tile > 4)
                {
                    Log(LogLevel.Error, "load_map_header: tile location " + tile + " out of bounds (" + (m.Name ?? "") + ")\n");
                }
                else
                {
                    if (m.TilePath[tile - 1] != null)
                    {
                        Log(LogLevel.Error, "load_map_header: tile location " + tile + " duplicated (" + (m.Name ?? "") + ")\n");
                    }
                    m.TilePath[tile - 1] = text;
                }
            }
            else if (key == "end")
            {
                break;
            }
            else
            {
                Log(LogLevel.Error, "Got unknown value in map header: " + key + " " + (value ?? "(null)") + "\n");
            }
        }
        if (key != "end")
        {
            Log(LogLevel.Error, "Got premature eof on map header!\n");
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// map.c <c>new_save_map</c>, the part that writes the header: <c>arch map</c> and the fields that are not 0, up to <c>end</c>.
    /// <paramref name="flag"/> is the function's flag: <c>swap_time</c> is written only when it is 0.
    /// </summary>
    public void SaveMapHeader(StringBuilder fp, MapHeader m, int flag)
    {
        fp.Append("arch map\n");
        if (m.Name != null)
        {
            fp.Append("name ").Append(m.Name).Append('\n');
        }
        if (flag == 0)
        {
            fp.Append("swap_time ").Append(m.SwapTime).Append('\n');
        }
        void Number(string key, int value)
        {
            if (value != 0)
            {
                fp.Append(key).Append(' ').Append(value).Append('\n');
            }
        }
        Number("reset_timeout", m.ResetTimeout);
        Number("fixed_resettime", m.FixedResettime);
        // we unfortunately have no idea if this is a value the creator set or a difficulty value we generated when the map was first loaded
        Number("difficulty", m.Difficulty);
        Number("darkness", m.Darkness);
        Number("width", m.Width);
        Number("height", m.Height);
        Number("enter_x", m.EnterX);
        Number("enter_y", m.EnterY);
        if (m.Msg != null)
        {
            fp.Append("msg\n").Append(m.Msg).Append("endmsg\n");
        }
        Number("unique", m.Unique);
        Number("outdoor", m.Outdoor);
        Number("temp", m.Temp);
        Number("pressure", m.Pressure);
        Number("humid", m.Humid);
        Number("windspeed", m.Windspeed);
        Number("winddir", m.Winddir);
        Number("sky", m.Sky);
        // Save any tiling information
        for (int i = 0; i < 4; i++)
        {
            if (m.TilePath[i] != null)
            {
                fp.Append("tile_path_").Append(i + 1).Append(' ').Append(m.TilePath[i]).Append('\n');
            }
        }
        fp.Append("end\n");
    }

    /// <summary>
    /// map.c <c>load_objects</c>: loads the objects of a map file, after its header, one block at a time. Each object that has an
    /// archetype goes to <see cref="CrossfireHost.InsertObInMap"/> (C's <c>insert_ob_in_map</c>) with the flags C gives it: a block
    /// ended as a further part (<c>LL_MORE</c>) is linked to the object before it. An object whose archetype is not found is
    /// discarded (a debug message). <c>link_multipart_objects</c> is not ported: it walks the map's squares, which the host keeps.
    /// </summary>
    public void LoadObjects(IMapHandle m, LoadSource fp, int mapflags)
    {
        int bufstate = Define.LO_NEWFILE;
        CfObject? prev = null;
        CfObject? lastMore = null;

        CfObject op = GetObject();
        op.Map = m; // To handle buttons correctly
        int i;
        while ((i = LoadObject(fp, op, bufstate, mapflags)) != 0)
        {
            // Since the loading of the map header does not load an object anymore, we need to pass LO_NEWFILE for the first object
            // loaded, and then switch to LO_REPEAT for faster loading.
            bufstate = Define.LO_REPEAT;

            // if the archetype for the object is null, means that we got an invalid object. Don't do anything with it.
            if (op.Arch == null)
            {
                if (op.Name != null)
                {
                    Log(LogLevel.Debug, "Discarded object " + op.Name + " - invalid archetype.\n");
                }
                continue;
            }

            // check for unique items, or unique squares
            bool unique = false;
            for (CfObject? otmp = Host.GetMapOb(m, op.X, op.Y); otmp != null; otmp = otmp.Above)
            {
                if (otmp.QueryFlag(Define.FLAG_UNIQUE))
                {
                    unique = true;
                }
            }
            if (op.QueryFlag(Define.FLAG_UNIQUE) || op.QueryFlag(Define.FLAG_OBJ_SAVE_ON_OVL))
            {
                unique = true;
            }
            if (!((mapflags & (Define.MAP_OVERLAY | Define.MAP_PLAYER_UNIQUE)) != 0 || unique))
            {
                op.SetFlag(Define.FLAG_OBJ_ORIGINAL);
            }

            switch (i)
            {
                case Define.LL_NORMAL:
                    Host.InsertObInMap(op, m, op, Define.INS_NO_MERGE | Define.INS_NO_WALK_ON | Define.INS_ON_TOP);
                    if (op.Inv != null)
                    {
                        SumWeight(op);
                    }
                    prev = op;
                    lastMore = op;
                    break;

                case Define.LL_MORE:
                    Host.InsertObInMap(op, m, op, Define.INS_NO_MERGE | Define.INS_NO_WALK_ON | Define.INS_ABOVE_FLOOR_ONLY);
                    op.Head = prev;
                    lastMore!.More = op;
                    lastMore = op;
                    break;
            }
            op = GetObject();
            op.Map = m;
        }
        FreeObject(op);
    }

    /// <summary>
    /// map.c <c>save_objects</c>: writes the objects of a map. <paramref name="squares"/> are the squares of the map in the order C
    /// walks them (column by column), each as its objects from the bottom up; the host supplies them, as the squares are its own.
    /// Parts of larger objects and objects that have an owner are not saved (only the head is); an object that is unique, or on a
    /// square with a unique floor, goes to <paramref name="fp2"/>. With <paramref name="flag"/> 0 every object is saved; with 2 those
    /// that were part of the original map (<c>FLAG_OBJ_ORIGINAL</c>) and unpaid ones are left out. Each is written by
    /// <see cref="SaveObject"/> with flag 3.
    /// </summary>
    public void SaveObjects(StringBuilder fp, StringBuilder fp2, IEnumerable<IEnumerable<CfObject>> squares, int flag)
    {
        foreach (IEnumerable<CfObject> square in squares)
        {
            bool unique = false;
            foreach (CfObject op in square)
            {
                if (op.QueryFlag(Define.FLAG_IS_FLOOR) && op.QueryFlag(Define.FLAG_UNIQUE))
                {
                    unique = true;
                }
                if (op.Type == Define.PLAYER)
                {
                    Log(LogLevel.Debug, "Player on map that is being saved\n");
                    continue;
                }
                if (op.Head != null || op.Owner != null)
                {
                    continue;
                }
                if (unique || op.QueryFlag(Define.FLAG_UNIQUE))
                {
                    SaveObject(fp2, op, 3);
                }
                else if (flag == 0 || (flag == 2 && !op.QueryFlag(Define.FLAG_OBJ_ORIGINAL) && !op.QueryFlag(Define.FLAG_UNPAID)))
                {
                    SaveObject(fp, op, 3);
                }
            }
        }
    }

    /// <summary>
    /// player.c <c>get_player_archetype</c>: the next archetype of type PLAYER after <paramref name="at"/> in the list of archetypes,
    /// wrapping round to the first; with null, the first player archetype. C exits when there is none (and loops for ever when it is
    /// called with null); the port logs the error and returns null (docs/differences.md).
    /// </summary>
    public Archetype? GetPlayerArchetype(Archetype? at)
    {
        Archetype? start = at;
        int steps = 0;
        for (Archetype? a = FirstArchetype; a != null; a = a.Next)
        {
            steps++;
        }
        for (int i = 0; i <= steps; i++)
        {
            if (at == null || at.Next == null)
            {
                at = FirstArchetype;
            }
            else
            {
                at = at.Next;
            }
            if (at == null)
            {
                break;
            }
            if (at.Clone.Type == Define.PLAYER)
            {
                return at;
            }
            if (at == start)
            {
                break;
            }
        }
        Log(LogLevel.Error, "No Player achetypes\n");
        return null;
    }
}
