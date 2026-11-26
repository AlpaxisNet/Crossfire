# What is ported from Crossfire, and what is not

The table of the files and functions of Crossfire's server at commit `8b7ee7f90` (2002-06-27) that the object model and the archetypes need; how to read it is below it.

### include/object.h, include/living.h

| C file | C name | C# member | Status |
| --- | --- | --- | --- |
| include/object.h | `struct obj` (`object`), every field | `CfObject` | ported: every field, in the C order, with the C type (`sint8` = `sbyte` ...; `signed long` = 32 bits, see differences). The `CASTING_TIME` fields are there (`config.h` defines it). The string fields are `string?` |
| include/object.h | `struct archt` (`archetype`) | `Archetype` | ported |
| include/object.h | `objectlink` | `ObjectLink` | ported (the friendly list) |
| include/object.h | `oblinkpt` | none | not ported: the buttons and gates of maps (`connected`) go to the host |
| include/object.h | `tag_t`, `LOOK_OBJ`, `UP_OBJ_*`, `INS_*` | `uint`, `Define.UP_OBJ_*`, `Define.INS_*` | ported (`LOOK_OBJ` is a client macro: not ported) |
| include/living.h | `struct liv` (`living`) | `Living` | ported, with the `memcpy` and `memcmp` that C does on it (`CopyFrom`, `SameAs`) |
| include/living.h | `STR`..`POW`, `NO_STAT_VAL` | `Define.STR`..`Define.POW` | ported |
| include/living.h | `cha_bonus`, `dex_bonus`, `thaco_bonus`, ... the bonus tables | none | not ported: they are data of `living.c`'s stat code (a later phase), not of the object model |

### include/define.h, attack.h, material.h, plugin.h, config.h (constants and tables)

| C file | C name | C# member | Status |
| --- | --- | --- | --- |
| include/define.h | the object types `PLAYER` .. `SYMPTOM` | `Define.PLAYER` .. `Define.SYMPTOM` | ported (generated from the header) |
| include/define.h | `WEAP_*`, `PU_*`, `SIZEOFFREE*`, `PATH_*`, `NRSPELLPATHS`, `NROFREALSPELLS`, `NROFSKILLS` | `Define.*` | ported |
| include/define.h | `FLAG_ALIVE` .. `FLAG_OBJ_SAVE_ON_OVL`, `NUM_FLAGS` | `Define.FLAG_*` | ported; `SET_FLAG`, `CLEAR_FLAG`, `QUERY_FLAG` are `CfObject.SetFlag`, `ClearFlag`, `QueryFlag` |
| include/define.h | `E_*`, `MAX_LIGHT_RADII`, `F_BUY`.., `DISTATT`..`HI4`, `ST_*`, `PREFER_*`, `MAX_STAT`, sizes, `OUT_OF_MEMORY`.. | `Define.*` | ported |
| include/define.h | `IS_WEAPON`, `IS_ARMOR`, `IS_LIVE`, `IS_ARROW`, `NROFNEWOBJS`, `SLOW_PENALTY`, `GENERATE_*`, `EXIT_*`, `DIRX`, `D_LOCK`, `GET_X_FROM_DIR`, `DESCRIBE_*`, `safe_strcat`, `apply_flag`, `RANDOM` | none (`RANDOM()` is `CfWorld.Random`) | not ported: macros of the code that uses them (item description, apply, exits, movement), ported with that code |
| include/attack.h | `NROFATTACKS`, `ATNR_*`, `AT_*`, `ATM_*`, `RESIST_*` | `Define.*` | ported |
| include/attack.h | `resist_save`, `attacktype_desc`, `resist_plus`, `change_resist_msg`, `resist_table`, `num_resist_table` | `Tables.ResistSave`, `AttacktypeDesc`, `ResistPlus`, `ChangeResistMsg`, `ResistTable`, `NumResistTable` | ported |
| include/attack.h | `attack_mess` (the attack messages table) | none | not ported: filled from `lib/attackmess` by the attack code (a later phase) |
| include/material.h | `M_*`, `NROFMATERIALS`, `materialtype` | `Define.M_*`, `MaterialType` | ported |
| common/item.c | `material[NROFMATERIALS]` | `Tables.Material` | ported (the data; the functions of item.c are in the next section) |
| common/living.c | `attacks`, `statname`, `short_stat_name` | `Tables.Attacks`, `Statname`, `ShortStatName` | ported |
| include/spellist.h | `spellpathnames` | `Tables.Spellpathnames` | ported (the spell list itself is a later phase) |
| include/plugin.h | `EVENT_*`, `NR_EVENTS` | `Define.EVENT_*` | ported (the plugin machinery is not) |
| include/config.h | `STARTMAX`, `OBJ_EXPAND`, `ARCHTABLE`, `MAXSTRING` | `Define.*` | ported (`STARTMAX` and `OBJ_EXPAND` are not used: no free list) |
| include/loader.h | `LL_*`, `LO_*`, `BUFADD`, `enum varnrs` | `Define.LL_*`, `Define.LO_*` | ported; `varnrs` and `variable_const` only as far as `get_ob_diff` and the flag table use them (`Define.FlagLinks`) |
| include/map.h | `MAP_STYLE` | `Define.MAP_STYLE` | ported (the rest of map.h is not) |
| include/newclient.h | `UPD_WEIGHT` | `CfWorld.Define_UPD_WEIGHT` | ported (the other `UPD_*` belong to the client) |

