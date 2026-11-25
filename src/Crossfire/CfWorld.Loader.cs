// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/loader.l, include/loader.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/loader.l:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/loader.h:
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
using System.Text;

namespace Crossfire;

/// <summary>
/// The text a loader reads: what the C <c>FILE *</c> and the flex buffer hold (include/loader.h <c>load_object(fp, ...)</c>). It is
/// the text of a file with one char per byte (<see cref="CStr.Latin1"/>), read forward.
/// </summary>
public sealed class LoadSource
{
    /// <summary>The whole text.</summary>
    public string Text { get; }

    /// <summary>The position of the next character to read.</summary>
    public int Pos { get; set; }

    /// <summary>Creates a source over a text.</summary>
    public LoadSource(string text) => Text = text;

    /// <summary>True when everything has been read.</summary>
    public bool AtEnd => Pos >= Text.Length;
}

public sealed partial class CfWorld
{
    // The flex scanner's start condition (%x MESSAGE) and msgbuf are globals of loader.l.
    private bool lexInMessage;
    private readonly StringBuilder msgbuf = new();

    // loader.l lex_error: -1 after a line the scanner has no rule for; set to 0 when lex_load starts, returned at the end of the text.
    private int lexError;

    /// <summary>
    /// Not in C: how many lines of each unknown key (lower case) the loader met, the lines it keeps on the object in
    /// <see cref="CfObject.Extra"/>. Comments (a # first) and blank lines are not counted.
    /// </summary>
    public Dictionary<string, int> UnknownKeys { get; } = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, int> EventNames = new(StringComparer.Ordinal)
    {
        ["apply"] = Define.EVENT_APPLY,
        ["attack"] = Define.EVENT_ATTACK,
        ["born"] = Define.EVENT_BORN,
        ["crash"] = Define.EVENT_CRASH,
        ["death"] = Define.EVENT_DEATH,
        ["drop"] = Define.EVENT_DROP,
        ["login"] = Define.EVENT_LOGIN,
        ["logout"] = Define.EVENT_LOGOUT,
        ["pickup"] = Define.EVENT_PICKUP,
        ["remove"] = Define.EVENT_REMOVE,
        ["say"] = Define.EVENT_SAY,
        ["shout"] = Define.EVENT_SHOUT,
        ["tell"] = Define.EVENT_TELL,
        ["time"] = Define.EVENT_TIME,
        ["throw"] = Define.EVENT_THROW,
        ["trigger"] = Define.EVENT_TRIGGER,
        ["close"] = Define.EVENT_CLOSE,
        ["timer"] = Define.EVENT_TIMER,
    };

    // The name of each event in the order of get_ob_diff's switch (an index of event_hook[] that is not one of these has no name).
    private static readonly string?[] EventKeyword = BuildEventKeywords();

    private static string?[] BuildEventKeywords()
    {
        var names = new string?[Define.NR_EVENTS + 1];
        foreach (KeyValuePair<string, int> kv in EventNames)
        {
            names[kv.Value] = kv.Key;
        }
        return names;
    }

    /// <summary>
    /// loader.l <c>yval()</c>: the value of a line. It skips the key up to the first space (not a tab), then the spaces, and cuts the
    /// spaces at the end of the line; "" when there is none. <paramref name="yytext"/> is the whole line, key included.
    /// </summary>
    public static string Yval(string yytext)
    {
        int cp = 0;
        // First skip over start of line, like animation or name
        while (cp < yytext.Length && yytext[cp] != ' ')
        {
            cp++;
        }
        if (cp >= yytext.Length)
        {
            return "";
        }
        // Skip over whitespace
        while (cp < yytext.Length && yytext[cp] == ' ')
        {
            cp++;
        }
        // Got last character before null and strip off trailing whitespace
        int end = yytext.Length;
        while (end > cp && yytext[end - 1] == ' ')
        {
            end--;
        }
        return yytext.Substring(cp, end - cp);
    }

    private static bool IsBlank(char c) => c == ' ' || c == '\t';

