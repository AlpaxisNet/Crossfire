// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/c_misc.c <c>command_title</c>, <c>command_peaceful</c>,
// <c>command_brace</c>, <c>command_fix_me</c>, <c>command_listen</c> (the verbose level), <c>command_who</c>, <c>onoff_value</c> and
// <c>command_save</c>'s texts, server/c_chat.c <c>command_tell</c>, and the table of server/commands.c as far as the extension has the
// commands (docs/mud-phase3.md section 4.6). The object commands <c>drop</c>, <c>dropall</c> and <c>take</c> are lane 5A's
// (PickupDrop.cs, Pickup.cs); <c>command_help</c> is in HelpCommand.cs.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/c_misc.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     server/c_chat.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     server/commands.c:
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

namespace Crossfire.Extension.Gk;

/// <summary>
/// The commands of server/c_misc.c, c_chat.c and commands.c that Graal Kingdoms' <c>mudcommand</c> reaches and that need no level: each
/// takes the player and the text after the command (null for none, as C's <c>params</c>) and says what it says through
/// <c>DrawInfo</c>. What differs from C is in docs/differences.md ("Commands").
/// </summary>
internal sealed class Commands
{
    /// <summary>include/define.h <c>MAX_NAME</c>.</summary>
    public const int MaxName = 16;

    /// <summary>include/define.h <c>MAX_BUF</c>.</summary>
    public const int MaxBuf = 256;

    /// <summary>login.c copies the archetype's name with <c>strncpy(pl->title, ..., MAX_NAME)</c> and <c>MAX_NAME</c> - 1 characters remain.</summary>
    internal static string CutTitle(string name) => name.Length >= MaxName ? name[..(MaxName - 1)] : name;

    /// <summary>The text of C's <c>parse_string</c> for a word that is no command.</summary>
    public const string UnknownCommand = "Unknown command.  Try help.";

