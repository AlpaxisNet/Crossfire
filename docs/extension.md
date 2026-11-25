# The Crossfire extension

`src/Crossfire.Extension` is the program a game server starts to get Graal Kingdoms' MUD library: it speaks
[protocol.md](protocol.md) over a Unix socket (`Crossfire.Extension --connect <socket path>`) and holds the object model of
`src/Crossfire`. It is implemented from the protocol text alone; nothing of the game server is in this repository.
The output folder of the build is an extension folder (protocol section 1): the executable and `extension.json` side by side.

## What this version does

- **Link.** The thread that runs `Main` (X) only reads: it answers `PING` at once (also while the worker is busy), refuses
  frames the host may not send (protocol section 9) and hands each `REQ` to one worker thread (W). Only W touches the `CfWorld`,
  which has no locks. Every frame is written whole under one lock. When the connection ends (EOF or a protocol error) the process
  gives W 3 s to finish and exits: status 0 on EOF, 3 on a protocol error, 2 when the host's `proto_major` is not 1 (no HELLO is
  sent then), 64 and 69 for wrong arguments and a socket that cannot be reached (a path too long for a socket address included). Nothing goes to standard output; the extension's
  own lines are `LOG` frames (a line on standard error only before the HELLO is sent, or after the connection is gone).
- **Operations.** `session.init` (loads `<world_dir>/Arch`, else `<world_dir>/arch`, through `ArchCollector` and
  `CfWorld.InitArchetypes`, replies `{archetypes: <count>}`, logs a summary), `session.flush` and `session.shutdown` (nothing is
  saved yet: nil), `get`, `get_many`, `stats` and the manifest functions below. `set`, `get_list` and `invoke` check their
  arguments and handle and then fail with `bad_args`, as the text says for a read-only member, a member that is no list of
  handles and a method that does not exist. Every other op (`session.tick`, `player.*`, `level.*`, `object.destroyed`,
  `objects.blobs`, the reserved 17 to 63 and unknown manifest ids) is `unknown_op`; a request before the REPLY to `session.init` is
  `unavailable`.
- **Handles.** The epoch of the host's HELLO in the high 32 bits and one tag per archetype in the low 32 bits, given out at the
  first `findarchetype` that finds it and kept for the life of the process. A nil, other-epoch, tag-0 or never-issued handle is
  `bad_handle`. Releases are ignored: archetypes are permanent.
- **Not here yet** (later waves): objects, players, levels, effects, events, queries, the file lock on `<data_dir>/.lock`,
  `session.tick`, saving.

## The GK adaptation

`src/Crossfire.Extension/Gk` holds what is Graal Kingdoms' and not Crossfire's: `GkHost` names the nine keys of its archetypes
that its server added to the loader (`graalnpctype`, `graalnpcparam`, `flag`, `hpbuff`, `spbuff`, `gracebuff`, `castingtime`,
`restrictaccess`, `nopeaceful`), so that the loader keeps those lines in `CfObject.Extra` instead of logging an error, and
`TMudArchMembers` reads the members of the type `TMudArch`. With Graal Kingdoms' own `Arch` folder, `session.init` reports 3,300
archetypes (the 3,297 `Object` lines of the backup of 2018-06-28, with the corrections the test of `GkTests` lists); the test
`GkArchetypesTests` checks that when `CROSSFIRE_GK_ARCH` names that folder.

## The manifest

| Function | Id | Params | Result | What it finds |
| --- | --- | --- | --- | --- |
| `findarchetype` | 64 | `string` | `handle:TMudArch` | the first archetype whose `Object` name is the argument (`find_archetype`, exact match); nil when there is none |
| `findarchetypebyrealname` | 65 | `string` | `handle:TMudArch` | the first archetype whose clone's `name` equals the argument (`find_archetype_by_object_name`, exact match, list order); nil when there is none |

One type, `TMudArch` (id 1, host `object`, no methods), whose members are all read-only (`access` "r") and **stable**: an
archetype never changes. No events, no tick, `limits.max_list` 256.

### Members of TMudArch

Read from the archetype's clone (`archt->clone` in Crossfire's object.h). The naming rule is `objects.md` 5.6: `mud` plus the
Crossfire field, with its exceptions (`name` is `mudrealname`, `nrof` is `mudquantity`, `Str` is `mudstrength`, `msg` is
`mudmessage`). The ids run from 100 in the order of the table; a test keeps this table and `TMudArchMembers` equal. "Extra line"
is a line with one of Graal Kingdoms' own keys, which the loader keeps in the clone's `Extra`.

