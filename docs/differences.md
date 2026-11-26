# Differences from Crossfire's C code

Every difference is deliberate and has its reason. Everything else is meant to behave as the C code of commit `8b7ee7f90` does,
quirks included (a few of those are listed at the end, so that nobody "fixes" them).

## Keys the loader does not know are kept (Graal Kingdoms)

The scanner of `common/loader.l` ends with the rule `.*`, which takes a line no other rule matched: it logs
`Unrecognized string: <the line>` as an error, sets `lex_error`, and the line is lost; `lex_load` then returns -1 instead of
`LL_EOF` when it meets the end of the text (a block ended by `end` returns `LL_NORMAL` all the same). The port does all of that, and
two things more. Graal Kingdoms' archetypes have such lines, and a game server needs them: `graalnpctype` (1,325 objects) and `graalnpcparam` (700) decide the class and the look of an object,
`flag gender=male` and `flag arms=...` (218, the key repeats in a block), `hpbuff`, `spbuff`, `gracebuff`, `castingtime`,
`restrictaccess`, `nopeaceful` and others. **The port keeps every such line on the object**: `CfObject.Extra` is an `ExtraKeys`, a list
of (key, value) pairs in file order; a key may repeat; keys are compared without regard to case (the scanner is case-insensitive)
but the spelling of the file is kept; the value is read as the C `yval` reads one (the blanks around it are cut). `copy_object`
copies them with the object, `clear_object` clears them, `get_ob_diff`/`save_object` write the ones an archetype does not have, and
`CfWorld.UnknownKeys` counts them by key. Comment lines (`#`), empty lines and lines of blanks are not kept (a line of blanks is an
unrecognized line in C as well). And **a key the host's loader reads** (`CrossfireHost.IsLoaderKey`, false for every key by
default) is no error: Graal Kingdoms' server had rules for its own keys, so its host names them (`graalnpctype`, `graalnpcparam`,
`flag`, `hpbuff`, `spbuff`, `gracebuff`, `castingtime`, `restrictaccess`, `nopeaceful`) and only its misspelled keys and broken
messages (below) are logged.

Three consequences that follow from C's rules, not from the port: `msg <edit msg>` (three blocks of `Weapon/Sword/graalswords.arc`) is
not the start of a message (C needs `msg` alone on its line), so its text lines are unknown lines too and `endmsg` an unknown key; a
key spelled as the loader has no rule for it (`resist_blindness`, `resist_life_stealing` in this version, `berserk`) is unknown;
`flag` and the others above are simply not Crossfire keys.

## Map files (common/map.c) and the player archetype

- **The port holds no maps.** `load_objects` hands each object to `CrossfireHost.InsertObInMap` and `get_map_ob` is the host's; the
  squares are not kept (`link_multipart_objects`, which walks them, is not ported); `save_objects` takes the squares as lists the host
  supplies. The header is `MapHeader`, read by `load_map_header` and written by the header part of `new_save_map`; opening and
  naming the file, compression and the file of unique items are the host's.
- **The tile paths** of a header are kept as written: C runs `check_path` and, when the file is not there, tries the path relative to
  the map's own, which needs the file system.
- **`get_player_archetype`** returns null and logs "No Player achetypes" where C calls `exit(-1)`; C also loops for ever when it is
  called with NULL and there is no player archetype, which the port does not.
- **`load_map_header` and the message**: C copies `msgbuf` even when it holds garbage; the port keeps a message only when it read one
  (the empty message of C is not kept either).

## Graal Kingdoms' map files (the extension, not the port)

Graal Kingdoms' `mudlevels` files are Crossfire's map format with these differences, all kept by the extension's reader and writer
(`LevelFiles`) and not by the port:

- **Positions are floats** (`x 3.328125`): Crossfire's `x` and `y` are integers and its rules read them with `atoi`. The extension
  turns the top block's `x` and `y` into `mudx` and `mudy` before the port reads the block and keeps the numbers as doubles; what the
  integer fields of the object hold is the archetype's, and a saved file writes the floats.
- **`flag name=value` lines** are the object's script variables (Graal Kingdoms' loader had a rule for the key `flag`); the extension
  keeps them with the object until the host gives its own state, then writes that as `mudblob`.
- **A float is written as C writes it** (`%f`, 6 decimals): `speed_left -0.100000023` comes back as `-0.100000`.
- **Inventory order.** `insert_ob_in_ob` puts an object read at the head of the inventory and `save_object` writes the inventory from
  the head, so C reverses an inventory at every save and load. The extension reverses it once after each load (`Inventories.RestoreFileOrder`)
  so that a round trip keeps the order of the file.
- **Merging.** `insert_ob_in_ob` merges what can merge (`CAN_MERGE`): two identical nested objects of a seed become one with a larger
  `nrof`, as in C.

## Gmaps (the extension, not the port)

The rules of protocol 1.1 section 7.1 are the extension's own (docs/extension.md, "Gmaps"). Where the protocol leaves a choice, Alpaxis chose:

