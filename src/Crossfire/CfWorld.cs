// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): include/global.h, common/friend.c, include/object.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     include/global.h:
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     common/friend.c:
//         Copyright (C) 2001 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/object.h:
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
/// What the C code of the object model calls in the parts of Crossfire that are not ported (maps, players and their client,
/// treasures, items): one virtual member for each call, each doing nothing, or what C does when that part is not there. The owner
/// of the object model derives from it. Each member names the C function it stands for (docs/porting.md).
/// </summary>
public class CrossfireHost
{
    private readonly Dictionary<string, TreasureList> treasureLists = new(StringComparer.Ordinal);

    /// <summary>
    /// Not in C: a key the host's own loader reads, as Graal Kingdoms' server added rules for <c>graalnpctype</c> and others to
    /// <c>loader.l</c>. A line with such a key is kept in <see cref="CfObject.Extra"/> without the error C logs for a line it has no
    /// rule for. This default knows none (docs/differences.md).
    /// </summary>
    public virtual bool IsLoaderKey(string key) => false;

    /// <summary>
    /// treasure.c: the treasure lists are loaded (<c>first_treasurelist</c> is not NULL). The loader leaves randomitems empty
    /// until then, and the second pass of the archetypes sets it.
    /// </summary>
    public virtual bool TreasuresLoaded { get; protected set; }

    /// <summary>treasure.c <c>load_treasures</c>, called between the two archetype passes. This default only marks them loaded.</summary>
    public virtual void LoadTreasures() => TreasuresLoaded = true;

    /// <summary>
    /// treasure.c <c>find_treasurelist</c> (after its special cases, which the world applies): the list of that name, or null when
    /// there is none (the world logs the error). Until treasure.c is ported this default has every name: it makes the list when it is
    /// asked for, so that the name of an object's randomitems is not lost.
    /// </summary>
    public virtual TreasureList? FindTreasureList(string name)
    {
        if (!treasureLists.TryGetValue(name, out TreasureList? tl))
        {
            tl = new TreasureList(name);
            treasureLists[name] = tl;
        }
        return tl;
    }

    /// <summary>
    /// fix_player of common/living.c: the stats of the player that carries or stops carrying an item are recomputed. Called by
    /// remove_ob and insert_ob_in_ob when the environment is inside a player (a player with a controller, FLAG_NO_FIX_PLAYER clear).
    /// </summary>
    public virtual void FixPlayer(CfObject player)
    {
    }

    /// <summary>
    /// The second half of object.c <c>remove_ob</c>, for an object that is on a map (<c>op-&gt;map != NULL</c>): unlinking it from
    /// the squares, walk-off, the looks of players, the lighting. Map code, not ported.
    /// </summary>
    public virtual void RemoveObFromMap(CfObject op)
    {
    }

    /// <summary>map.h <c>map-&gt;in_memory == MAP_IN_MEMORY</c> (object.c get_split_ob and free_object).</summary>
    public virtual bool MapInMemory(IMapHandle map) => false;

    /// <summary>map.c <c>wall(map, x, y)</c> (object.c free_object).</summary>
    public virtual bool Wall(IMapHandle map, int x, int y) => false;

    /// <summary>object.c <c>insert_ob_in_map</c> (free_object drops the inventory of a destroyed object on its map square).</summary>
    public virtual CfObject? InsertObInMap(CfObject op, IMapHandle map, CfObject? originator, int flag) => op;

    /// <summary>map.c <c>get_map_ob(map, x, y)</c> (map.c load_objects): the bottom object of a square of the map, or null. Default: no map has objects.</summary>
    public virtual CfObject? GetMapOb(IMapHandle map, int x, int y) => null;

    /// <summary>object.c <c>update_object</c> (update_turn_face).</summary>
    public virtual void UpdateObject(CfObject op, int action)
    {
    }

    /// <summary>player.h <c>first_player</c> walk of object.c decrease_ob_nr: the object of the player whose open container is <paramref name="container"/>, or null.</summary>
    public virtual CfObject? FindPlayerWithContainer(CfObject container) => null;

    /// <summary>
    /// The <c>new_draw_info</c> and <c>new_draw_info_format</c> of the server (common/glue.c's function pointer, socket/info.c): a line
    /// of text for the player <paramref name="who"/> (flags and colour are not passed: Graal Kingdoms' client colours the text itself).
    /// This default shows nothing; the extension's host sends it to the player's script (docs/extension.md).
    /// </summary>
    public virtual void DrawInfo(CfObject who, string text)
    {
    }

    /// <summary>The <c>esrv_send_item</c> function pointer of common/glue.c (the client).</summary>
    public virtual void EsrvSendItem(CfObject player, CfObject op)
    {
    }