    /// <summary>
    /// loader.l <c>load_object</c>: loads an object from the text. Variables are read and patched into the object until the line
    /// <c>end</c> or the end of the text. <paramref name="bufstate"/> is one of LO_REPEAT (the same buffer as the last call),
    /// LO_LINEMODE (the text is multi-purpose: the lines are scanned one at a time), LO_NEWFILE (the first read of a text) and LO_NOREAD
    /// (reset the buffers, do not read). Returns LL_NORMAL or LL_MORE for a finished block, LL_EOF (0) at the end of the text,
    /// LL_IGNORED (-1) for an Object line without a name.
    /// </summary>
    public int LoadObject(LoadSource fp, CfObject? op, int bufstate, int mapFlags)
    {
        int retval;

        msgbuf.Clear();
        if (bufstate == Define.LO_NEWFILE || bufstate == Define.LO_NOREAD)
        {
            // The C code switches the scanner to a new buffer on the file; the start condition of the scanner stays as it is.
            if (bufstate == Define.LO_NOREAD)
            {
                return Define.LL_NORMAL;
            }
        }
        if (bufstate == Define.LO_LINEMODE)
        {
            // while (fgets(inbuf, MAX_BUF-3, fp)): each line is scanned as a string of its own.
            while (!fp.AtEnd)
            {
                int nl = fp.Text.IndexOf('\n', fp.Pos);
                int end = nl >= 0 ? nl + 1 : fp.Text.Length;
                if (end - fp.Pos > Define.MAX_BUF - 3 - 1)
                {
                    end = fp.Pos + Define.MAX_BUF - 3 - 1;
                }
                var line = new LoadSource(fp.Text.Substring(fp.Pos, end - fp.Pos));
                fp.Pos = end;
                retval = LexLoad(op!, mapFlags, line);
                if (retval == Define.LL_NORMAL)
                {
                    return retval;
                }
            }
            Log(LogLevel.Debug, "Got eof while scanning strings\n");
            return Define.LL_EOF;
        }

        retval = LexLoad(op!, mapFlags, fp);
        if (op!.CurrentWeaponScript != null)
        {
            op.CurrentWeapon = Host.FindBestWeaponUsedMatch(op, op.CurrentWeaponScript);
            Log(LogLevel.Debug, "CurrentWeapon Loaded !\n");
        }

        return retval;
    }

    /// <summary>
    /// loader.l <c>set_variable</c>: scans the buffer for variables and sets them in the object (the lines of a block without the
    /// Object line and the end). Used by the editor and the wizard commands to override and mutate values.
    /// </summary>
    public int SetVariable(CfObject op, string buf)
    {
        msgbuf.Clear();
        bool saved = lexInMessage;
        lexInMessage = false; // yy_push_state(INITIAL)
        int retval = LexLoad(op, 0, new LoadSource(buf));
        lexInMessage = saved; // yy_pop_state()
        return retval;
    }

    /// <summary>Not in C: remembers a line the C scanner has no rule for, on the object (docs/differences.md).</summary>
    private void RecordUnknown(CfObject op, string line)
    {
        int s = 0;
        while (s < line.Length && (IsBlank(line[s]) || line[s] == '\r'))
        {
            s++;
        }
        if (s >= line.Length || line[s] == '#')
        {
            return;
        }
        int e = s;
        while (e < line.Length && !IsBlank(line[e]))
        {
            e++;
        }
        string key = line.Substring(s, e - s);
        int v = e;
        while (v < line.Length && IsBlank(line[v]))
        {
            v++;
        }
        int ve = line.Length;
        while (ve > v && IsBlank(line[ve - 1]))
        {
            ve--;
        }
        op.Extra ??= new ExtraKeys();
        op.Extra.Add(key, line.Substring(v, ve - v));
        string lk = key.ToLowerInvariant();
        UnknownKeys[lk] = UnknownKeys.GetValueOrDefault(lk) + 1;
    }