- **The level an `npc.pos` row or a `player.pos` answer names, for a script on a gmap with a layout.** The position is translated into the
  gmap's tiles, and the level is the gmap's name, so that the level and the position a script reads belong together (a script on a gmap
  counts every position in the gmap's tiles). The protocol says only that the position is translated.
- **A refused `gmap.layout` forgets the gmap's earlier layout** (the text says the extension "keeps no layout of that gmap").
- **Names are level keys**: a segment's name and the gmap's own name are folded to lower case, and every level name the extension sends
  is in that form.

## Language and runtime

- **Strings.** The C code shares strings through `common/shstr.c` (`add_string`, `free_string`, `add_refcount`) and compares them by
  pointer. The port uses .NET strings and compares them by value, which gives the same answers (shared strings are equal exactly
  when their text is). `add_string` and friends are not ported. Text is read with one char per byte (`CStr.Latin1`), as C reads
  bytes, so no byte of a file is changed.
- **`signed long` is 32 bits** (`weight`, `carrying`, `weight_limit`, `move_status`), as on Windows and on the 32-bit machines the code
  was written for, and `atol`/`atoi` saturate at the range of `int`. On 64-bit Linux C would keep 64 bits and `dam 100000000000` would
  read as another number; Graal Kingdoms' one such value (a test archetype) reads as -1 here (the `sint16` of `dam` takes the low bits of
  2147483647). `unsigned long` of `hasharch` is 64 bits (it only changes which slot a name gets).
- **No free list.** C keeps the freed objects of `objarray` and `free_objects` and gives them out again; `get_object` here makes a new
  object and `free_object` lets it go (it is marked `FLAG_FREED` with count 0, as C marks it, so `was_destroyed` and `get_owner` see
  it). `expand_objects`, `count_free`, `nrofallocobjects`, `STARTMAX` and `OBJ_EXPAND` are not ported.
- **`abort()` and `fatal()`** (removing a removed object, a full archetype table) throw a `CrossfireFatalException`.
- **`RANDOM()`** is `CfWorld.Random`, a delegate (default: a seeded `System.Random`), so that a run can be made to repeat.
- **`LOG(...)`** is `CfWorld.Log`: a delegate, and a count per level (`LogCounts`).
- **Files** become text: `load_archetypes` takes the text of the `archetypes` file (what `ArchCollector.Collect` or collect.pl makes), `init_anim`
  the text of the `animations` file, `load_object` a `LoadSource`; `save_object` writes into a `StringBuilder`; `dump_object` returns the text.
  `load_object_str` reads the string instead of a temporary file.

## Names, descriptions and prices (common/item.c, server/shop.c)

- **Strings, not static buffers.** `query_name`, `query_short_name`, `query_base_name`, `describe_item`, `ring_desc`, `describe_resistance`,
  `describe_monster`, `query_weight`, `get_levelnumber`, `get_number` and `cost_string_from_value` return strings; C returns a pointer into a
  static buffer (`query_name` cycles through five). The text is the same; the buffers' sizes are kept where they cut the text
  (`safe_strcat` with 4096 for `query_short_name` and `query_name`, 1024 for `ring_desc`, 256 for `query_base_name`), and the slip in
  `safe_strcat` (`*curlen=maxlen=1`: after an overflow the length is 1, not the buffer's size, so the next piece is written from the
  second character on) is ported as it is. The pieces that C writes with `strcat` and `sprintf` and no check (`describe_item`,
  `describe_monster`, the stat pieces of `ring_desc`) are not cut: C overflows its 1024 buffer there, which no object reaches.
- **`%f` is exact.** `query_weight` (`%6.1f`) and `describe_item` (`%1.2f`) go through `CStr.FormatFixed`, which rounds the exact binary value with
  ties to the even digit as glibc does (.NET would round 0.25 to "0.3", C prints "0.2"). `%+d` is `CStr.Plus`.
- **A stack without a plural name** (`nrof` other than 1, `name_pl` NULL): C copies a NULL pointer and crashes. `query_short_name` logs an
  error and uses the empty string. (Objects read by the loader always have one: the `name` rule of `loader.l` sets it to the name when there is none.)
- **`spells[]`** is the host's (`CrossfireHost.SpellName`, `SpellCharges`). The default host has no spell table: names are "" and a wand
  holds `max(1, 0)` charges, so a scroll shows as "scroll of  (lvl 3)" until the spell phase supplies the table. A spell number outside the table
  (`stats.sp` is -1 after `identify` of an unknown spellbook) is "" where C reads whatever lies before the table.
