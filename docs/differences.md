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
- **Treasure lists.** The lists belong to `common/treasure.c` (the treasure phase). Until then `CrossfireHost.FindTreasureList` makes a
  `TreasureList` for any name, so that the `randomitems` of an archetype keeps its name; the cases of C stay: `randomitems none` is no list
  (and the second pass logs "Failed to link treasure to arch", 48 times in Graal Kingdoms' folder) and nothing is set while the first pass
  runs. A host that knows the lists returns null for the others and gets C's "Couldn't find treasurelist" message.
- **`ArchCollector`** is collect.pl's archetype half in C#: the archetype folder to the text of the two files. `anim ... mina` blocks become
  an `animation` line and an entry of the animations file; the colour lines (`color_fg`, `color_bg`, `visibility`, `magicmap`) are removed.
- **`CrossfireHost`** (the host) and the marker interfaces `IPlayerHandle`, `IMapHandle`, `ISpellHandle`: see [porting.md](porting.md).

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
