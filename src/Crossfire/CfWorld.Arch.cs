// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/arch.c, common/treasure.c.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/arch.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     common/treasure.c:
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
namespace Crossfire;

public sealed partial class CfWorld
{
    private readonly Archetype?[] archTable;
    private Archetype? blankEmptyArchetype;

    /// <summary>arch.c <c>arch_cmp</c>: how many string compares the table did.</summary>
    public int ArchCmp { get; private set; }

    /// <summary>arch.c <c>arch_search</c>: how many searches the table did.</summary>
    public int ArchSearch { get; private set; }

    /// <summary>
    /// The empty archetype of arch.c <c>init_archetypes</c>, or, when the archetypes have none, a blank one made on the spot (the C code
    /// would dereference NULL when it dumps or saves an object: docs/differences.md).
    /// </summary>
    public Archetype EmptyArchetypeOrBlank()
    {
        if (EmptyArchetype != null)
        {
            return EmptyArchetype;
        }
        blankEmptyArchetype ??= GetArchetypeStruct();
        return blankEmptyArchetype;
    }

    /// <summary>
    /// arch.c <c>find_archetype_by_object_name</c> (GROS): the archetype whose object has this name as it appears in the game
    /// (the name of the clone, not the key of the archetype). It walks the whole list each time.
    /// </summary>
    public Archetype? FindArchetypeByObjectName(string? name)
    {
        if (name == null)
        {
            return null;
        }
        for (Archetype? at = FirstArchetype; at != null; at = at.Next)
        {
            if (at.Clone.Name != null && at.Clone.Name == name)
            {
                return at;
            }
        }
        return null;
    }

    /// <summary>
    /// arch.c <c>get_archetype_by_object_name</c> (GROS): a new object for the name that appears in the game. It takes the full name
    /// and keeps shortening it (down to its first letter) until it finds a match; a singularity object when nothing matches.
    /// </summary>
    public CfObject GetArchetypeByObjectName(string name)
    {
        string tmpname = name.Length > Define.MAX_BUF - 1 ? name.Substring(0, Define.MAX_BUF - 1) : name;
        for (int i = tmpname.Length; i > 0; i--)
        {
            Archetype? at = FindArchetypeByObjectName(tmpname.Substring(0, i));
            if (at != null)
            {
                return ArchToObject(at)!;
            }
        }
        return CreateSingularity(name);
    }

    /// <summary>
    /// arch.c <c>init_archetypes</c> (called from add_player() and edit() in C) with <c>load_archetypes</c>: reads the archetypes of the
    /// archetypes file's text (what collect.pl writes, or <see cref="ArchCollector"/>), once; then finds the empty archetype.
    /// </summary>
    public void InitArchetypes(string archetypesText)
    {
        if (FirstArchetype != null)
        {
            return; // Only do this once
        }
        ArchInit = true;
        LoadArchetypes(archetypesText);
        ArchInit = false;
        EmptyArchetype = FindArchetype("empty_archetype");
    }

    /// <summary>arch.c <c>arch_info</c>: the statistics of the archetype table, as the text C sends to the player.</summary>
    public string ArchInfo()
    {
        ErrMsg = ArchSearch + " searches and " + ArchCmp + " strcmp()'s";
        return ErrMsg;
    }

    /// <summary>arch.c <c>clear_archetable</c>: initialises the hash table used by the archetypes.</summary>
    public void ClearArchetable() => Array.Clear(archTable);

    /// <summary>arch.c <c>init_archetable</c>: puts every archetype of the list, and every part of a multipart one, in the hash table.</summary>
    public void InitArchetable()
    {
        Log(LogLevel.Debug, " Setting up archetable...");
        for (Archetype? at = FirstArchetype; at != null; at = at.More == null ? at.Next : at.More)
        {
            AddArch(at);
        }
        Log(LogLevel.Debug, "done\n");
    }

    /// <summary>arch.c <c>dump_arch</c>: the dump of the clone of an archetype.</summary>
    public string DumpArch(Archetype at) => DumpMe(at.Clone);

