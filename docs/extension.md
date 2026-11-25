# The Crossfire extension

`src/Crossfire.Extension` is the program a game server starts to get Graal Kingdoms' MUD library: it speaks
[protocol.md](protocol.md) over a Unix socket (`Crossfire.Extension --connect <socket path>`) and holds the object model of
`src/Crossfire`. It is implemented from the protocol text alone; nothing of the game server is in this repository.
The output folder of the build is an extension folder (protocol section 1): the executable and `extension.json` side by side.

## What this version does

- **Link.** The thread that runs `Main` (X) only reads: it answers `PING` at once (also while the worker is busy), refuses
  frames the host may not send (protocol section 9) and hands each `REQ` to one worker thread (W). Only W touches the `CfWorld`,
  which has no locks. Every frame is written whole under one lock. Exit statuses: 0 on EOF or after a `session.shutdown`, 3 on a
  protocol error, 2 when the host's `proto_major` is not 1 (no HELLO is sent then), 64 and 69 for wrong arguments and a socket that
  cannot be reached (a path too long for a socket address included). Nothing goes to standard output; the extension's own lines
  are `LOG` frames (a line on standard error only before the HELLO is sent, or after the connection is gone).
- **Operations.** `session.init` (takes the lock, loads `<world_dir>/Arch`, else `<world_dir>/arch`, through `ArchCollector` and
  `CfWorld.InitArchetypes`, replies `{archetypes: <count>}`, logs a summary), `session.tick` (only with `wants_tick`),
  `session.flush`, `session.shutdown`, `get`, `get_many`, `stats` and the manifest functions below. `set`, `get_list` and
  `invoke` check their arguments and handle and then fail with `bad_args`, as the text says for a read-only member, a member that
  is no list of handles and a method that does not exist. Every other op (`session.tick` when the manifest has `wants_tick` false,
  `player.*`, `level.*`, `object.destroyed`, `objects.blobs`, the reserved 17 to 63 and unknown manifest ids) is `unknown_op`; a
  request before the REPLY to `session.init` is `unavailable`.
- **Handles.** The epoch of the host's HELLO in the high 32 bits and one tag per archetype in the low 32 bits, given out at the
  first `findarchetype` that finds it and kept for the life of the process. A nil, other-epoch, tag-0 or never-issued handle is
  `bad_handle`. Releases are ignored: archetypes are permanent.
- **Not here yet** (later waves): objects, players, levels, effects, events, queries, the account and level files. The paging
  helpers for `level.load` are here and tested with synthetic items (below).

## Lifecycle