### common/object.c

| C function | C# member | Status |
| --- | --- | --- |
| `CAN_MERGE` | `CfWorld.CanMerge` | ported |
| `sum_weight` | `CfWorld.SumWeight` | ported |
| `is_player_inv` | `CfWorld.IsPlayerInv` | ported |
| `dump_object2`, `dump_object` | `CfWorld.DumpObject` (text in `ErrMsg`) | ported |
| `dump_me` | `CfWorld.DumpMe` | ported |
| `dump_all_objects` | none | not ported: writes to the log file; `DumpObject` on each of `Objects` does it |
| `get_nearest_part` | `CfWorld.GetNearestPart` | ported |
| `find_object` | `CfWorld.FindObject` | ported |
| `find_object_name` | `CfWorld.FindObjectName` | ported |
| `free_all_object_data` | none | not ported: statistics of the free list, which the port does not have |
| `get_owner`, `clear_owner`, `set_owner_simple`, `set_skill_pointers`, `set_owner`, `copy_owner` | `CfWorld.GetOwner`, `ClearOwner`, `SetOwner`, `CopyOwner` (the other two private) | ported |
| `reset_object` | `CfWorld.ResetObject` | ported |
| `clear_object` | `CfWorld.ClearObject` | ported (also clears `Extra`) |
| `copy_object` | `CfWorld.CopyObject` | ported (also copies `Extra`) |
| `expand_objects` | none | not ported: the list of free objects is not kept (differences) |
| `get_object` | `CfWorld.GetObject` | ported without the free list |
| `update_turn_face` | `CfWorld.UpdateTurnFace` | ported; its `update_object` is `CrossfireHost.UpdateObject` |
| `update_ob_speed` | `CfWorld.UpdateObSpeed` | ported |
| `update_object` | `CrossfireHost.UpdateObject` | not ported: updates the squares of a map |
| `free_object` | `CfWorld.FreeObject` | ported; the inventory of an object on a map goes to `CrossfireHost.InsertObInMap` (with `MapInMemory`, `Wall`) |
| `count_free` | none | not ported: no free list |
| `count_used`, `count_active` | `CfWorld.CountUsed`, `CountActive` | ported |
| `sub_weight`, `add_weight` | `CfWorld.SubWeight`, `AddWeight` | ported |
| `remove_ob` | `CfWorld.RemoveOb` | ported for an object in an inventory; the half for an object on a map is `CrossfireHost.RemoveObFromMap` (squares, walk-off, looks of players, lighting); `fix_player` is `CrossfireHost.FixPlayer`; `abort()` is a `CrossfireFatalException` |
| `merge_ob` | `CfWorld.MergeOb` | ported |
| `insert_ob_in_map`, `replace_insert_ob_in_map` | `CrossfireHost.InsertObInMap` | not ported: map code (only free_object calls it) |
| `get_split_ob` | `CfWorld.GetSplitOb` | ported (`map->in_memory` is `CrossfireHost.MapInMemory`) |
| `decrease_ob_nr` | `CfWorld.DecreaseObNr` | ported; the client calls (`esrv_send_item`, `esrv_del_item`, `esrv_update_item`) are `CrossfireHost.EsrvSendItem`, `EsrvDelItem`, `EsrvUpdateItem`; the walk of `first_player` is `CrossfireHost.FindPlayerWithContainer` |
| `insert_ob_in_ob` | `CfWorld.InsertObInOb` | ported (`update_all_los` and `MAP_DARKNESS` are `CrossfireHost.UpdateAllLos`, `MapDarkness`) |
| `check_walk_on`, `present_arch`, `present`, `find_free_spot`, `find_first_free_spot`, `find_dir` | none | not ported: they look at maps |
| `present_in_ob`, `present_arch_in_ob` | `CfWorld.PresentInOb`, `PresentArchInOb` | ported |
| `set_cheat` | `CfWorld.SetCheat` | ported |
| `distance`, `find_dir_2`, `absdir`, `dirdiff` | `CfWorld.Distance`, `FindDir2`, `Absdir`, `Dirdiff` | ported |
| `can_pick` | `CfWorld.CanPick` | ported |
| `ObjectCreateClone` | `CfWorld.ObjectCreateClone` | ported |
| `was_destroyed` | `CfWorld.WasDestroyed` | ported |
| `load_object_str` | `CfWorld.LoadObjectStr` | ported (reads the string, not a temporary file) |
| the globals `objects`, `active_objects`, `free_objects`, `ob_count`, `errmsg`, `freearr_*`, `maxfree`, `freedir` | `CfWorld.Objects`, `ActiveObjects`, `ObCount`, `ErrMsg`, `FreeArrays.*` | ported (`free_objects` not) |