    /// <summary>The <c>esrv_del_item</c> function pointer (the client).</summary>
    public virtual void EsrvDelItem(IPlayerHandle? player, uint tag)
    {
    }

    /// <summary>The <c>esrv_update_item</c> function pointer (the client); flags are the UPD_ values.</summary>
    public virtual void EsrvUpdateItem(int flags, CfObject player, CfObject op)
    {
    }

    /// <summary>button.c <c>add_button_link</c> (loader.l <c>connected</c>).</summary>
    public virtual void AddButtonLink(CfObject op, IMapHandle? map, int value)
    {
    }

    /// <summary>button.c <c>get_button_value</c> (loader.l get_ob_diff).</summary>
    public virtual int GetButtonValue(CfObject op) => 0;

    /// <summary>los.c <c>update_all_los</c> (object.c insert_ob_in_ob).</summary>
    public virtual void UpdateAllLos(IMapHandle map, int x, int y)
    {
    }

    /// <summary>map.h <c>MAP_DARKNESS(map)</c>.</summary>
    public virtual int MapDarkness(IMapHandle map) => 0;

    /// <summary>
    /// include/spells.h <c>spells[index].name</c> (common/item.c <c>query_short_name</c>, <c>query_base_name</c>,
    /// <c>look_up_spell_name</c>): the name of the spell with that number; null when the table has no such spell (C would read
    /// whatever lies there). The spell table is a later phase: this default has no spells, and returns the empty string for every
    /// number (so a scroll shows as "scroll of  (lvl 3)"), which is what an empty name gives.
    /// </summary>
    public virtual string? SpellName(int index) => "";

    /// <summary>include/spells.h <c>spells[index].charges</c> (server/shop.c <c>query_cost</c> of a wand): the most charges a wand of that spell has. Default: 0 (no spell table yet).</summary>
    public virtual int SpellCharges(int index) => 0;

    /// <summary>include/player.h <c>contr-&gt;digestion</c> (common/item.c <c>describe_monster</c>).</summary>
    public virtual int PlayerDigestion(IPlayerHandle contr) => 0;

    /// <summary>include/player.h <c>contr-&gt;gen_grace</c> (common/item.c <c>describe_monster</c>).</summary>
    public virtual int PlayerGenGrace(IPlayerHandle contr) => 0;

    /// <summary>include/player.h <c>contr-&gt;gen_sp</c> (common/item.c <c>describe_monster</c>).</summary>
    public virtual int PlayerGenSp(IPlayerHandle contr) => 0;

    /// <summary>include/player.h <c>contr-&gt;gen_hp</c> (common/item.c <c>describe_monster</c>).</summary>
    public virtual int PlayerGenHp(IPlayerHandle contr) => 0;

    /// <summary>
    /// include/treasure.h, common/item.c <c>describe_monster</c>: the names (<c>item-&gt;clone.name</c>) of the items of the
    /// treasure list that have type ABILITY, in the order of the list. The treasure lists are a later phase: default, none.
    /// </summary>
    public virtual IEnumerable<string> TreasureAbilityNames(TreasureList list) => Array.Empty<string>();

    /// <summary>The <c>monster_check_apply_func</c> pointer of common/glue.c (treasure.c <c>put_treasure</c> with GT_APPLY): a monster applies the item it was given. Default: nothing.</summary>
    public virtual void MonsterCheckApply(CfObject monster, CfObject item)
    {
    }

    /// <summary>arch.c <c>find_best_weapon_used_match</c> (needs item.c's name matching, not ported).</summary>
    public virtual CfObject? FindBestWeaponUsedMatch(CfObject player, string parameters) => null;
}

/// <summary>
/// The globals of Crossfire's object code (include/global.h, common/object.c, common/arch.c, common/loader.l, common/friend.c) and
/// the functions that use them. One world is one running Crossfire: its object list, its archetypes, its faces and animations.
/// The functions are in the partial files CfWorld.Object.cs, CfWorld.Arch.cs, CfWorld.Loader.cs and CfWorld.Save.cs.
/// </summary>
public sealed partial class CfWorld
{
    private readonly CfLog sink;

    /// <summary>The numbers of messages logged, by level: how the tests and the measure of an archetype folder count errors.</summary>
    public int[] LogCounts { get; } = new int[4];

    /// <summary>What the port calls in the parts of Crossfire it does not hold.</summary>
    public CrossfireHost Host { get; }

    /// <summary>global.h <c>new_faces</c> and image.c <c>FindFace</c>.</summary>
    public FaceTable Faces { get; }

    /// <summary>global.h <c>animations</c> and anim.c.</summary>
    public AnimationTable Animations { get; }

