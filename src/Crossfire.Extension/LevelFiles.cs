// Not ported from a Crossfire file: the level files of the Crossfire extension: Crossfire's map text (the header, then one block per
// object, each as its differences from the archetype), with the lines Graal Kingdoms' mudlevels files add. Reading and writing go
// through the port's load_map_header, load_objects, save_objects and save_object (src/Crossfire/CfWorld.Map.cs).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;
using System.Text;
using Crossfire.Extension.Gk;

namespace Crossfire.Extension;

/// <summary>What reading a level's text found.</summary>
internal sealed record LevelRead(Level Level, int Objects, int DiscardedParts, int Errors);

/// <summary>Reads and writes level files (see docs/extension.md, "The level and account files").</summary>
internal static class LevelFiles
{
    /// <summary>
    /// Changes the position lines of the objects (<c>x</c> and <c>y</c>, at the top of an object block, not in a nested one) into
    /// <c>mudx</c> and <c>mudy</c>, which the host's loader keeps whole: Crossfire's <c>x</c> and <c>y</c> are integers (the C
    /// rules read them with <c>atoi</c>), and Graal Kingdoms' files hold tile fractions such as <c>x 3.328125</c>. A block's
    /// message text is skipped as C's scanner skips it.
    /// </summary>
    public static string RenamePositions(string text)
    {
        var sb = new StringBuilder(text.Length + 64);
        int depth = 0;
        bool inMessage = false;
        int pos = 0;
        while (pos < text.Length)
        {
            int nl = text.IndexOf('\n', pos);
            int end = nl < 0 ? text.Length : nl + 1;
            string line = text.Substring(pos, end - pos);
            pos = end;
            string trimmed = line.TrimEnd('\n', '\r', ' ', '\t');
            if (inMessage)
            {
                if (trimmed == "endmsg")
                {
                    inMessage = false;
                }
                sb.Append(line);
                continue;
            }
            int s = 0;
            while (s < line.Length && (line[s] == ' ' || line[s] == '\t'))
            {
                s++;
            }
            int e = s;
            while (e < line.Length && line[e] != ' ' && line[e] != '\t' && line[e] != '\r' && line[e] != '\n')
            {
                e++;
            }
            string key = line.Substring(s, e - s);
            if (trimmed.Trim() == "msg")
            {
                inMessage = true;
            }
            else if (key.Equals("arch", StringComparison.OrdinalIgnoreCase))
            {
                depth++;
            }
            else if (key.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                if (depth > 0)
                {
                    depth--;
                }
            }
            else if (depth == 1 && key.Length == 1 && (key[0] == 'x' || key[0] == 'X' || key[0] == 'y' || key[0] == 'Y'))
            {
                sb.Append(line, 0, s).Append("mud").Append(char.ToLowerInvariant(key[0])).Append(line, e, line.Length - e);
                continue;
            }
            sb.Append(line);
        }
        return sb.ToString();
    }

