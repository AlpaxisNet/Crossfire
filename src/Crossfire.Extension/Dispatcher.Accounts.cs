// Not ported from a Crossfire file: the accounts of the Crossfire extension (docs/protocol.md section 7: ops 5 and 6; design section 10).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Text;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    private readonly Dictionary<string, PlayerState> players = new(StringComparer.Ordinal);

    private Outcome PlayerLogin(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || !(args[0].IsNil || args[0].Kind == ValKind.String))
        {
            return Outcome.Fail(Status.BadArgs, "player.login takes an account name");
        }
        string account = args[0].AsString;
        if (players.TryGetValue(account, out PlayerState? held))
        {
            // a second login without a logout: the same handle, and the player's strings are sent again
            SendClientStrings(held);
            StartItems(held, again: true);
            return Outcome.Ok(Val.Handle(handles.HandleOf(held)));
        }
        PlayerState? player = OpenPlayer(account);
        if (player == null)
        {
            return Outcome.Ok(Val.Nil);
        }
        players[account] = player;
        AttachController(player);
        SendClientStrings(player);
        StartItems(player, again: false);
        events.Add(Val.Map(
            ("name", Val.Str(Manifest.EventOnLoadedMudAccount)),
            ("target", Val.Str("player")),
            ("player", Val.Str(account)),
            ("args", Val.List(Val.Handle(handles.HandleOf(player))))));
        return Outcome.Ok(Val.Handle(handles.HandleOf(player)));
    }

    /// <summary>
    /// The player of an account: read from its file, or made new (from the first player archetype, as Crossfire's
    /// <c>get_player_archetype</c> chooses it). Null, with a log line, for an account name that cannot be a file name or holds a control character, a file that
    /// cannot be read (it is never written over) or a world without a player archetype.
    /// </summary>
    private PlayerState? OpenPlayer(string account, string? fileKey = null, bool create = true)
    {
        fileKey ??= account;
        string? rel = FileNames.AccountFile(fileKey);
        if (rel == null)
        {
            log(1, "player.login: the account name cannot be made a file name (empty, or its escaped form is over " + FileNames.MaxEscapedLength + " bytes)");
            return null;
        }
        if (account.Any(c => c < 0x20 || c == 0x7f))
        {
            // the name is written into the account's file as the player's name: a line break or another control character would write lines of its own
            log(1, "player.login: the account name holds a control character, it is refused");
            return null;
        }
        if (refused.Contains("account:" + fileKey))
        {
            return null;
        }
        CfObject? ob;
        bool created = false;
        try
        {
            byte[]? bytes = ReadDataFile(rel);
            if (bytes != null)
            {
                ob = world!.GetObject();
                int errorsBefore = world.LogCounts[(int)LogLevel.Error];
                forwardWorldLog = true;
                int r;
                try
                {
                    r = world.LoadObject(new LoadSource(FileText.FromBytes(bytes)), ob, Define.LO_NEWFILE, Define.MAP_PLAYER_UNIQUE);
                }
                finally
                {
                    forwardWorldLog = false;
                }
                if (r == Define.LL_EOF || ob.Arch == null)
                {
                    FreeCfObject(ob);
                    refused.Add("account:" + fileKey);
                    log(0, "the file of account " + fileKey + " holds no player, it is left as it is");
                    return null;
                }
                if (world.LogCounts[(int)LogLevel.Error] > errorsBefore)
                {
                    log(1, "the file of account " + fileKey + " had " + (world.LogCounts[(int)LogLevel.Error] - errorsBefore) + " errors");
                }
                if (ob.Extra?.Get("account") is { } named && FileText.UnescapeLine(named).TrimEnd(' ') != FileText.ToBytesText(fileKey).TrimEnd(' '))
                {
                    log(1, "the file of account " + fileKey + " says it belongs to another account; the file's name decides");
                }
                ob.Extra?.Remove("account");
                Inventories.RestoreFileOrder(ob);
            }
            else if (!create)
            {
                return null; // an account file of a slot that was never saved: there is nothing to load
            }
            else
            {
                Archetype? at = world!.GetPlayerArchetype(null);
                if (at == null)
                {
                    log(0, "player.login: the world has no player archetype");
                    return null;
                }
                ob = world.ArchToObject(at)!;
                ob.Name = FileText.ToBytesText(account);
                ob.NamePl = ob.Name;
                created = true;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or CrossfireFatalException)
        {
            refused.Add("account:" + fileKey);
            log(0, "the file of account " + fileKey + " cannot be read, it is left as it is: " + e.Message);
            return null;
        }
        return new PlayerState(account, ob) { Dirty = created, FileKey = fileKey };
    }

    private Outcome PlayerLogout(IReadOnlyList<Val> args)
    {
        if (args.Count != 1 || !(args[0].IsNil || args[0].Kind == ValKind.String))
        {
            return Outcome.Fail(Status.BadArgs, "player.logout takes an account name");
        }
        if (!players.TryGetValue(args[0].AsString, out PlayerState? player))
        {
            return Outcome.Ok(Val.Nil);
        }
        SubmitAccount(player);
        foreach (string name in player.ClientOrder)
        {
            effects.Add(Val.Map(("k", Val.Str("client.del")), ("player", Val.Str(player.Account)), ("name", Val.Str(name))));
        }
        handles.Kill(player);
        DetachController(player);
        players.Remove(player.Account);
        FreeCfObject(player.Ob);
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>Writes the account's file (through the writer). An account that was unloaded (<c>unloadmudaccount</c>) writes nothing: its file is kept.</summary>
    private void SubmitAccount(PlayerState player)
    {
        (string, byte[])? file = AccountFile(player);
        if (file != null)
        {
            PutFile(file.Value.Item1, file.Value.Item2);
        }
    }

    /// <summary>The file of an account (its path under the data folder and its content), and its dirty flag cleared; null when it has no usable file name or was unloaded (<c>unloadmudaccount</c>: its file is kept).</summary>
    private (string, byte[])? AccountFile(PlayerState player)
    {
        string? rel = FileNames.AccountFile(player.FileKey);
        if (rel == null || !player.Loaded)
        {
            return null;
        }
        byte[] bytes = AccountFileBytes(player);
        player.Dirty = false;
        return (rel, bytes);
    }

    /// <summary>The text of the account's file: the player's object as <c>save_object</c> writes it, and the name of the file's account after its first line.</summary>
    private byte[] AccountFileBytes(PlayerState player)
    {
        var sb = new StringBuilder();
        world!.SaveObject(sb, player.Ob, 3);
        string text = sb.ToString();
        int nl = text.IndexOf('\n');
        if (nl >= 0)
        {
            text = text.Insert(nl + 1, "account " + FileText.EscapeLine(FileText.ToBytesText(player.FileKey)) + "\n");
        }
        return FileText.ToBytes(text);
    }

    /// <summary>Sets one of the player's client strings (the whole flag name) when its value is not what the client has.</summary>
    internal void SetClientString(PlayerState player, string name, string value)
    {
        if (player.ClientStrings.TryGetValue(name, out string? old) && old == value)
        {
            return;
        }
        if (!player.ClientOrder.Contains(name))
        {
            player.ClientOrder.Add(name);
        }
        player.ClientStrings[name] = value;
        effects.Add(Val.Map(("k", Val.Str("client.set")), ("player", Val.Str(player.Account)), ("name", Val.Str(name)), ("value", Val.Str(value))));
    }

    /// <summary>The strings of the player's life and stats (players.md 3.2) as the client shows them; sent at login, again at a second login.</summary>
    private void SendClientStrings(PlayerState player)
    {
        // a second login sends everything again, even what the client has
        player.ClientStrings.Clear();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ObjMember<PlayerState> m in TMudPlayerMembers.Table.Members)
        {
            if (m.ClientName is { } name && seen.Add(name))
            {
                string value = Text(m.Read(this, player));
                if (value.Length > 0)
                {
                    SetClientString(player, name, value);
                }
            }
        }
    }
}