| id | member | type | C field | evidence (the specification of the objects, sections of objects.md) |
| --- | --- | --- | --- | --- |
| 100 | `mudname` | string | archetype `name` (the `Object` key; `archt->name`) | 5.5: EVIDENCED, `monster.mudname == nightmonster`; equal to `archname` |
| 101 | `archname` | string | archetype `name` | 5.5: "same as mudname"; 405 reads |
| 102 | `mudrealname` | string | `clone->name` | 4.7 table (name -> mudrealname); 5.5 |
| 103 | `fullname` | string | `query_name(clone)` | 4.8: help member `fullname` (string, read only); the rule given for this work: Crossfire's `query_name` of the clone |
| 104 | `mudfullname` | string | `query_short_name(clone)` | 5.5: composed full name "without state words and quantity" (a grouping key), inferred as `query_short_name`; `fullname` (103) keeps `query_name`, so the two differ by the state words, for example "(applied)" on an applied archetype |
| 105 | `icon` | string | `clone->face->name`, derived | 4.5: `"mud_" + face without ".<digits>" + ".png"`, a face ending `.png` as it is; "" without a face (or with `blank.111` or `empty.111`) |
| 106 | `mudtype` | int | `clone->type` | 4.4, 5.5 |
| 107 | `mudlevel` | int | `clone->level` | 5.5, 4.8 (`findArchetype(x).mudlevel`) |
| 108 | `mudhp` | int | `clone->stats.hp` | 4.7, 5.5 |
| 109 | `mudmaxhp` | int | `clone->stats.maxhp` | 4.7, 5.5 |
| 110 | `mudsp` | int | `clone->stats.sp` | 4.7, 5.5 |
| 111 | `mudmaxsp` | int | `clone->stats.maxsp` | 4.7, 5.5 |
| 112 | `mudgrace` | int | `clone->stats.grace` | 4.7, 5.5 |
| 113 | `mudmaxgrace` | int | `clone->stats.maxgrace` | 4.7, 5.5 |
| 114 | `mudhpbuff` | int | extra line `hpbuff` (0 when absent) | 4.7 table; GK's own key, kept by the loader in `clone->Extra` |
| 115 | `mudspbuff` | int | extra line `spbuff` (0 when absent) | 4.7 table |
| 116 | `mudgracebuff` | int | extra line `gracebuff` (0 when absent) | 4.7 table |
| 117 | `mudac` | int | `clone->stats.ac` | 4.7 table |
| 118 | `mudwc` | int | `clone->stats.wc` | 4.7 table |
| 119 | `muddamage` | int | `clone->stats.dam` | 4.7 table (`dam` -> `muddamage`) |
| 120 | `mudattacktype` | int | `clone->attacktype` | 4.7 table |
| 121 | `mudspeed` | float | `clone->speed` (a float, as written in the file) | 5.5: float RO in the help; GK writes it on instances |
| 122 | `mudexp` | int | `clone->stats.exp` | 4.7 table |
| 123 | `mudfood` | int | `clone->stats.food` | 4.7 table |
| 124 | `mudweight` | int | `clone->weight` | 4.7 table; 5.5 (grams of one piece) |
| 125 | `mudcarrying` | int | `clone->carrying` | 4.7 table (read only: the weight of the contents) |
| 126 | `mudquantity` | int | `clone->nrof` | 4.7 table (nrof -> mudquantity) |
| 127 | `mudnrof` | int | `clone->nrof` | 4.7 table (nrof -> mudquantity / mudnrof); 5.5: "the same" |
| 128 | `mudmaterial` | int | `clone->material` | 4.7 table |
| 129 | `mudmagic` | int | `clone->magic` | 4.7 table |
| 130 | `mudluck` | int | `clone->stats.luck` | 4.7 table |
| 131 | `mudstrength` | int | `clone->stats.Str` | 4.7 table (Str -> mudstrength) |
| 132 | `muddexterity` | int | `clone->stats.Dex` | 4.7 table |
| 133 | `mudconstitution` | int | `clone->stats.Con` | 4.7 table |
| 134 | `mudwisdom` | int | `clone->stats.Wis` | 4.7 table |
| 135 | `mudintelligence` | int | `clone->stats.Int` | 4.7 table |
| 136 | `mudpower` | int | `clone->stats.Pow` | 4.7 table |
| 137 | `mudcharm` | int | `clone->stats.Cha` | 4.7 table (Cha -> mudcharm) |
| 138 | `mudcharisma` | int | `clone->stats.Cha` | 5.5: alias; the identification list names `mudcharisma` |
| 139 | `mudtitle` | string | `clone->title` | 4.7 table |
| 140 | `mudslaying` | string | `clone->slaying` | 4.7 table |
| 141 | `mudrace` | string | `clone->race` | 4.7 table |
| 142 | `mudmessage` | list:string | `clone->msg`, split into lines | 4.7 table: "array of lines; `mudmessage[0]` is the first"; the empty piece after the last newline is dropped |
| 143 | `value` | string | `clone->value`, as a string | 5.5 (last rows): the help says `value - string (read only)`; `mudvalue(n)` is a method and comes later |
| 144 | `otherarchname` | string | `clone->other_arch->name` ("" when none) | 4.7 table (other_arch -> `otherarch`, `otherarchname`); the member `otherarch` (an object) comes later |
| 145 | `mudgraaltype` | int | extra line `graalnpctype` (-1 when absent) | 4.5: 1,325 archetypes have it; absent is -1 (INFERRED there: `mudapplyobject()` runs only `if (this.mudgraaltype >= 0)`) |
| 146 | `npctype` | int | extra line `graalnpctype` (-1 when absent) | 4.5: GK reads both names (23 reads) |
| 147 | `mudgraalparam` | string | extra line `graalnpcparam` ("" when absent) | 4.5: 700 archetypes have it |
| 148 | `npcparam` | string | extra line `graalnpcparam` ("" when absent) | 4.5: GK reads both names; the instance override `-1` does not exist on an archetype |
| 149 | `castingtime` | int | extra line `castingtime` (0 when absent) | 4.7 table; 5.5 (4 uses) |
| 150 | `restrictaccess` | string | extra line `restrictaccess` ("" when absent) | 4.7 table; 5.5: 40 archetypes carry it |
| 151 | `nopeaceful` | bool | extra line `nopeaceful` (true when not 0) | 4.7 table (the flag keeps its name) |
| 152 | `applied` | bool | `FLAG_APPLIED` | 4.7 table; 5.5 |
| 153 | `identified` | bool | `FLAG_IDENTIFIED` | 4.7 table; 5.5 |
| 154 | `cursed` | bool | `FLAG_CURSED` | 4.7 table; 5.5 |
| 155 | `damned` | bool | `FLAG_DAMNED` | 4.7 table; 5.5 |
| 156 | `unpaid` | bool | `FLAG_UNPAID` | 4.7 table; 5.5 |
| 157 | `nopick` | bool | `FLAG_NO_PICK` | 4.7 table (no_pick -> nopick); 5.5: EVIDENCED |
| 158 | `nodamage` | bool | `FLAG_NO_DAMAGE` | 4.7 table (no_damage -> nodamage) |
| 159 | `alive` | bool | `FLAG_ALIVE` | 4.7 table; 5.5 |
| 160 | `undead` | bool | `FLAG_UNDEAD` | 4.7 table |
| 161 | `scared` | bool | `FLAG_SCARED` | 4.7 table; 5.5 |
| 162 | `flying` | bool | `FLAG_FLYING` | 4.7 table |
| 163 | `confused` | bool | `FLAG_CONFUSED` | 4.7 table |
| 164 | `blind` | bool | `FLAG_BLIND` | 4.7 table |
| 165 | `startequip` | bool | `FLAG_STARTEQUIP` | 4.7 table; 5.5 |
| 166 | `ismudmonster` | bool | `FLAG_MONSTER` | 4.7 table (monster -> ismudmonster, read only); 5.5: 602 archetypes |
| 167 | `ismudobject` | bool | always true | 5.5: "true for every mud object" |