### common/arch.c

| C function | C# member | Status |
| --- | --- | --- |
| `find_archetype_by_object_name`, `get_archetype_by_object_name` | `CfWorld.FindArchetypeByObjectName`, `GetArchetypeByObjectName` | ported |
| `item_matched_string`, `find_best_weapon_used_match` | `ItemMatching.ItemMatchedString`, `ItemMatching.FindBestWeaponUsedMatch` | ported in the extension (`Gk/ItemMatching.cs`: they write the player's controller, `Player.Count`); `CrossfireHost.FindBestWeaponUsedMatch` is the loader's call, which the extension's host answers with the port |
| `init_archetypes` | `CfWorld.InitArchetypes(text)` | ported |
| `arch_info` | `CfWorld.ArchInfo` | ported (returns the text; C sends it to the player with `draw_info`) |
| `clear_archetable`, `init_archetable` | `CfWorld.ClearArchetable`, `InitArchetable` | ported |
| `dump_arch`, `dump_all_archetypes` | `CfWorld.DumpArch`, `DumpAllArchetypes` | ported (return the text) |
| `free_all_archs` | none | not ported: garbage collection |
| `get_archetype_struct` | `CfWorld.GetArchetypeStruct` | ported |
| `first_arch_pass` | `CfWorld.FirstArchPass` | ported |
| `second_arch_pass` | `CfWorld.SecondArchPass` | ported (including `fgets` with a buffer of `MAX_BUF`) |
| `check_generators` | `CfWorld.CheckGenerators` | ported |
| `load_archetypes` | `CfWorld.LoadArchetypes(text)` | ported; the text takes the place of the file; between the passes `CrossfireHost.LoadTreasures` is called and then, when `CfWorld.TreasureText` is set, `load_treasures` reads it (`CfWorld.LoadTreasureLists`) |
| `arch_to_object`, `create_singularity`, `get_archetype` | `CfWorld.ArchToObject`, `CreateSingularity`, `GetArchetype` | ported |
| `hasharch`, `find_archetype`, `add_arch` | `CfWorld.Hasharch`, `FindArchetype`, `AddArch` | ported (5003 slots, linear probing, `ARCHTABLE_TOO_SMALL` is a `CrossfireFatalException`) |
| `type_to_archetype`, `clone_arch` | `CfWorld.TypeToArchetype`, `CloneArch` | ported |
| `ObjectCreateArch` | `CfWorld.ObjectCreateArch` | ported |
| `arch_cmp`, `arch_search`, `arch_init`, `warn_archetypes`, `first_archetype`, `empty_archetype` | `CfWorld.ArchCmp`, `ArchSearch`, `ArchInit`, `WarnArchetypes`, `FirstArchetype`, `EmptyArchetype` | ported |
| `find_treasurelist` (common/treasure.c) | `CfWorld.FindTreasurelist` with `CrossfireHost.FindTreasureList` | its special cases ported (`none`, before the lists are loaded); the lists themselves are the treasure phase |

### common/loader.l (the `.arc` reader and the saver)

| C function | C# member | Status |
| --- | --- | --- |
| the scanner and its rules section (`lex_load`) | `CfWorld.LexLoad` (private) with `ApplyKeyRule` | ported, rule by rule, in the order of the C file: `Object`, `name`, `name_pl`, `race`, `slaying`, `Inventory`, `arch` (the archetype of an object, or an object of the inventory), `other_arch`, `animation`, `More`, `end`, `msg`/`endmsg`, `title`, every number, flag and `resist_*` key, the old `immune`/`protected`/`vulnerable`, the ignored `can_*` keys, the plugin events, and the last rules (`CfWorld.NoRule`: empty lines and comments ignored, `.*` logs `Unrecognized string` and sets `lex_error`, which `<<EOF>>` returns) |
| `yval` | `CfWorld.Yval` | ported |
| `yyerror` | none | not ported: flex's error hook |
| `load_object` | `CfWorld.LoadObject` | ported (`LO_REPEAT`, `LO_LINEMODE`, `LO_NEWFILE`, `LO_NOREAD`; the `FILE *` is a `LoadSource`) |
| `set_variable` | `CfWorld.SetVariable` | ported |
| `flag_links` | `Define.FlagLinks` | ported |
| `variable_const`, `init_vars` | none | not ported: `variable_const` is only an array of keywords, which `get_ob_diff` writes literally; `init_vars` is empty |
| `save_double`, `save_long` (common/porting.c) | private helpers of `CfWorld.GetObDiff` | ported |
| `get_ob_diff` | `CfWorld.GetObDiff` | ported |
| `save_object` | `CfWorld.SaveObject` (into a `StringBuilder`) | ported |

### common/map.c, include/map.h, server/player.c (the map file's header and object list, the player archetype)

| C file | C name | C# member | Status |
| --- | --- | --- | --- |
| include/map.h | `mapdef`: the fields the header of a map file sets and writes | `MapHeader` | ported (name, msg, the timers, size, entry point, `unique`, `outdoor`, the weather fields and the four tile paths, with the defaults of `get_linked_map`); the squares, the players and the timers of a loaded map are the host's |
| include/map.h | `MAP_FLUSH`, `MAP_PLAYER_UNIQUE`, `MAP_BLOCK`, `MAP_STYLE`, `MAP_OVERLAY` | `Define.MAP_*` | ported |
| common/map.c | `load_map_header` | `CfWorld.LoadMapHeader` | ported (the tile paths are kept as written: `check_path` looks at the file system) |
| common/map.c | `new_save_map` | `CfWorld.SaveMapHeader` | ported for the header it writes; opening, compressing and naming the file, and the file of unique items, are the host's |
| common/map.c | `load_objects` | `CfWorld.LoadObjects` | ported: each object goes to `CrossfireHost.InsertObInMap` (`insert_ob_in_map`) with C's flags, `get_map_ob` is `CrossfireHost.GetMapOb`; `link_multipart_objects` is not ported (it walks the squares) |
| common/map.c | `save_objects` | `CfWorld.SaveObjects` | ported: the host supplies the squares as lists of objects, bottom up, in the order C walks them |
| common/map.c | `get_linked_map`'s defaults, `get_map_ob`, `insert_ob_in_map` (map.c's callers) | `MapHeader` defaults, `CrossfireHost` | the rest of map.c (loading and saving whole maps, swapping, the squares, `get_empty_map`, tiling, `map_path`, ...) is not ported: maps are the host's |
| server/player.c | `get_player_archetype` | `CfWorld.GetPlayerArchetype` | ported (returns null with an error where C exits, and where C loops for ever when called with null and no player archetype exists) |
| (not in C) | `ExtraKeys.Set`, `ExtraKeys.Remove` | `ExtraKeys` | helpers of the Graal Kingdoms extra lines |