    private static bool StartsWithHeader(string text)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            int nl = text.IndexOf('\n', pos);
            int end = nl < 0 ? text.Length : nl;
            string line = text.Substring(pos, end - pos).Trim();
            pos = nl < 0 ? text.Length : nl + 1;
            if (line.Length == 0)
            {
                continue;
            }
            return line == "arch map";
        }
        return false;
    }

    /// <summary>
    /// Reads a level from a map text (a saved level or Graal Kingdoms' mudlevels seed): the header (when the text starts with
    /// <c>arch map</c>), then the objects by the port's <c>load_objects</c>. An object whose archetype is unknown is discarded as C
    /// discards it. Nothing is de-duplicated.
    /// </summary>
    public static LevelRead Read(CfWorld world, GkHost host, string name, string text)
    {
        var level = new Level(name);
        int errorsBefore = world.LogCounts[(int)LogLevel.Error];
        var src = new LoadSource(text);
        if (StartsWithHeader(text))
        {
            world.LoadMapHeader(src, level.Header);
        }
        level.Header.Name ??= FileText.ToBytesText(name);
        string rest = RenamePositions(text.Substring(src.Pos));

        var collected = new List<CfObject>();
        host.Collecting = collected;
        try
        {
            world.LoadObjects(level, new LoadSource(rest), Define.MAP_PLAYER_UNIQUE);
        }
        finally
        {
            host.Collecting = null;
        }

        int parts = 0;
        foreach (CfObject ob in collected)
        {
            if (ob.Head != null)
            {
                parts++; // a further part of a larger object: it stays linked to its head
                continue;
            }
            var lo = new LevelObject(level, ob);
            ob.Map = level;
            Inventories.RestoreFileOrder(ob);
            if (ob.Extra != null)
            {
                lo.X = Take(ob.Extra, "mudx");
                lo.Y = Take(ob.Extra, "mudy");
                // the object's own flag lines: the ones its archetype has too came with the copy of the archetype, not from the file
                List<string> own = ob.Arch?.Clone.Extra?.GetAll("flag").ToList() ?? new List<string>();
                foreach (string flag in ob.Extra.GetAll("flag"))
                {
                    if (!own.Remove(flag))
                    {
                        lo.SeedFlags.Add(flag);
                    }
                }
                ob.Extra.Remove("flag");
                if (ob.Extra.Get("mudblob") is { } blob)
                {
                    ob.Extra.Remove("mudblob");
                    string text2 = FileText.FromBytesText(FileText.UnescapeLine(blob));
                    lo.Blob = text2.Length == 0 ? null : text2;
                }
            }
            level.Objects.Add(lo);
        }
        return new LevelRead(level, level.Objects.Count, parts, world.LogCounts[(int)LogLevel.Error] - errorsBefore);
    }

    private static double Take(ExtraKeys extra, string key)
    {
        double v = 0;
        if (extra.Get(key) is { } s)
        {
            double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
            extra.Remove(key);
        }
        return double.IsFinite(v) ? v : 0;
    }

    /// <summary>The text of a level's file: the header, then each object that is saved, in the order of the level.</summary>
    public static string Write(CfWorld world, Level level)
    {
        var sb = new StringBuilder();
        world.SaveMapHeader(sb, level.Header, 1);
        foreach (LevelObject lo in level.Objects)
        {
            if (lo.SaveLocally && !lo.Dead)
            {
                sb.Append(ObjectBlock(world, lo));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// One object's block: the port's <c>save_objects</c> writes it (the object as its differences from the archetype, the
    /// inventory nested), and the lines of this extension follow the <c>arch</c> line: <c>x</c>, <c>y</c> as the host's floats,
    /// the script variables the object's file had while the host has given no state, and the host's state as <c>mudblob</c>.
    /// </summary>
    public static string ObjectBlock(CfWorld world, LevelObject lo)
    {
        CfObject ob = lo.Ob;
        short x = ob.X;
        short y = ob.Y;
        if (ob.Arch != null)
        {
            ob.X = ob.Arch.Clone.X; // the integer position is the archetype's: only the floats below are the object's
            ob.Y = ob.Arch.Clone.Y;
        }
        var block = new StringBuilder();
        try
        {
            world.SaveObjects(block, block, new[] { new[] { ob } }, 0);
        }
        finally
        {
            ob.X = x;
            ob.Y = y;
        }
        string text = block.ToString();
        int nl = text.IndexOf('\n');
        if (nl < 0)
        {
            return "";
        }
        var extra = new StringBuilder();
        extra.Append("x ").Append(Number(lo.X)).Append('\n');
        extra.Append("y ").Append(Number(lo.Y)).Append('\n');
        if (lo.Blob == null)
        {
            foreach (string flag in lo.SeedFlags)
            {
                extra.Append("flag ").Append(flag).Append('\n');
            }
        }
        else
        {
            extra.Append("mudblob ").Append(FileText.EscapeLine(FileText.ToBytesText(lo.Blob))).Append('\n');
        }
        return text.Insert(nl + 1, extra.ToString());
    }

    /// <summary>A position as text: the shortest form that reads back as the same number.</summary>
    public static string Number(double d) => d.ToString("R", CultureInfo.InvariantCulture);
}
