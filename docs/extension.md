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
  `session.flush`, `session.shutdown`, `player.login`, `player.logout`, `level.load`, `level.unload`, `get`, `set`, `get_many`,
  `get_list`, `invoke`, `object.destroyed`, `stats`, `objects.blobs` and the manifest functions below. `get_list` checks its
  arguments, handle and field ids and gives the rows of a member that is a list of handles (`muditems`, lane 4C); a member that is no
  list of handles is `bad_args`, more rows than `limits.max_list` is `too_large`. Every other op (`session.tick` when the manifest has `wants_tick` false, the reserved 17 to 63 and unknown
  manifest ids) is `unknown_op`; a request before the REPLY to `session.init` is `unavailable`. A reply carries the effects
  (`npc.place`, `npc.remove`, `client.set`, `client.del`) and events (`onUpdateObject`, `onLoadedMudAccount`) of its operation, only
  when its status is 0. No query is sent yet (the extension asks the host nothing in this version).
- **Version.** The extension sends `proto_minor` 1 and knows the link's minor version once it has read the host's HELLO
  (`Dispatcher.LinkMinor`, below); its manifest is built after that and has no 1.1 key for a 1.0 host. Nothing 1.1 is declared by the
  core: the lanes of phase 3 add what they need ("The skeleton of wave 4" and "Items of a player" at the end).
- **Handles.** The epoch of the host's HELLO in the high 32 bits and one tag per archetype, level object and logged-in player in the
  low 32 bits, given out at the first need and never again for another object. A nil, other-epoch, tag-0, never-issued or dead handle
  is `bad_handle`. Releases are ignored: archetypes are permanent, objects in a level are never released, players log out. See
  "Levels, objects and accounts" below.
- **Items.** A player's inventory has tags, strings for the client and the members `muditems`, `mudmoney`, `mudowner`, `markedobject` and
  `currentweapon` ("Items of a player" at the end). **Not here yet** (later waves): pick up and drop, apply, skills, the rules of phases 3
  and 4, treasure lists, queries.

## Lifecycle