- **A player without a controller** (`op->contr` NULL, type `PLAYER`) shows no regeneration pieces in `describe_item`; C dereferences NULL.
- **`get_levelnumber` and `get_number` of a negative number** are "" (C reads before its tables).
- **`isqrt`** keeps its sum in 64 bits: with the C `int` an argument near 2^31 would wrap the sum and never end.
- **`query_cost` and a charisma outside the table** (a player's `Cha` under 0, or over 30 without the bargaining skill cap, e.g. 40):
  C reads outside `cha_bonus`; the port takes it as the C code's own "illegal charisma bonus" case (an error is logged, `diff` is 0.9).
  The error text prints the bonus as an integer; C prints a `float` with `%d`, which is garbage.
- **`cost_string_from_value`** with a coin archetype of value 0 divides by zero as C does (a `DivideByZeroException`); the coins of
  Crossfire's archetypes never have value 0.
- **Quirks kept on purpose** (do not "fix"): `query_base_name` of a plain item returns the singular name even when the plural was asked for;
  an object with `nrof` 0 and a weight uses its plural name; `query_cost` truncates `4.0*(float)val*(1.0-diff)` (a sale at charisma 30 of
  1000 gives 3199, not 3200); `query_cost` of a wand with an unknown spell divides by 1; "Spell forumla for" is the C spelling; `cost_string_from_value` says
  "nothing" when the platinum coin archetype is missing even if gold and silver exist.

## Places where C would crash or read garbage

- `editable` on an object that has no archetype (a nested block inside an archetype: its archetype cannot be found yet, as the table is
  empty during the first pass) logs an error instead of dereferencing NULL (Graal Kingdoms: one such block).
- A block with `More` before any archetype (no head to attach to) logs an error and is taken as an archetype.
- An archetype without a name (an `end` line without an `Object` line before it: 4 in Graal Kingdoms' folder) is put in the hash table
  under the empty name; C hashes a NULL pointer.
- `empty_archetype` missing (Graal Kingdoms has none): `EmptyArchetypeOrBlank` makes a blank archetype so that `dump_object` and
  `save_object` work; C dereferences NULL.
- `get_ob_diff` keeps the text of its last event line in a local variable, not a `static` that survives from call to call; the
  repeated line it writes for an event index that has no name (C quirk, below) is the same within a call.
- `GetSplitOb` of an object that is on no map and in no environment treats the missing map as "not in memory".
- A face number that `FindFace` returns for an integer name and that the table does not have gives face 0 (`new_faces[i]` is outside
  the array in C).

## Additions that C does not have

- **Objects of nested blocks in an archetype stay.** `copy_object` copies no inventory, so the objects of an `arch ... end` block inside
  an archetype (Graal Kingdoms' `Test/` has two) are lost in C when the block is copied into the archetype's clone and the working object is
  cleared. `first_arch_pass` moves them to the inventory of the clone (`at.Clone.Inv`, their `Env` the clone). `arch_to_object` still
  copies no inventory, as in C: whoever makes an object from such an archetype reads `Clone.Inv`.
- **Faces by name.** C finds a face in the `bmaps` file that collect.pl writes from the pictures, and an unknown name gives face 0 with a
  message. The port has no pictures: `FaceTable.AutoRegister` (the default) gives a name a number the first time it is seen, so no face name
  of an archetype is lost; with it off `FindFace` returns its `error` argument as C does. Number 0 is `bug.111`, 1 `blank.111` (the face of a
  cleared object), 2 `empty.111`.
- **Treasure lists.** `CfWorld.TreasureText` (the text of the treasures file; the extension supplies it) is read by `load_treasures`
  between the two archetype passes; the lists then answer `find_treasurelist` and a name that is not one gets C's "Couldn't find
  treasurelist" message. Without a text `CrossfireHost.FindTreasureList` makes a `TreasureList` for any name, so that the `randomitems` of
  an archetype keeps its name; the cases of C stay: `randomitems none` is no list (and the second pass logs "Failed to link treasure to
  arch", 48 times in Graal Kingdoms' folder) and nothing is set while the first pass runs.
- **`fix_generated_item` is partly ported.** Of the function only the `GT_STARTEQUIP` branch and `fix_flesh_item` are: random magic
  (`set_magic`, so the `magic` of a list element is not used for a bonus), the artifacts (`generate_artifact` and the 1 in 20 roll for one,
  which also takes a `RANDOM()` that is not taken here) and what the type switch gives rings, amulets, wands, rods, scrolls, books,
  spellbooks, potions, runes, bracers and cursed weapons or armour are not (phase 4: they need the spell table, `readable.c` and
  `artifact.c`). An item of those types comes out as its archetype is. The starting items of the six class lists are not touched by it
  except the amulet and the books of the priest and the casters, which are as the archetypes have them.
- **A treasure element without archetype or list**, or a list that does not exist, makes nothing: C reads the null pointer
  (`t->item->clone`, `t->total_chance`) and crashes after logging the error.
- **`ArchCollector`** is collect.pl's archetype half in C#: the archetype folder to the text of the two files. `anim ... mina` blocks become
  an `animation` line and an entry of the animations file; the colour lines (`color_fg`, `color_bg`, `visibility`, `magicmap`) are removed.
- **`CrossfireHost`** (the host) and the marker interfaces `IPlayerHandle`, `IMapHandle`, `ISpellHandle`: see [porting.md](porting.md).

## Items of a player in the extension (Graal Kingdoms; lane 4C of phase 3)

These are choices of Alpaxis, not of Crossfire, and they are in the extension (`Gk/ItemStrings.cs`, `Dispatcher.Items.cs`).

- **The item string has the weight of the whole stack.** `esrv_send_item` sends the weight of one piece and lets Crossfire's client multiply;
  Graal Kingdoms' Item-Menu prints the field as it is (mud-phase3.md section 5, fact 3a). The field is `nrof * weight + carrying` (an
  object with `nrof` 0 counts once).
- **`F_NOPICK` for sends and updates alike.** C ORs it into the flag word only in `esrv_send_inventory`, and sends the weight -1 for such
  an object. The string has the flag word with the bit for every object with `FLAG_NO_PICK`, and the real weight.
- **Types 162, 163 are drawn as wielded, 164 to 166 as worn.** `query_flags` gives every type of Graal Kingdoms' own (160 to 166) the
  state "applied", which its Item-Menu does not draw on the paper doll (fact 3b). 160 and 161 stay "applied" (they cannot be applied).
- **`mud_weight` is the whole weight the player carries** (his own weight and `carrying`), and `mud_weight_lim` is `weight_limit[Str]`;
  both are written by the flush, in the extension only. Wave 3 wrote `ob->weight` as `mud_weight`, which is a constant of the body.
- **The seven fields, and only the top level and the open container have a string**; the field 6 is 1 inside the open container, 0
  elsewhere. `mud_container` is `<id>,<name>,<picture>` of the open container, and the empty text after it is closed. An invisible object
  (a skill or a force) has no string and is not in `muditems`: it is not an item for a script, as it is not one for the client.
- **`esrv_update_item` writes the whole string** whatever its flags say: the text is compared with what the client holds, so only a change is
  sent.
- **Items made by a script are identified** (`FLAG_IDENTIFIED` at creation): the original network's rule is not known; the shops of
  Graal Kingdoms price and name an item as identified (mud-phase3.md section 3, fact 4).
- **The carried weight is fixed at login.** A file holds no sum and C calls `fix_weight` from its tick, which the extension does not
  have: `Dispatcher.FixWeight` is called for the player at `player.login`.
- **`DrawInfo` is an event.** On a 1.1 link `new_draw_info` is the player event `onRPGMessage(text)` with the `client.text` effect as its
  fallback; on a 1.0 link the effect alone.
- **`mudowner` is not `ob->owner`.** It is an extra line of the object (the account text) or, for an NPC owner, kept in memory; the
  port's saver skips objects that have an `owner`, which would lose every item a player dropped.

## Behaviour of C that the port keeps (so that nobody fixes it)

- **The first of two archetypes with one name wins.** `init_archetable` adds in file order and `find_archetype` returns the first slot
  of the name. (The suite's specification of Graal Kingdoms guessed "the last wins": 58 names occur twice there, e.g. `club`; the
  Crossfire code gives the first in `collect.pl`'s order: the sorted path.)
- A key needs a blank after it and at least one more character (`name ` with one blank is no line of the loader); the value starts at the
  first **space**, not at a tab (`hp<TAB>5` reads 0); trailing spaces are cut, trailing tabs are not.
- Inside a message an empty line is dropped and a line of blanks is kept (`msg`, `A`, an empty line, two blanks, `B`, `endmsg` gives
  "A\n  \nB\n"): the rule `<*>(^{WS}$)|\n` meant to ignore empty lines has `^` and `$` inside parentheses, where flex reads them as
  plain characters, so it ignores only a newline (and a line "^ $"). Checked with flex on the rules of `loader.l`.
- `end`, `More`, `msg` and `endmsg` must stand alone on their line and the line must end with a newline: an `end` that is the last
  bytes of a file, without a newline, is not an end.
- A block without its `end` runs on into the next `Object` line, which renames it (Graal Kingdoms: `thug1` in `Monster/Human/thugs.arc`
  is lost to `thug2`); an extra `end` makes an empty archetype.
- `copy_object` of a negative speed gives `speed_left` a random value (so does the `speed` key), and the first pass then sets
  `speed_left` of every archetype to -0.1.
- `remove_ob` of a stack whose `nrof` has just been set to 0 (`get_split_ob`, taking the whole stack) subtracts the weight of one
  object, not of the stack, from the environment.
- `ObjectCreateClone` copies `carrying` with the head and then adds the weight of the copied inventory: it counts twice.
- `get_ob_diff` writes `run_away 1` and `pick_up 1` for the flags `FLAG_RUN_AWAY` and `FLAG_PICK_UP` (the flag table gives them those
  keywords), which the loader reads back as the numbers `run_away` and `pick_up`, not as flags; and for an event index that has no
  keyword (`EVENT_STOP`, `EVENT_CLOCK`...) it writes the previous event line again.
- `set_owner` loops for ever when two objects own each other and their counts match; only a freed and reused object (counts differ)
  ends it. C's comment says so; the port does not add a guard.
- `item_matched_string` returns 6 for a count above 1 when the plural name is **not** equal to the text (C tests the result of
  `strcasecmp` as a truth value), and sets the player's `count` there; a text that is the plural name falls through to the other matches.

## The start path (`Crossfire.Extension/Gk`)

- **`fix_player` is ported for players** (`FixPlayer.Fix`; wave 5D completed it). A monster is left as it is. `AT_PHYSICAL` (the bit mask 1)
  indexes `prot[]` in the C code of a player that cannot use armour, so the magic resistance (index 1) is what the formula reads there:
  kept as C has it. A player whose stats were
  never rolled (an account of this extension from before the start path) is left as its file has it: C always has `orig_stats` and
  the level arrays, the extension has them only after `roll_stats` (kept as the lines `mudorigstats`, `mudlevhp`, `mudlevsp` and
  `mudlevgrace` of the account file), and then its armour class, speed and resistances are left as they are too, not only the stats.
- **`change_abil`** tells the player through `Host.DrawInfo` (no colour flag), leaves out `check_walk_on` of a player that floats down, and
  keeps the changed rolled stats of a potion with the account (`StoreStats`). `change_skill` cannot apply a skill until wave 6B sets
  `SkillTable.ApplySpecial`.
- **The flush writes the strings of life and stats** (`mud_ac`, `mud_wc`, `mud_dam`, `mud_speed` and the others) when the text changed
  and the player's strings were sent at login: Crossfire sends the whole status to the client at each `fix_player`; the extension
  writes what changed.
- **`give_initial_items` takes the list as an argument** and tests that: C tests `pl->randomitems` and uses `items`; here the player
  archetype has no `randomitems` and the class does.
- **`init_player_exp`**: for a player that has some of the experience objects but not all, C's loop reads past its array and adds copies
  without end; the port adds each missing object once. For more objects than categories it logs and adds none.
- **`link_player_skills`** gives up with an error when the player still has the wrong number of experience objects after
  `init_player_exp` (C calls itself again without end for more objects than categories). A skill number outside the table is a skill
  with no category (C reads past the table).
- **`init_exp_obj`**: C exits when the world has no experience archetype or more than seven; the port logs the error and goes on
  with none, or with the first seven.
- **`player_lvl_adj`** has no dragon part and no messages; `add_exp` has the 0-experience branch only.
- **`setmudclass`, `mudresetplayer` and the account methods** are Alpaxis's own: GK's original is not available (docs/mud-phase3.md
  section 4.7 gives its source). Alpaxis's choices: `mudclass` is the class name without `_class`; choosing a class again takes the
  first choice's items out; `mudresetplayer` clears `mudclass`; `loadmudaccount` with a suffix fails when the file is missing;
  `backupmudaccount` writes `backups/<file>.<UTC time>.txt`; the methods answer a bool where a script might test them.

## Pick up and drop (`Crossfire.Extension/Gk/Pickup*.cs`; lane 5A of phase 3)

- **An object on the ground is a level object.** The object model has no squares: C's `remove_ob` of an object on a map is the
  extension's `npc.remove` and the removal from the level's list, and `insert_ob_in_map` of a dropped object is a new level object
  with an `npc.place` (the same handle the item had; never merged with another object of the square; no script variables, no blob).
  A level object is flagged `FLAG_REMOVED` all the time (the extension never inserts it in a map), so `pick_up_object` takes the branch
  C has for an object that is removed already.
- **`unpaid` is refused by the script functions** `mudpickupobject` and `mudpickupobject2` (false, no text), as `nopick` is: Alpaxis's
  choice (docs/mud-phase3.md section 3, rule 6). C lets a player take an unpaid object and tells him what it will cost; `command_take`
  (which Graal Kingdoms' command table does not have) keeps that.
- **"That item is too heavy for you to put it in your inventory."** is the text for a pick up that is too heavy (docs/mud-phase3.md
  section 4.2); C says "...to pick up.".
- **`drop_object` with a number for an object that is no stack.** C's test `nrof && tmp->nrof != nrof` sends `muddropinvobject(id, x, y, 1)`
  of a sword (`nrof` 0) to `get_split_ob`, which answers "There are only 1 swords."; a script that drops a count of one would never
  drop a sword. A number equal to what the object has (`nrof`, or 1 for an object that is no stack) is the whole object here; a
  larger number goes to `get_split_ob`, which says "There are only N <name>s." and changes nothing.
- **A negative number** (the script's `n` of `mudpickupobject` or `muddropinvobject`) changes nothing and answers false; C's count is
  unsigned.
- **A drop on a level the extension does not hold** (a group or single-player level, a position in no segment of a gmap, a level that
  has the most objects it may) answers false and "You can't drop that here." before anything changes (docs/mud-phase3.md section 3, rule 8).
- **`mudowner`** of a dropped object is the dropping player's account (C has no such field: its `owner` would keep the object out of the
  level file, docs/mud-phase3.md section 3, rule 5).
- **An applied object is unapplied before it is dropped** through `Dispatcher.ApplySpecialUnapply`, which lane 6B sets (C calls `apply_special`); a cursed one stays. Without that hook (a dispatcher that did not run `session.init`) an applied object is not dropped;
  a drop into an open container is `Dispatcher.PutObjectInSack` (lane 6A, "Containers" below).
- **The square of `command_take`** is the level's objects whose tile is the player's (the position from the query `player.pos`),
  the one placed last first; C's chain `op->below`. An object that no `level.load` has shown to the host yet is not on the square.
## Containers (`Gk/Container.cs`, `Gk/ContainerApply.cs`, `Dispatcher.Wave6A.cs`; lane 6A of phase 3)

- **`put_object_in_sack` moves a container's contents into `op->container`.** C reads that pointer without looking: it is null when
  `esrv_move_object` puts a container that holds objects onto a sack and no container is open, and C then crashes in `query_name`. The
  target sack stands in for it. When another container is open, C's mixture is kept: the open one is asked whether each object fits and
  the object goes into the sack that was named.
- **The count of a stack.** A count that is the whole object (its `nrof`, or 1 for an object that is no stack) is no part of it, as in
  `drop_object` (above): `mudmoveinvobject(id, to, 1)` of a sword puts the sword in; C would send it to `get_split_ob`, which says "There
  are only 1 swords.". A larger count is refused by `get_split_ob` with its text after `sack_can_hold` passed.
- **C names a stack by its plural without the number.** "You put the bananas in sack (open) (active)." for four bananas, "You pick up
  the bananas.", "The swords is not a container." for a sword that is no stack (its `nrof` is 0): `query_name` of C, as 5A found for the pick up.
- **`esrv_move_object` has no drop to the ground and no count of the player.** `to` 0 (C: the ground) and -1 are Graal Kingdoms' window
  (the pack), which is `pick_up`; `muddropinvobject` drops. `pl->count`, the one id C sends for the pack, is the tag of the player's own
  handle (the object model's `count` is not a script's id); an item's tag wins when the two are equal. `to` may also be the id of the open
  container, which is the proxy's tag that `mud_container` names, so that a container on the ground can be the target.
- **The weight test of a move out of a sack counts the object twice.** `pick_up_object` adds the weight of what is picked up to what the
  player carries, and the object is part of what he carries already; a player near his limit cannot take an object out of his own sack.
  C does the same; the port keeps it.
- **`usekeys` is `key_inventory` for every player** (C's default, which a player gets when his object is made; the command and the
  saved setting are not ported). A key in a sack of his pack is not used: "The X in your Y vibrates as you approach the door", with no
  colour (`NDI_BROWN` is not carried by `DrawInfo`). The message says "the door" for a locked container too, as C does.
- **Nothing closes an open container when the player walks away** (C's `FLAG_WALK_OFF` sets it up; the host moves the player, not this
  world), and the `EVENT_CLOSE` of the plugins is not run. A container on the ground opened here stays open (and its `applied`, `walk_off`
  and `fly_off` are in the level file) until closed or until the player logs out; after a restart the level object is "applied" with nobody
  holding it open, which `esrv_apply_container` handles as C does (it is cleared and set again by the next open).
- **A container on the ground has no item string and no `findinvobjectbyid` id.** Its tag is the one of its proxy, shown in
  `mud_container` and accepted by `mudmoveinvobject`. A container that is picked up whole while it is open keeps `op->container`: close it first.
- **`apply_container`** (the version of C for a player without a client window) is not ported; see porting.md.

## The inventory functions (`Gk/Inventory.cs`, `Dispatcher.Wave5B.cs`; lane 5B of phase 3)

Choices of Alpaxis where Graal Kingdoms' functions have no original in Crossfire (the names are in `docs/extension.md`, "The inventory
functions").

- **Field [0] of an item string has no comma.** A comma in a name (Graal Kingdoms has an archetype `Evil Master, Bonehead`, and scripts set
  `mudrealname`) would shift the seven comma fields; `ItemStrings.DisplayName` writes it as a semicolon, in the item string and in the
  names `mudlistweapons` and its kin write, which the Item-Menu compares.
- **An object without `nrof` counts one** in `countinvobject`, `mudcountinvobject` and the removals (C's `pick_up_object` does the same:
  `tmp_nrof`); the add sets `nrof` to the count, also for a count of 1.
- **Remove is all or nothing**, from the stacks of the archetype at the top level in the order of the inventory; an absent item is false and
  says nothing (the login hook asks for a flag that most players do not have).
- **`findinvobjectbytitle`** (the original is not known): the first visible object whose `title` is the text, else the first whose
  "name title" is, letters in any case.
- **`mudskilllevel`** is the level of the skill's experience object (C's `show_skills` calls it the one that is always right), else the
  skill's own level; `mudhaveskill` and it match the skill name in any case.
- **The lists of the quick menu** write `<name>,<icon>` entries and an empty end marker with `caller.setprop`, in the order of the
  inventory. What is a weapon (types 15, 14, 162, 163) and what is equipment (applied) is Alpaxis's reading: the original's lists are not
  known. `mudlistspells` writes the spell paths and no spell, because the port has no spell table (a player's spells are numbers).
- **`mudresetinvobject`** is `copy_object` of the archetype's clone over the item: the extra lines (`mudowner`, `mudrealname`) are gone;
  count, identified and applied state are kept. `mudorderinventory` is a relinking of the inventory, saved with the account.
## Money and examine in the extension (lane 5C of phase 3)

- **Division by zero and missing archetypes.** `pay_for_amount` and the coin loop look up the three coin archetypes first and
  refuse (false, an error line) when one is missing or has no value, where C would dereference a null pointer or divide by zero.
  `examine_monster` writes no life line for a monster whose `maxhp + 1` is 0, where C stops with a division by zero.
- **`pay_for_amount`** answers false for a negative amount (C's callers never ask for one) and its `FLAG_WAS_WIZ` lines, which name
  a variable the function does not have, are left out.
- **Kept from C so that nobody fixes it:** "You would get ... for the it." / "for the them." (the C format string); the `@match` test
  of `examine` leaves out only a message that is exactly `@match` (`strncasecmp` with 7 compares the end of the pattern too); `pay_for_amount`
  says true when a coin that is no coin of the shop (a changed value) was not taken apart and less was paid than was asked; a player's price is
  4 times the value times the bargain by charisma (`query_cost`).
- **Identified at creation.** Every object made for a script comes from `Dispatcher.NewItem`, identified, including the coins of
  `mudaddmoney` and the change; the original network's choice is not known (docs/mud-phase3.md 4.3, question 11). An unidentified
  stack of coins and an identified one do not merge on insert (C's `CAN_MERGE`), so a player may hold two stacks of one coin until
  the next payment, which merges them as `pay_from_container` does.
- **`mudvalue` of an archetype** prices one object made of it (identified, `nrof` 1); of an object, the stack as it is. A flag other
  than 0 and 1 is refused (C has `F_TRUE`, which no script uses).
- **`mudobjectattr`** is Alpaxis's: the scripts show the attribute names and the call forms only. `weight` and `value` are of one
  piece, `name` is what the window shows, an archetype is read as one object made of it.
- **`SpellLevel`** is a new member of `CrossfireHost` (like `SpellName`): the level of `spells[]`, 0 until the spell phase.
- **A level that the host loaded is held, empty if it has no file and no seed** (wave 5 integration). The protocol says that a level the
  extension knows nothing of gives no effects; the extension still takes the first `level.load` of a name as the host's announcement of
  that level, so that `muddropinvobject` can place an object in a level where nothing has ever been made. Without it, the first drop in
  every level that has no seed was refused ("You can't drop that here."). Nothing is written for such a level until it holds an object.

## Apply and wearing (`Gk/Apply*.cs`, `Dispatcher.Wave6B.cs`; lane 6B of phase 3)

- **The plugin hook of `manual_apply` is the script's `onApply`.** C asks the object's plugin and, if it answers non-zero, does nothing
  more. Here the extension sends the player event `onApplyItem` (always, with the item) and the item event `onApply` on the item's proxy
  with the invoke of `defaultApply` as its `fallback`; the host runs the class function, which usually calls `this.defaultApply(pl)`,
  and runs the fallback when the class has none (docs/mud-phase3.md section 4.4). `manual_apply` answers 1 at once: the script decides
  later, in a request of its own. On a link of 1.0 (no item events, no `fallback`) the switch runs at once. `apply -a` and `apply -u`
  (`command_apply`) also run at once: the fallback carries no flag.
- **An object on the ground has no `onApply` event.** The manifest gives one name one target, and `onApply` is the item's; GK's faces
  would need it on the NPC (target `object`) as well. `mudapplyobject` of a level object runs `defaultApply` at once (the first line of
  the object's own `onApply`, for example a restricted chest's, is not asked). This is open: it needs a second event name or a host that
  runs one event on both targets (reported to the lead).
- **`defaultApply(pl)` says what `player_apply` says.** Its answer is `manual_apply`'s (0, 1 or 2), and for 0 and 2 it says "I don't know
  how to apply the X." and "You must get it first!", which C's `player_apply` says after the switch.
- **Applies that need code that is not here do not use the object up.** Scrolls, spell books, skill scrolls, rods, horns, skill tools,
  the improvers, the clock, the shop menu, the power crystal, the save bed and exits say "That is not available yet." and answer 1
  (told). Handles and triggers do nothing and answer 1 (docs/mud-phase3.md section 4.4; the map's buttons are not here). Furniture (160)
  and house doors (161, unless lane 6D answers) say "You can't apply that.".
- **Graal Kingdoms' types.** 162 and 163 (tools) are wielded in the place of the weapon: wielding one takes a wielded weapon or tool
  off and the other way round, they set `current_weapon` (so `getwieldedobject` names them) and need `can_use_weapon` and
  `check_weapon_power` like a weapon, but no skill. 164 to 166 (mask, legs, body) are worn, one per type, like armour but without
  `can_use_armour` (clothes are not armour). Both are [I]: nothing shows what Graal Kingdoms' own server did.
- **A weapon's refused wield gives the part of a stack back.** C's `apply_special` splits `nrof - 1` off a stack before it checks the
  skill, and `return 1` after a failed `check_skill_to_apply` leaves the split-off object in no inventory (it is lost). Here it goes back
  into the player's inventory, as it does for the other refusals.
- **The hooks after a change.** `onWeaponWielded`/`UnWielded` (weapons, tools), `onWandReadied`/`UnReadied`, `onArmorWorn`/`UnWorn`
  (the armour types, rings and amulets) and `onClothWorn`/`UnWorn` are player events with the item as typed argument, sent after the
  flag `applied` is set or cleared and after `fix_player` (so `getwieldedobject` and the strings are right); the unapply of the rival
  of a slot sends its hook first, and a taken-off object that merged into a stack gives the stack as the argument. Bows, rods, horns and
  skills have no hook (`mudplayers` declares none). On a link of 1.0 none is sent.
- **The class of an item's type** (`GkSettings.MudTypeName`, `GkClasses.MudTypeClass`) is `mudtype_<name>` when the world has that class,
  else `mudtype_general`. The names are Crossfire's (`weapon`, `potion`, `container`, `sign`, `book`, `scroll`, `skill`, `treasure`,
  `lighter`, `poison`, ...) and the names of Graal Kingdoms' own files (`fireweapon` for a bow, `cfhandle`, `powercrystal`,
  `armourimprover`, `weaponimprover`, `skillscroll`); rings and amulets are `armour`; flesh and drink are `food` [G]; 161 is
  `housedoor`, 162 and 163 `tool`, 164 to 166 `cloth`. The other armour types (helmet, shield, boots, gloves, girdle, bracers, cloak)
  have no class of their own and use the general one, as the rule says; whether Graal Kingdoms' own server sent them to `armour` is
  not known.
- **The unapply of a dropped object and of a skill.** `Dispatcher.ApplySpecialUnapply` (lane 5A's `drop_object`) and
  `SkillTable.ApplySpecial` (lane 5D's `change_skill`) are set at `session.init` and call `apply_special`.

## Consumables (`Gk/Consumable.cs`; lane 6C of phase 3)

`apply_food`, `eat_special_food`, `dragon_eat_flesh`, `apply_poison` and `apply_potion` are ported from `server/apply.c` at 8b7ee7f90 with
C's arithmetic (golden values: `tests/Crossfire.Extension.Tests/golden/consumable_golden.c`). What differs, and why:

- **A potion whose effect needs code the extension does not have is not drunk.** A potion that casts a spell (`stats.sp` is not 0, which
  includes C's "super" stat potions), a cursed potion of restoration (`drain_stat`) and a protection potion (a `force` of type
  `POTION_EFFECT` that the tick of C counts down; the extension has no tick, so the protection would never end) tell the player "That is
  not available yet." through `DrawInfo` and stay in the pack, as the design says (mud-phase3.md 4.4: 39 of Graal Kingdoms' 51 potion
  archetypes would be eaten for nothing). C has already identified the potion by then, so it is identified. `apply_potion` still
  returns 1 (manual_apply's value).
- **Food that changes stats or protects** (`eat_special_food`'s `cast_change_attr`: strength, dexterity, constitution, charisma and the
  twelve resistances that have a protection spell) says "That is not available yet." once; the food is eaten, its food level and its
  hit and spell points are applied as in C (it is not a potion: C's other effects of it are exact).
- **`hit_player(..., AT_POISON)` does no damage.** In C an attack of type poison has `dam = 0` after the roll for the effect (the type is an
  effect, `hit_player_attacktype`), so a poisonous drink and a cursed food lose no hit points; what C does is `poison_player` (a
  `poisoning` force that the tick works on), which is left out with the rest of the tick. The line of text and the loss of a quarter of
  the food level are C's. `contr->killer` is not kept (nothing kills a player in the extension).
- **`new_draw_info`'s colour** is not passed (the skin message of a dragon is red in C).
- **`onEatFood(foodarch, food, wascursed)`** is a player event that Graal Kingdoms' `mudplayers` defines; it is sent after the food is
  used up on a link of 1.1 (it has typed parameters), so `food` is a dead handle (nil in the script) when the last item of the stack was
  eaten; the archetype is always there. It is not sent for a monster and not for a potion or poison (C's eating is food, drink and flesh).
