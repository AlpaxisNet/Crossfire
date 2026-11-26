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
| `find_best_weapon_used_match`, `item_matched_string` | `CrossfireHost.FindBestWeaponUsedMatch` | not ported: need the player's count (c_object.c's `count`; `query_name` is `CfWorld.QueryName` now); the loader calls the host when `current_weapon_script` is set |
| `init_archetypes` | `CfWorld.InitArchetypes(text)` | ported |
| `arch_info` | `CfWorld.ArchInfo` | ported (returns the text; C sends it to the player with `draw_info`) |
| `clear_archetable`, `init_archetable` | `CfWorld.ClearArchetable`, `InitArchetable` | ported |
| `dump_arch`, `dump_all_archetypes` | `CfWorld.DumpArch`, `DumpAllArchetypes` | ported (return the text) |
| `free_all_archs` | none | not ported: garbage collection |
| `get_archetype_struct` | `CfWorld.GetArchetypeStruct` | ported |
| `first_arch_pass` | `CfWorld.FirstArchPass` | ported |
| `second_arch_pass` | `CfWorld.SecondArchPass` | ported (including `fgets` with a buffer of `MAX_BUF`) |
| `check_generators` | `CfWorld.CheckGenerators` | ported |
| `load_archetypes` | `CfWorld.LoadArchetypes(text)` | ported; the text takes the place of the file, `load_treasures` is `CrossfireHost.LoadTreasures` |
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
| common/living.c | `fix_player` | `CrossfireHost.FixPlayer` | not ported: the player's stats are the next phase |
| common/treasure.c | `load_treasures`, the treasure lists | `CrossfireHost.LoadTreasures`, `FindTreasureList` | not ported here (the treasure phase); the default host knows every name |
| include/player.h, include/map.h | `struct pl`, `mapdef` | `IPlayerHandle`, `IMapHandle` | not ported: markers that the owner implements |

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
| `pay_for_amount`, `pay_for_item`, `pay_from_container`, `get_payment2`, `get_payment`, `sell_item` | none | not ported: they take coins out of inventories and send the changes to the client of Crossfire (`esrv_del_item`, `fix_player`, `draw_info`, the shop map squares); the price they use is `QueryCost` |
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
| include/spells.h | `spells[]` (`name`, `charges`) | `CrossfireHost.SpellName`, `SpellCharges` | not ported: the spell table is a later phase |
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