**The lock.** `session.init` first creates `<data_dir>` (from the host's HELLO) when it is missing and takes an exclusive
`flock(2)` on `<data_dir>/.lock` (the file is created when missing), before it reads a single archetype: the host's HELLO deadline
is 15 s, shorter than the wait. A lock held by an older process is waited for up to 20 s (the file is tried every 25 ms: `flock`
has no wait with a time limit); if it is still held, `session.init` answers `busy` (status 7), no world is loaded, and the
process exits when the host closes the connection. If the connection ends during the wait, the wait stops at once. The lock belongs
to the open file and is held until the process ends (the kernel drops it on a crash too), so another process sees it held: the
tests show that with `flock(1)` as the second process, in both directions. The file is opened with `open(2)` directly, because
`File.OpenHandle` makes the .NET runtime take advisory locks of its own, which would make a plain open fail while an older process
holds the flock. The lock is Linux's `flock`; other systems are not supported. A `session.init` that failed after the lock was
taken (no `Arch` folder) keeps it, and a second `session.init` does not wait for it.

**Flush.** `session.flush` hands the file writer a snapshot and kicks it (a write at once instead of after the quiet time), then
replies nil without waiting for the files.

**Shutdown.** `session.shutdown` with `save` true hands a snapshot to the writer and replies after the files are on disk
(`internal` when they cannot be written; the wait is 8 s at most, the host waits 10 s). With `save` false or nil it writes nothing
and drops what the writer was still holding (including the snapshot of an earlier flush that was not written yet): the process then
writes nothing at the exit either. After the REPLY, a further `REQ` is a protocol error (as before).

**The end of the connection** (EOF, an error, a protocol error): the reader stops, a waiting lock stops waiting, the worker is given
2 s to finish, then the state is saved (unless `session.shutdown` with `save` false came before, or the shutdown with `save` true
already wrote it, or `session.init` never took the lock) and the writer is given 2 s to write: the process is gone well within the 5 s
of protocol section 1. A file that cannot be written in time is logged and left.

**Ticks.** The shipped manifest has `wants_tick` false (Graal Kingdoms' monsters come in wave 3); a test option sets it true. With
it, `session.tick [now_ms]` answers nil and keeps `now_ms` in `HostClock`: the host's monotonic time in milliseconds, which later
waves read as `NowMs` (the last tick's value plus the extension's own stopwatch since it arrived), never as a count of ticks. A
`now_ms` that is not an int, is negative or is before the last tick's is `bad_args` and changes nothing. Without `wants_tick` the
host must not send the op (protocol op 2: "Only with `wants_tick`"), and the extension answers `unknown_op`, as for any op the
manifest did not ask for.

**The spin.** After writing a REPLY the worker looks at its queue for 50 microseconds before it blocks (design section 7: a chain of
requests stays hot). It spins only after a REPLY, so an idle extension uses no CPU. `spin_hits` counts the requests that arrived
inside the spin and `spin_misses` the spins that ended empty.

## The files

All files are under `<data_dir>`, written by the writer thread Wr, never by the worker: W makes an immutable snapshot (the whole
content of a file as bytes) and hands it over, and does not wait for Wr except in `session.shutdown` with `save` true and at the exit.

- **Writing.** A file is written to `<name>.<pid>.tmp` in its own folder (folders are created), flushed to disk and renamed over
  the file (an atomic replace on one file system): a reader sees the old or the new content, never a part. A snapshot is written
  1 s after the last change that followed it, and no change waits more than 5 s however many follow it (protocol section 1: "within
  5 s"); a flush, a shutdown and the exit write at once. The newest snapshot of a name wins. The writer holds one lock, for its own
  table, never while it writes; a write that fails (disk full, folder missing) is logged, counted (`writer_failures`) and tried
  again after the quiet time. A name must be a relative path of the data folder (no `..`, no empty part).
- **`.lock`**: the lock file (empty).
- **`session.json`**: what the process knows of itself, written when the world is loaded and at every flush, shutdown (save) and
  exit (the later snapshots replace it). JSON, one object: `format` (1), `reason` (`init`, `flush`, `shutdown` or `exit`), `epoch`
  and `server_name` of the host's HELLO, `started_utc`, `initialized_utc` (after `session.init`), `saved_utc`, `last_tick_ms` (-1
  before the first tick), `ticks`, `flushes`, `archetypes`, `requests`, `errors`, `frames_in`, `frames_out` (the counters at the
  time). The extension does not read it back; wave 3's `accounts/` and `levels/` go through the same writer.

## Counters (`stats`, op 15)

`stats` returns a flat map of ints. The totals: `requests`, `errors`, `frames_in`, `frames_out`, `bytes_in`, `bytes_out` (frames
including their 4-byte length; the HELLOs are not counted in `frames_in`), `pings`, `spin_hits`, `spin_misses`, `archetypes`, `tags`,
`ticks`, `last_tick_ms`, `flushes`. The GC: `gc_gen0`, `gc_gen1`, `gc_gen2` (collection counts of the process),
`gc_pause_ns` (`GC.GetTotalPauseDuration`), `gc_allocated_bytes`. The writer: `writer_writes` (passes), `writer_files`,
`writer_failures`, `writer_ns_total`, `writer_ns_max` (time of a pass). For each op that was called, with `<op>` one of
`session_init`, `session_tick`, `session_flush`, `session_shutdown`, `get`, `set`, `get_many`, `get_list`, `invoke`, `stats`,
`findarchetype`, `findarchetypebyrealname` (and the names of the later ops), or `unknown_<number>`: `op_<op>_calls`,
`op_<op>_errors` (status not 0), `op_<op>_handle_ns_total` and `_max` (the time in the dispatcher), `op_<op>_wait_ns_total` and
`_max` (from the reader queueing the request to the worker starting it), `op_<op>_bytes_in` (the REQ frames) and
`op_<op>_bytes_out` (the REPLY frames). An op's own counters are recorded after its REPLY is written, so `stats` does not show itself.

## Paging helpers (`Paging.cs`)

- `Paging.LevelPage(items, cursor, perPage, sizeOf, budget)` makes the page of a `level.load` call: at most 64 items (`perPage`
  1 to 64) and, with `sizeOf` and `budget`, as many as fit in the budget; the result is `{more, cursor}` (`Page.Result()`). The
  cursor is nil for the first call, then the index of the next item as an int (a few bytes; `Paging.CursorFits` checks any cursor
  against the 1,024 bytes of the protocol). The list must keep its order and items between the calls of one load. A cursor that is
  not nil or an int in `0..count` is a `BadCursorException` (the `level.load` handler of wave 3 answers `bad_args`); an item that alone is over the
  budget is an `ItemTooLargeException`.
- `Paging.ReplyBudget(maxFrame, result, reserve)` is the number of bytes the lists of a REPLY may hold together so that the frame
  stays within the link's maximum (the frame with empty lists and the largest request id and status, the growth of the two counts,
  and `reserve`, for example `Paging.MaxCursorBytes` for a cursor not yet known).
- `Paging.Split(items, sizeOf, budget)` splits a list into consecutive parts that each fit in the budget (a part is never empty);
  `Paging.Split(values, budget)` does so by the encoded size of values. Wave 3 uses them for the effects of `level.load` and the
  rows of `get_list` and `objects.blobs`.

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