    /// <summary>
    /// loader.l <c>lex_load</c> (the flex scanner and its rules): reads lines of the source and applies each to the object until a
    /// line <c>end</c> (LL_NORMAL, or LL_MORE after a line <c>More</c>), the end of the text (LL_EOF) or an Object line without a name
    /// (LL_IGNORED). The keys are matched without regard to case (flex -i), and a rule needs white space and then at least one
    /// more character after the key; <c>msg</c>, <c>More</c> and <c>end</c> must stand alone on a line that ends with a newline.
    /// </summary>
    private int LexLoad(CfObject op, int mapFlags, LoadSource sc)
    {
        bool ismore = false;
        lexError = 0;

        while (true)
        {
            if (sc.Pos >= sc.Text.Length)
            {
                // <<EOF>>: if we got an error, return the error; otherwise, return that we got EOF.
                return lexError != 0 ? lexError : Define.LL_EOF;
            }
            int nl = sc.Text.IndexOf('\n', sc.Pos);
            bool hasNewline = nl >= 0;
            string line = hasNewline ? sc.Text.Substring(sc.Pos, nl - sc.Pos) : sc.Text.Substring(sc.Pos);
            sc.Pos = hasNewline ? nl + 1 : sc.Text.Length;

            if (lexInMessage)
            {
                // <MESSAGE>^endmsg{WS}$ ends it, <MESSAGE>.* is a line of the text
                if (hasNewline && IsKeyWithBlanks(line, "endmsg"))
                {
                    lexInMessage = false;
                    op.Msg = msgbuf.ToString();
                    // Just print a warning so we can be reasonably safe about not overflowing the buffer.
                    if (op.Msg.Length > Define.HUGE_BUF / 2)
                    {
                        string shown = op.Msg.Length > 80 ? op.Msg.Substring(0, 80) : op.Msg;
                        Log(LogLevel.Debug, "\n\tWarning message length > " + Define.HUGE_BUF / 2 + " (max allowed=" + Define.HUGE_BUF + "): " + op.Msg.Length + "\n>" + shown + "<\n");
                    }
                }
                else if (hasNewline && line.Length == 0)
                {
                    // An empty line: <MESSAGE>.* matches nothing there and the rule for a newline wins, so it is not part of the
                    // text. A line of blanks is (.* matches it; the ^{WS}$ of the ignore rule is literal text inside its parentheses).
                }
                else
                {
                    msgbuf.Append(line).Append('\n');
                }
                continue;
            }

            int ws = 0;
            while (ws < line.Length && !IsBlank(line[ws]))
            {
                ws++;
            }
            string key = line.Substring(0, ws).ToLowerInvariant();

            // The rules that must stand alone: ^msg{WS}$  ^more{WS}$  ^end{WS}$
            if (hasNewline && (key == "msg" || key == "more" || key == "end") && IsBlankRest(line, ws))
            {
                if (key == "msg")
                {
                    lexInMessage = true;
                    msgbuf.Clear();
                }
                else if (key == "more")
                {
                    // We need to record that this is a multipart object, so the calling function can glue things back together
                    ismore = true;
                }
                else
                {
                    // Legacy cases of name_pl: if the object doesn't have a name_pl, the object name is used; and when the object is
                    // not using the normal name of the archetype the loaded name is used too (otherwise the plural would lose
                    // information: just "hearts" and not "goblins heart").
                    if (op.Arch != null && op.Name != op.Arch.Clone.Name && op.NamePl == op.Arch.Clone.NamePl)
                    {
                        op.NamePl = null;
                    }
                    if (op.NamePl == null)
                    {
                        op.NamePl = op.Name;
                    }

                    return ismore ? Define.LL_MORE : Define.LL_NORMAL;
                }
                continue;
            }

            if (hasNewline && key.StartsWith("inventory", StringComparison.Ordinal))
            {
                Log(LogLevel.Error, "Got depreciated Inventory command?\n");
                continue;
            }

            // ^key{S}: the key, a blank, and at least one more character
            string rest = line.Substring(ws);
            if (rest.Length >= 2 && IsBlank(rest[0]) && ApplyKeyRule(key, line, op, mapFlags, sc, ref ismore, out int ret))
            {
                if (ret != 0x7fffffff)
                {
                    return ret;
                }
                continue;
            }

            NoRule(op, line, hasNewline);
        }
    }

    /// <summary>
    /// The last rules of loader.l, for a line no rule above matched: <c>&lt;*&gt;(^{WS}$)|\n</c> ignores an empty line (inside the
    /// parentheses flex reads <c>^</c> and <c>$</c> as plain characters, so the first half matches the text "^", blanks, "$"),
    /// <c>#.*\n</c> a comment, and <c>.*</c> logs <c>yyerror("Unrecognized string")</c> and sets <c>lex_error</c> for any other line,
    /// a line of blanks included. Not in C: a line with a key the host reads (<see cref="CrossfireHost.IsLoaderKey"/>) is no error,
    /// and every unrecognized line that starts with a key is kept on the object (docs/differences.md).
    /// </summary>
    private void NoRule(CfObject op, string line, bool hasNewline)
    {
        if (line.Length == 0 || (hasNewline && line[0] == '#') || IsLiteralCaretBlanksDollar(line))
        {
            return;
        }
        int e = 0;
        while (e < line.Length && !IsBlank(line[e]))
        {
            e++;
        }
        if (e == 0 || !Host.IsLoaderKey(line.Substring(0, e)))
        {
            // yyerror: LOG(llevError, "%s: %s\n", s, yytext)
            Log(LogLevel.Error, "Unrecognized string: " + line + "\n");
            lexError = -1;
        }
        if (e > 0)
        {
            RecordUnknown(op, line);
        }
    }

    private static bool IsLiteralCaretBlanksDollar(string line) =>
        line.Length >= 2 && line[0] == '^' && line[^1] == '$' && IsBlankRest(line.Substring(0, line.Length - 1), 1);