### Other files

| C file | C name | C# member | Status |
| --- | --- | --- | --- |
| common/anim.c | `init_anim`, `find_animation` | `AnimationTable.Load(text)`, `FindAnimation` | ported (the text of the `animations` file replaces the file) |
| common/anim.c | `animate_object`, `free_all_anim` | none | not ported: `animate_object` is the per-tick animation of objects on maps; the table is garbage collected |
| include/global.h | `Animations`, `SET_ANIMATION`, `GET_ANIM_ID` | `Animation`, `CfWorld.SetAnimation`, `AnimationTable[...]` | ported |
| common/image.c | `FindFace`, `new_faces`, `blank_face`, `empty_face` | `FaceTable.FindFace`, `NewFace`, `FaceTable.BlankFace`, `EmptyFace` | ported as far as the object model uses them; the pictures, the bmaps file and the faces file belong to the client |
| common/friend.c | `add_friendly_object`, `remove_friendly_object` | `CfWorld.AddFriendlyObject`, `RemoveFriendlyObject` | ported; `dump_friendly_objects` and `clean_friendly_list` are not (the list is cleaned where the objects are processed, a later phase) |
| lib/collect.pl.in | the traversal, the colour lines, the `anim` blocks | `ArchCollector.Collect` | ported as far as it changes the text of the `archetypes` and `animations` files; the pictures, `bmaps`, `faces` are not |
| common/button.c | `add_button_link`, `get_button_value` | `CrossfireHost.AddButtonLink`, `GetButtonValue` | not ported: connections of a map |
| common/living.c | `fix_player` | `CrossfireHost.FixPlayer` | not ported: the player's stats are a later wave; the extension's host answers the call by asking for a look at the player's window (docs/extension.md "Items of a player") |
| common/living.c | `weight_limit[MAX_STAT + 1]` | `ItemMatching.WeightLimit`, `WeightLimitOf` | ported in the extension (the table of 31 values; a strength above `MAX_STAT` takes the last, a negative one, which C would index outside the table, the first) |
| server/c_object.c | `find_best_object_match`, `find_marked_object`, `command_mark` | `ItemMatching.FindBestObjectMatch`, `FindMarkedObject`, `CommandMark` | ported in the extension (`Gk/ItemMatching.cs`; the lines C writes with `new_draw_info` go to `CrossfireHost.DrawInfo`) |
| server/c_object.c | `sack_can_hold`, `pick_up_object`, `pick_up`, `command_take` | `Pickup.SackCanHold`, `PickUpObject`, `PickUp`, `CommandTake` | ported in the extension (`Gk/Pickup.cs`, lane 5A). Not ported: the `FLAG_WAS_WIZ` branches of `pick_up_object` (no object here is made by a wizard), `stop_item` and `fix_stopped_item` of `pick_up` (they act on arrows and thrown objects only) and its case of no object (C's `op->below`, a square the object model does not have: the caller always gives the object). The object on the ground is a level object: C's `remove_ob` of it is `Dispatcher.RemoveObFromMap` (`npc.remove`); see docs/differences.md "Pick up and drop" |
| server/c_object.c | `drop_object`, `drop`, `command_dropall`, `command_drop` | `Pickup.DropObject`, `Drop`, `CommandDropAll`, `CommandDrop` | ported in the extension (`Gk/PickupDrop.cs`, lane 5A). C's `insert_ob_in_map` of the dropped object is `Dispatcher.InsertObInMap` (`npc.place`); not ported: the plugin event, `sell_item` on a shop floor (Graal Kingdoms' shops are scripts), the `SAVE_INTERVAL` save (config.h has it off) and the re-insertion of the player (he is no object of a map here). `apply_special` and `put_object_in_sack` are wave 6's: `Dispatcher.ApplySpecialUnapply` and `Dispatcher.PutObjectInSack` are where they plug in |
| server/c_object.c | `put_object_in_sack` | `Pickup.PutObjectInSack` | ported in the extension (`Gk/Container.cs`, lane 6A). The recursion for a container with contents is `PutInSack`; the target stands in for a null `op->container` (differences.md); a whole stack given as a count is no part of it |
| socket/item.c | `esrv_move_object` | `Pickup.EsrvMoveObject` | ported in the extension (`Gk/Container.cs`): the object and the target container are found by the dispatcher in the player's own tree; C's drop to the ground (`to == 0`) is `muddropinvobject`'s, and the "window" of Graal Kingdoms' client takes its place for the pick up to the pack |
| server/apply.c | `esrv_apply_container` | `Pickup.EsrvApplyContainer` | ported in the extension (`Gk/ContainerApply.cs`). Not ported: the `EVENT_CLOSE` of the plugins (no plugins), and `esrv_send_inventory` (the flush looks at the whole window) |
| server/apply.c | `apply_container` | none | not ported: C's version for a player without a client window; `manual_apply` calls `esrv_apply_container` for a player and the other version answers 0 for any other object, which the apply of lane 6A does too (it does nothing) |
| server/apply.c | `manual_apply`, the cases `CONTAINER` and `CLOSE_CON` | `Dispatcher.ApplyWave6A` | ported in the extension (`Dispatcher.Wave6A.cs`; the rest of `manual_apply` is lane 6B's) |
| server/player.c | `find_key` | `Pickup.FindKey` | ported in the extension (`Gk/ContainerApply.cs`); `pl->contr->usekeys` is `Pickup.UseKeys`, always `key_inventory` (the command is not ported). The use of the key (`player_attack_door`, `apply_key`) is not ported: no object of this world is a door |
| server/apply.c | `check_weapon_power`, `player_apply`, `manual_apply`, `apply_special` | `Apply.CheckWeaponPower`, `PlayerApply`, `ManualApply` and `DefaultApply`, `ApplySpecial` | ported in the extension (`Gk/Apply.cs`, lane 6B). `manual_apply` is split where its plugin hook is: `ManualApply` is the whole function with the script's `onApply` in the hook's place, `DefaultApply` the switch without it (what `defaultApply` runs). The cases of the types that lanes 6A, 6C and 6D own are theirs (`Dispatcher.ApplyByLane`); the cases for exits, scrolls, spell books, skill scrolls, rods, horns, skill tools, the improvers, the clock, the shop menu, the power crystal and the save bed say "That is not available yet." and change nothing (they need map or phase 4 code); levers and handles do nothing (no buttons). Not ported: the `FLAG_WAS_WIZ` branch of `player_apply`, GROS's `current_weapon_script` (plugins), `chosen_item_spell`, `known_spell` and `last_value` of a readied wand or rod (the casting is phase 4), `player_apply_below` on the squares of a map (only the open container). `can_apply_object` and `AP_NOPRINT` do not exist at this commit. See docs/differences.md "Apply" |
| server/c_object.c, server/apply.c | `command_apply`, `player_apply_below` | `Apply.CommandApply`, `PlayerApplyBelow` | ported in the extension (`Gk/ApplyCommand.cs`, lane 6B); wave 7's `mudcommand` table calls `CommandApply` |
| server/c_object.c | `pick_up_object` (the weight test only) | `Inventory.TooHeavy` | ported in the extension (`Gk/Inventory.cs`: his weight and what he carries plus the new weight against `weight_limit[Str]`; the rest of the function is lane 5A's) |
| common/object.c | `present_arch_in_ob` | `Inventory.PresentArch` | ported for every stack of the archetype (`removeinvobject` takes from them in order) |
| server/skill_util.c | `show_skills` (the level of a skill) | `Inventory.SkillLevel` | the level of the experience object, as that function's comment says |
| server/c_object.c | `command_examine`, `examine`, `examine_monster`, `long_desc` | `Examine.CommandExamine`, `ExamineObject`, `ExamineMonster`, `LongDesc` | ported in the extension (`Gk/Examine.cs`); every `new_draw_info` is `CrossfireHost.DrawInfo`; `LOOK_OBJ` is written out; `inventory`, `draw_find` and the rest of c_object.c are not ported |
| socket/item.c | `MarkItem` | `ItemMatching.MarkItem` | ported with the object already found (the client's tag is looked up by the caller, in the player's own tree) |
| server/player.c | `fix_weight` | `ItemMatching.FixWeight`, `Dispatcher.FixWeight` | ported in the extension: for the players it is given; the extension calls it for a player at login (C calls it from its tick) |
| socket/item.c | `query_flags` | `ItemStrings.QueryFlags` | ported in the extension (`Gk/ItemStrings.cs`); `ItemStrings.Flags` adds Graal Kingdoms' types 162 to 166 and `F_NOPICK` (docs/differences.md) |
| socket/item.c | `esrv_send_item`, `esrv_update_item`, `esrv_del_item` | `CrossfireHost.EsrvSendItem`, `EsrvUpdateItem`, `EsrvDelItem` | not ported: the client's packets. The extension's host answers each call by marking the item in the player's `ShownTable`, and one flush at the end of the request writes the item strings |
| socket/info.c, common/glue.c | `new_draw_info`, `new_draw_info_format` | `CrossfireHost.DrawInfo` | not ported: the extension's host sends the text to the player's script (docs/extension.md); flags and colour are not passed |
| socket/item.c | `esrv_send_inventory`, `esrv_draw_look`, `esrv_apply_container`, `esrv_get_ob_from_count`, `ExamineCmd`, `ApplyCmd` | none | not ported: packets of the client; the item strings and the id scope of the extension (docs/extension.md) take their place |
| common/treasure.c | `load_treasures`, the treasure lists | `CrossfireHost.LoadTreasures`, `FindTreasureList` | not ported here (the treasure phase); the default host knows every name |
| common/living.c | `fix_player` | `CrossfireHost.FixPlayer` | not ported: the player's stats are the next phase |
| common/treasure.c | `load_treasures`, the treasure lists | see "common/treasure.c" below | the lists are read from `CfWorld.TreasureText` when it is set; without it the default host knows every name |
| include/player.h, include/map.h | `struct pl`, `mapdef` | `IPlayerHandle`, `IMapHandle` | not ported: markers that the owner implements |

### common/treasure.c, include/treasure.h (the treasure lists, the subset phase 3 needs)

| C function | C# member | Status |
| --- | --- | --- |
| `treasure`, `treasurelist`, `GT_*` | `Treasure`, `TreasureList`, `GT` | ported |
| `load_treasure` (static), `check_treasurelist` (static), `load_treasures` | `CfWorld.LoadTreasure` (private), `CheckTreasurelist` (private), `LoadTreasureLists(text)` | ported (the text takes the place of the file; `treasure`, `treasureone`, `arch`, `list`, `change_name`, `change_title`, `change_slaying`, `chance`, `nrof`, `magic`, `yes`, `no`, `more`, `end`; `sscanf`'s true-on-EOF and the stale word of `variable` are kept) |
| `find_treasurelist` | `CfWorld.FindTreasurelist` | ported (the lists read from the text answer; the first of a name is the one found) |
| `put_treasure` (static), `change_treasure` (static) | `CfWorld.PutTreasure` (private), `ChangeTreasure` (private) | ported; `monster_check_apply_func` is `CrossfireHost.MonsterCheckApply` |
| `create_all_treasures`, `create_one_treasure`, `create_treasure` | `CfWorld.CreateAllTreasures`, `CreateOneTreasure`, `CreateTreasure` | ported (a list or archetype that does not exist, which C reads through a null pointer, makes nothing; `abort()` is a `CrossfireFatalException`) |
| `fix_generated_item` | `CfWorld.FixGeneratedItem` | ported for the `GT_STARTEQUIP` branch and `fix_flesh_item` only (docs/differences.md "Treasure lists") |
| `fix_flesh_item` | `CfWorld.FixFleshItem` | ported |
| `init_archetype_pointers`, `generate_treasure`, `magic_from_difficulty`, `set_abs_magic`, `set_magic`, `set_ring_bonus`, the artifacts (`load_artifacts`, `generate_artifact`, ...), `dump_*`, `free_*` | none | not ported: random magic and artifacts (phase 4), debugging dumps |

### server/player.c, common/living.c, common/utils.c, server/skill_util.c, include/skillist.h (the start path, `Crossfire.Extension/Gk`)

| C function | C# member | Status |
| --- | --- | --- |
| player.c `roll_stat`, `roll_stats` | `Start.RollStat`, `Start.RollStats` | ported (with `USE_SWAP_STATS` and `SORT_ROLLED_STATS` of config.h; the stats are in `Player.OrigStats`; `add_exp(op, 0)` is `LivingRules.AddExpNone`) |
| player.c `give_initial_items` | `Start.GiveInitialItems` | ported (`start_spells` and `start_prayers` as the numbers of `enum spellnrs`; `pl->randomitems != NULL` is a test of the list that is passed; `known_spells` is `Player.KnownSpells`) |
| player.c `key_change_class` | `Dispatcher.SetMudClass` (private) | the order of its `d` branch (`init_player_exp`, `give_initial_items`, `link_player_skills`) with the class object put into the tree; the stat bonus part (`add_statbonus`) is ported as `LivingChange.AddStatbonus` but not called: GK's class is an object whose stats `fix_player` adds |
| player.c `get_player`, `check_login`, `save_player`, `Roll_Again`, `Swap_Stat`, `remove_unpaid_objects`, the rest | none | not ported: the client's dialogue, the login of Crossfire's players (the extension reads the account its own way) |
| living.c `fix_player` | `FixPlayer.Fix` (called by `GkHost.FixPlayer`) | ported for players (wave 4D: the stats, the life values, the weight limit; wave 5D: the resistances, ac, wc, damage, speed, `weapon_sp`, light, flags, regeneration, digestion, `SPELL_ENCUMBRANCE` and `SEARCH_ITEMS` as `Player.Encumbrance` and `Searching`). Not ported: the part for monsters (a monster is left as it is), `PLUGINS` (`current_weapon_script`) |
| living.c `change_abil`, `change_attr_value`, `add_statbonus`, `remove_statbonus`, `allowed_class` | `LivingChange.*` | ported; `check_walk_on` of a floating-down player is left out (no squares); the messages go through `Host.DrawInfo` without `NDI_BLUE` |
| living.c `dex_bonus`, `speed_bonus`, `dam_bonus`, `thaco_bonus`, `max_carry`, `gain_msg`, `lose_msg` | `LivingTables.*` | ported |
| skill_util.c `change_skill`, `check_skill_to_apply` | `SkillTable.ChangeSkill`, `SkillTable.CheckSkillToApply` | ported; `apply_special` is a delegate (`SkillTable.ApplySpecial`) that wave 6B sets |
| living.c `check_stat_bounds`, `level_exp`, `player_lvl_adj`, `con_bonus`, `sp_bonus`, `grace_bonus`, `levels` | `LivingRules.*` | ported (`settings.simple_exp` is off; `player_lvl_adj` leaves out the dragon's gifts and the "You are now level" messages) |
| living.c `add_exp` | `LivingRules.AddExpNone` | the branch for 0 experience only |
| utils.c `die_roll` | `LivingRules.DieRoll` | ported |
| skill_util.c `init_new_exp_system`, `init_exp_obj`, `link_skills_to_exp`, `check_link`, `read_skill_params`, `lookup_skill_by_name` | `SkillTable.Create` and its private parts | ported (see docs/differences.md for a world with no or too many experience archetypes) |
| skill_util.c `init_player_exp`, `link_player_skills`, `link_player_skill`, `unlink_skill` | `SkillTable.InitPlayerExp`, `LinkPlayerSkills`, `LinkPlayerSkill`, `UnlinkSkill` | ported |
| skill_util.c `find_skill_exp_name` | none | not ported: it fills the client's skill experience statistics |
| skill_util.c `do_skill`, `check_skill_to_apply`, `change_skill`, `calc_skill_exp`, `learn_skill`, ... | none | not ported (wave 5D and phase 4) |
| skillist.h `skills[]` | `SkillTable.Skills` | ported |
| lib/treasures, lib/skill_params | `src/Crossfire.Extension/Data/treasures`, `skill_params` | the files of 8b7ee7f90, unchanged, carried as resources; a file of the same name in the world's folder replaces them |

### common/item.c (names and descriptions of items)

| C function | C# member | Status |
| --- | --- | --- |
| `numbers`, `numbers_10`, `levelnumbers`, `levelnumbers_10` | private tables of `CfWorld` | ported (the C spellings "fourty", "eighteen", "nineteen" stay) |
| `describe_resistance` | `CfWorld.DescribeResistance` | ported (both forms: "(resist fire +30)" and "resist fire 30" lines) |
| `query_weight` | `CfWorld.QueryWeight` | ported (`%6.1f` through `CStr.FormatFixed`: ties go to the even digit as in glibc) |
| `get_levelnumber`, `get_number` | `CfWorld.GetLevelnumber`, `GetNumber` | ported (a negative number gives "": C reads outside its table) |
| `ring_desc` | `CfWorld.RingDesc` | ported (`DESCRIBE_PATH_SAFE` with the 1024 buffer) |
| `query_short_name` | `CfWorld.QueryShortName` | ported (4096 buffer, `FULL_RING_DESCRIPTION` is defined as in config.h; the spell name is `CrossfireHost.SpellName`) |
| `query_name` | `CfWorld.QueryName` | ported (the five rotating static buffers are not needed for strings) |
| `query_base_name` | `CfWorld.QueryBaseName` | ported (256 buffer) |
| `describe_monster` (static) | `CfWorld.DescribeMonster` (private) | ported; `contr->digestion`, `gen_grace`, `gen_sp`, `gen_hp` are `CrossfireHost.PlayerDigestion`, `PlayerGenGrace`, `PlayerGenSp`, `PlayerGenHp`; the `ABILITY` items of `randomitems->items` are `CrossfireHost.TreasureAbilityNames` (the treasure phase) |
| `describe_item` | `CfWorld.DescribeItem` | ported |
| `is_magical`, `need_identify` | `CfWorld.IsMagical`, `NeedIdentify` | ported |
| `look_up_spell_name` | `CfWorld.LookUpSpellName` | ported; the `spells[]` table is `CrossfireHost.SpellName` (the spell phase; the default host has no spells) |
| `identify` | `CfWorld.Identify` | ported; `update_object` is `CrossfireHost.UpdateObject`, `esrv_send_item_func` is `CrossfireHost.EsrvSendItem` |
| `material[]` | `Tables.Material` | ported (earlier phase) |

### server/shop.c (prices and the texts of money)

| C function | C# member | Status |
| --- | --- | --- |
| `coins`, `NUM_COINS` | private `CfWorld.Coins`, `NumCoins` | ported |
| `query_cost` | `CfWorld.QueryCost` | ported (32-bit `int` arithmetic wraps; `spells[].charges` of a wand is `CrossfireHost.SpellCharges`; `cha_bonus` is `Tables.ChaBonus`; `find_skill`, `isqrt` below) |
| `find_next_coin` (static) | `CfWorld.FindNextCoin` (private) | ported |
| `cost_string_from_value` | `CfWorld.CostStringFromValue` | ported (returns the text; no static buffer) |
| `query_cost_string` | `CfWorld.QueryCostString` | ported |
| `query_money` | `CfWorld.QueryMoney` | ported |
| `pay_for_amount`, `pay_from_container` | `Money.PayForAmount`, `Money.PayFromContainer` | ported in the extension (`Gk/Money.cs`); the client calls are `CrossfireHost`'s hooks; the `FLAG_WAS_WIZ` lines (they name a variable the function does not have) are left out; the coin archetypes are looked up first (C would stop on a missing one) |
| `sell_item` | `Money.AddCoins` | its coin loop (lines 538 to 570) is ported in the extension; the texts ("You receive ...", "We're not interested in ...") and `identify` are not: a script says them |
| `pay_for_item`, `get_payment2`, `get_payment` | none | not ported: they pay for the unpaid objects of a shop (the shop floor is Graal Kingdoms' scripts' own) |
| `shop_sort`, `add_shop_item`, `shop_listing` | none | not ported: the listing of the shop's map squares to a player |

### What the two files above call (other files)

| C file | C name | C# member | Status |
| --- | --- | --- | --- |
| include/define.h | `safe_strcat` | `CfWorld.SafeStrcat` | ported, with the C slip `*curlen=maxlen=1` (the length restarts at 1 when the text overflows) |
| include/define.h | `DESCRIBE_ABILITY`, `DESCRIBE_PATH`, `DESCRIBE_PATH_SAFE` | `CfWorld.DescribeAbility`, `DescribePath`, `DescribePathSafe` | ported (`DESCRIBE_ABILITY_SAFE` is not used by the code of this phase) |
| include/define.h | `ARMOUR_SPEED`, `ARMOUR_SPELLS`, `WEAPON_SPEED`, `FABS` | `CfObject.LastSp`, `LastHeal`, `Math.Abs` | ported (used in place) |
| include/skills.h | `enum skillnrs` | `Define.SK_STEALING` .. `Define.SK_LEVITATION` | ported (the `skills[]` table is not) |
| common/living.c | `get_attr_value`, `cha_bonus` | `CfWorld.GetAttrValue`, `Tables.ChaBonus` | ported |
| common/player.c | `find_skill`, `atnr_is_dragon_enabled`, `is_dragon_pl` | `CfWorld.FindSkill`, `AtnrIsDragonEnabled`, `IsDragonPl` | ported |
| common/porting.c | `isqrt` | `CfWorld.Isqrt` | ported (64-bit sum, see differences) |
| (C runtime) | `%+d`, `%.Nf` | `CStr.Plus`, `CStr.FormatFixed` | not ported from a Crossfire file; the behaviour of `printf` that item.c relies on |
| include/spells.h | `spells[]` (`name`, `charges`, `level`) | `CrossfireHost.SpellName`, `SpellCharges`, `SpellLevel` | not ported: the spell table is a later phase |
| include/player.h | `struct pl` (`digestion`, `gen_*`) | `CrossfireHost.PlayerDigestion` .. | not ported: the player is the next phase |

## How to read the table

The base is Crossfire's server at commit `8b7ee7f90` (2002-06-27). This page is the table of the files and functions the object model
and the archetypes need. **Ported** means the C# member does what the C function does (the same results, the same order of effects,
the same messages) and names the C file and function in its XML doc comment. **Not ported** means the C function is left out, or
its place is an interface, and the table says why. A deliberate difference of a ported function is in
[differences.md](differences.md).

Where the C code calls a part of Crossfire this repository does not hold (maps, the players and their client, treasures, items), the
call goes to a virtual member of `CrossfireHost` (the "host" below); its default does nothing, or what C does when that part is
not there. The owner of the object model (the Alpaxis GameServer) derives from it.