    /// <summary>arch.c <c>dump_all_archetypes</c>: the dumps of all archetypes, each on a line of its own.</summary>
    public string DumpAllArchetypes()
    {
        var sb = new System.Text.StringBuilder();
        for (Archetype? at = FirstArchetype; at != null; at = at.More == null ? at.Next : at.More)
        {
            sb.Append(DumpArch(at)).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// arch.c <c>get_archetype_struct</c>: a new archetype with its clone cleared by clear_object, FLAG_FREED off and FLAG_REMOVED on
    /// ("copy_object() doesn't copy these flags").
    /// </summary>
    public Archetype GetArchetypeStruct()
    {
        var created = new Archetype();
        created.Next = null;
        created.Name = null;
        created.Clone.OtherArch = null;
        created.Clone.Name = null;
        created.Clone.NamePl = null;
        created.Clone.Title = null;
        created.Clone.Race = null;
        created.Clone.Slaying = null;
        created.Clone.Msg = null;
        ClearObject(created.Clone); // to initial state other also
        created.Clone.ClearFlag(Define.FLAG_FREED);
        created.Clone.SetFlag(Define.FLAG_REMOVED);
        created.Head = null;
        created.More = null;
        return created;
    }

    /// <summary>
    /// arch.c <c>first_arch_pass</c>: reads the archetypes of a text and copies them into a linked list of archetype structures. A
    /// block that ends with a normal <c>end</c> starts a new archetype; a block that has the line <c>More</c> is a further part of
    /// the archetype before it. The archetype hash table is empty during this pass, so arch lines and other_arch find nothing yet.
    /// </summary>
    public void FirstArchPass(LoadSource fp)
    {
        Archetype? head = null;
        Archetype? lastMore = null;
        int first = Define.LO_NEWFILE;
        int i;

        CfObject op = GetObject();
        Archetype at = GetArchetypeStruct();
        op.Arch = at;
        FirstArchetype = at;

        while ((i = LoadObject(fp, op, first, 0)) != 0)
        {
            first = 0;
            CopyObject(op, at.Clone);
            at.Clone.SpeedLeft = -0.1f;
            // Not in C (docs/differences.md): copy_object does not copy the inventory, so the objects of the nested blocks of an
            // archetype are lost in C. The port keeps them in the inventory of the archetype's clone.
            if (op.Inv != null)
            {
                at.Clone.Inv = op.Inv;
                for (CfObject? t = op.Inv; t != null; t = t.Below)
                {
                    t.Env = at.Clone;
                }
                op.Inv = null;
            }

            switch (i)
            {
                case Define.LL_MORE when head == null:
                    // C reads head->clone through a NULL pointer here. A part before any archetype is taken as an archetype.
                    Log(LogLevel.Error, "Archetype part (More) without a head: " + at.Name + "\n");
                    goto case Define.LL_NORMAL;

                case Define.LL_NORMAL: // A new archetype, just link it with the previous
                    if (lastMore != null)
                    {
                        lastMore.Next = at;
                    }
                    if (head != null)
                    {
                        head.Next = at;
                    }
                    head = lastMore = at;
                    at.TailX = 0;
                    at.TailY = 0;
                    break;

                case Define.LL_MORE: // Another part of the previous archetype, link it correctly
                    at.Head = head;
                    at.Clone.Head = head!.Clone;
                    if (lastMore != null)
                    {
                        lastMore.More = at;
                        lastMore.Clone.More = at.Clone;
                    }
                    lastMore = at;

                    // If this multipart image is still composed of individual small images, don't set the tail values: they can't
                    // be used anyway, and zero makes the map sending to the client easier.
                    if (at.Clone.Face != head.Clone.Face)
                    {
                        head.TailX = 0;
                        head.TailY = 0;
                    }
                    else
                    {
                        if (at.Clone.X > head.TailX)
                        {
                            head.TailX = (sbyte)at.Clone.X;
                        }
                        if (at.Clone.Y > head.TailY)
                        {
                            head.TailY = (sbyte)at.Clone.Y;
                        }
                    }
                    break;
            }
            at = GetArchetypeStruct();
            ClearObject(op);
            op.Arch = at;
        }
        FreeObject(op);
    }

    private static IEnumerable<string> Fgets(string text, int size)
    {
        // fgets(buf, size, fp): up to size-1 characters, ending after a newline.
        int pos = 0;
        while (pos < text.Length)
        {
            int nl = text.IndexOf('\n', pos);
            int end = nl >= 0 ? nl + 1 : text.Length;
            if (end - pos > size - 1)
            {
                end = pos + size - 1;
            }
            yield return text.Substring(pos, end - pos);
            pos = end;
        }
    }

    private static bool IsSpace(char c) => c == ' ' || (c >= '\t' && c <= '\r');

    /// <summary>
    /// arch.c <c>second_arch_pass</c>: reads the archetypes' text once more and links all pointers between archetypes: the
    /// <c>other_arch</c> and <c>randomitems</c> lines of each <c>Object</c> (case-sensitive, any line of the block, nested ones too).
    /// </summary>
    public void SecondArchPass(string text)
    {
        string? argument = null;
        Archetype? at = null;

        foreach (string buf in Fgets(text, Define.MAX_BUF))
        {
            if (buf[0] == '#')
            {
                continue;
            }
            string variable;
            int sp = buf.IndexOf(' ');
            if (sp >= 0)
            {
                variable = buf.Substring(0, sp);
                string arg = buf.Substring(sp + 1);
                int e = arg.Length;
                while (e > 0 && IsSpace(arg[e - 1]))
                {
                    e--;
                }
                argument = arg.Substring(0, e);
            }
            else
            {
                variable = buf;
            }
            if (variable == "Object")
            {
                if ((at = FindArchetype(argument)) == null)
                {
                    Log(LogLevel.Error, "Warning: failed to find arch " + (argument ?? "(null)") + "\n");
                }
            }
            else if (variable == "other_arch")
            {
                if (at != null && at.Clone.OtherArch == null)
                {
                    Archetype? other = FindArchetype(argument);
                    if (other == null)
                    {
                        Log(LogLevel.Error, "Warning: failed to find other_arch " + (argument ?? "(null)") + "\n");
                    }
                    else
                    {
                        at.Clone.OtherArch = other;
                    }
                }
            }
            else if (variable == "randomitems")
            {
                if (at != null)
                {
                    TreasureList? tl = FindTreasurelist(argument ?? "");
                    if (tl == null)
                    {
                        Log(LogLevel.Error, "Failed to link treasure to arch: " + variable + "\n");
                    }
                    else
                    {
                        at.Clone.Randomitems = tl;
                    }
                }
            }
        }
    }

    /// <summary>arch.c <c>check_generators</c> (DEBUG): warns of a generator that has no other_arch.</summary>
    public void CheckGenerators()
    {
        for (Archetype? at = FirstArchetype; at != null; at = at.Next)
        {
            if (at.Clone.QueryFlag(Define.FLAG_GENERATOR) && at.Clone.OtherArch == null)
            {
                Log(LogLevel.Error, "Warning: " + at.Name + " is generator but lacks other_arch.\n");
            }
        }
    }

    /// <summary>
    /// arch.c <c>load_archetypes</c>: clears the hash table, reads and parses the archetypes with the first and second pass, between
    /// them sets up the table and loads the treasures. The text takes the place of the file.
    /// </summary>
    public void LoadArchetypes(string archetypesText)
    {
        ClearArchetable();
        Log(LogLevel.Debug, " arch-pass 1...\n");
        FirstArchPass(new LoadSource(archetypesText));

        Log(LogLevel.Debug, " done\n");
        InitArchetable();
        WarnArchetypes = true;

        Log(LogLevel.Debug, " loading treasure...\n");
        Host.LoadTreasures();
        Log(LogLevel.Debug, " done\n arch-pass 2...\n");
        SecondArchPass(archetypesText);
        Log(LogLevel.Debug, " done\n");
        CheckGenerators();
        Log(LogLevel.Debug, " done\n");
    }

    /// <summary>arch.c <c>arch_to_object</c>: a new object that is a copy of the archetype; null when the archetype is null (with a message once warnings are on).</summary>
    public CfObject? ArchToObject(Archetype? at)
    {
        if (at == null)
        {
            if (WarnArchetypes)
            {
                Log(LogLevel.Error, "Couldn't find archetype.\n");
            }
            return null;
        }
        CfObject op = GetObject();
        CopyObject(at.Clone, op);
        op.Arch = at;
        return op;
    }

    /// <summary>
    /// arch.c <c>create_singularity</c>: the object that <see cref="GetArchetype"/> returns when the archetype is not found, so that
    /// it always returns an object: named "singluarity (name)" (the C spelling), with NO_PICK.
    /// </summary>
    public CfObject CreateSingularity(string name)
    {
        string buf = "singluarity (" + name + ")";
        CfObject op = GetObject();
        op.Name = buf;
        op.NamePl = buf;
        op.SetFlag(Define.FLAG_NO_PICK);
        return op;
    }

    /// <summary>arch.c <c>get_archetype</c>: a new object copied from the archetype of that name; a singularity when there is none.</summary>
    public CfObject GetArchetype(string name)
    {
        Archetype? at = FindArchetype(name);
        if (at == null)
        {
            return CreateSingularity(name);
        }
        return ArchToObject(at)!;
    }

    /// <summary>arch.c <c>hasharch</c>: the hash function of the archetype table (64-bit <c>unsigned long</c>, signed <c>char</c>).</summary>
    public static ulong Hasharch(string str, int tablesize)
    {
        ulong hash = 0;
        int i = 0;
        int rot = 0;
        foreach (char c in str)
        {
            if (i >= Define.MAXSTRING)
            {
                break;
            }
            hash ^= unchecked((ulong)(long)(sbyte)c) << rot;
            rot += 2;
            if (rot >= (8 - 1) * 8)
            {
                rot = 0;
            }
            i++;
        }
        return hash % (ulong)tablesize;
    }

    /// <summary>
    /// arch.c <c>find_archetype</c>: uses the hash table to find the archetype of that name (the first one added when a name occurs
    /// more than once); null when there is none (with a message once warnings are on).
    /// </summary>
    public Archetype? FindArchetype(string? name)
    {
        if (name == null)
        {
            return null;
        }

        int index = (int)Hasharch(name, Define.ARCHTABLE);
        ArchSearch++;
        for (; ; )
        {
            Archetype? at = archTable[index];
            if (at == null)
            {
                if (WarnArchetypes)
                {
                    Log(LogLevel.Error, "Couldn't find archetype " + name + "\n");
                }
                return null;
            }
            ArchCmp++;
            if (at.Name == name)
            {
                return at;
            }
            if (++index >= Define.ARCHTABLE)
            {
                index = 0;
            }
        }
    }

    /// <summary>arch.c <c>add_arch</c>: adds an archetype to the hash table; a full table is fatal (ARCHTABLE_TOO_SMALL).</summary>
    public void AddArch(Archetype at)
    {
        int index = (int)Hasharch(at.Name ?? "", Define.ARCHTABLE);
        int orgIndex = index;
        for (; ; )
        {
            if (archTable[index] == null)
            {
                archTable[index] = at;
                return;
            }
            if (++index == Define.ARCHTABLE)
            {
                index = 0;
            }
            if (index == orgIndex)
            {
                throw new CrossfireFatalException("fatal: ARCHTABLE_TOO_SMALL (" + Define.ARCHTABLE + " archetypes)");
            }
        }
    }

    /// <summary>arch.c <c>type_to_archetype</c>: the first archetype (parts included) that has this type; used in treasure generation.</summary>
    public Archetype? TypeToArchetype(int type)
    {
        for (Archetype? at = FirstArchetype; at != null; at = at.More == null ? at.Next : at.More)
        {
            if (at.Clone.Type == type)
            {
                return at;
            }
        }
        return null;
    }

    /// <summary>arch.c <c>clone_arch</c>: a new object copied from the first archetype of this type; null when there is none.</summary>
    public CfObject? CloneArch(int type)
    {
        CfObject op = GetObject();
        Archetype? at = TypeToArchetype(type);
        if (at == null)
        {
            Log(LogLevel.Error, "Can't clone archetype " + type + "\n");
            op.SetFlag(Define.FLAG_REMOVED);
            FreeObject(op);
            return null;
        }
        CopyObject(at.Clone, op);
        return op;
    }

    /// <summary>arch.c <c>ObjectCreateArch</c>: makes an instance (head and parts, each at its offset) of the archetype and its parts.</summary>
    public CfObject? ObjectCreateArch(Archetype? at)
    {
        CfObject? prev = null;
        CfObject? head = null;

        while (at != null)
        {
            CfObject op = ArchToObject(at)!;
            op.X = at.Clone.X;
            op.Y = at.Clone.Y;
            if (head != null)
            {
                op.Head = head;
                prev!.More = op;
            }
            if (head == null)
            {
                head = op;
            }
            prev = op;
            at = at.More;
        }
        return head;
    }

    /// <summary>treasure.c <c>find_treasurelist</c>: "none" gives null (it overrides the default random items) and so does the time before the treasures are loaded (the second archetype pass sets them); an unknown name is an error.</summary>
    public TreasureList? FindTreasurelist(string name)
    {
        if (name == "none" || !Host.TreasuresLoaded)
        {
            return null;
        }
        TreasureList? tl = Host.FindTreasureList(name);
        if (tl == null)
        {
            Log(LogLevel.Error, "Couldn't find treasurelist " + name + "\n");
        }
        return tl;
    }
}