**The lock.** `session.init` first creates `<data_dir>` (from the host's HELLO) when it is missing and takes an exclusive
`flock(2)` on `<data_dir>/.lock` (the file is created when missing), before it reads a single archetype: the host's HELLO deadline
is 15 s, shorter than the wait. A lock held by an older process is waited for up to 20 s (the file is tried every 25 ms: `flock`
has no wait with a time limit); if it is still held, `session.init` answers `busy` (status 7), no world is loaded, and the
process exits when the host closes the connection. SIGTERM is ignored: a service manager stops the host and the extension of one control group at once, and the extension must outlive the signal to write what the host's last `objects.blobs` and `session.shutdown` bring; it ends at the shutdown or when the connection closes (the host kills it when it does not). If the connection ends during the wait, the wait stops at once. The lock belongs
to the open file and is held until the process ends (the kernel drops it on a crash too), so another process sees it held: the
tests show that with `flock(1)` as the second process, in both directions. The file is opened with `open(2)` directly, because
`File.OpenHandle` makes the .NET runtime take advisory locks of its own, which would make a plain open fail while an older process
holds the flock. The lock is Linux's `flock`; other systems are not supported. A `session.init` that failed after the lock was
taken (no `Arch` folder) keeps it, and a second `session.init` does not wait for it.

**Flush.** `session.flush` hands the file writer a snapshot and kicks it (a write at once instead of after the quiet time), then
replies nil without waiting for the files.

**Shutdown.** `session.shutdown` with `save` true hands a snapshot to the writer and replies after the files are on disk
(`internal` when they cannot be written; the wait is 8 s at most, the host waits 10 s). With `save` false or nil it writes nothing
and drops what the writer was still holding, including the snapshot of an earlier flush that was not written yet and a batch that
the writer had already taken (no file of it is renamed into place after the REPLY; only a rename that is already under way is
waited for): the process then writes nothing at the exit either. After the REPLY, a further `REQ` is a protocol error (as before).

**The end of the connection** (EOF, an error, a protocol error): the reader stops, a waiting lock stops waiting, the worker is given
2 s to finish, then the state is saved (unless `session.shutdown` with `save` false came before, or the shutdown with `save` true
already wrote it, or `session.init` never took the lock) and the writer is given 2 s to write: the process is gone within the 5 s
of protocol section 1: at most 2 s (worker) + 2 s (save) + 0.5 s (the writer's thread stops) = 4.5 s, so the margin is 0.5 s and the
three waits must not be lengthened without the others being shortened. A file that cannot be written in time is logged and left.

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
  table, never while it writes. After the rename the folder is fsynced too, so that the rename itself survives a power loss (what
  the tests cannot observe: neither the fsync of the file nor that of the folder). A name must be a relative path of the data
  folder: no `..`, no empty part, no control character (including NUL), and it does not end in `.tmp` (those are the temporary
  files); `Submit` throws `ArgumentException` otherwise.
- **Failures.** Any exception of a write is a failed write; it never ends the thread. A name the system refuses
  (`ArgumentException`, `NotSupportedException`, `PathTooLongException`) is given up at once. Any other failure (disk full, a folder
  in the way, no permission) is counted (`writer_failures`, once per file and pass) and tried again after the quiet time, then
  after twice that and so on up to 30 s (`MaxRetryDelay`). The back-off is per file: a file that failed waits its own time while the
  others are written at their usual times; a newer submission of a file that waits replaces its content and does not shorten the
  wait. Only the first failure of a series (a failure while no other file is waiting to be tried again) is logged; after 8 failed
  passes of one file in a row (`MaxFailedPasses`) that content of the file is given up and logged once per pass (a newer content that was submitted meanwhile was never tried and is still written). A flush or shutdown forces a try of
  every file regardless of the back-off; `session.shutdown` with `save` true answers `internal` when a file was given up or not
  written in time.
- **Start.** When the writer starts (the process holds the lock then) it removes `<name>.<pid>.tmp` files that a crashed process
  left in the data folder.
- **`.lock`**: the lock file (empty).
- **`session.json`**: what the process knows of itself, written when the world is loaded and at every flush, shutdown (save) and
  exit (the later snapshots replace it). JSON, one object: `format` (1), `reason` (`init`, `flush`, `shutdown` or `exit`), `epoch`
  and `server_name` of the host's HELLO, `started_utc`, `initialized_utc` (after `session.init`), `saved_utc`, `last_tick_ms` (-1
  before the first tick), `ticks`, `flushes`, `archetypes`, `requests`, `errors`, `frames_in`, `frames_out` (the counters at the
  time). The extension does not read it back. `levels/` and `accounts/` (below) go through the same writer.
- **`levels/<name>.map`** and **`accounts/<name>.txt`**: the level files and the account files (below).

## Levels, objects and accounts

The extension keeps its own index of the objects of every level it holds, and the players that are logged in. Everything here runs on
the worker; the files go through the writer (above).

### Handles of objects

One tag per object for as long as the extension holds it: the same table as the archetypes' (`HandleTable`), so a tag is never used
for a second object in one connection. An object that is destroyed (`object.destroyed`, `destroymudobject`), whose level is
unloaded or whose player logs out gives its tag up for good: the handle is dead (`bad_handle`) and the tag is not used again. The
releases of a request are ignored (protocol section 5): every handle names an archetype, an object in a level (never released) or a
logged-in player. `stats` counts the tags given out (`tags`, dead ones included), the levels held (`levels`), their objects
(`level_objects`) and the players (`players`).

### `level.load` (op 7) and `level.unload` (op 8)

- **Where the objects come from.** The first call (cursor nil) for a level the extension does not hold reads
  `<data_dir>/levels/<file>` (a level is held, named and filed by its name with the ASCII letters folded to lower case: the host compares level names without regard to case, so `Hall.nw` and `HALL.NW` are one level; the seed file is found whatever its own case) when the extension saved the level before, else the seed `<world_dir>/mudlevels/<level>` (the file of
  that name, only when the level's name is a plain file name: no `/`, `\` or NUL, not `.` or `..`), else the level is unknown. A
  level that is saved takes precedence over the seed for good: a level whose saved file holds no objects stays empty. A file that
  exists and cannot be read (no permission, a folder in its place) is logged, the level stays unknown to every op, and the file is never
  written over.
- **Paging.** The objects that were not shown yet are the pages' list (`Level.Snapshot`); a page holds at most 64 `npc.place`
  effects and as many as fit in the frame; the cursor is the index of the next one (an int). A cursor that is not an int in
  `0..count` is `bad_args`; a cursor for a level that is not held, or that has no load running, gives `{more: false, cursor: nil}`.
  A level whose objects were all shown, or an unknown level, gives `{more: false, cursor: nil}` and no effects. An object that
  `mudaddobject` made is already shown (its `npc.place` was in that reply) and is not in a later `level.load`. An object whose
  `npc.place` would not fit a frame is given up with a log line.
- **`level.unload`** saves the level when it changed, forgets its objects (their handles are dead) and replies nil with no effects. A
  later `level.load` reads the file again and gives new handles. The text of a file handed to the writer is kept until the writer has
  surely written it (`PutFile`), so a level or an account read again at once sees what was saved.
- **The seed's duplicates and unknowns** (objects.md 8.3: the backup's snapshot has the same block two or three times at one place,
  and shop displays that are meant to be made again). **They are all loaded, none is merged or removed**: a stack of 104 identical
  armours at one spot is a shop's wall, a second `desertarcher` is as real as the first, and the objects' own scripts clean up
  what they must. The 1,582 objects of the 140 files with objects load (1,631 blocks, 49 of them nested contents); the one block of an
  archetype the world does not have is a nested one (`arrow`), logged as C logs it. A block of an unknown archetype is discarded as C's
  `load_objects` discards it (and is lost from the level at the next save).
- **Not loaded** unless asked: the seed is only read for a level the host loads (the host decides what to load); nothing imports the
  whole `mudlevels` folder.

### The level file

The text of a Crossfire map file, read and written by the port's `LoadMapHeader`, `LoadObjects`, `SaveMapHeader` and `SaveObjects`
(and `SaveObject` under it), with one char per byte. The seed and the saved file have the same form.

```
arch map                    the header: the fields that are not 0, as C's new_save_map writes them (a header read from
name hall.nw                the seed keeps its fields; swap_time is not written)
width 8
...
end
arch banana                 one block per object: the archetype, then the differences from it under the archetype's own field names
x 3.328125                  the host's position in tiles, as floats (Crossfire's x and y are integers; see differences.md)
y 1.125
nrof 3
flag born=1524297788.08     the script variables of the file the object came from, until the host gave its own state
mudblob mode=2\nlist=...    the host's state (objects.blobs), on one line: \\ for a backslash, \n, \r, \xNN for other controls
arch banana                 nested objects (the contents): as many levels as there are
nrof 2
end
end
```

- **Position.** The host's `x` and `y` (objects.blobs) are the truth; the extension's copy is at most one save point old. A block
  without `x` or `y` is at 0; a new object is where `mudaddobject` or `cloneMudObject` put it. A level file is read with
  `x` and `y` of the top block turned into the keys `mudx` and `mudy` (the port's rules read integers): see `LevelFiles.RenamePositions`.
- **Script variables and the blob.** `flag name=value` lines of a seed are the object's script variables. Until the host gives its
  own state (a row of `objects.blobs`) they are sent in `npc.place` as `props`, `this.name`: a whole number as an int, another
  number as a float, any other text as text (a comma list stays text, with its quotes); a name that is no identifier is left out
  (and stays in the file). When a blob comes, the `flag` lines go away and `mudblob` is written; `npc.place` then gives the blob
  back and no props. An empty blob is stored as none. The blob is opaque: the extension never reads it. A flag line the object's
  archetype has too is the archetype's, not the file's.
- **Which objects are written.** Every object of the level; the contents of an object are written inside it. There is no member
  `savelocally`: it is a built-in property of the host's NPCs, and the host decides what to keep. An object the host does not keep is
  reported with `object.destroyed` (op 14), and is gone from the level and from its file at the next write. A level that did not change since it was read or saved is not written again (a seed level is
  saved as soon as it has objects: the saved file then takes over). A level whose objects are all gone is saved as a header only.
  Written: at `level.unload`, `session.flush`, `session.shutdown` with save and at the end of the connection.
- **Floats** are written as C's saver writes them (`%f`, 6 decimals): the seed's `speed_left -0.100000023` comes back as
  `-0.1`, which the archetype has as well. **Inventory order**: C reads each object of an inventory to the head and writes from the
  head, so a save and a load would reverse an inventory; the extension reverses it once after reading, so the order of the file stays.
- **Log lines.** The first five errors of the port's loader in a request are forwarded as warnings (`crossfire: ...`), and one line
  says how many objects a level has and where they came from.

### `objects.blobs` (op 16)

Each row `[handle, x, y, blob]` stores the position (as floats) and the blob with the object, replacing the row it had; the next
`npc.place` of the object gives them back. Every row is checked first (a list of four: a handle, two numbers, a string or nil): a
row that is not is `bad_args` and nothing is applied. A row for a dead, unknown or not-object handle is ignored.

### Accounts: `player.login` (op 5) and `player.logout` (op 6)

- **Login.** The account is compared as bytes. The file is `<data_dir>/accounts/<escaped name>.txt`: read it, or, when there is
  none, make a new player from the first archetype of type PLAYER in the list of archetypes (the port's `GetPlayerArchetype(null)`,
  Crossfire's `get_player_archetype`), named after the account. The reply is the handle (type `TMudPlayer`) and carries a `client.set`
  for each string of the player's life and stats (below) and the event `onLoadedMudAccount`. A second login without a logout gives
  the same handle, sends the strings again and fires no second event. `nil` (and a log line) when the world has no player
  archetype, the name is empty, holds a control character (below 0x20 or 0x7f: the name is written into the file as the player's name, and a line break would write lines of its own) or its escaped form is over 200 bytes, or the file exists and holds no player (it is never written
  over). The file's name decides which account it is: an `account` line that names another is only logged.
- **The strings** (`clientr.mud_<name>` of players.md 3.2): `hp maxhp sp maxsp grace maxgrace food exp level str dex con int wis pow
  cha luck speed weight ac wc dam`; the table of members says which member has which. A `set` of such a member changes its string
  (one `client.set`, only when the text changed). The extension remembers every name it set for a player.
- **Logout.** Saves the account (through the writer), replies with a `client.del` for every string it set, kills the handle and
  frees the player. A player who is not logged in: nil, no effects.
- **The file**: the player's object as `SaveObject` writes it (the archetype, the differences, the inventory nested, `end`), with the
  line `account <name>` after the `arch` line, and the plain state of the player as extra lines (`mudclass`, `mudpeaceful`,
  `mudlastdeath`, `mudverbose`, `mudlevelcap`, `mudwccap`, `mudaccap`). Written at logout, `session.flush`, `session.shutdown` and
  the end of the connection (every logged-in account each time). Inventories, skills and money are phase 3: a block nested in a
  player's file is read and written back as it is.

### File names

A level or account name becomes a file name by `FileNames.Escape`: the UTF-8 bytes of the name; the ASCII letters, digits, `_` and
`-` stay, `.` stays except as the first character, every other byte is `%` and two capital hexadecimal digits. It is reversible
(`TryUnescape`), never makes a name with a slash, a NUL or a leading dot, and keeps `a/b`, `a%2Fb` and `A%2Fb` apart. The files are
`levels/<escaped>.map` and `accounts/<escaped>.txt` (so a name never ends in `.tmp`); an escaped name over 200 bytes, or an empty
name, has no file: the level is unknown, the login gives nil. Names are compared as bytes, and the data folder is on a file system
that tells `Abc` from `abc` (Linux).

### The functions and methods

- **`mudaddobject(x, y, archname)`** (id 66, `handle:TMudObject`): the level is the request's `ctx.level` (none: nil and a log line).
  An archetype that does not exist gives nil and one log line (`mudaddobject: archetype X not found`). A name `treasure <list>` gives
  nil and one log line until the treasure lists of phase 4 come. The object is made from the archetype
  (`ArchToObject`: the head of a larger object only), put at (x, y), shown by the `npc.place` in the same reply; the result is its
  handle. A level the extension holds no index for yet is read first (saved file, else seed) and its objects stay to be placed by
  the host's `level.load`; a level with neither file starts empty. At most 5,000 objects per level (a guess of objects.md 5.1,
  `GkSettings.MaxObjectsPerLevel`): beyond it nil and a log line.
- **`mudaddspecialobject(x, y, value, "<arch> <rest>", msg)`** (id 67): the first word is the archetype, the rest of the text
  (trimmed) the object's `slaying`, `msg` its message (when not empty), `value` its `value` (cut to an int).
- **`cloneMudObject(x, y)`** (method 300): a copy of the object (`ObjectCreateClone`: fields, extra lines and contents) at (x, y) of
  the same level; not the host's script variables or blob. The copy's `npc.place` is in the reply; the result is its handle.
- **`destroymudobject()`** (method 301): the object leaves the level at once (it is not saved again), its handle is dead, and the reply
  has `npc.remove` for it; no `onMudDestroy` (objects.md F5). Nothing else changes.
- **`object.destroyed`** (op 14): the same without the effect (the host's NPC is gone), no event (objects.md F7). `bad_handle` for a
  handle that is not an object in a level.
- **Events.** `onUpdateObject` (target object, sync, `args` empty) fires after a successful `set` of a member of a level object that
  changed the value, whichever writable member (objects.md 3.2 says every write of a `mud*` member and of the state flags:
  INFERRED; here also `img` and the extra-line members). A `set` that stores the value the member already had fires no event and
  does not mark the level changed, so a handler that writes a member back to what it read cannot loop (GK's own handlers write
  none: `showhp` and `showobject` only draw and keep script variables). It does not fire for a player (the event's target is an object: a player has no NPC),
  for a read, or for a refused write. `onMudDestroy` (object, sync) is in the manifest for the rules of phase 4 and is never sent
  yet. `onLoadedMudAccount` (player, sync) fires at the login that loaded the player, with the player's handle as its one argument.

## Counters (`stats`, op 15)

`stats` returns a flat map of ints. The totals: `requests`, `errors`, `frames_in`, `frames_out`, `bytes_in`, `bytes_out` (frames
including their 4-byte length; the HELLOs are not counted in `frames_in`), `pings`, `spin_hits`, `spin_misses`, `archetypes`, `tags`,
`ticks`, `last_tick_ms`, `flushes`, `levels`, `level_objects` (the objects of the levels held), `players`. The GC: `gc_gen0`, `gc_gen1`, `gc_gen2` (collection counts of the process),
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
  not nil or an int in `0..count` is a `BadCursorException` (`level.load` answers `bad_args`); an item that alone is over the
  budget is an `ItemTooLargeException`.
- `Paging.ReplyBudget(maxFrame, result, reserve)` is the number of bytes the lists of a REPLY may hold together so that the frame
  stays within the link's maximum (the frame with empty lists and the largest request id and status, the growth of the two counts,
  and `reserve`, for example `Paging.MaxCursorBytes` for a cursor not yet known).
- `Paging.Split(items, sizeOf, budget)` splits a list into consecutive parts that each fit in the budget (a part is never empty);
  `Paging.Split(values, budget)` does so by the encoded size of values. `level.load` uses `LevelPage` and `ReplyBudget`; `Split` is
  for the rows of `get_list` of a later wave.

## The GK adaptation

`src/Crossfire.Extension/Gk` holds what is Graal Kingdoms' and not Crossfire's:

- `GkHost` names the nine keys of its archetypes that its server added to the loader (`graalnpctype`, `graalnpcparam`, `flag`,
  `hpbuff`, `spbuff`, `gracebuff`, `castingtime`, `restrictaccess`, `nopeaceful`) and the fourteen the extension writes into its own
  files (`mudx`, `mudy`, `mudblob`, `mudimg`, `randomvar`, `marketprice`, `account`, `mudclass`, `mudpeaceful`, `mudlastdeath`,
  `mudverbose`, `mudlevelcap`, `mudwccap`, `mudaccap`), so that the loader keeps those lines in `CfObject.Extra` instead of logging an
  error. It is also the `insert_ob_in_map` of the port's `load_objects`: it collects the objects a level file holds.
- `TMudArchMembers`, `TMudObjectMembers` and `TMudPlayerMembers` read and write the members of the three types; `Members.cs` is
  their machinery (a member's id, type, access, stability and its reading and writing function).
- `GkClasses` and `GkLook` decide what the host shows (below). `GkSettings` holds the choices: the class prefixes, the default class,
  the cap of objects per level.

With Graal Kingdoms' own `Arch` folder, `session.init` reports 3,300 archetypes (the 3,297 `Object` lines of the backup of
2018-06-28, with the corrections the test of `GkTests` lists); the test `GkArchetypesTests` checks that when `CROSSFIRE_GK_ARCH` names that
folder.

**The classes of an object** (objects.md 3.1), the `classes` of its `npc.place`, one name:

| the object's archetype | the class |
| --- | --- |
| `graalnpctype` 2 and the world has a class `special_<archname>` | `special_<archname>` |
| `graalnpctype` 1 and the world has a class `monsters_<archname>` | `monsters_<archname>` |
| anything else (no `graalnpctype`, another number, or the class does not exist) | `mudobjects` |

The prefixes `special_` and `monsters_` and the default class `mudobjects` are `GkSettings` (objects.md says they should be options).
The world's classes are read once at `session.init`: the files `*.txt` of the world's `scripts` folder (the folder is found without
regard to the case of its name), each without the suffix; names compare without regard to case and the class is given in the spelling of
its file. A world without that folder has no classes, so every object gets `mudobjects` (the host skips a class it does not have). The
type of the object is read from the object (its own `graalnpctype` line, which an instance may change), the archetype's name from its
archetype.

**The image** is the picture of the object, its `img` (the extra line `mudimg` when a script set one, else `mud_<face>.png` as the
icon of `TMudArch`): the NPC shows it before the object's own `showobject()` runs; an object without a face has "". **The props** are
the script variables of the seed (above): nothing else. Not set by the extension: the NPC's name, `ani`, `nick`, `dir`, its shape.
The class-specific looks are the scripts' (objects.md 3.3).

With Graal Kingdoms' own world, three tests (skipped without the variables) report what they find:

| variable | folder | test |
| --- | --- | --- |
| `CROSSFIRE_GK_ARCH`, `CROSSFIRE_GK_MUDLEVELS` | `Arch`, `mudlevels` | every one of the **1,233 files** of `mudlevels` is read: header names 1,233, **140 with objects**, **1,582 objects** (1,631 blocks, 49 nested contents), 1,564 with script variables, **one error** (the nested `arrow` has no archetype) |
| the same | | each of the 140 levels with objects is written and read again with the same objects, positions and script variables, and the second text is a fixpoint |
| `CROSSFIRE_GK_ARCH`, `CROSSFIRE_GK_SCRIPTS` | `Arch`, `scripts` | the class of each of the 3,300 archetypes follows the table above: **2,015 classes**, 307 archetypes get a `special_` class, 208 a `monsters_` class, 23 with `graalnpctype` 2 and 35 with 1 have none and get `mudobjects` |

## The manifest

| Function | Id | Params | Result | What it finds |
| --- | --- | --- | --- | --- |
| `findarchetype` | 64 | `string` | `handle:TMudArch` | the first archetype whose `Object` name is the argument (`find_archetype`, exact match); nil when there is none |
| `findarchetypebyrealname` | 65 | `string` | `handle:TMudArch` | the first archetype whose clone's `name` equals the argument (`find_archetype_by_object_name`, exact match, list order); nil when there is none |
| `mudaddobject` | 66 | `float`, `float`, `string` | `handle:TMudObject` | objects.md F1: a new object of that archetype at (x, y) of the request's level; nil and one log line for an unknown archetype or a `treasure <list>` name |
| `mudaddspecialobject` | 67 | `float`, `float`, `int`, `string`, `string` | `handle:TMudObject` | objects.md F2: the first word of the fourth argument is the archetype, the rest its `slaying`; the fifth its `msg`; the third its `value` |

Three types: `TMudArch` (id 1, host `object`, no methods), whose members are all read-only (`access` "r") and **stable**: an
archetype never changes; `TMudObject` (id 2, host `npc`) and `TMudPlayer` (id 3, host `player`), below. Three events (below), no
tick, `limits.max_list` 256.

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


### The types TMudObject and TMudPlayer

`TMudObject` (id 2, host **`npc`**: its members and methods are on every level NPC of the host that shows an object, and on the proxy)
holds the members of objects.md 5.5 that are fields of the Crossfire object or plain state of a level object, and two methods.
`TMudPlayer` (id 3, host **`player`**: on the host's player objects, for the player of the handle `player.login` gave) holds the plain
fields of a player's object. Member ids run from 200 (`TMudObject`) and 500 (`TMudPlayer`), methods from 300; the ids are in the tables, a test keeps
the tables and the code equal.

**Stable** (protocol 6.1): only a member that nothing but a `set` changes, now and in the rules of phases 3 and 4 as far as they are
known: names, kinds, texts, the Graal Kingdoms keys, the fixed bonuses, `nopick`, `nodamage`, `startequip`. Not stable: life and
experience (`mudhp` ... `mudexp`, `mudfood`, `mudlevel`, regeneration and combat), `mudspeed`, the weight and quantity (inventories,
`muddecreasequantity`), `mudmagic`, `applied`, `identified`, `cursed`, `damned`, `unpaid`, the status flags (`alive`, `scared`,
`confused`, `blind`, `undead`, `flying`) and the derived names `fullname` and `mudfullname`. Every member of a player but `mudid` and
`ismudobject` is not stable (the player's rules recompute them).

Of two members that name one field (`mudgraaltype` and `npctype`, `mudgraalparam` and `npcparam`, `mudcharm` and `mudcharisma`) neither
is stable: a set of one would change a value the host cached for the other.

**Texts on one line.** A `set` of a text member of a level object (`img`, `npcparam`, `mudgraalparam`, `restrictaccess`,
`mudrealname`, `mudtitle`, `mudslaying`, `mudrace`) whose value holds a newline or a carriage return is refused (status "bad
arguments" with the reason): the object is saved as lines, and a break in a value would write lines of a script's choosing into the
level file. The same for a player's text members.

**Names.** A name that is a built-in property of the host's NPCs or players makes the host refuse the manifest (protocol 6.1). The
names are those Graal Kingdoms' scripts use (objects.md 5.5, 5.6); a name the host turns out to refuse is renamed in
this table only with that evidence (the end-to-end run of `scripts/e2e-with-suite.sh` checks the host at hand).

**Writing.** A value of another type than the member's is `bad_args` (a number for a number member is converted as protocol 3.2 says;
nil reads as the zero of the type). A number is clamped to the range of the C field (`mudhp` is a `sint16`: -32768 to 32767; the
stats and `mudac`, `mudwc` a `sint8`) and the reply to the `set` is the stored value. A text member takes the bytes of the UTF-8 text;
an empty `mudtitle`, `mudslaying` or `mudrace` clears the field. `mudrealname` changes `name` and leaves `name_pl`.

#### Members of TMudObject

| member | id | type | access | stable | Crossfire field or state | evidence (sections of `objects.md` and `players.md`) |
| --- | --- | --- | --- | --- | --- | --- |
| `mudname` | 200 | string | r | yes | archetype `name` (`ob->arch->name`) | 5.5: EVIDENCED; equal to `archname` |
| `archname` | 201 | string | r | yes | archetype `name` | 5.5: "same as mudname", 405 reads |
| `arch` | 202 | handle:TMudArch | r | yes | `ob->arch`, as the handle of its `TMudArch` | 5.5: object, read only, about 6 reads |
| `mudid` | 203 | int | r | yes | the object's tag (the low half of its handle) | 2.3, 5.5: an id that is unique while the server runs, ascending, never saved |
| `fullname` | 204 | string | r | no | `query_name(ob)` | 4.8: help member `fullname`; as on `TMudArch` |
| `mudfullname` | 205 | string | r | no | `query_short_name(ob)` | 5.5: the full name without state words and quantity; as on `TMudArch` |
| `icon` | 206 | string | r | yes | `ob->face->name`, derived as on `TMudArch` | 4.5, 5.5: read only |
| `img` | 207 | string | rw | yes | extra line `mudimg` when a script set one, else the icon | 3.3: INFERRED, 141 reads and 33 writes in 77 files; GK writes it |
| `mudtype` | 208 | int | r | yes | `ob->type` | 4.4, 5.5: read only |
| `mudgraaltype` | 209 | int | rw | no | extra line `graalnpctype` (-1 when absent) | 4.5, 5.5; written by GK's staff item tool |
| `npctype` | 210 | int | rw | no | extra line `graalnpctype` (-1 when absent) | 4.5: GK reads both names; not stable: one field with `mudgraaltype` (protocol 6.1) |
| `mudgraalparam` | 211 | string | rw | no | extra line `graalnpcparam` ("" when absent) | 4.5, 5.5 |
| `npcparam` | 212 | string | rw | no | extra line `graalnpcparam` ("" when absent) | 4.5: GK reads both names; not stable: one field with `mudgraalparam` (protocol 6.1) |
| `mudlevel` | 213 | int | rw | no | `ob->level` | 5.5; written by GK's staff tool; not stable (the experience rules of phase 4 change it). Client string `clientr.mud_level`. |
| `mudhp` | 214 | int | rw | no | `ob->stats.hp` | 5.5: EVIDENCED, 354 writes; not stable (regeneration, combat). Client string `clientr.mud_hp`. |
| `mudmaxhp` | 215 | int | rw | no | `ob->stats.maxhp` | 5.5: 243 writes. Client string `clientr.mud_maxhp`. |
| `mudsp` | 216 | int | rw | no | `ob->stats.sp` | 5.5. Client string `clientr.mud_sp`. |
| `mudmaxsp` | 217 | int | rw | no | `ob->stats.maxsp` | 5.5. Client string `clientr.mud_maxsp`. |
| `mudgrace` | 218 | int | rw | no | `ob->stats.grace` | 5.5. Client string `clientr.mud_grace`. |
| `mudmaxgrace` | 219 | int | rw | no | `ob->stats.maxgrace` | 5.5. Client string `clientr.mud_maxgrace`. |
| `mudexp` | 220 | int | rw | no | `ob->stats.exp` | 5.5: 47 reads; a house id on doors. Client string `clientr.mud_exp`. |
| `mudfood` | 221 | int | rw | no | `ob->stats.food` | 5.5: 66 reads. Client string `clientr.mud_food`. |
| `mudac` | 222 | int | rw | yes | `ob->stats.ac` | 5.5. Client string `clientr.mud_ac`. |
| `mudwc` | 223 | int | rw | yes | `ob->stats.wc` | 5.5. Client string `clientr.mud_wc`. |
| `muddamage` | 224 | int | rw | yes | `ob->stats.dam` | 5.5: 193 writes (every monster class sets it). Client string `clientr.mud_dam`. |
| `mudattacktype` | 225 | int | rw | yes | `ob->attacktype` | 5.5: a bit mask |
| `mudspeed` | 226 | float | rw | no | `ob->speed`, a float as written in the file | 5.5: float, read only in the help, but GK writes it (67 writes). Client string `clientr.mud_speed`. |
| `mudweight` | 227 | int | r | no | `ob->weight` | 5.5: grams of one piece, read only. No client string of its own. |
| `mudcarrying` | 228 | int | r | no | `ob->carrying` | 5.5: the weight of the contents, read only |
| `mudmagic` | 229 | int | rw | no | `ob->magic` | 5.5; not stable (enchanting is a rule of a later phase) |
| `mudluck` | 230 | int | rw | yes | `ob->stats.luck` | 5.5. Client string `clientr.mud_luck`. |
| `mudstrength` | 231 | int | rw | yes | `ob->stats.Str` | 5.5. Client string `clientr.mud_str`. |
| `muddexterity` | 232 | int | rw | yes | `ob->stats.Dex` | 5.5. Client string `clientr.mud_dex`. |
| `mudconstitution` | 233 | int | rw | yes | `ob->stats.Con` | 5.5. Client string `clientr.mud_con`. |
| `mudwisdom` | 234 | int | rw | yes | `ob->stats.Wis` | 5.5. Client string `clientr.mud_wis`. |
| `mudintelligence` | 235 | int | rw | yes | `ob->stats.Int` | 5.5. Client string `clientr.mud_int`. |
| `mudpower` | 236 | int | rw | yes | `ob->stats.Pow` | 5.5. Client string `clientr.mud_pow`. |
| `mudcharm` | 237 | int | rw | no | `ob->stats.Cha` | 5.5: the item tool and GK write `mudcharm`. Client string `clientr.mud_cha`. |
| `mudcharisma` | 238 | int | rw | no | `ob->stats.Cha` | 5.5: alias; the identification list names `mudcharisma`. Client string `clientr.mud_cha`; not stable: one field with `mudcharm` (protocol 6.1) |
| `mudrealname` | 239 | string | rw | yes | `ob->name` | 5.5: 22 writes (GK prefixes enchant names); `name_pl` is left as it is |
| `mudtitle` | 240 | string | rw | yes | `ob->title` | 5.5: writable |
| `mudslaying` | 241 | string | rw | yes | `ob->slaying` | 5.5: the key text, the pet race, the effect list |
| `mudrace` | 242 | string | rw | yes | `ob->race` | 5.5: 35 writes |
| `nodamage` | 243 | bool | rw | yes | `FLAG_NO_DAMAGE` | 4.7 table; 5.5 |
| `alive` | 244 | bool | rw | no | `FLAG_ALIVE` | 4.7 table; 5.5 |
| `undead` | 245 | bool | rw | no | `FLAG_UNDEAD` | 4.7 table; 5.5 |
| `scared` | 246 | bool | rw | no | `FLAG_SCARED` | 4.7 table; 5.5 |
| `flying` | 247 | bool | rw | no | `FLAG_FLYING` | 4.7 table; 5.5 |
| `confused` | 248 | bool | rw | no | `FLAG_CONFUSED` | 4.7 table; 5.5 |
| `blind` | 249 | bool | rw | no | `FLAG_BLIND` | 4.7 table; 5.5 |
| `mudhpbuff` | 250 | int | rw | yes | extra line `hpbuff` (0 when absent) | 4.7 table; 47 writes in the item generator |
| `mudspbuff` | 251 | int | rw | yes | extra line `spbuff` (0 when absent) | 4.7 table |
| `mudgracebuff` | 252 | int | rw | yes | extra line `gracebuff` (0 when absent) | 4.7 table |
| `mudquantity` | 253 | int | r | no | `ob->nrof` | 5.5: read only; changed by `muddecreasequantity`, pickup and drop (later waves) |
| `mudnrof` | 254 | int | r | no | `ob->nrof` | 5.5: "the same" |
| `mudmaterial` | 255 | int | rw | yes | `ob->material` | 5.5: a bit mask; written by the item tool |
| `mudmessage` | 256 | list:string | r | yes | `ob->msg`, split into lines | 5.5: the object's readable text; as on `TMudArch` |
| `value` | 257 | string | r | yes | `ob->value`, as a string | 5.5: the help says string, read only; `mudvalue(n)` is a later wave |
| `otherarchname` | 258 | string | r | yes | `ob->other_arch->name` ("" when none) | 4.7 table |
| `castingtime` | 259 | int | rw | yes | extra line `castingtime` (0 when absent) | 5.5: 4 uses |
| `restrictaccess` | 260 | string | rw | yes | extra line `restrictaccess` ("" when absent) | 5.5: 40 archetypes carry it; GK checks it, the extension does not enforce it |
| `mudrandomvar` | 261 | int | rw | yes | extra line `randomvar` (0 when absent) | 5.5: a number kept with the object (65 lines in the level files) |
| `mudmarketprize` | 262 | int | rw | yes | extra line `marketprice` (0 when absent) | 5.5: saved, 9 reads |
| `nopeaceful` | 263 | bool | rw | yes | extra line `nopeaceful` (true when not 0) | 4.7 table |
| `applied` | 264 | bool | rw | no | `FLAG_APPLIED` | 4.7 table; 5.5; not stable (apply is a rule of a later wave) |
| `identified` | 265 | bool | rw | no | `FLAG_IDENTIFIED` | 4.7 table; 5.5; not stable (`mudidentify`) |
| `cursed` | 266 | bool | rw | no | `FLAG_CURSED` | 4.7 table; 5.5 |
| `damned` | 267 | bool | rw | no | `FLAG_DAMNED` | 4.7 table; 5.5 |
| `unpaid` | 268 | bool | rw | no | `FLAG_UNPAID` | 4.7 table; 5.5: the shop flag, 210 reads |
| `nopick` | 269 | bool | rw | yes | `FLAG_NO_PICK` | 4.7 table (no_pick -> nopick); 5.5: EVIDENCED, 114 writes |
| `startequip` | 270 | bool | rw | yes | `FLAG_STARTEQUIP` | 4.7 table; 5.5 |
| `ismudmonster` | 271 | bool | r | yes | `FLAG_MONSTER` | 4.7 table: read only |
| `ismudobject` | 272 | bool | r | yes | always true | 5.5: "true for every mud object" |
| `muditems` | 1000 | list:handle:TMudObject | r | no | the visible objects of `ob->inv`, in inventory order | mud-phase3.md 4.2: each element gets its tag when it is read; `get_list` gives rows of at most `limits.max_list` (1,024 on a 1.1 link), more is `too_large` and the host reads the member with `get` |
| `mudmoney` | 1001 | int | r | no | `query_money` (shop.c) | mud-phase3.md 4.2: the coins of a player or a container, 0 for any other object |
| `mudowner` | 1002 | ref | rw | no | extra line `mudowner` (an account), or an NPC in memory | mud-phase3.md 3 rule 5 and 4.2: **1.1 only** (a `ref`: not in the manifest of a 1.0 link). Read: the handle of the player while that account is online, else the account text, nil for none; written: a player's handle or a text (the account, saved with the object), an NPC's handle (kept in memory only), nil or "" clears it |

**Methods of TMudObject**

| method | id | params | result | what it does |
| --- | --- | --- | --- | --- |
| `cloneMudObject` | 300 | `float`, `float` | `handle:TMudObject` | objects.md F6 (EVIDENCED, 8 calls): a copy at (x, y) of the same level with the mud state, not the script variables |
| `destroymudobject` | 301 | none | `void` | objects.md F5 (EVIDENCED, 38 calls): the object leaves the level, `npc.remove`, no `onMudDestroy` |

Not here, because they need the rules of phases 3 and 4: `muddecreasequantity`, `mudvalue`, `getresistance`, `setresistance`, `apply`,
`examine`, `mudidentify`, the attack methods; the members `mudowner`, `muditems`, `mudenvironment`, `mudmoney`, `datablock`,
`dontmuddestroy` (a script flag), the resistances by name.

#### Members of TMudPlayer

The same Crossfire fields as on an object (a player is a Crossfire object of type PLAYER), none of them stable but `mudid` and
`ismudobject`, and the state objects.md 5.4 and players.md 5.9 and 5.13 name for the player. Its client strings are listed in the
last column.

| member | id | type | access | stable | Crossfire field or state | evidence (sections of `objects.md` and `players.md`) |
| --- | --- | --- | --- | --- | --- | --- |
| `mudid` | 500 | int | r | yes | the player's tag (the low half of its handle) | players.md 2.3: a session id, never saved |
| `mudlevel` | 501 | int | rw | no | `ob->level` | 5.5; written by GK's staff tool; not stable (the experience rules of phase 4 change it). Client string `clientr.mud_level`. |
| `mudhp` | 502 | int | rw | no | `ob->stats.hp` | 5.5: EVIDENCED, 354 writes; not stable (regeneration, combat). Client string `clientr.mud_hp`. |
| `mudmaxhp` | 503 | int | rw | no | `ob->stats.maxhp` | 5.5: 243 writes. Client string `clientr.mud_maxhp`. |
| `mudsp` | 504 | int | rw | no | `ob->stats.sp` | 5.5. Client string `clientr.mud_sp`. |
| `mudmaxsp` | 505 | int | rw | no | `ob->stats.maxsp` | 5.5. Client string `clientr.mud_maxsp`. |
| `mudgrace` | 506 | int | rw | no | `ob->stats.grace` | 5.5. Client string `clientr.mud_grace`. |
| `mudmaxgrace` | 507 | int | rw | no | `ob->stats.maxgrace` | 5.5. Client string `clientr.mud_maxgrace`. |
| `mudexp` | 508 | int | rw | no | `ob->stats.exp` | 5.5: 47 reads; a house id on doors. Client string `clientr.mud_exp`. |
| `mudfood` | 509 | int | rw | no | `ob->stats.food` | 5.5: 66 reads. Client string `clientr.mud_food`. |
| `mudac` | 510 | int | rw | no | `ob->stats.ac` | 5.5. Client string `clientr.mud_ac`. |
| `mudwc` | 511 | int | rw | no | `ob->stats.wc` | 5.5. Client string `clientr.mud_wc`. |
| `muddamage` | 512 | int | rw | no | `ob->stats.dam` | 5.5: 193 writes (every monster class sets it). Client string `clientr.mud_dam`. |
| `mudattacktype` | 513 | int | rw | no | `ob->attacktype` | 5.5: a bit mask |
| `mudspeed` | 514 | float | rw | no | `ob->speed`, a float as written in the file | 5.5: float, read only in the help, but GK writes it (67 writes). Client string `clientr.mud_speed`. |
| `mudweight` | 515 | int | r | no | `ob->weight` | 5.5: grams of one piece, read only. The client string `clientr.mud_weight` is the whole weight the player carries (his own weight and `carrying`), written by the flush of lane 4C, not this member. |
| `mudcarrying` | 516 | int | r | no | `ob->carrying` | 5.5: the weight of the contents, read only |
| `mudmagic` | 517 | int | rw | no | `ob->magic` | 5.5; not stable (enchanting is a rule of a later phase) |
| `mudluck` | 518 | int | rw | no | `ob->stats.luck` | 5.5. Client string `clientr.mud_luck`. |
| `mudstrength` | 519 | int | rw | no | `ob->stats.Str` | 5.5. Client string `clientr.mud_str`. |
| `muddexterity` | 520 | int | rw | no | `ob->stats.Dex` | 5.5. Client string `clientr.mud_dex`. |
| `mudconstitution` | 521 | int | rw | no | `ob->stats.Con` | 5.5. Client string `clientr.mud_con`. |
| `mudwisdom` | 522 | int | rw | no | `ob->stats.Wis` | 5.5. Client string `clientr.mud_wis`. |
| `mudintelligence` | 523 | int | rw | no | `ob->stats.Int` | 5.5. Client string `clientr.mud_int`. |
| `mudpower` | 524 | int | rw | no | `ob->stats.Pow` | 5.5. Client string `clientr.mud_pow`. |
| `mudcharm` | 525 | int | rw | no | `ob->stats.Cha` | 5.5: the item tool and GK write `mudcharm`. Client string `clientr.mud_cha`. |
| `mudcharisma` | 526 | int | rw | no | `ob->stats.Cha` | 5.5: alias; the identification list names `mudcharisma`. Client string `clientr.mud_cha`. |
| `mudrealname` | 527 | string | rw | no | `ob->name` (a new player's name is its account) | players.md 2.2: `name` equals the account |
| `mudtitle` | 528 | string | rw | no | `ob->title` | 5.5: writable |
| `mudslaying` | 529 | string | rw | no | `ob->slaying` | 5.5: the key text, the pet race, the effect list |
| `mudrace` | 530 | string | rw | no | `ob->race` | 5.5: 35 writes |
| `nodamage` | 531 | bool | rw | no | `FLAG_NO_DAMAGE` | 4.7 table; 5.5 |
| `alive` | 532 | bool | rw | no | `FLAG_ALIVE` | 4.7 table; 5.5 |
| `undead` | 533 | bool | rw | no | `FLAG_UNDEAD` | 4.7 table; 5.5 |
| `scared` | 534 | bool | rw | no | `FLAG_SCARED` | 4.7 table; 5.5 |
| `flying` | 535 | bool | rw | no | `FLAG_FLYING` | 4.7 table; 5.5 |
| `confused` | 536 | bool | rw | no | `FLAG_CONFUSED` | 4.7 table; 5.5 |
| `blind` | 537 | bool | rw | no | `FLAG_BLIND` | 4.7 table; 5.5 |
| `mudclass` | 538 | string | rw | no | extra line `mudclass` | players.md 5.9, 5.13: the class's name as text; set by `setmudclass` (phase 3) |
| `peaceful` | 539 | bool | rw | no | extra line `mudpeaceful` | objects.md 5.4 (TMudPlayer members), players.md 5.6 |
| `lastdeath` | 540 | int | rw | no | extra line `mudlastdeath` (0 when absent) | objects.md 5.4: a timestamp GK writes |
| `mudverbose` | 541 | bool | rw | no | extra line `mudverbose` | players.md 5.13: 16 reads |
| `levelcap` | 542 | int | rw | no | extra line `mudlevelcap` | players.md 5.9: GK writes it for trial accounts |
| `wccap` | 543 | int | rw | no | extra line `mudwccap` | players.md 5.9 |
| `accap` | 544 | int | rw | no | extra line `mudaccap` | players.md 5.9 |
| `ismudobject` | 545 | bool | r | yes | always true | objects.md 5.5: true for a player with a loaded mud account |
| `muditems` | 1500 | list:handle:TMudObject | r | no | the visible objects of the player's `ob->inv` | as on `TMudObject` |
| `mudmoney` | 1501 | int | r | no | `query_money` over the player's tree | mud-phase3.md 4.2; GS1's `playermudmoney` reads it |
| `markedobject` | 1502 | ref | rw | no | `contr->mark` (c_object.c `find_marked_object`) | mud-phase3.md 4.2: **1.1 only**. Read: the proxy of the marked item, "" when none (or when the item left the inventory: C clears the mark then); written: an item of the player's own top level marks it, nil or "" clears it, any other handle changes nothing |
| `currentweapon` | 1503 | handle:TMudObject | r | no | `ob->current_weapon` | mud-phase3.md 4.2: the applied weapon when it is in the player's inventory, else nil (the same source as `getwieldedobject`) |

### Events

| name | target | mode | when |
| --- | --- | --- | --- |
| `onUpdateObject` | object | sync | after a `set` of a member of a level object (above); `args` empty |
| `onMudDestroy` | object | sync | declared for the rules of phase 4; `object.destroyed` and `destroymudobject` do not fire it (objects.md F5, F7) |
| `onLoadedMudAccount` | player | sync | at the `player.login` that loaded the player; `args` is `[the player's handle]` |
| `onRPGMessage` | player | sync | **1.1 only** (lane 4C): a line of text for the player (C `new_draw_info`); `args` is `[text]` (param type `string`); the fallback is the `client.text` effect, which the host runs when the player's class has no `onRPGMessage` |

The modes are the design's default (events sync). A handle in `args` becomes the NPC that shows it, or nil (protocol 5): for a player
that is nil until the host passes the player object itself (a protocol gap, reported with this wave).

### Choices where the protocol and the sources are silent (wave 3)

- A seed level is read for any level name that is a plain file name (the host decides which levels it loads); a level with neither
  file is unknown to `level.load` and starts empty for `mudaddobject`.
- Position and blob: the host's row replaces the extension's; an empty blob is none; the seed's `flag` lines are the props until then.
- `onUpdateObject` fires for a writable member (also `img`, the extra-line members) when the set changed its value, not for players.
- `onLoadedMudAccount` fires once per load, not at a second login; its argument is the handle of the player.
- A new player is the first PLAYER archetype of the world in the order of the archetypes (C's `get_player_archetype(NULL)`; in Graal
  Kingdoms' own `Arch` that is `dwarf_player`); its `name` is the account. Class, race and stats are phase 3: whether Graal Kingdoms'
  players start from another archetype is not known.
- A file that exists and cannot be read is never written over, whether it is a level or an account.
- `mudaddobject` of a level not held reads the level first and leaves its other objects to the host's `level.load`.
- `object.destroyed` for a handle that is no level object (an archetype, a player) is `bad_handle`.
- `mudaddspecialobject` splits its fourth argument at the first white space; `value` is cut to an int.
- An object's `mudrealname` is `ob->name`; `fullname` and `mudfullname` are `query_name` and `query_short_name` of the object (so they
  change with `nrof`, the state flags and `name`).

## The skeleton of wave 4

Wave 4 of phase 3 (mud-phase3.md section 8 of the suite) is written by lanes that each own files. This part is what they share. All
of it is internal to the extension and tested in `SkeletonTests.cs`.

### The link's minor version

`HostHello.LinkMinor` is the smaller of the host's `proto_minor` and `Manifest.ProtoMinor` (1); it is read from the host's HELLO
before the manifest is built. The dispatcher keeps it as **`Dispatcher.LinkMinor`** (and `Dispatcher.Speaks11`); the same property
is `SessionState.LinkMinor`. Every 1.1 addition is for a link where it is at least 1 (protocol 4.1): a function, type or event
registered with `MinMinor` 1 is not in the manifest of a 1.0 link, and the dispatcher answers `unknown_op` for such a function. The
HELLO always says `proto_minor` 1. The key `player_classes` is written only for 1.1 and only when a lane called
`AddPlayerClass`; the key `params` of an event only for 1.1 (below it the event has its 1.0 behaviour).

### Lanes and id ranges

The manifest is collected by `Manifest.Collect(linkMinor)`: `RegisterCore`, then one method per lane in the order of the table,
each in a file of its own (`Gk/Manifest.Items.cs`, `Manifest.Start.cs`, `Manifest.Gmaps.cs`, `Manifest.Wave5A.cs` to `Wave5D.cs`; a
lane edits its own file only). The method gets a `ManifestBuilder`: `AddFunction(lane, FunctionDef)`, `AddType(lane, TypeDef)`,
`AddEvent(lane, EventDef)`, `AddPlayerClass(name)`. The builder throws `InvalidOperationException` (found by the first test run) for
an id outside the lane's range, a duplicate id or name, a type name that is not one of the protocol's, a `ref` name without
`MinMinor` 1, a `handle:` or `ref:` type that is not in the manifest, and an event target or mode that is none. Ids stay unique
across the whole manifest (members and methods together), as the protocol asks. The ranges, kept equal to `Lane` by a test:

| Lane | Functions | Types | Members and methods |
| --- | --- | --- | --- |
| core | 64 to 999 | 1 to 9 | 1 to 999 |
| 4C | 1000 to 1999 | 10 to 19 | 1000 to 1999 |
| 4D | 2000 to 2999 | 20 to 29 | 2000 to 2999 |
| 4G | 3000 to 3999 | 30 to 39 | 3000 to 3999 |
| 5A | 4000 to 4999 | 40 to 49 | 4000 to 4999 |
| 5B | 5000 to 5999 | 50 to 59 | 5000 to 5999 |
| 5C | 6000 to 6999 | 60 to 69 | 6000 to 6999 |
| 5D | 7000 to 7999 | 70 to 79 | 7000 to 7999 |

The core uses functions 64 to 67, types 1 to 3, members and methods 100 to 545. A lane's 1,000 member ids are split: the first 500
are for its members and methods of `TMudObject`, the next 250 for `TMudPlayer`, the last 250 for its own new types
(`Lane.ObjectMembersFirst`, `PlayerMembersFirst`, `OwnTypesMembersFirst`).

**Functions.** `new FunctionDef(id, name, params, ret, Slow: false, Handler: ..., MinMinor: 0)`: `id` is also the operation number,
and `Handler` (`Func<Dispatcher, IReadOnlyList<Val>, Outcome>`) is what the dispatcher runs for it, in the worker, with the request's
arguments unchecked (the handler checks them). A lane's handlers are methods of its own partial file of `Dispatcher`
(`Dispatcher.Items.cs`, for example), so that they reach the dispatcher's private state.

**Members of the existing types.** `TMudObjectMembers` and `TMudPlayerMembers` are partial. A lane implements
`static partial void ExtendItems(FieldBuilder<T> b, List<MethodDef> methods)` (`ExtendStart`, `ExtendGmaps`, `ExtendWave5A` to
`5D` likewise) in `Gk/TMudObjectMembers.Items.cs` and so on, for `LevelObject` and for `PlayerState` separately; the builder is
already set to the lane's range for that type, and a lane that uses up its range gets an exception. A method's id is
`b.NextMethodId()`. A partial method that is not implemented adds nothing. The binding of a new member to the handle's target is the
lane's work in `BindMember` when it adds a new target class.

**Tests.** `ExtensionOptions.ExtraManifest` registers what only a test needs, after the lanes.

### The player controller

`Gk/PlayerController.cs`: `Player : IPlayerHandle` is the C `struct pl` as far as phase 3 reads it: `Count`, `CountLeft` (uint),
`Mark` (`CfObject?`) with `MarkCount` (uint: the tag the marked object had), `Mode` (uint), `LastUsed` with `LastUsedId`, and
`WeightLimit` (long, for lane 4C), with `State` (the `PlayerState`) and `Account`. It is made at `player.login` (the first login of
the account; a second login keeps it) and set as `PlayerState.Ob.Contr` (also `PlayerState.Contr`); `player.logout` sets `Contr`
to null and drops a flush that was asked for. `Dispatcher.ControllerOf(account)` finds it.

`Player.Shown` is the `ShownTable`: `Sent` (the whole flag name with the text the client holds), and the tags marked by the ported
C's `esrv_*` calls: `MarkDirty(tag)`, `MarkRemoved(tag)` (the last mark wins, each tag is in one list, in the order of first
marking), `Dirty`, `Removed`, `ClearMarks()`. A mark calls `Player.Touch()`, which puts the controller in the dispatcher's list for
the end of the request (once). Lane 4C wrote the flush ("Items of a player").

### The flush at the end of a request

At the end of a request that ends with status 0 and before its REPLY is built (`Dispatcher.Handle`), `FlushControllers` takes each
controller that asked for a flush, in the order it asked, once, and calls `partial void FlushShown(PlayerState)` (4C implemented it
in `Dispatcher.Items.cs`; it sets the strings with `SetClientString` (internal) and effects with `effects`, clears the marks) and
then the test hook `ExtensionOptions.FlushHook`. The flush's effects follow those the request made itself. After a request that
fails nothing is flushed and the marks stay for the next request that succeeds. When the REPLY is then replaced by `too_large`, the
effects are lost: every controller of that flush gets `Shown.Forget()` (`Sent` cleared, `NeedsResend` true, flushed again at the
next request), and 4C's flush sends everything again when `NeedsResend` is true.

### Slow functions and `busy`

`FunctionDef.Slow` is the manifest's `slow`. `Dispatcher.Busy` (a text, null when not busy; lane 4F sets it while the worker saves or
recovers) makes the dispatcher answer error 7 `busy` with that text, before the handler runs, for a lane's function that is `Slow`.
Code that changes items or money and is not such a function (a method of a type) calls `Dispatcher.CheckBusy()` first and returns
its outcome when it is not null. A function that is not slow runs while busy.

### Events from any part

On the dispatcher (`Dispatcher.Events.cs`), for a lane's code in the worker; the event must be registered with the same target:

- `EmitItemEvent(name, target, type, classes, args, fallback = null)`: an event of target `item` (protocol 11.1): `target` is the
  object whose handle the proxy takes (`HandleTable` keys: a `CfObject` or a wrapper, the lane's choice, the same as it gives out for
  that item elsewhere), `type` a manifest type name, `classes` the script classes of the object. The keys are written in the order
  `name`, `target`, `handle`, `type`, `classes`, `args`, `fallback`. **On a 1.0 link nothing is added** and the result is
  `EmitResult.NotSent`. A name nobody registered, a type that is not in the manifest are programming faults (`InvalidOperationException`:
  the request is `internal`).
- `EmitPlayerEvent(name, account, args, fallback = null)` and `EmitObjectEvent(name, target, args, fallback = null)`: events of the
  1.0 targets with the 1.1 `fallback` (11.1); on a 1.0 link the event is sent without the fallback (`EmitResult.SentWithoutFallback`),
  so the caller knows that the host will not replace a missing function.
- `Dispatcher.TextFallback(account, text)` (the effect `client.text`) and `dispatcher.InvokeFallback(method, target, args)` (the invoke
  map) make the two kinds of fallback.

## Items of a player (lane 4C of phase 3)

What the extension does for the objects in a logged-in player's tree (docs/mud-phase3.md sections 3, 4.2 and 5). The code is
`Dispatcher.Items.cs`, `Gk/ItemStrings.cs`, `Gk/ItemMatching.cs` and `Gk/TMudObjectMembers.Items.cs`; `SkeletonTests.cs` and
`ItemTests.cs` test it.

### Tags and their lifecycle

An object of an inventory has a **proxy**, a `LevelObject` whose `Level` is `Level.Inventory` (a sentinel that is in no table and is
never saved). The proxy is the key of the handle table, so the members of `TMudObject` read an item as they read an object of a level, and
`mudid` is its tag. `Dispatcher.ItemProxy(ob)` makes the proxy and its tag at the first call; every item of a player's top level has one
at `player.login`, an item in a sack gets one when `muditems` is read, an item that enters the tree when the client is told of it. The lane
that moves an object between a level and an inventory (5A) keeps the one key: it gives the proxy a level (`LevelObject.Level`), or
registers a level object's own proxy with `Dispatcher.AdoptLevelObject`; the tag then does not change.

**Every exit of an object from the tree kills its tag.** The flush at the end of a request (below) looks at the whole window when the
tree changed (the host's `FixPlayer` and `EsrvDelItem` tell it: an object entered or left a tree under a player) and **sweeps** the
objects the player's tree remembers: an object that is freed has its tag killed (a decrease to 0, a merge's freed twin, `free_object`,
a reset); one that is now in another player's tree keeps its tag and that player's window shows it; one that is nowhere, and whose proxy
has no level, is killed. An object that got a tag outside a tree (a twin made before it was inserted) is swept as a "loose" item. At
`player.logout` and at a level's unload, `FreeCfObject` kills the tags of everything inside the freed object. Test:
`Tags_Given_a_thousand_logins_and_logouts...` (`Dispatcher.LiveTags` is the number of live tags; the tags themselves are never reused, so
`Issued` grows, 300 slots of 8 bytes per login of a full pack).

### The ids a client sends

`Dispatcher.FindItemById(player, id, deep)` is the one way to turn an id that came from a client into an object: it looks in the
player's own tree only (the top level and the open container, and with `deep` the sacks). Another player's tag, a level object's tag, a
dead tag, 0 and a value outside 32 bits give null. `IMemberEnv.ItemOfPlayer` is the same for a handle; the write of `markedobject` uses it.

### The strings: `Shown` and the flush

The ported C's `esrv_send_item` and `esrv_update_item` (the host's `EsrvSendItem`, `EsrvUpdateItem`) mark the item in the owning player's
`ShownTable` (`Dispatcher.ItemSent`); `esrv_del_item` (C names the object by a count that a freed object has lost) asks for a look at the whole window; a write of a member of an
item marks it. At the end of a request that ends with status 0 (`FlushControllers`) each marked controller is flushed once
(`Dispatcher.FlushShown`): a request with marks only compares the marked items with what the client holds; a request after a change of the
tree (or after the effects of a request were lost: `NeedsResend`, which also sends the 22 stats again) compares the whole window. Only a
changed text is written, as `client.set` of `clientr.mud_item<id>` (a stack that grows five times in one request writes one effect);
the strings of what left the window are `client.del`. `ItemStrings.Text` is the seven comma fields (docs/mud-phase3.md section 5): the
short name, the picture, the type, the quantity (`nrof`), the weight of the whole stack in grams, the flag word of `ItemStrings.Flags`
(`query_flags`, Graal Kingdoms' types 162 to 166, `F_NOPICK`), and 1 inside the open container. The player's `clientr.mud_weight` (his
weight and `carrying`) and `clientr.mud_weight_lim` (`weight_limit[Str]`) follow in each flush; `clientr.mud_container` is
`<id>,<name>,<picture>` of the container in `ob->container`, the empty text when it was closed. At most 3,800 items are shown (the request
carries at most 4,096 effects); a log line says so once. The names are in `PlayerState.ClientOrder`, so `player.logout` deletes them.
`ExtensionOptions.FlushHook`, a test's stand-in, replaces this flush.

### Lines of text

`CrossfireHost.DrawInfo(who, text)` (C `new_draw_info`) is answered for a logged-in player by the player event `onRPGMessage` (target
player, sync, one parameter of type `string`, `MinMinor` 1) with the `client.text` effect as its fallback; on a 1.0 link, which has no
fallback, by the effect alone. For an object that is not a logged-in player it does nothing. The class `mudplayers` is declared as a
player class on a 1.1 link (`ManifestBuilder.AddPlayerClass`).

### The C functions of the player's controller

`Gk/ItemMatching.cs` ports `item_matched_string`, `find_best_weapon_used_match`, `find_best_object_match`, `find_marked_object`,
`command_mark`, `MarkItem`, `fix_weight` and the table `weight_limit[]`; the loader's call `FindBestWeaponUsedMatch` of the host is
answered by the port. `Player.Count`, `Mark` and `MarkCount` are what C keeps in `struct pl`.

### The members of lane 4C

Ids 1000 to 1002 on `TMudObject` and 1500 to 1503 on `TMudPlayer` (the tables above). `muditems` is a `list:handle:TMudObject`: `get`
gives the handles, `get_list` the rows (`[handle, field values]`), at most `limits.max_list` of them, which is 1,024 on a 1.1 link (256 on
a 1.0 link); more is `too_large`. `mudowner` and `markedobject` have the type `ref`, a 1.1 name: they are not in the manifest of a 1.0 link
(`MemberTable.ToManifest(linkMinor)`), and a type of the core may hold the members of every lane. `mudowner` is stored as an extra line
(`mudowner`, a state key of the loader) with the account text; the extension sends the handle of the player while he is online. Objects a
script or the engine makes (`mudaddobject`, `mudaddspecialobject`, `Dispatcher.NewItem`) are **identified**.

