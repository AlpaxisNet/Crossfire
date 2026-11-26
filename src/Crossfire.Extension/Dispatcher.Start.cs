// Not ported from a Crossfire file: the start path of the Crossfire extension (lane 4D, docs/mud-phase3.md section 4.7): the data
// the ported code reads, the functions and methods that start a character (<c>mudresetplayer</c>, <c>rollstats</c>,
// <c>setmudclass</c>, <c>setmudclassnoaction</c>) and the methods that load, unload, save and back up an account.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    // ------------------------------------------------------------------ for the tests

    /// <summary>The world (a test makes the dice repeat with <see cref="CfWorld.Random"/>).</summary>
    internal CfWorld? WorldOfTests => world;

    /// <summary>The handle table (a test asks whether an item still has a handle).</summary>
    internal HandleTable HandlesOfTests => handles;

    /// <summary>The skills and experience categories of the world.</summary>
    internal SkillTable? SkillsOfTests => gk?.Skills;

    // ------------------------------------------------------------------ the data of the ported code

    /// <summary>
    /// Before the archetypes are read: the world's host knows its world, and the treasure lists are given to it as text (C's
    /// <c>load_treasures</c> reads them between the two archetype passes): the world's own <c>treasures</c> file, else Crossfire's
    /// <c>lib/treasures</c> that the extension carries.
    /// </summary>
    private void PrepareStartPath(CfWorld w)
    {
        gk!.World = w;
        w.TreasureText = WorldOrOwnText("treasures");
    }

    /// <summary>After the archetypes were read: the skills and the experience categories (C's <c>init_new_exp_system</c>), with the world's own <c>skill_params</c> file or Crossfire's.</summary>
    private void InitSkills(CfWorld w) => gk!.Skills = SkillTable.Create(w, WorldOrOwnText("skill_params"));

    // the file of that name in the world's folder, else the copy of Crossfire's lib file in this program
    private string WorldOrOwnText(string name)
    {
        string path = Path.Combine(host.WorldDir, name);
        if (File.Exists(path))
        {
            return FileText.FromBytes(File.ReadAllBytes(path));
        }
        using Stream own = typeof(Dispatcher).Assembly.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        own.CopyTo(ms);
        return FileText.FromBytes(ms.ToArray());
    }

    // ------------------------------------------------------------------ the functions

    /// <summary>The function <c>mudresetplayer</c>: <see cref="ResetPlayer"/> for the player of the request's context.</summary>
    internal Outcome MudResetPlayerOfContext(IReadOnlyList<Val> args) =>
        args.Count != 0 ? Outcome.Fail(Status.BadArgs, "mudresetplayer takes no arguments") : ContextPlayerOr(ResetPlayer);

    /// <summary>The function <c>mudrollstats</c>: <see cref="RollStatsOf"/> for the player of the request's context.</summary>
    internal Outcome MudRollStatsOfContext(IReadOnlyList<Val> args) =>
        args.Count != 0 ? Outcome.Fail(Status.BadArgs, "mudrollstats takes no arguments") : ContextPlayerOr(RollStatsOf);

    private Outcome ContextPlayerOr(Func<PlayerState, Outcome> then) =>
        ctxPlayer != null && players.TryGetValue(ctxPlayer, out PlayerState? ps) ? then(ps) : Outcome.Fail(Status.BadArgs, "the request has no logged-in player in its context");

    // ------------------------------------------------------------------ the methods of TMudPlayer

    /// <summary>The methods of TMudPlayer that lane 4D declares; null for any other method id.</summary>
    private Outcome? InvokeStartMethod(PlayerState ps, long method, IReadOnlyList<Val> args)
    {
        switch (method)
        {
            case TMudPlayerMembers.MethodMudResetPlayer:
                return args.Count == 0 ? ResetPlayer(ps) : Outcome.Fail(Status.BadArgs, "mudresetplayer takes no arguments");
            case TMudPlayerMembers.MethodRollStats:
                return args.Count == 0 ? RollStatsOf(ps) : Outcome.Fail(Status.BadArgs, "rollstats takes no arguments");
            case TMudPlayerMembers.MethodSetMudClass:
                return Text1(args) is { } name ? SetMudClass(ps, name) : Outcome.Fail(Status.BadArgs, "setmudclass takes a class name");
            case TMudPlayerMembers.MethodSetMudClassNoAction:
                return Text1(args) is { } plain ? SetMudClassNoAction(ps, plain) : Outcome.Fail(Status.BadArgs, "setmudclassnoaction takes a class name");
            case TMudPlayerMembers.MethodLoadMudAccount:
                return args.Count <= 1 && (args.Count == 0 || Text1(args) != null) ? LoadMudAccount(ps, args.Count == 0 ? "" : Text1(args)!) : Outcome.Fail(Status.BadArgs, "loadmudaccount takes a suffix");
            case TMudPlayerMembers.MethodUnloadMudAccount:
                return args.Count == 0 ? UnloadMudAccount(ps) : Outcome.Fail(Status.BadArgs, "unloadmudaccount takes no arguments");
            case TMudPlayerMembers.MethodSaveMudAccount:
                return args.Count == 0 ? SaveMudAccount(ps) : Outcome.Fail(Status.BadArgs, "savemudaccount takes no arguments");
            case TMudPlayerMembers.MethodBackupMudAccount:
                return args.Count == 0 ? BackupMudAccount(ps, "") : Text1(args) is { } suffix ? BackupMudAccount(ps, suffix) : Outcome.Fail(Status.BadArgs, "backupmudaccount takes a name");
            case TMudPlayerMembers.MethodMudLoadAccount:
                return Text1(args) is { } slot ? LoadMudAccount(ps, "_" + slot) : Outcome.Fail(Status.BadArgs, "Mud_LoadAccount takes a slot");
            default:
                return null;
        }
    }

    // the one argument of a call as text (nil is the empty text); null when there is not exactly one or it is of another kind
    private static string? Text1(IReadOnlyList<Val> args) =>
        args.Count == 1 && (args[0].IsNil || args[0].Kind == ValKind.String) ? args[0].AsString : null;

    // ------------------------------------------------------------------ the player's items and strings

    // takes an item out of the player's tree and frees it: its tag and the tags of what it holds die now (docs/mud-phase3.md section
    // 4.2), and the next flush looks at the whole window and deletes the strings of what left
    private void DropItem(PlayerState ps, CfObject item)
    {
        ReleaseItemTags(item);
        KillItem(item);
        ps.Contr?.Tracked.Remove(item);
        world!.RemoveOb(item);
        world.FreeObject(item);
        AskForWholeWindow(ps);
    }

    // the whole window of the player is to be looked at at the next flush (a new tree, or a changed one): lane 4C's reconcile
    // compares every string with what the client holds, the tags are the proxies' (Dispatcher.ItemProxy)
    private static void AskForWholeWindow(PlayerState ps)
    {
        if (ps.Contr is Player contr)
        {
            contr.Reconcile = true;
            contr.Touch();
        }
    }

    /// <summary>Sets the player's client strings whose text changed (login sets all of them; this one is for a change of stats and life).</summary>
    private void RefreshClientStrings(PlayerState player)
    {
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

    // deletes every string the player has at the client (the strings of the stats and the ones of the items)
    private void DeleteClientStrings(PlayerState player)
    {
        foreach (string name in player.ClientOrder)
        {
            effects.Add(Val.Map(("k", Val.Str("client.del")), ("player", Val.Str(player.Account)), ("name", Val.Str(name))));
        }
        player.ClientStrings.Clear();
        player.ClientOrder.Clear();
        player.Contr?.Shown.Sent.Clear();
    }

    // ------------------------------------------------------------------ rollstats, mudresetplayer, setmudclass

    /// <summary><c>rollstats</c>: <c>roll_stats</c> (player.c), which ends in the hit, spell and grace points of <c>fix_player</c>, then the strings.</summary>
    private Outcome RollStatsOf(PlayerState ps)
    {
        if (!ps.Loaded || ps.Contr is not { } contr)
        {
            return Outcome.Ok(Val.Nil);
        }
        Start.RollStats(world!, ps.Ob, contr);
        ps.Dirty = true;
        RefreshClientStrings(ps);
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>
    /// <c>mudresetplayer</c>: every item is freed (its handle dies, its string is deleted at the flush), the experience, the readied
    /// skill, the known spells and the class are gone, the level is 1 and the food 999, and the stats are rolled again (docs/mud-phase3.md
    /// section 4.7). It does not fire <c>onLoadedMudAccount</c>.
    /// </summary>
    private Outcome ResetPlayer(PlayerState ps)
    {
        if (CheckBusy() is { } busy)
        {
            return busy;
        }
        if (!ps.Loaded || ps.Contr is not { } contr)
        {
            return Outcome.Ok(Val.Nil);
        }
        CfObject pl = ps.Ob;
        pl.SetFlag(Define.FLAG_NO_FIX_PLAYER); // the player is fixed once, after all is gone
        while (pl.Inv != null)
        {
            DropItem(ps, pl.Inv);
        }
        pl.ClearFlag(Define.FLAG_NO_FIX_PLAYER);
        pl.ChosenSkill = null;
        pl.ExpObj = null;
        contr.KnownSpells.Clear();
        contr.Mark = null;
        contr.MarkCount = 0;
        contr.LastUsed = null;
        contr.LastUsedId = 0;
        pl.Extra?.Remove("mudclass");
        pl.Stats.Exp = 0;
        pl.Level = 1;
        pl.Stats.Food = 999;
        Start.RollStats(world!, pl, contr);
        ps.Dirty = true;
        RefreshClientStrings(ps);
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>
    /// <c>setmudclass("&lt;x&gt;_class")</c>: what an earlier choice of a class gave goes (the class object and the items with
    /// <c>startequip</c>), the class archetype goes into the player's tree, the player gets the experience objects it lacks, the
    /// starting items of the class's <c>randomitems</c> list (<c>give_initial_items</c>) and its skills linked to their experience
    /// (<c>key_change_class</c> in player.c does these in this order for <c>d</c>), and <c>mudclass</c> becomes the name without
    /// <c>_class</c>. True when the class exists. The stat bonuses of the class (<c>add_statbonus</c>) are not called: GK's class is an object whose stats fix_player adds (docs/differences.md).
    /// </summary>
    private Outcome SetMudClass(PlayerState ps, string name)
    {
        if (CheckBusy() is { } busy)
        {
            return busy;
        }
        if (!ps.Loaded || ps.Contr == null)
        {
            return Outcome.Ok(Val.False);
        }
        string className = FileText.ToBytesText(name);
        Archetype? at = world!.FindArchetype(className);
        if (at == null)
        {
            log(1, "setmudclass: the world has no archetype " + name);
            return Outcome.Ok(Val.False);
        }
        CfObject pl = ps.Ob;

        // what an earlier choice gave goes first: the class, the items that were given and the items with the flag startequip
        Player contr = ps.Contr;
        var earlier = new List<CfObject>();
        for (CfObject? c = pl.Inv; c != null; c = c.Below)
        {
            if (c.Type == Define.CLASS || c.QueryFlag(Define.FLAG_STARTEQUIP) || contr.ClassGiven.Contains(c))
            {
                earlier.Add(c);
            }
        }
        contr.ClassGiven.Clear();
        foreach (CfObject c in earlier)
        {
            DropItem(ps, c);
        }

        var had = new HashSet<CfObject>(ReferenceEqualityComparer.Instance);
        for (CfObject? c = pl.Inv; c != null; c = c.Below)
        {
            had.Add(c);
        }
        pl.SetFlag(Define.FLAG_NO_FIX_PLAYER); // fixed once at the end
        world.InsertObInOb(world.ArchToObject(at)!, pl);
        gk!.Skills?.InitPlayerExp(world, pl);
        Start.GiveInitialItems(world, pl, at.Clone.Randomitems);
        gk.Skills?.LinkPlayerSkills(world, pl);
        pl.ClearFlag(Define.FLAG_NO_FIX_PLAYER);
        Gk.FixPlayer.Fix(world, pl);
        for (CfObject? c = pl.Inv; c != null; c = c.Below)
        {
            if (!had.Contains(c) && c.Type != Define.EXPERIENCE)
            {
                contr.ClassGiven.Add(c);
            }
        }

        string cls = className.EndsWith("_class", StringComparison.Ordinal) && className.Length > 6 ? className.Substring(0, className.Length - 6) : className;
        (pl.Extra ??= new ExtraKeys()).Set("mudclass", cls);
        AskForWholeWindow(ps);
        ps.Dirty = true;
        RefreshClientStrings(ps);
        return Outcome.Ok(Val.True);
    }

    /// <summary><c>setmudclassnoaction</c>: sets <c>mudclass</c> only (the empty text clears it).</summary>
    private Outcome SetMudClassNoAction(PlayerState ps, string name)
    {
        if (FieldBuilder<PlayerState>.HasLineBreak(name))
        {
            return Outcome.Fail(Status.BadArgs, FieldBuilder<PlayerState>.LineBreakRefused);
        }
        string bytes = FileText.ToBytesText(name);
        if (bytes.Length == 0)
        {
            ps.Ob.Extra?.Remove("mudclass");
        }
        else
        {
            (ps.Ob.Extra ??= new ExtraKeys()).Set("mudclass", bytes);
        }
        ps.Dirty = true;
        return Outcome.Ok(Val.Nil);
    }

    // ------------------------------------------------------------------ the account's file

    /// <summary>
    /// <c>loadmudaccount(suffix)</c>: with the empty suffix it does nothing while the account is loaded (login loaded it before the
    /// Control NPC's call); after <c>unloadmudaccount</c> it reads the file again. A suffix reads the file of <c>&lt;account&gt;&lt;suffix&gt;</c>
    /// (which is what the account is saved to after that) and fails, with the player as it was, when there is none. A load replaces the
    /// player's tree: the old items die with their handles and strings, the strings of the stats are sent again, and
    /// <c>onLoadedMudAccount</c> fires. True when the account is loaded.
    /// </summary>
    private Outcome LoadMudAccount(PlayerState ps, string suffix)
    {
        if (CheckBusy() is { } busy)
        {
            return busy;
        }
        if (suffix.Length == 0 && ps.Loaded)
        {
            return Outcome.Ok(Val.True);
        }
        bool slot = suffix.Length != 0;
        string key = ps.Account + FileText.ToBytesText(suffix);
        if (HasControlCharacter(suffix))
        {
            return Outcome.Fail(Status.BadArgs, "the suffix holds a control character");
        }
        PlayerState? fresh = OpenPlayer(ps.Account, key, create: !slot);
        if (fresh == null)
        {
            log(1, "loadmudaccount: " + (slot ? "there is no file " + key : "the file of account " + key + " cannot be read"));
            return Outcome.Ok(Val.False);
        }
        bool fromFile = !fresh.Dirty; // a player made new (no file) is Dirty
        ReplacePlayerObject(ps, fresh.Ob, key, loaded: true);
        FixLoadedAccount(ps, fromFile);
        SendClientStrings(ps);
        SendLoginHooks(ps);
        events.Add(Val.Map(
            ("name", Val.Str(Manifest.EventOnLoadedMudAccount)),
            ("target", Val.Str("player")),
            ("player", Val.Str(ps.Account)),
            ("args", Val.List(Val.Handle(handles.HandleOf(ps))))));
        return Outcome.Ok(Val.True);
    }

    private static bool HasControlCharacter(string text) => text.Any(c => c < 0x20 || c == 0x7f);

    /// <summary>
    /// <c>unloadmudaccount</c>: the memory state of the account goes without being saved (the file stays as it is, and is not written
    /// until the account is loaded again): the tree is freed, the strings are deleted, and the player is a blank one.
    /// </summary>
    private Outcome UnloadMudAccount(PlayerState ps)
    {
        if (CheckBusy() is { } busy)
        {
            return busy;
        }
        if (!ps.Loaded)
        {
            return Outcome.Ok(Val.Nil);
        }
        Archetype? at = world!.GetPlayerArchetype(null);
        if (at == null)
        {
            return Outcome.Fail(Status.Internal, "the world has no player archetype");
        }
        CfObject blank = world.ArchToObject(at)!;
        blank.Name = FileText.ToBytesText(ps.Account);
        blank.NamePl = blank.Name;
        ReplacePlayerObject(ps, blank, ps.FileKey, loaded: false);
        return Outcome.Ok(Val.Nil);
    }

    // puts another object in place of the player's: the old tree dies with its handles and strings, the new one is sent at the next flush
    private void ReplacePlayerObject(PlayerState ps, CfObject fresh, string key, bool loaded)
    {
        DeleteClientStrings(ps);
        CfObject old = ps.Ob;
        DetachController(ps);
        FreeCfObject(old); // the tags of its items die with it
        ps.Ob = fresh;
        ps.FileKey = key;
        ps.Loaded = loaded;
        ps.Dirty = false;
        AttachController(ps);
        if (ps.Contr is { } contr)
        {
            contr.NeedsResend = true;
            AskForWholeWindow(ps);
            contr.Touch();
        }
    }

    /// <summary><c>savemudaccount</c>: the file is written now, not after the quiet time of the writer.</summary>
    private Outcome SaveMudAccount(PlayerState ps)
    {
        if (ps.Loaded)
        {
            SubmitAccount(ps);
            writer!.Kick();
        }
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>
    /// <c>backupmudaccount(name)</c>: the account as it is now is written to <c>backups/&lt;file&gt;&lt;name&gt;.&lt;UTC time&gt;.txt</c> of the
    /// data folder (and the account's own file is brought up to date). The name is checked like an account's (docs/mud-phase3.md section
    /// 4.7): it is escaped like the account's file name, so it cannot name a place outside the folder, it may not hold a control
    /// character, and the whole file name must be one the account's files may have; otherwise the call fails and nothing is written.
    /// </summary>
    private Outcome BackupMudAccount(PlayerState ps, string name)
    {
        if (HasControlCharacter(name))
        {
            return Outcome.Fail(Status.BadArgs, "the name holds a control character");
        }
        if (!ps.Loaded)
        {
            return Outcome.Ok(Val.Nil);
        }
        string key = ps.FileKey + FileText.ToBytesText(name);
        if (FileNames.AccountFile(ps.FileKey) == null)
        {
            return Outcome.Ok(Val.Nil);
        }
        if (FileNames.AccountFile(key) == null)
        {
            return Outcome.Fail(Status.BadArgs, "the name is too long for a file name");
        }
        SubmitAccount(ps);
        string stamp = options.Time.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        PutFile(FileNames.BackupFolder + "/" + FileNames.Escape(key) + "." + stamp + FileNames.AccountSuffix, AccountFileBytes(ps));
        writer!.Kick();
        return Outcome.Ok(Val.Nil);
    }
}