    /// <summary>object.c <c>objects</c>: the list of used objects.</summary>
    public CfObject? Objects { get; private set; }

    /// <summary>object.c <c>active_objects</c>: the objects that need to be processed (speed over MIN_ACTIVE_SPEED).</summary>
    public CfObject? ActiveObjects { get; private set; }

    /// <summary>global.h <c>ob_count</c>: how many objects were made (the count of the last one).</summary>
    public long ObCount { get; set; }

    /// <summary>object.c: nrofallocobjects without a free list, the objects made and not freed (see docs/differences.md).</summary>
    public int UsedObjects { get; private set; }

    /// <summary>global.h <c>first_archetype</c>.</summary>
    public Archetype? FirstArchetype { get; set; }

    /// <summary>global.h <c>empty_archetype</c>.</summary>
    public Archetype? EmptyArchetype { get; set; }

    /// <summary>arch.c <c>arch_init</c>: true while the archetypes are read.</summary>
    public bool ArchInit { get; set; }

    /// <summary>global.h <c>warn_archetypes</c>: write a warning when an archetype is not found.</summary>
    public bool WarnArchetypes { get; set; }

    /// <summary>global.h <c>editor</c>: true in the map editor (copy_object then does not randomise speed_left).</summary>
    public bool Editor { get; set; }

    /// <summary>friend.c <c>first_friendly_object</c>.</summary>
    public ObjectLink? FirstFriendlyObject { get; private set; }

    /// <summary>global.h <c>errmsg</c>: the text of the last failure of get_split_ob and of dump_object.</summary>
    public string ErrMsg { get; set; } = "";

    /// <summary>define.h <c>RANDOM()</c>: a non-negative random number of 31 bits. Replace it to make a run repeat.</summary>
    public Func<int> Random { get; set; }

    /// <summary>
    /// Creates a world.
    /// </summary>
    /// <param name="host">What the world calls in the parts of Crossfire that are not ported (null: the defaults).</param>
    /// <param name="log">Where LOG() goes (null: nowhere; the counts are kept in <see cref="LogCounts"/> either way).</param>
    public CfWorld(CrossfireHost? host = null, CfLog? log = null)
    {
        Host = host ?? new CrossfireHost();
        sink = log ?? ((_, _) => { });
        var rng = new System.Random(1);
        Random = () => rng.Next();
        Faces = new FaceTable(Log);
        Animations = new AnimationTable(Faces, Log);
        archTable = new Archetype?[Define.ARCHTABLE];
    }

    /// <summary>logger.c <c>LOG(level, ...)</c>.</summary>
    public void Log(LogLevel level, string message)
    {
        LogCounts[(int)level]++;
        sink(level, message);
    }

    private static string Cat(CfObject? op) => op?.Name ?? "(null)";

    /// <summary>common/friend.c <c>add_friendly_object</c>.</summary>
    public void AddFriendlyObject(CfObject op)
    {
        for (ObjectLink? ol = FirstFriendlyObject; ol != null; ol = ol.Next)
        {
            if (ol.Ob == op)
            {
                Log(LogLevel.Error, "add_friendly_object: Trying to add object already on list (" + op.Name + ")\n");
                return;
            }
        }
        ObjectLink? old = FirstFriendlyObject;
        FirstFriendlyObject = new ObjectLink { Ob = op, Id = unchecked((int)op.Count), Next = old };
    }

    /// <summary>common/friend.c <c>remove_friendly_object</c>.</summary>
    public void RemoveFriendlyObject(CfObject op)
    {
        op.ClearFlag(Define.FLAG_FRIENDLY);
        if (FirstFriendlyObject == null)
        {
            Log(LogLevel.Error, "remove_friendly_object called with empty friendly list, remove ob=" + op.Name + "\n");
            return;
        }
        if (FirstFriendlyObject.Ob == op)
        {
            FirstFriendlyObject = FirstFriendlyObject.Next;
        }
        else
        {
            ObjectLink prev = FirstFriendlyObject;
            ObjectLink? t;
            for (t = FirstFriendlyObject.Next; t != null; t = t.Next)
            {
                if (t.Ob == op)
                {
                    break;
                }
                prev = t;
            }
            if (t != null)
            {
                if (t.Id != unchecked((int)op.Count))
                {
                    Log(LogLevel.Error, "remove_friendly_object, tags do no match, " + (op.Name ?? "none") + ", " + op.Count + " != " + t.Id + "\n");
                }
                prev.Next = t.Next;
            }
        }
    }

    /// <summary>Appends text to a StringBuilder of the size C gives the static buffers (dump functions only grow it).</summary>
    internal static StringBuilder Sb() => new();
}
