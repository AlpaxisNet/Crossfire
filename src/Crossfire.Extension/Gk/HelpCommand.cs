// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/c_misc.c <c>command_help</c>, <c>help_topics</c> and
// <c>show_commands</c> (docs/mud-phase3.md section 4.6). The files <c>command_help</c> reads, Crossfire's lib/def_help and lib/help/*
// at that commit, are embedded in the extension unchanged (Data/def_help, Data/help/*; docs/extension.md, "Commands").
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/c_misc.c:
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

/// <summary>The help files of the extension (lib/def_help and lib/help/*) and the command that shows them.</summary>
internal static class HelpCommand
{
    private const string TopicPrefix = "help/";

    private static readonly Lazy<IReadOnlyList<string>> topics = new(() =>
        typeof(HelpCommand).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(TopicPrefix, StringComparison.Ordinal))
            .Select(n => n[TopicPrefix.Length..])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList());

    /// <summary>The names of the help topics (the files of lib/help), in the order of their names (C lists them in the order the directory has them).</summary>
    public static IReadOnlyList<string> Topics => topics.Value;

    /// <summary>The lines of a help file, as C reads them with <c>fgets</c> into a <c>MAX_BUF</c> buffer (a longer line is cut into pieces); null when there is no such file.</summary>
    public static List<string>? Lines(string resource)
    {
        using Stream? file = typeof(HelpCommand).Assembly.GetManifestResourceStream(resource);
        if (file == null)
        {
            return null;
        }
        using var ms = new MemoryStream();
        file.CopyTo(ms);
        return SplitLines(CStr.Latin1.GetString(ms.ToArray()));
    }

    /// <summary>The lines of a text as C's <c>fgets</c> loop gives them (see <see cref="Lines"/>).</summary>
    public static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        int pos = 0;
        while (pos < text.Length)
        {
            int nl = text.IndexOf('\n', pos);
            int end = nl < 0 ? text.Length : nl + 1;
            if (end - pos > Commands.MaxBuf - 1)
            {
                end = pos + Commands.MaxBuf - 1; // fgets stops when the buffer is full
            }
            string line = text[pos..end];
            // the newline is taken off; a line that has none (the last, or a piece) loses nothing
            lines.Add(line.EndsWith('\n') ? line[..^1] : line);
            pos = end;
        }
        return lines;
    }

    /// <summary>c_misc.c <c>command_help</c> for a player who is no wizard (the topics of lib/wizhelp are not shipped).</summary>
    public static void Run(CfWorld world, CfObject op, string? parameters)
    {
        void Say(string text) => world.Host.DrawInfo(op, text);

        // main help page?
        if (parameters == null)
        {
            foreach (string line in Lines("def_help")!)
            {
                Say(line);
            }
            return;
        }

        // topics list
        if (parameters == "topics")
        {
            ListTopics(Say, misc: true);
            ListTopics(Say, misc: false);
            return;
        }

        // commands list
        if (parameters == "commands")
        {
            ShowCommands(Say, Commands.Names, "      Commands:");
            ShowCommands(Say, Commands.CommunicationNames, "      Communication commands:");
            return;
        }

        // user wants info about a command
        if (parameters.Contains('.') || parameters.Contains(' ') || parameters.Contains('/'))
        {
            Say("Illegal characters in '" + parameters + "'");
            return;
        }

        // lib/mischelp does not exist at that commit, so a topic is a file of lib/help
        List<string>? help = Lines(TopicPrefix + parameters);
        if (help == null)
        {
            Say("No help availble on '" + parameters + "'"); // sic
            return;
        }
        Say("Help about '" + parameters + "'");
        foreach (string line in help)
        {
            Say(line);
        }
    }

    // c_misc.c help_topics: the names of the files of a folder, a line of at most 42 characters at a time; lib/mischelp has none
    private static void ListTopics(Action<string> say, bool misc)
    {
        say(misc ? "      Misc help:" : "      Commands:");
        if (misc)
        {
            return; // opendir fails
        }
        WrapNames(say, Topics);
    }

    // c_misc.c show_commands
    private static void ShowCommands(Action<string> say, IReadOnlyList<string> names, string header)
    {
        say(header);
        WrapNames(say, names);
    }

    private static void WrapNames(Action<string> say, IReadOnlyList<string> names)
    {
        string line = "";
        int linelen = 0;
        foreach (string name in names)
        {
            linelen += name.Length + 1;
            if (linelen > 42)
            {
                say(line);
                line = " " + name;
                linelen = name.Length + 1;
                continue;
            }
            line += " " + name;
        }
        say(line);
    }
}
