// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): lib/collect.pl.in.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     lib/collect.pl.in:
//         Part of the Crossfire distribution; the file carries no copyright line of its own, the licence is that of the distribution: Copyright (C) 2001 Mark Wedel & Crossfire Development Team, Copyright (C) 1992 Frank Tore Johansen
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

/// <summary>The result of <see cref="ArchCollector.Collect"/>: the two files of collect.pl that the object model needs.</summary>
public sealed class ArchCollection
{
    /// <summary>The text of the <c>archetypes</c> file: every .arc file in order, with each anim block replaced by an <c>animation</c> line.</summary>
    public string Archetypes { get; init; } = "";

    /// <summary>The text of the <c>animations</c> file: <c>anim name</c>, the frames, <c>mina</c> for each animation, sorted by name.</summary>
    public string Animations { get; init; } = "";

    /// <summary>The .arc files read, in the order of reading.</summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();

    /// <summary>The warnings collect.pl prints.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// lib/collect.pl.in: the script that turns the folder of raw archetypes (<c>.arc</c> files) into the <c>archetypes</c> file and
/// the <c>animations</c> file. Only what changes the text of those two files is ported: the traversal (sorted names, the folders
/// <c>dev</c>, <c>trashbin</c> and <c>CVS</c> skipped), the removal of the face-colour lines, and the <c>anim</c> ... <c>mina</c>
/// blocks. The pictures, bmaps and faces files are the client's and are not ported (docs/porting.md).
/// </summary>
public static class ArchCollector
{
    /// <summary>
    /// collect.pl <c>traverse</c> and <c>archsOut</c>/<c>animOut</c>: reads every <c>.arc</c> file under <paramref name="root"/>.
    /// </summary>
    /// <param name="root">The folder of the raw archetypes (ARCHDIR).</param>
    /// <param name="faceExists">Whether a face (picture) of that name exists: collect.pl warns of the missing ones. Null: all exist.</param>
    public static ArchCollection Collect(string root, Func<string, bool>? faceExists = null)
    {
        var archs = new List<string>();
        Traverse(root, archs);

        var arch = new StringBuilder();
        var anim = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        faceExists ??= _ => true;

        foreach (string file in archs)
        {
            string archName = file;
            string[] lines = SplitLines(CStr.Latin1.GetString(File.ReadAllBytes(file)));
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                // chop: removes the last character, whatever it is
                if (line.Length > 0)
                {
                    line = line.Substring(0, line.Length - 1);
                }
                string[] fields = line.Split(new[] { ' ', '\t', '\n', '\r', '\f', '\v' }, StringSplitOptions.RemoveEmptyEntries);
                string v = fields.Length > 0 ? fields[0] : "";
                if (v == "Object")
                {
                    archName = string.Join("_", fields.Skip(1));
                }
                // Process the colour/face info now: these lines are not put in the archetypes file
                if (v == "color_fg" || v == "color_bg" || v == "visibility" || v == "magicmap")
                {
                    continue;
                }
                if (v == "anim")
                {
                    if (anim.TryGetValue(archName, out string? ex) && ex.Length != 0)
                    {
                        warnings.Add(archName + " is a duplicate animation name");
                        anim[archName] = "";
                    }
                    var frames = new StringBuilder(anim.GetValueOrDefault(archName, ""));
                    while (++li < lines.Length)
                    {
                        string l = lines[li];
                        // chomp: removes a trailing newline
                        if (l.EndsWith('\n'))
                        {
                            l = l.Substring(0, l.Length - 1);
                        }
                        // last if ($var =~ "mina\s*"): a line that has mina in it (the "\s" of a Perl string is just "s")
                        if (l.Contains("mina", StringComparison.Ordinal))
                        {
                            break;
                        }
                        if (!System.Text.RegularExpressions.Regex.IsMatch(l, @"facings \S+$") && !faceExists(l))
                        {
                            warnings.Add(archName + " is missing face " + l);
                        }
                        frames.Append(l).Append('\n');
                    }
                    anim[archName] = frames.ToString();
                    arch.Append("animation ").Append(archName).Append('\n');
                    continue;
                }
                if (v == "face" && fields.Length > 1 && !faceExists(fields[1]))
                {
                    warnings.Add(archName + " is missing face " + fields[1]);
                }
                arch.Append(line).Append('\n');
            }
        }

        var animOut = new StringBuilder();
        foreach (KeyValuePair<string, string> kv in anim)
        {
            animOut.Append("anim ").Append(kv.Key).Append('\n').Append(kv.Value).Append("mina\n");
        }
        return new ArchCollection { Archetypes = arch.ToString(), Animations = animOut.ToString(), Files = archs, Warnings = warnings };
    }

    private static string[] SplitLines(string text)
    {
        // Perl's <FH> reads a line with its newline; the last one may have none.
        var res = new List<string>();
        int pos = 0;
        while (pos < text.Length)
        {
            int nl = text.IndexOf('\n', pos);
            int end = nl >= 0 ? nl + 1 : text.Length;
            res.Add(text.Substring(pos, end - pos));
            pos = end;
        }
        return res.ToArray();
    }

    private static void Traverse(string dir, List<string> archs)
    {
        var names = new List<string>();
        foreach (string p in Directory.EnumerateFileSystemEntries(dir))
        {
            names.Add(Path.GetFileName(p));
        }
        names.Sort(StringComparer.Ordinal);
        foreach (string tfile in names)
        {
            if (tfile.StartsWith('.'))
            {
                continue;
            }
            string file = Path.Combine(dir, tfile);
            if (Directory.Exists(file))
            {
                if (tfile != "dev" && tfile != "trashbin" && tfile != "CVS")
                {
                    Traverse(file, archs);
                }
            }
            else if (tfile.EndsWith(".arc", StringComparison.Ordinal))
            {
                archs.Add(file);
            }
        }
    }
}
