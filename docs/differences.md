# Differences from Crossfire's C code

Every difference is deliberate and has its reason. Everything else is meant to behave as the C code of commit `8b7ee7f90` does,
quirks included (a few of those are listed at the end, so that nobody "fixes" them).

## Keys the loader does not know are kept (Graal Kingdoms)

The scanner of `common/loader.l` ignores a line it has no rule for (flex echoes it to stdout). Graal Kingdoms' archetypes have such
lines, and a game server needs them: `graalnpctype` (1,325 objects) and `graalnpcparam` (700) decide the class and the look of an object,
`flag gender=male` and `flag arms=...` (218, the key repeats in a block), `hpbuff`, `spbuff`, `gracebuff`, `castingtime`,
`restrictaccess`, `nopeaceful` and others. **The port keeps every such line on the object**: `CfObject.Extra` is an `ExtraKeys`, a list
of (key, value) pairs in file order; a key may repeat; keys are compared without regard to case (the scanner is case-insensitive)
but the spelling of the file is kept; the value is read as the C `yval` reads one (the blanks around it are cut). `copy_object`
copies them with the object, `clear_object` clears them, `get_ob_diff`/`save_object` write the ones an archetype does not have, and
`CfWorld.UnknownKeys` counts them by key. Comment lines (`#`) and blank lines are not kept.

Three consequences that follow from C's rules, not from the port: `msg <edit msg>` (three blocks of `Weapon/Sword/graalswords.arc`) is
not the start of a message (C needs `msg` alone on its line), so its text lines are unknown lines too and `endmsg` an unknown key; a
key spelled as the loader has no rule for it (`resist_blindness`, `resist_life_stealing` in this version, `berserk`) is unknown;
`flag` and the others above are simply not Crossfire keys.

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
