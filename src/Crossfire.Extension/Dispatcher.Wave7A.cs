// Not ported from a Crossfire file: lane 7A of wave 7, the <c>mudcommand</c> table (docs/mud-phase3.md section 4.6; docs/extension.md,
// "Commands"). The function <c>mudcommand(cmd, params)</c> takes the place of C's <c>parse_string</c>: the command word is looked up in
// the table below, and the handler is one of the ported commands (Gk/Command.cs, Gk/HelpCommand.cs, and lane 5A's <c>take</c>,
// <c>drop</c> and <c>dropall</c>), which say what they say through <c>DrawInfo</c>. The function answers nil.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    private Commands? commandRules;

    /// <summary>The ported commands of c_misc.c and c_chat.c of this dispatcher.</summary>
    internal Commands CommandRules => commandRules ??= new Commands(this);

    private partial Outcome? InvokeWave7A(object target, long method, IReadOnlyList<Val> args) => null;

    /// <summary>The logged-in players of this extension (those with a controller), by name, as <c>who</c> and <c>tell</c> list them.</summary>
    internal List<PlayerState> LoggedIn() =>
        players.Values.Where(p => p.Contr != null && p.Loaded).OrderBy(p => p.Ob.Name ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Account, StringComparer.Ordinal).ToList();

    /// <summary>
    /// <c>mudcommand(cmd, params)</c> (a slow function, because <c>drop</c>, <c>take</c> and <c>save</c> change items and the account):
    /// <c>ctx.player</c> runs the command. C's <c>parse_string</c> reads the word up to the first space as the command (lower case) and
    /// the rest, without its leading spaces, as the parameters; a call that has no parameters, or an empty text, gives C's NULL.
    /// Nil is the answer. The commands that change something answer <c>busy</c> before they do.
    /// </summary>
    internal Outcome MudCommand(IReadOnlyList<Val> args)
    {
        if (args.Count is < 1 or > 2 || BytesText(args[0]) is not { } word || (args.Count == 2 ? BytesText(args[1]) : "") is not { } rest)
        {
            return Outcome.Fail(Status.BadArgs, "mudcommand takes a command and its parameters");
        }
        if (ActorOfRequest() is not { } ps)
        {
            log(1, "mudcommand: the call has no logged-in player");
            return Outcome.Ok(Val.Nil);
        }
        // the whole line as C's parse_string gets it: "cmd params"
        string line = rest.Length == 0 ? word : word + " " + rest;
        line = line.TrimStart(' ');
        string cmd = line;
        string? parameters = null;
        int space = line.IndexOf(' ');
        if (space >= 0)
        {
            cmd = line[..space];
            parameters = line[(space + 1)..].TrimStart(' ');
            if (parameters.Length == 0)
            {
                parameters = null;
            }
        }
        return RunCommand(ps, cmd.ToLowerInvariant(), parameters);
    }

    /// <summary>The table of the commands (docs/mud-phase3.md section 4.6): the handler of <paramref name="cmd"/> (lower case), or C's text for an unknown word.</summary>
    private Outcome RunCommand(PlayerState ps, string cmd, string? parameters)
    {
        Commands rules = CommandRules;
        switch (cmd)
        {
            case "drop":
                return CommandDrop(ps, parameters);
            case "dropall":
                return CommandDropAll(ps, parameters);
            case "take":
            case "get":
                return CommandTake(ps, parameters);
            case "mark":
                return CommandMark(ps, parameters);
            // the function is slow: the dispatcher answers busy before it gets here, so these need no check of their own
            case "save":
                return CommandSave(ps);
            case "brace":
                return Done(() => rules.Brace(ps, parameters));
            case "fix_me":
            case "fixme":
                return Done(() => rules.FixMe(ps));
            case "title":
                return Done(() => rules.Title(ps, parameters));
            case "peaceful":
                return Done(() => rules.Peaceful(ps));
            case "verbose":
                return Done(() => rules.Verbose(ps, parameters));
            case "who":
                return Done(() => rules.Who(ps));
            case "tell":
                return Done(() => rules.Tell(ps, parameters));
            case "help":
                return Done(() => HelpCommand.Run(world!, ps.Ob, parameters));
            default:
                // useskill, readyskill, party, blood, search, cast, invoke (phase 4) and every other word
                return Done(() => world!.Host.DrawInfo(ps.Ob, Commands.UnknownCommand));
        }
    }

    // runs a handler that only says things and changes the player's state, and answers nil
    private static Outcome Done(Action run)
    {
        run();
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>
    /// c_object.c <c>command_mark</c> for the text of the call, with Graal Kingdoms' addition (docs/mud-phase3.md section 4.6): a text of
    /// digits only is the <c>mudid</c> of an item of the player's top level (the Item-Menu sends <c>#s(item)</c>, the id as text), and marks
    /// it; any other text is matched by name as C does, and no text says what is marked.
    /// </summary>
    private Outcome CommandMark(PlayerState ps, string? parameters)
    {
        CfWorld w = world!;
        if (parameters != null && parameters.All(char.IsAsciiDigit))
        {
            CfObject? item = long.TryParse(parameters, NumberStyles.None, CultureInfo.InvariantCulture, out long id) ? FindItemById(ps, id, deep: false) : null;
            if (item != null && item.Env == ps.Ob && item.Invisible == 0)
            {
                ItemMatching.MarkItem(w, ps.Ob, item);
            }
            else
            {
                w.Host.DrawInfo(ps.Ob, "Could not find an object that matches " + parameters);
            }
        }
        else
        {
            ItemMatching.CommandMark(w, ps.Ob, parameters);
        }
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>
    /// c_misc.c <c>command_save</c>: the account is written now, not after the quiet time of the writer ("You have been saved."), or
    /// "SAVE FAILED!" when it cannot be (it has no usable file name, or it was unloaded: its file is kept).
    /// </summary>
    private Outcome CommandSave(PlayerState ps)
    {
        if (!ps.Loaded || FileNames.AccountFile(ps.FileKey) == null)
        {
            world!.Host.DrawInfo(ps.Ob, "SAVE FAILED!");
            return Outcome.Ok(Val.Nil);
        }
        SubmitAccount(ps);
        writer!.Kick();
        world!.Host.DrawInfo(ps.Ob, "You have been saved.");
        return Outcome.Ok(Val.Nil);
    }
}