### What is left out, and why

- `template` (a detached object made from the archetype) and `arch`, `otherarch`, `muditems`, `mudenvironment`, `mudowner`,
  `mudid`, `mudmoney`: they need objects (a later wave).
- `mudvalue(n)`, `getResistance(i)` and the other methods: they need method support (a later wave). The plain `value` member, a
  string, is there.
- `mudrandomvar` and `mudmarketprize` (`randomvar` and `marketprice`): no Crossfire field of the base (`8b7ee7f90`) has these
  names and no archetype of Graal Kingdoms carries them (the loader's nine keys do not include them): they belong to instances.
- `mudcarrying` is the archetype's own `carrying` (0 unless the file writes one), not the weight of an inventory.
- Writable members (`mudrealname`, `mudspeed`, `icon`, ...) are writable on instances only; an archetype refuses every write.

### Choices where the sources are silent

- `findarchetype` matches exactly. `objects.md` 4.8 guesses "exact first, then case-insensitive"; the protocol text and the
  task say neither, so the literal reading is the exact one.
- `fullname` is `query_name` of the clone (the rule given for this work), `mudfullname` is `query_short_name` as `objects.md` 5.5
  infers ("without state words"). They agree except for a clone with state words: 11 of Graal Kingdoms' findable archetypes (the
  `*_force` ones with the `applied` flag) read "NAME (applied)" in `fullname` and "NAME" in `mudfullname`.
- A nil list argument of `get_many`, `get_list` and `invoke` reads as the empty list (protocol 6.3), like a nil string.
- `fullname` and `mudfullname` are what Crossfire's `query_short_name` makes of an object whose `nrof` is 0: the plural
  (`name_pl`) when the object has a weight, a title or a magic bonus, else `name` ("sword" with `weight 3000` and no `nrof 1` line
  reads "swords"). That is the C rule for such an object; whether Graal Kingdoms' scripts expect the singular is not known.
- A nil argument of `findarchetype` reads as "" (protocol 6.3) and finds nothing.
- `icon` never looks at the host's folder of pictures (it cannot see it): the derived name is returned even when the host has no
  picture of that name.