    private static bool IsBlankRest(string line, int from)
    {
        for (int i = from; i < line.Length; i++)
        {
            if (!IsBlank(line[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsKeyWithBlanks(string line, string key) =>
        line.StartsWith(key, StringComparison.OrdinalIgnoreCase) && IsBlankRest(line, key.Length);

    private void SetProtection(CfObject op, uint mask, ushort proVal)
    {
        if (mask == 0)
        {
            return; // Unlikely, but might as well check
        }
        for (int i = 0; i < Define.NROFATTACKS; i++)
        {
            if ((mask & (1U << i)) != 0)
            {
                op.Resist[i] = unchecked((short)proVal);
            }
        }
    }

    /// <summary>
    /// The rules section of loader.l for a line that has the shape <c>^key{S}</c>. Returns false when there is no rule for the key.
    /// <paramref name="result"/> is the value the scanner returns, or int.MaxValue when the scanner goes on with the next line.
    /// </summary>
    private bool ApplyKeyRule(string key, string yytext, CfObject op, int mapFlags, LoadSource sc, ref bool ismore, out int result)
    {
        result = 0x7fffffff;

        // event_<name>, event_<name>_plugin, event_<name>_options
        if (key.StartsWith("event_", StringComparison.Ordinal))
        {
            string name = key.Substring(6);
            string kind = "val";
            if (name.EndsWith("_plugin", StringComparison.Ordinal))
            {
                kind = "plugin";
                name = name.Substring(0, name.Length - 7);
            }
            else if (name.EndsWith("_options", StringComparison.Ordinal))
            {
                kind = "options";
                name = name.Substring(0, name.Length - 8);
            }
            if (EventNames.TryGetValue(name, out int ev))
            {
                string yv = Yval(yytext);
                if (yv.Length == 0)
                {
                    Log(LogLevel.Error, "Event (" + name + ") without " + kind + "\n");
                }
                else if (kind == "val")
                {
                    op.EventHook[ev] = yv;
                }
                else if (kind == "plugin")
                {
                    op.EventPlugin[ev] = yv;
                }
                else
                {
                    op.EventOptions[ev] = yv;
                }
                return true;
            }
            return false;
        }

        string v = Yval(yytext);
        int Ival() => CStr.Atoi(v);
        double Fval() => CStr.Atof(v);

        switch (key)
        {
            case "object":
                if (v.Length == 0)
                {
                    Log(LogLevel.Error, "Object lacks name.\n");
                    result = Define.LL_IGNORED;
                    return true;
                }
                if (!ArchInit)
                {
                    Log(LogLevel.Error, "Got object info when not in arch_init (" + v + ")?\n");
                }
                else
                {
                    if (op.Arch != null)
                    {
                        op.Arch.Name = v;
                    }
                    op.Name = v;
                }
                return true;

            case "name":
                if (v.Length == 0)
                {
                    Log(LogLevel.Error, "Name without val\n");
                }
                else
                {
                    op.Name = v;
                }
                return true;
            case "name_pl":
                if (v.Length == 0)
                {
                    Log(LogLevel.Error, "Name without val\n");
                }
                else
                {
                    op.NamePl = v;
                }
                return true;
            case "race":
                op.Race = v; // FREE_AND_COPY: an empty value is an empty string
                return true;
            case "slaying":
                op.Slaying = v;
                return true;

            case "arch":
                // If op->arch has been set, then this new object must be part of the inventory. So process appropriately.
                if (op.Arch != null)
                {
                    CfObject tmp = GetObject();
                    tmp.Arch = FindArchetype(v);
                    if (tmp.Arch != null)
                    {
                        CopyObject(tmp.Arch.Clone, tmp);
                    }
                    msgbuf.Clear();
                    LexLoad(tmp, mapFlags, sc);
                    InsertObInOb(tmp, op);
                }
                else
                {
                    // This is the actual archetype definition then
                    op.Arch = FindArchetype(v);
                    if (op.Arch != null)
                    {
                        CopyObject(op.Arch.Clone, op);
                    }
                }
                return true;

            case "other_arch":
                op.OtherArch = FindArchetype(v);
                return true;
            case "animation":
                if (v == "NONE")
                {
                    op.AnimationId = 0;
                    op.ClearFlag(Define.FLAG_ANIMATE);
                }
                else
                {
                    op.AnimationId = unchecked((ushort)Animations.FindAnimation(v));
                    op.SetFlag(Define.FLAG_ANIMATE);
                }
                return true;

            case "last_heal":
                op.LastHeal = Ival();
                return true;
            case "last_sp":
                op.LastSp = Ival();
                return true;
            case "last_grace":
                op.LastGrace = unchecked((short)Ival());
                return true;
            case "last_eat":
                op.LastEat = unchecked((short)Ival());
                return true;
            case "speed":
                op.Speed = (float)Fval();
                if (op.Speed < 0)
                {
                    op.SpeedLeft = (float)(op.SpeedLeft - Random() % 100 / 100.0);
                }
                if ((mapFlags & Define.MAP_STYLE) == 0)
                {
                    UpdateObSpeed(op);
                }
                if ((mapFlags & Define.MAP_STYLE) != 0 && Math.Abs((double)op.Speed) <= Define.MIN_ACTIVE_SPEED)
                {
                    UpdateObSpeed(op);
                }
                return true;
            case "speed_left":
                op.SpeedLeft = (float)Fval();
                return true;
            case "slow_move":
                // SET_SLOW_PENALTY(op, FVAL): stats.exp = fl * 1000
                op.Stats.Exp = CStr.ToInt(Fval() * 1000);
                op.SetFlag(Define.FLAG_SLOW_MOVE);
                return true;
            case "face":
                op.Face = Faces[Faces.FindFace(v, 0)];
                return true;
            case "str":
                op.Stats.Str = unchecked((sbyte)Ival());
                return true;
            case "dex":
                op.Stats.Dex = unchecked((sbyte)Ival());
                return true;
            case "con":
                op.Stats.Con = unchecked((sbyte)Ival());
                return true;
            case "wis":
                op.Stats.Wis = unchecked((sbyte)Ival());
                return true;
            case "cha":
                op.Stats.Cha = unchecked((sbyte)Ival());
                return true;
            case "int":
                op.Stats.Int = unchecked((sbyte)Ival());
                return true;
            case "pow":
                op.Stats.Pow = unchecked((sbyte)Ival());
                return true;
            case "hp":
                op.Stats.Hp = unchecked((short)Ival());
                return true;
            case "maxhp":
                op.Stats.Maxhp = unchecked((short)Ival());
                return true;
            case "sp":
                op.Stats.Sp = unchecked((short)Ival());
                return true;
            case "maxsp":
                op.Stats.Maxsp = unchecked((short)Ival());
                return true;
            case "grace":
                op.Stats.Grace = unchecked((short)Ival());
                return true;
            case "maxgrace":
                op.Stats.Maxgrace = unchecked((short)Ival());
                return true;
            case "exp":
                op.Stats.Exp = CStr.Atol(v);
                return true;
            case "food":
                op.Stats.Food = unchecked((short)Ival());
                return true;
            case "dam":
                op.Stats.Dam = unchecked((short)Ival());
                return true;
            case "wc":
                op.Stats.Wc = unchecked((sbyte)Ival());
                return true;
            case "ac":
                op.Stats.Ac = unchecked((sbyte)Ival());
                return true;
            case "x":
                op.X = unchecked((short)Ival());
                op.Ox = op.X;
                return true;
            case "y":
                op.Y = unchecked((short)Ival());
                op.Oy = op.Y;
                return true;
            case "nrof":
                op.Nrof = unchecked((uint)CStr.Atol(v));
                return true;
            case "level":
                op.Level = unchecked((short)Ival());
                return true;
            case "direction":
                op.Direction = unchecked((sbyte)Ival());
                return true;
            case "type":
                op.Type = unchecked((byte)Ival());
                return true;
            case "material":
                op.Material = unchecked((ushort)Ival());
                return true;
            case "value":
                op.Value = Ival();
                return true;
            case "weight":
                op.Weight = CStr.Atol(v);
                return true;
            case "carrying":
                op.Carrying = CStr.Atol(v);
                return true;
            case "attacktype":
                op.Attacktype = unchecked((uint)Ival());
                return true;
            case "path_attuned":
                op.PathAttuned = unchecked((uint)Ival());
                return true;
            case "path_repelled":
                op.PathRepelled = unchecked((uint)Ival());
                return true;
            case "path_denied":
                op.PathDenied = unchecked((uint)Ival());
                return true;
            case "invisible":
                op.Invisible = unchecked((short)Ival());
                return true;
            case "magic":
                op.Magic = unchecked((sbyte)Ival());
                return true;
            case "state":
                op.State = unchecked((byte)Ival());
                return true;
            case "alive":
                op.SetOrClearFlag(Define.FLAG_ALIVE, Ival());
                return true;
            case "applied":
                op.SetOrClearFlag(Define.FLAG_APPLIED, Ival());
                return true;
            case "unpaid":
                op.SetOrClearFlag(Define.FLAG_UNPAID, Ival());
                return true;
            case "need_an":
            case "need_ie":
                return true; // not used - just ignore
            case "no_pick":
                op.SetOrClearFlag(Define.FLAG_NO_PICK, Ival());
                return true;
            case "no_pass":
                op.SetOrClearFlag(Define.FLAG_NO_PASS, Ival());
                return true;
            case "walk_on":
                op.SetOrClearFlag(Define.FLAG_WALK_ON, Ival());
                return true;
            case "walk_off":
                op.SetOrClearFlag(Define.FLAG_WALK_OFF, Ival());
                return true;
            case "fly_on":
                op.SetOrClearFlag(Define.FLAG_FLY_ON, Ival());
                return true;
            case "fly_off":
                op.SetOrClearFlag(Define.FLAG_FLY_OFF, Ival());
                return true;
            case "is_animated":
                op.SetOrClearFlag(Define.FLAG_ANIMATE, Ival());
                return true;
            case "flying":
                op.SetOrClearFlag(Define.FLAG_FLYING, Ival());
                return true;
            case "monster":
                op.SetOrClearFlag(Define.FLAG_MONSTER, Ival());
                return true;
            case "neutral":
                op.SetOrClearFlag(Define.FLAG_NEUTRAL, Ival());
                return true;
            case "no_attack":
                op.SetOrClearFlag(Define.FLAG_NO_ATTACK, Ival());
                return true;
            case "no_damage":
                op.SetOrClearFlag(Define.FLAG_NO_DAMAGE, Ival());
                return true;
            case "friendly":
                if (Ival() != 0)
                {
                    op.SetFlag(Define.FLAG_FRIENDLY);
                    if (op.Type != Define.PLAYER)
                    {
                        Log(LogLevel.Debug, " Adding friendly object " + op.Name + ".\n");
                        AddFriendlyObject(op);
                    }
                }
                else
                {
                    op.ClearFlag(Define.FLAG_FRIENDLY);
                }
                return true;
            case "generator":
                op.SetOrClearFlag(Define.FLAG_GENERATOR, Ival());
                return true;
            case "is_thrown":
                op.SetOrClearFlag(Define.FLAG_IS_THROWN, Ival());
                return true;
            case "auto_apply":
                op.SetOrClearFlag(Define.FLAG_AUTO_APPLY, Ival());
                return true;
            case "treasure":
                op.SetOrClearFlag(Define.FLAG_TREASURE, Ival());
                return true;
            case "apply_once":
                op.SetOrClearFlag(Define.FLAG_APPLY_ONCE, Ival());
                return true;
            case "see_invisible":
                op.SetOrClearFlag(Define.FLAG_SEE_INVISIBLE, Ival());
                return true;
            case "can_roll":
                op.SetOrClearFlag(Define.FLAG_CAN_ROLL, Ival());
                return true;
            case "is_turnable":
                op.SetOrClearFlag(Define.FLAG_IS_TURNABLE, Ival());
                return true;
            case "is_used_up":
                op.SetOrClearFlag(Define.FLAG_IS_USED_UP, Ival());
                return true;
            case "identified":
                if (Ival() != 0)
                {
                    op.SetFlag(Define.FLAG_IDENTIFIED);
                    op.ClearFlag(Define.FLAG_KNOWN_MAGICAL);
                }
                else
                {
                    op.ClearFlag(Define.FLAG_IDENTIFIED);
                }
                return true;
            case "reflecting":
                op.SetOrClearFlag(Define.FLAG_REFLECTING, Ival());
                return true;
            case "changing":
                op.SetOrClearFlag(Define.FLAG_CHANGING, Ival());
                return true;
            case "splitting":
                op.SetOrClearFlag(Define.FLAG_SPLITTING, Ival());
                return true;
            case "hitback":
                op.SetOrClearFlag(Define.FLAG_HITBACK, Ival());
                return true;
            case "startequip":
                op.SetOrClearFlag(Define.FLAG_STARTEQUIP, Ival());
                return true;
            case "blocksview":
                op.SetOrClearFlag(Define.FLAG_BLOCKSVIEW, Ival());
                return true;
            case "editable":
                if (op.Arch != null)
                {
                    op.Arch.Editable = unchecked((uint)Ival());
                }
                else
                {
                    Log(LogLevel.Error, "editable on an object without an archetype\n");
                }
                return true;
            case "undead":
                op.SetOrClearFlag(Define.FLAG_UNDEAD, Ival());
                return true;
            case "scared":
                op.SetOrClearFlag(Define.FLAG_SCARED, Ival());
                return true;
            case "unaggressive":
                op.SetOrClearFlag(Define.FLAG_UNAGGRESSIVE, Ival());
                return true;
            case "reflect_missile":
                op.SetOrClearFlag(Define.FLAG_REFL_MISSILE, Ival());
                return true;
            case "reflect_spell":
                op.SetOrClearFlag(Define.FLAG_REFL_SPELL, Ival());
                return true;
            case "no_magic":
                op.SetOrClearFlag(Define.FLAG_NO_MAGIC, Ival());
                return true;
            case "wiz":
                if (Ival() != 0)
                {
                    op.SetFlag(Define.FLAG_WIZ);
                    op.SetFlag(Define.FLAG_WAS_WIZ);
                    op.SetFlag(Define.FLAG_WIZPASS);
                }
                else
                {
                    op.ClearFlag(Define.FLAG_WIZ);
                    op.ClearFlag(Define.FLAG_WIZPASS);
                }
                return true;
            case "was_wiz":
                op.SetOrClearFlag(Define.FLAG_WAS_WIZ, Ival());
                return true;
            case "no_fix_player":
                op.SetOrClearFlag(Define.FLAG_NO_FIX_PLAYER, Ival());
                return true;
            case "tear_down":
                op.SetOrClearFlag(Define.FLAG_TEAR_DOWN, Ival());
                return true;
            case "luck":
                op.Stats.Luck = unchecked((sbyte)Ival());
                return true;
            case "run_away":
                op.RunAway = unchecked((ushort)Ival());
                return true;
            case "pass_thru":
                op.SetOrClearFlag(Define.FLAG_PASS_THRU, Ival());
                return true;
            case "can_pass_thru":
                op.SetOrClearFlag(Define.FLAG_CAN_PASS_THRU, Ival());
                return true;
            case "pick_up":
                op.PickUp = unchecked((byte)Ival());
                return true;
            case "anim_speed":
                op.AnimSpeed = unchecked((byte)Ival());
                return true;
            case "container":
                op.WeightLimit = Ival();
                return true;
            case "no_drop":
                op.SetOrClearFlag(Define.FLAG_NO_DROP, Ival());
                return true;
            case "no_pretext":
                op.SetOrClearFlag(Define.FLAG_NO_PRETEXT, Ival());
                return true;
            case "will_apply":
                op.WillApply = unchecked((byte)Ival());
                return true;
            case "random_movement":
            case "random_move":
                op.SetOrClearFlag(Define.FLAG_RANDOM_MOVE, Ival());
                return true;
            case "can_apply":
                op.CanApply = unchecked((byte)Ival());
                return true;
            case "can_cast_spell":
                op.SetOrClearFlag(Define.FLAG_CAST_SPELL, Ival());
                return true;
            case "can_use_scroll":
                op.SetOrClearFlag(Define.FLAG_USE_SCROLL, Ival());
                return true;
            case "can_use_wand":
                op.SetOrClearFlag(Define.FLAG_USE_WAND, Ival());
                return true;
            case "can_use_bow":
                op.SetOrClearFlag(Define.FLAG_USE_BOW, Ival());
                return true;
            case "can_use_armour":
                op.SetOrClearFlag(Define.FLAG_USE_ARMOUR, Ival());
                return true;
            case "can_use_weapon":
                op.SetOrClearFlag(Define.FLAG_USE_WEAPON, Ival());
                return true;
            case "can_use_ring":
                op.SetOrClearFlag(Define.FLAG_USE_RING, Ival());
                return true;
            case "has_ready_wand":
                op.SetOrClearFlag(Define.FLAG_READY_WAND, Ival());
                return true;
            case "has_ready_bow":
                op.SetOrClearFlag(Define.FLAG_READY_BOW, Ival());
                return true;
            case "xrays":
                op.SetOrClearFlag(Define.FLAG_XRAYS, Ival());
                return true;
            case "is_floor":
                op.SetOrClearFlag(Define.FLAG_IS_FLOOR, Ival());
                return true;
            case "lifesave":
                op.SetOrClearFlag(Define.FLAG_LIFESAVE, Ival());
                return true;
            case "no_strength":
                op.SetOrClearFlag(Define.FLAG_NO_STRENGTH, Ival());
                return true;
            case "sleep":
                op.SetOrClearFlag(Define.FLAG_SLEEP, Ival());
                return true;
            case "stand_still":
                op.SetOrClearFlag(Define.FLAG_STAND_STILL, Ival());
                return true;
            case "only_attack":
                op.SetOrClearFlag(Define.FLAG_ONLY_ATTACK, Ival());
                return true;

            // armour is loaded for compatibility reasons
            case "armour":
                op.Resist[Define.ATNR_PHYSICAL] = unchecked((short)Ival());
                return true;
            case "immune":
                SetProtection(op, unchecked((uint)Ival()), unchecked((ushort)Define.RESIST_IMMUNE));
                return true;
            case "protected":
                SetProtection(op, unchecked((uint)Ival()), unchecked((ushort)Define.RESIST_PROT));
                return true;
            case "vulnerable":
                SetProtection(op, unchecked((uint)Ival()), unchecked((ushort)Define.RESIST_VULN));
                return true;

            case "attack_movement":
                op.MoveType = unchecked((ushort)Ival());
                return true;
            case "move_state":
                op.MoveStatus = Ival();
                return true;
            case "confused":
                op.SetOrClearFlag(Define.FLAG_CONFUSED, Ival());
                return true;
            case "stealth":
                op.SetOrClearFlag(Define.FLAG_STEALTH, Ival());
                return true;
            case "connected":
                Host.AddButtonLink(op, op.Map, Ival());
                return true;
            case "cursed":
                op.SetOrClearFlag(Define.FLAG_CURSED, Ival());
                return true;
            case "damned":
                op.SetOrClearFlag(Define.FLAG_DAMNED, Ival());
                return true;
            case "see_anywhere":
                op.SetOrClearFlag(Define.FLAG_SEE_ANYWHERE, Ival());
                return true;
            case "known_magical":
                op.SetOrClearFlag(Define.FLAG_KNOWN_MAGICAL, Ival());
                return true;
            case "known_cursed":
                op.SetOrClearFlag(Define.FLAG_KNOWN_CURSED, Ival());
                return true;
            case "can_use_skill":
                op.SetOrClearFlag(Define.FLAG_CAN_USE_SKILL, Ival());
                return true;
            case "been_applied":
                op.SetOrClearFlag(Define.FLAG_BEEN_APPLIED, Ival());
                return true;
            case "title":
                if (v.Length == 0)
                {
                    Log(LogLevel.Error, "Title without value.\n");
                }
                else
                {
                    op.Title = v;
                }
                return true;
            case "has_ready_rod":
                op.SetOrClearFlag(Define.FLAG_READY_ROD, Ival());
                return true;
            case "can_use_rod":
                op.SetOrClearFlag(Define.FLAG_USE_ROD, Ival());
                return true;
            case "has_ready_horn":
                op.SetOrClearFlag(Define.FLAG_READY_HORN, Ival());
                return true;
            case "can_use_horn":
                op.SetOrClearFlag(Define.FLAG_USE_HORN, Ival());
                return true;
            case "expmul":
                op.Expmul = Fval();
                return true;
            case "unique":
                op.SetOrClearFlag(Define.FLAG_UNIQUE, Ival());
                return true;
            case "make_invisible":
                op.SetOrClearFlag(Define.FLAG_MAKE_INVIS, Ival());
                return true;
            case "inv_locked":
                op.SetOrClearFlag(Define.FLAG_INV_LOCKED, Ival());
                return true;
            case "is_wooded":
                op.SetOrClearFlag(Define.FLAG_IS_WOODED, Ival());
                return true;
            case "is_hilly":
                op.SetOrClearFlag(Define.FLAG_IS_HILLY, Ival());
                return true;
            case "has_ready_skill":
                op.SetOrClearFlag(Define.FLAG_READY_SKILL, Ival());
                return true;
            case "has_ready_weapon":
                op.SetOrClearFlag(Define.FLAG_READY_WEAPON, Ival());
                return true;
            case "no_skill_ident":
                op.SetOrClearFlag(Define.FLAG_NO_SKILL_IDENT, Ival());
                return true;
            case "glow_radius":
                op.GlowRadius = unchecked((short)Ival());
                return true;
            case "is_blind":
                op.SetOrClearFlag(Define.FLAG_BLIND, Ival());
                return true;
            case "can_see_in_dark":
                op.SetOrClearFlag(Define.FLAG_SEE_IN_DARK, Ival());
                return true;
            case "is_cauldron":
                op.SetOrClearFlag(Define.FLAG_IS_CAULDRON, Ival());
                return true;
            case "randomitems":
                op.Randomitems = FindTreasurelist(v);
                return true;
            case "is_dust":
                op.SetOrClearFlag(Define.FLAG_DUST, Ival());
                return true;
            case "no_steal":
                op.SetOrClearFlag(Define.FLAG_NO_STEAL, Ival());
                return true;
            case "one_hit":
                op.SetOrClearFlag(Define.FLAG_ONE_HIT, Ival());
                return true;

            // Some archetypes have these values in them: the new combat code's, ignored
            case "can_knockback":
            case "can_parry":
            case "can_impale":
            case "can_cut":
            case "can_dam_armour":
                return true;
            case "weapontype":
                op.Weapontype = CStr.ToUInt(Fval());
                return true;
            case "casting_speed":
                op.CastingSpeed = (float)Fval();
                return true;
            case "elevation":
                op.Elevation = Ival();
                return true;
            case "client_type":
                op.ClientType = unchecked((ushort)Ival());
                return true;
        }

        // resist_<attack>: the rules of loader.l go from physical to internal; resist_life_stealing has none in this version.
        if (key.StartsWith("resist_", StringComparison.Ordinal))
        {
            string an = key.Substring(7);
            for (int i = 0; i <= Define.ATNR_INTERNAL; i++)
            {
                if (Tables.ResistSave[i] == an)
                {
                    op.Resist[i] = unchecked((short)Ival());
                    return true;
                }
            }
        }
        return false;
    }
}