    /// <summary>The names of the normal commands of this extension's table, in the order <c>help commands</c> lists them (commands.c <c>Commands</c>).</summary>
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "brace", "drop", "dropall", "fix_me", "fixme", "get", "help", "mark", "peaceful", "save", "take", "title", "verbose", "who",
    };

    /// <summary>The names of the communication commands (commands.c <c>CommunicationCommands</c>).</summary>
    public static readonly IReadOnlyList<string> CommunicationNames = new[] { "tell" };

    private readonly Dispatcher d;

    public Commands(Dispatcher dispatcher)
    {
        d = dispatcher;
    }

    private CfWorld World => d.WorldOrNull!;

    private void Say(CfObject who, string text) => World.Host.DrawInfo(who, text);

    /// <summary>c_misc.c <c>command_title</c> (config.h <c>SET_TITLE</c>): says or sets the title the player gave himself.</summary>
    public void Title(PlayerState ps, string? parameters)
    {
        CfObject op = ps.Ob;
        var contr = (Player)op.Contr!;
        // dragon players cannot change titles
        if (CfWorld.IsDragonPl(op) != 0)
        {
            Say(op, "Dragons cannot change titles.");
            return;
        }
        if (parameters == null)
        {
            Say(op, "Your title is '" + (contr.OwnTitle.Length == 0 ? contr.Title : contr.OwnTitle) + "'.");
            return;
        }
        if (parameters == "clear" || parameters == "default")
        {
            Say(op, contr.OwnTitle.Length == 0 ? "Your title is the default title." : "Title set to default.");
            contr.OwnTitle = "";
            return;
        }
        if (parameters.Length >= MaxName)
        {
            Say(op, "Title too long.");
            return;
        }
        contr.OwnTitle = parameters;
    }

    /// <summary>C <c>pl-&gt;peaceful</c>: get_player (player.c) sets it to 1 ("default peaceful") and check_login (login.c) keeps that when the file has no <c>peaceful</c> line, so an account without the line <c>mudpeaceful</c> is peaceful; only a line of 0 is not.</summary>
    internal static bool IsPeaceful(CfObject ob) => !(ob.Extra?.Get(PeacefulKey) is { } t && CStr.Atoi(t) == 0);

    /// <summary>The account's line that holds <c>peaceful</c>.</summary>
    internal const string PeacefulKey = "mudpeaceful";

    /// <summary>c_misc.c <c>command_peaceful</c>: toggles whether the player attacks other players (GK's member <c>peaceful</c>, the account's line <c>mudpeaceful</c>).</summary>
    public void Peaceful(PlayerState ps)
    {
        CfObject op = ps.Ob;
        bool peaceful = !IsPeaceful(op);
        (op.Extra ??= new ExtraKeys()).Set("mudpeaceful", peaceful ? "1" : "0");
        ps.Dirty = true;
        Say(op, peaceful ? "You will not attack other players." : "You will attack other players.");
    }

    /// <summary>c_misc.c <c>command_brace</c>: without parameters toggles, else sets from <see cref="OnoffValue"/>; <c>fix_player</c> follows.</summary>
    public void Brace(PlayerState ps, string? parameters)
    {
        CfObject op = ps.Ob;
        var contr = (Player)op.Contr!;
        contr.Braced = parameters == null ? !contr.Braced : OnoffValue(parameters) != 0;
        Say(op, contr.Braced ? "You are braced." : "Not braced.");
        World.Host.FixPlayer(op);
    }

    /// <summary>c_misc.c <c>command_fix_me</c>: <c>fix_player</c>.</summary>
    public void FixMe(PlayerState ps) => World.Host.FixPlayer(ps.Ob);

    /// <summary>
    /// c_misc.c <c>command_listen</c> (Graal Kingdoms calls it <c>verbose</c>: players.md 5.10): sets the verbose level, which GK's
    /// <c>sendMessage</c> compares with the level of a message (<c>mudverbose</c>). C stores it in a <c>char</c>.
    /// </summary>
    public void Verbose(PlayerState ps, string? parameters)
    {
        CfObject op = ps.Ob;
        if (parameters == null || !ScanInt(parameters, out int i))
        {
            long now = op.Extra?.Get("mudverbose") is { } t ? CStr.Atoi(t) : 0;
            Say(op, "Set listen to what (presently " + now.ToString(CultureInfo.InvariantCulture) + ")?");
            return;
        }
        (op.Extra ??= new ExtraKeys()).Set("mudverbose", unchecked((sbyte)i).ToString(CultureInfo.InvariantCulture));
        ps.Dirty = true;
        Say(op, "Your verbose level is now " + i.ToString(CultureInfo.InvariantCulture) + ".");
    }

    /// <summary>
    /// c_misc.c <c>command_who</c>: the players of this extension that are logged in, one line each. C's line has the player's host and
    /// map, which the extension does not know (docs/differences.md, "Commands"); it has the name, the title and <c>P</c> or <c>W</c>.
    /// </summary>
    public void Who(PlayerState ps)
    {
        CfObject op = ps.Ob;
        Say(op, "Players:");
        foreach (PlayerState other in d.LoggedIn())
        {
            var contr = (Player)other.Ob.Contr!;
            bool peaceful = IsPeaceful(other.Ob);
            Say(op, other.Ob.Name + " the " + (contr.OwnTitle.Length == 0 ? contr.Title : contr.OwnTitle) + " " + (peaceful ? "P" : "W"));
        }
    }

    /// <summary>
    /// c_chat.c <c>command_tell</c>: <paramref name="parameters"/> are the name and the message. Only the players this extension knows
    /// can be reached; the name is compared as C's <c>strncasecmp(..., MAX_NAME)</c> does.
    /// </summary>
    public void Tell(PlayerState ps, string? parameters)
    {
        CfObject op = ps.Ob;
        string? name = null;
        string? msg = null;
        if (parameters != null)
        {
            name = parameters;
            int space = name.IndexOf(' ');
            if (space >= 0)
            {
                msg = name[(space + 1)..];
                name = name[..space];
                if (msg.Length == 0)
                {
                    msg = null;
                }
            }
        }
        if (name == null)
        {
            Say(op, "Tell whom what?");
            return;
        }
        if (msg == null)
        {
            Say(op, "Tell " + name + " what?");
            return;
        }
        string line = op.Name + " tells you: ";
        line += msg.Length > MaxBuf - line.Length - 1 ? msg[..Math.Max(0, MaxBuf - line.Length - 1)] : msg;
        foreach (PlayerState other in d.LoggedIn())
        {
            if (SameName(other.Ob.Name ?? "", name))
            {
                Say(other.Ob, line);
                Say(op, "You tell to " + name + ": " + msg);
                return;
            }
        }
        Say(op, "No such player.");
    }

    // strncasecmp(a, b, MAX_NAME) == 0
    private static bool SameName(string a, string b)
    {
        a = a.Length > MaxName ? a[..MaxName] : a;
        b = b.Length > MaxName ? b[..MaxName] : b;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>c_misc.c <c>onoff_value</c>: a number is true unless it is 0; else the first letter decides (<c>on</c>, <c>y</c>, <c>k</c>, <c>s</c> and <c>d</c> are true).</summary>
    public static int OnoffValue(string line)
    {
        if (ScanInt(line, out int i))
        {
            return i != 0 ? 1 : 0;
        }
        char c0 = line.Length > 0 ? line[0] : '\0';
        char c1 = line.Length > 1 ? line[1] : '\0';
        switch (c0)
        {
            case 'o':
                return c1 == 'n' ? 1 : 0;
            case 'y':
            case 'k':
            case 's':
            case 'd':
                return 1;
            default:
                return 0;
        }
    }

    /// <summary>C's <c>sscanf(line, "%d", &amp;i)</c> succeeding: white space, an optional sign and digits (a number too large is clamped).</summary>
    public static bool ScanInt(string line, out int value)
    {
        value = 0;
        int p = 0;
        while (p < line.Length && (line[p] == ' ' || (line[p] >= '\t' && line[p] <= '\r')))
        {
            p++;
        }
        int start = p;
        if (p < line.Length && (line[p] == '+' || line[p] == '-'))
        {
            p++;
        }
        int digits = p;
        while (p < line.Length && line[p] >= '0' && line[p] <= '9')
        {
            p++;
        }
        if (p == digits)
        {
            return false;
        }
        long n = 0;
        for (int i = digits; i < p; i++)
        {
            n = Math.Min(n * 10 + (line[i] - '0'), (long)int.MaxValue + 1);
        }
        value = (int)Math.Clamp(line[start] == '-' ? -n : n, int.MinValue, int.MaxValue);
        return true;
    }
}
