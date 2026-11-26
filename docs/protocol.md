# The MUD extension protocol, version 1.1

This text is the contract between a game server (the **host**, the Alpaxis GameServer) and a MUD engine that runs as a separate
program (the **extension**, such as the Crossfire extension). Each side implements it from this text alone; no code is shared. The
host's design around it (threads, deadlines, failure handling, persistence, costs) is in the suite's `server/docs/mud-extension.md`;
where the two differ, this text wins.

**Licence of this text:** CC0 1.0 Universal (public domain dedication). To the extent possible under law, the authors have waived
all copyright and related rights to this text, so that any implementation, under any licence, may carry it unchanged. The suite
keeps it as `server/docs/mud-protocol.md` and the Crossfire repository as `docs/protocol.md`; the two copies are identical.

Words: **must** is a requirement; **should** is advice. Every number is decimal unless it is written as bytes (`0a`) or with `0x`.
A breach of a "must" about the bytes or the order of frames is a **protocol error** (section 9); other breaches have the outcome the
text gives at that place.

**Version 1.1 only adds to 1.0.** Everything that this text says about 1.0 keeps its meaning. Each addition of 1.1 starts with the tag
**(1.1)** (in a table: at the start of the row, key or cell); a sentence tagged "(clarified in 1.1)" describes what both sides of 1.0
already did where the 1.0 text was silent or wrong, and changes no behaviour. A side that speaks 1.1 uses an addition only on a link
whose minor version is at least 1 (section 4.1, "Versions"); on any other link it speaks 1.0, and a 1.0 host with a 1.1 extension, or
the reverse, agree on 1.0. The additions are: events on items, typed event parameters, the type `ref`, the event key `fallback`, the
effect `caller.setprop`, the manifest key `player_classes`, the queries `player.pos` and `player.at`, and gmap layouts (the operation
`gmap.layout`).

## 1. Finding, starting and stopping the extension

An extension is a folder `<host installation>/extensions/<name>/` with a file `extension.json` and an executable:

```json
{
  "name": "crossfire",
  "version": "0.1.0",
  "protocol": 1,
  "executable": "Crossfire.Extension",
  "licence": "GPL-2.0-or-later",
  "source": "https://github.com/AlpaxisNet/Crossfire"
}
```

- `name` matches `[a-z0-9-]{1,32}` and equals the folder's name. `protocol` is the major version the extension speaks.
  `executable` is a file name in the same folder (no `/` or `\`). `version`, `licence` and `source` are shown to the host's operator.
  The minor version is not in `extension.json`; the HELLOs carry it (section 4.1).
- The host chooses an extension by name from its own configuration, never from a path given by a script or a remote user.
- **Start.** The host creates a private directory (mode 0700) whose socket path is at most 100 bytes long, listens there on a Unix
  domain stream socket (`AF_UNIX`, `SOCK_STREAM`), and starts the executable with exactly two arguments, `--connect <absolute socket
  path>`, with the extension's folder as the working directory. The host accepts **one** connection and then closes the listener and
  removes the socket's path and directory. There is one connection for the life of the extension's process.
- **Failed start.** Any of these is a failed start, after which the host closes the connection and kills the process: no connection
  within 15 s; the process exits before the REPLY to `session.init`; no HELLO from the extension within 15 s of the connection; a
  HELLO that breaks section 4.1 or 6; `session.init` answered with a status other than 0 or with a result that is not a map. What
  the host does next (retry, run without the extension) is its own design.
- **Running.** The extension holds an exclusive lock (`flock`) on the file `<data_dir>/.lock` while it runs; a new process waits up to
  20 s for it, and fails its start if it cannot get it. It should write a change of its state to its files within 5 s.
- **Stopping.** After the REPLY to `session.shutdown` the host closes the connection. When the connection ends (EOF, an error, a
  protocol error), the extension saves its state (unless `session.shutdown` with `save` false came before) and exits within 5 s;
  the host may kill it after that.
- Standard input, output and error carry no protocol. The extension sends its log lines in `LOG` frames (section 4.5) and should
  write nothing to standard output.

## 2. Frames

A frame is a 4-byte **length** `N` (unsigned, little endian) followed by `N` bytes of **body**. `N` is at least 1 and at most the
link's maximum: 16,777,216 bytes until both HELLOs are read, then the smallest of 16,777,216 and the `max_frame` values the two
HELLOs give (each at least 65,536). The body is one **kind** byte and then the kind's fields, in the order section 4 gives, with no
bytes after the last field. A sender writes every frame whole: the bytes of two frames are never interleaved, whichever threads send
them.

A field is either a **varint** (section 3.1, untagged) or a **value** (section 3.2, a tag byte and its body). Section 4 says which, and
which type a value field has; a value field of another type is a protocol error.

## 3. Encoding

### 3.1 Varints

An unsigned integer below 2^64 in LEB128: seven bits per byte, least significant group first, the high bit set on every byte but the
last. Encoders write the shortest form; decoders must accept any form of at most 10 bytes whose value fits in 64 bits, and must reject
a longer one or one that overflows.

### 3.2 Values

| Tag | Type | Body |
| --- | --- | --- |
| 0 | nil | none |
| 1 | false | none |
| 2 | true | none |
| 3 | int | a signed 64-bit integer `n`, zigzag-mapped (`(n << 1) ^ (n >> 63)`, the shift arithmetic) and written as a varint |
| 4 | float | 8 bytes, IEEE 754 binary64, little endian (NaN and the infinities are allowed) |
| 5 | string | a varint byte count, then that many bytes of UTF-8 (a decoder replaces ill-formed sequences with U+FFFD) |
| 6 | handle | 8 bytes, an unsigned 64-bit integer, little endian (section 5) |
| 7 | list | a varint count, then that many values |
| 8 | map | a varint count, then that many pairs: a key (a varint byte count and UTF-8, untagged) and a value |

- An unknown tag is a protocol error. So is a body that ends inside a varint, a string, a key or an 8-byte body.
- **Counts:** a count of elements, or a byte count of a string or key, larger than the number of bytes left in the body is a protocol
  error.
- **Maps:** keys are unique, compared as their bytes before any replacement (a duplicate is a protocol error). Encoders write keys in
  the order this text lists them, and maps whose keys this text does not list in any order; decoders accept any order and ignore keys
  they do not know.
- **Depth:** a value that is not a list or a map has depth 1; a list or map has depth 1 plus the largest depth of its elements (1 when
  empty). Each field's value, counted from the field, has depth 16 at most.
- **Numbers:** wherever this text or a manifest asks for an int, a float or a number, a receiver accepts an int or a float and
  converts: a float to an int by truncation toward zero, clamped to the 64-bit range, NaN giving 0. A bool, a string or a handle is
  accepted only where its type is asked for, and nil only where the text allows it or the type is `any` (section 6). An int beyond
  2^53 reaches scripts with less precision (scripts count in binary64).

### 3.3 Map keys: required and optional

A key this text lists for a map is **required** unless it is marked *optional* with its default. A HELLO or manifest that lacks a
required key or holds one of the wrong type is invalid (a failed start for the host to read; a protocol error for the extension to
read). An effect or event with such a fault is skipped and counted (sections 10 and 11).

## 4. Frame kinds

| Kind | Name | Sent by | Fields |
| --- | --- | --- | --- |
| 1 | HELLO | both, once, first | value `hello` (map) |
| 2 | REQ | host | varint `reqid`, varint `op`, value `releases` (list of handles), value `ctx` (map), value `args` (list) |
| 3 | REPLY | extension | varint `reqid`, varint `status`, value `result`, value `effects` (list), value `events` (list), value `touched` (nil in 1.0) |
| 4 | QUERY | extension | varint `qid`, value `name` (string), value `args` (list) |
| 5 | QRESULT | host | varint `qid`, varint `status`, value `result` |
| 6 | PING | both | varint `n` |
| 7 | PONG | both | varint `n` |
| 8 | LOG | extension | varint `level` (0 error, 1 warning, 2 information, 3 debug; a larger number counts as 3), value `text` (string) |

An unknown kind is a protocol error, and so is a frame sent at a time this section does not allow.

### 4.1 HELLO

**The host sends its HELLO first**, right after it accepts the connection, and sends nothing more until it has read the extension's.
The extension reads the host's HELLO and answers with its own as its first frame; after that it may send LOG and PING at once. Any
other frame read before the other side's HELLO is a protocol error. If the host's `proto_major` is not one the extension speaks, the
extension sends no HELLO, closes the connection and exits with a non-zero status.

The host's map: `proto_major` (int, 1), `proto_minor` (int, 0), `role` ("gameserver"), `name` (string), `version` (string),
`server_name` (string), `epoch` (int from 1 to 2^32 - 1: one more for every connection the host's process accepts, failed starts
included; section 5), `data_dir` (string: an absolute path the host created, where the extension keeps its files), `world_dir`
(string: an absolute path the extension reads and never writes), `max_frame` (int, at least 65,536), `call_timeout_ms` (int: the
deadline of a normal request; the host may allow some operations longer, section 4.2).

The extension's map: `proto_major` (int, 1), `proto_minor` (int), `role` ("extension"), `name` (string, equal to `extension.json`'s),
`version` (string), `features` (list of strings, *optional*, default []: capabilities beyond the minor version; none are defined, not even in 1.1:
the minor version decides, see below), `manifest` (map, section 6), `max_frame` (int, at least 65,536, *optional*: no further limit). The extension's HELLO must fit in 16,777,216 bytes and
in the host's `max_frame`.

**Versions.** A `proto_major` of the extension that differs from the host's, or from `extension.json`'s `protocol`, is a failed start.
A higher `proto_minor` on either side is accepted; a side ignores map keys it does not know.

(1.1) **How a side knows the other speaks 1.1.** A side's `proto_minor` is the highest minor version it speaks. The **link's minor
version** is the smaller of the two `proto_minor` values, known to the extension when it has read the host's HELLO and to the host when
it has read the extension's. This host sends `proto_minor` 1; this extension sends `proto_minor` 1 when it speaks 1.1. Every 1.1
addition is for a link whose minor version is at least 1, and for none other:

- The **extension** sends a 1.1 key, effect, query, target, type name or operation, and builds its manifest with the 1.1 keys, only
  when the host's `proto_minor` is at least 1. Otherwise it speaks 1.0 and its manifest has no 1.1 key, no event target `item`, no
  `params` and no `ref`. (Its manifest is built after it has read the host's HELLO, so that it can follow this rule.) An extension
  that speaks 1.1 may send `proto_minor` 1 even to a 1.0 host: the 1.0 host accepts it and the link is 1.0.
- The **host** offers 1.1 behaviour (the operation `gmap.layout`, the answers to `player.pos` and `player.at`, the handling of the
  1.1 manifest keys, event keys and effects) only when the extension's `proto_minor` is at least 1. A manifest of an extension whose
  `proto_minor` is 0 is read by the 1.0 rules: a 1.1 key in it (`player_classes`, an event's `params`) is ignored, as every key the
  reader does not know is (section 3.2), and an event target `item` or the type name `ref` in it makes it invalid. A 1.0 host that
  meets `item` or `ref` is in the same case (a failed start); one that meets `params` or `player_classes` ignores them, and runs the
  events with the 1.0 conversion (section 11), which is why an extension follows the rule above.
- After a reconnection both HELLOs are read again and the link's minor version is decided again; the manifest must still be equal
  (section 6).

### 4.2 REQ and REPLY

- `reqid` starts at 1 on each connection and grows by 1 with every REQ. The REPLY to a REQ carries its `reqid`; a REPLY with another
  `reqid` than the one REQ outstanding is a protocol error.
- **One request at a time.** The host sends a REQ only when the REPLY to its previous REQ has been read. The extension answers REQs
  in the order they arrive, each with exactly one REPLY.
- **Deadlines.** The host waits for a REPLY up to a deadline (`call_timeout_ms` for most operations; longer for some, as its design
  says). When the deadline passes, the script's call fails, but the request stays outstanding: the host sends no other REQ until the
  late REPLY arrives, then applies its effects, fires its events (deferred, section 11) and does not use its result.
- `op` is an operation (section 7). `releases` lists handles the host no longer refers to (section 5). `ctx` is a map with `player`
  (the account name of the player the running script acts for, a string, or nil) and `level` (the name of the running script's level,
  or nil); the host always sends both keys; `ctx` describes the caller and may differ from the arguments. (Clarified in 1.1: for a script that runs on a gmap, `ctx.level` is the
  gmap's name and the positions in `args` are in the gmap's tiles; section 7.1 says what an extension does with them.) `args` are the operation's
  arguments: exactly as many as section 7 or the manifest gives, each of its type or nil (section 6.3); another number is `bad_args`.
- `status` is 0 for success or an error code (section 8). On success `result` is the operation's value. On an error `result` is a
  string that says what went wrong, `effects` and `events` are empty and `touched` is nil.
- `effects` (section 10) and `events` (section 11) are lists, applied by the host after it has read the whole REPLY: first every
  effect in order, then every event in order. A REPLY carries at most 4,096 effects and events together; an extension pages larger
  work over several requests.
- `touched` is nil in 1.0. (A later minor version may list the handles whose changeable members the request changed.)
- A REPLY that would not fit in the link's maximum is replaced by a REPLY with status `too_large`; what the operation changed in the
  extension stays changed.

### 4.3 QUERY and QRESULT

While a REQ is outstanding, the extension may ask the host questions. **At most one QUERY is outstanding**: the extension waits for
its QRESULT, and sends no REPLY and no other QUERY before it. `qid` starts at 1 on each connection and grows by 1 with every QUERY;
the QRESULT carries it.

Queries are reads: answering one changes nothing on the host and runs no script. The host answers with status 4 (`unavailable`) a
QUERY that comes while no REQ is outstanding, during `session.init`, `session.flush` and `session.shutdown`, and after the deadline
of the outstanding REQ has passed. An unknown name is status 1, wrong arguments status 2. On a status other than 0 the `result` is a
string; the extension fails its operation or uses a default. The names are in section 12.

### 4.4 PING and PONG

Either side may send PING after the HELLOs; the other answers PONG with the same `n` within 1 s, even while it is busy with a request
(its reader must not wait for its worker). A PONG whose `n` the side did not send is ignored. **Silence:** while a REQ is
outstanding, the host sends a PING after 2 s in which it read no frame, and again every 2 s; after 10 s without any frame (a PONG
counts) the host closes the connection and treats the extension as stopped.

### 4.5 LOG

The extension may send LOG at any time after its HELLO. The host writes the text to its log at that level and may limit how many
lines it keeps.

## 5. Handles

A handle names an object of the extension: an unsigned 64-bit integer whose high 32 bits are the **epoch** of the host's HELLO and
whose low 32 bits are the extension's **tag** for the object.

- **One tag per object.** The extension gives an object one tag for as long as it holds the object, and every reply that names the
  object carries that handle. Tag 0 is never used, and a tag is never used for a second object in one connection. When the tags of an
  epoch are used up, the extension answers every request that would need a new tag with `internal`.
- **Epochs.** A new process starts with no tags in use. The extension never gives a handle of another epoch than its connection's.
  A handle of another epoch is dead: the host turns it into nil, the extension answers `bad_handle` where a live object is needed.
- **Releases.** The host lists a handle in `releases` only when, at the moment it writes the REQ, it holds no script object for it
  (so a handle it was given again in the meantime is not released; (1.1) a deferred item event that the host still holds counts as a script object, section 11.1). It sends at most 4,096 handles in one REQ and keeps the rest for
  later REQs; it never sends a REQ for releases alone; `releases` is empty in `session.init`. The extension applies the releases
  **before** the REQ's operation, ignores dead, unknown and never-issued handles in them, and may then forget an object that nothing
  in its world holds; an object in a level, an inventory, a player or the archetypes keeps its tag. Handles of objects shown by level
  NPCs (section 10) are never released.
- **Typed handles.** A handle may appear where this text or the manifest gives a handle type. The host turns it into a script object:
  the level NPC that shows the object, if one does; else the proxy object of the manifest type (section 6), one proxy per handle, so
  that scripts can compare them. A handle in a place without a handle type (a value of type `any` or `list`, an event's `args`
  without `params`) becomes the NPC that shows it, else the player the handle was bound to by `player.login` (clarified in 1.1: the
  1.0 text said "or nil" and the host also resolves the player), else nil. A script object passed as an argument of a handle type is sent as its handle (a proxy's, an
  NPC's that shows an object, the player's from `player.login`); any other value is sent as nil.
- (1.1) **The type `ref`** (section 6, "Type names") names a place that holds either an object or a text: on the wire it is a handle
  or a string. A script object that has a handle (as above) goes out as that handle, a script text or number as a string (its text,
  as for `string`), anything else, and nil, as nil. A handle that comes in is read as the player bound to it, else the NPC that
  shows it, else the proxy of the type that the name gives (`ref:<type>`, one proxy per handle as above; the bare name `ref` has no
  proxy type, so such a handle is read as nil), nil for a dead handle or one of another epoch; a string stays text; nil stays nil.
  The extension chooses what it sends: for an owner it sends the handle of an online player (a player whose `player.login` handle
  the extension holds) and otherwise the owner's name as a string.
- (1.1) **Typed handles of a type whose `host` is `player`**, in the `params` of an event (section 11), resolve to the player bound
  to the handle, else as in the first bullet. A known asymmetry: elsewhere (arguments, results, members) a `handle:<T>` of such a
  type is still the proxy of `T`, while a `ref` gives the bound player first, so the same handle read through the two types gives two
  different script objects there.

## 6. The manifest

The extension's HELLO carries a map `manifest` that says which script functions, types, members, methods and events exist. The host
registers them once per process. After a reconnection the manifest must be equal to the registered one (as values: lists in order,
maps without regard to the order of keys), or the host runs without the extension until it restarts.

| Key | Value |
| --- | --- |
| `functions` | list of maps `{id, name, params, ret, slow}` (*optional*, default []): `id` an int from 64 to 2^31 - 1, unique; `name` the script function's name; `params` a list of type names; `ret` a type name; `slow` a bool (*optional*, default false: the host allows the function a longer deadline) |
| `types` | list of maps `{id, name, host, members, methods}` (*optional*, default []): `id` an int from 1 to 2^31 - 1, unique; `name` the script type's name (what `objecttype()` gives), unique; `host` one of `object`, `npc`, `player` (section 6.2); `members` a list of maps `{id, name, type, access, stable}` (*optional*, default []), with `access` "r" or "rw" (*optional*, default "r") and `stable` a bool (*optional*, default false, section 6.1); `methods` a list of maps `{id, name, params, ret}` (*optional*, default []) |
| `events` | list of maps `{name, target, mode, params}` (*optional*, default []): `name` unique; `target` one of `object`, `player`, `global`, and (1.1) `item`; `mode` `sync` or `deferred` (section 11); (1.1) `params` a list of type names (*optional*: absent, the event has its 1.0 behaviour; section 11) |
| `wants_tick` | bool (*optional*, default false): the host sends `session.tick` |
| `limits` | map `{max_list}` (*optional*): `max_list` an int from 1 to 4,096 (*optional*, default 256), the most rows `get_list` gives |
| (1.1) `player_classes` | list of strings (*optional*, default []): the names of script classes the host joins to every player it binds (section 6.2). The Crossfire extension gives `["mudplayers"]` |

- **Ids.** Member and method ids are ints from 1 to 2^31 - 1, unique together across the whole manifest. Function, type and event
  names, and the member and method names of one type, are unique when compared with ASCII case folding; names match
  `[A-Za-z_][A-Za-z0-9_]*` and are at most 64 characters long. A name the host already uses for a built-in function, or for a built-in
  property of the object the members land on (section 6.2), makes the manifest invalid.
- **Type names:** `int`, `float`, `bool`, `string`, `any`, `void` (only as `ret`: the result is nil), `handle:<type>` (a handle whose
  proxy is of the manifest type of that name, compared exactly; the type must exist), `list` (a list of `any`), `list:<type name>` (a
  list whose elements have that type, any type name but `void`, so `list:handle:<type>` and `list:list:int` are allowed), and (1.1)
  `ref` and `ref:<type>` (an object or a text, section 5; the type, when given, must exist; it is where `ref` is allowed: anywhere
  a type name is, `params`, `ret`, a member's `type` and the `params` of events, and as the element of a list). Any other name makes
  the manifest invalid.
- (1.1) The key `player_classes`, the event target `item`, an event's `params` and the type names `ref` and `ref:<type>` are for a
  link whose minor version is at least 1, and an extension builds them only for a host whose `proto_minor` is at least 1 (section
  4.1). A host reads a manifest of an extension whose `proto_minor` is 0 by the 1.0 rules: it ignores `player_classes` and `params`
  (unknown keys), and the target `item` and the type names `ref` make the manifest invalid.
- A manifest that breaks this section is invalid: a failed start (section 1).

### 6.1 Stable members

A member is **stable** when its value changes only through a `set` of that member over this link: no other operation, method, event,
tick, login or load changes it. The host may then answer a read from the value it last read, or the value the last `set` returned,
without a request. A member that the extension's rules recompute (hit points, carried weight, a value that depends on other fields)
must not be stable. The host forgets what it holds for a handle when a request for it fails, when a `set` or `invoke` on it passes
its deadline, on `bad_handle`, `object.destroyed`, `npc.remove` and `level.unload`, and on a new connection.

### 6.2 Where members land

Every type has a **proxy** form: a script object of that type name with its members and methods. A type's `host` says where else they
are:

- `object`: only on the proxy.
- `npc`: also on every level NPC of the host (the members of a script object are shared by all NPCs). On an NPC that shows no object
  of this type, a member reads as nil and a method gives nil, without a request. At most one type has host `npc`.
- `player`: also on the host's player objects, for the player of the handle `player.login` gave. At most one type has host `player`,
  and `player.login` gives handles of that type; it must exist if the extension answers `player.login` with a handle.
- (Clarified in 1.1.) **Script variables and members.** On an NPC that shows **no** object, a member's name is not a member but
  the NPC's script variable of that name (`this.<name>`): a read gives its value and a write sets it, with no request (for a member with access "r" a write is not a script-variable write: no variable is set). On an NPC
  that shows an object of the type, the member wins (the NPC is a face of the object). On players the 1.0 rule stays: the member
  of that name, nil where the player has no MUD part, so that a script variable of the same name is not reachable that way.
- (1.1) **Player classes.** When the host binds a player to the handle that `player.login` gave (the first login of the account on
  this link, or a login the host repeats after a reconnection), it joins the player to each class of `player_classes`, in order,
  before it applies the effects and the events of that REPLY, so that the events of `player.login` (for example a `player` event
  `onLoadedMudAccount`) find the functions of those classes. A class is joined as the script function `join` does: its functions
  are the player's from then on and its first lines run. A class the host does not have is skipped and counted. A player that is
  already joined to a class is not joined again. A player without a MUD part (`player.login` gave nil) is joined to none.

### 6.3 Arguments and results

The host sends exactly as many arguments as `params` names, converted to the declared types: numbers as int or float (a script number
for `any` as an int when it has no fraction and lies within ±2^53, else a float), other values as their text for `string`, truthiness
for `bool`, a script array as a list, a script object as section 5 says. An argument the script left out is nil; any argument may
be nil, and the extension reads nil as the zero of the declared type (0, 0.0, false, "", no object, []). A lookup that finds nothing
returns nil, not an error. A result of another type than `ret` (after the conversion of numbers) is a failed call: the script gets
the zero of `ret`, and the host logs it.

(1.1) **`ref`.** An argument of type `ref` is sent as section 5 says (a handle for a script object that has one, else its text,
else nil); its zero is nil. A value of type `ref` (a result, a member's value, a `set` value, an event's argument) is a handle or a
string or nil, any other type is a failed call; the host turns it into a script value as section 5 says.

## 7. Operations

Ops 1 to 63 are fixed ((1.1) 17 is `gmap.layout`; 18 to 63 are reserved: the extension answers `unknown_op`; so does a 1.0 extension,
or a 1.1 extension on a 1.0 link, for 17); ops from 64 up are the manifest's functions (an id
the manifest does not have is `unknown_op`).

| Op | Name | Args | Result |
| --- | --- | --- | --- |
| 1 | `session.init` | `[]` | a map; its keys are information only (the Crossfire extension sends `archetypes`, an int). The first REQ of every connection: the extension loads its world. Before its REPLY no other op is sent; a second `session.init` is `bad_args` |
| 2 | `session.tick` | `[now_ms: int]` | nil. Only with `wants_tick`. `now_ms` is a monotonic count of milliseconds since the host's process started; the host sends a tick about every 100 ms when no other request is outstanding; the extension must use `now_ms`, not the number of ticks, as time |
| 3 | `session.flush` | `[]` | nil. The extension saves a snapshot of everything it holds (it may reply before the files are written, and must write them) |
| 4 | `session.shutdown` | `[save: bool]` | nil, after the files are written when `save` is true; with false it writes nothing. The last REQ: the host closes the connection after its REPLY |
| 5 | `player.login` | `[account: string]` | the player's handle, or nil when the extension has no player for the account (the host then treats the player as one without a MUD part). An account it has no file for makes a new player. A second login of an account without a logout gives the same handle and sends the player's strings again. Accounts are compared as bytes, and `ctx.player` and every effect's `player` use the same string. (Clarified in 1.1.) A nil result is normal, not an error: the extension refused the account's name (for example one with a control character) and the player has no MUD part for this session. The host sends no `player.login` for a guest. When a second epoch's `player.login` repeats an account, the handle is new and the extension sends all the player's strings again; the host deletes the client strings that the old epoch had set for the player and that this login did not set again (a player the host did not log in again keeps them until its logout deletes them) |
| 6 | `player.logout` | `[account: string]` | nil. The effects delete every client string the extension set for the player (it remembers them); the player's handle is dead afterwards. For an account not logged in: nil, no effects |
| 7 | `level.load` | `[level: string, cursor: any]` | a map `{more: bool, cursor: any}`. The first call has cursor nil; each call gives at most 64 `npc.place` effects for this level; the host calls again with the returned cursor (opaque to it, at most 1,024 bytes encoded) until `more` is false, and may send other REQs between two calls. A level the extension knows nothing of, or whose objects are already placed, gives `{more: false, cursor: nil}` and no effects |
| 8 | `level.unload` | `[level: string]` | nil. The extension saves the level's objects and forgets them; their handles are dead afterwards, and a later `level.load` gives new ones. No `npc.remove` effects: the host removes the NPCs itself |
| 9 | `get` | `[handle, member: int]` | the member's value |
| 10 | `set` | `[handle, member: int, value: any]` | the value stored (the extension may clamp it; a stable member is never changed again but by `set`) |
| 11 | `get_many` | `[handle, members: list of int]` | a list of the values, in order |
| 12 | `get_list` | `[handle, member: int, fields: list of int]` | for a member of type `list:handle:<T>`: a list of rows `[element handle, value of each field in order]` (`fields` are members of T; `[]` gives rows of one element). More rows than `max_list` is `too_large` (the host then reads the member with `get`) |
| 13 | `invoke` | `[handle, method: int, args: list]` | the method's value (`args` as its `params`) |
| 14 | `object.destroyed` | `[handle]` | nil. A script destroyed the NPC that showed the object: the extension destroys the object as its own rules would (its effects and events follow), and its handle is dead afterwards |
| 15 | `stats` | `[]` | a map of the extension's counters (names and meanings are its own) |
| 16 | `objects.blobs` | `[blobs]` | nil. `blobs` is a list of rows `[handle, x: number, y: number, blob: string]`: for objects shown by level NPCs, the NPC's position in tiles and its script state as text, which the extension stores with the object without reading it, replacing the row it had, and gives back in the `npc.place` that shows the object again. The host sends the rows of a level's NPCs before `level.unload`, and of every NPC that shows an object before `session.flush` and `session.shutdown`, in as many requests as the frame limit needs. Rows for dead or unknown handles are ignored |
| (1.1) 17 | `gmap.layout` | `[gmap: string, width: int, height: int, segments: list of string, size: int]` | nil. Section 7.1. Sent only on a link whose minor version is at least 1 |
| 64+ | a manifest function | as its `params` | as its `ret` |

**Level names.** (Clarified in 1.1.) Level names compare without regard to ASCII case (`A` to `Z` against `a` to `z`; level names are
ASCII), wherever a name is compared: `ctx.level`, `level.load`, `level.unload`, `npc.place`, the queries. A side may answer in any
spelling (the Crossfire extension answers in lower case) and a receiver compares without case: the same holds for the segment names
of `gmap.layout`, so that a layout that repeats a segment name in another case repeats it. A gmap's name (section 7.1) is a level name for this rule.

**Handles in requests.** Where an operation needs a live object, a nil, dead, never-issued or wrong-epoch handle is `bad_handle`. A
member or method id that does not exist or does not belong to the object's type, or a `set` of a member with `access` "r", is
`bad_args`; `get_many` and `get_list` with any bad id fail as a whole. A `set` value of another type is converted as section 3.2 says
for numbers, else it is `bad_args`.

### 7.1 (1.1) Gmaps

A gmap is a grid of levels, its **segments**, that scripts see as one big level. A script that runs on a gmap segment names the gmap
in `ctx.level` and counts positions in the gmap's tiles; the host's levels, `level.load` and `level.unload` are the segments. In 1.0
an extension keeps an object made by such a script under the gmap's name, so that it is not saved with the segment that holds it and
does not come back with it. 1.1 fixes this and changes nothing else: **the extension resolves a gmap position to a segment and the
position in it, and every level name that the extension sends to the host or reads from it is a segment's.** The extension does the
resolving (and not the host, which would have to guess which arguments are positions) from a layout that the host tells it.

**`gmap.layout`** (op 17) `[gmap, width, height, segments, size]`: `gmap` the gmap's name; `width` and `height` ints, at least 1, the
number of segments in a row and of rows; `segments` a list of `width * height` strings (at most 4,096), the segments' level names row by row, left to
right inside a row (the segment at column `c`, row `r` is `segments[c + r * width]`), the empty string for a cell that has no level;
`size` an int at least 1, the side of a segment in tiles (64 for every Graal gmap). The result is nil. The rules:

- The host sends it only on a link whose minor version is at least 1, for every gmap that has a segment it will load for the
  extension, **once per connection and before the first `level.load` of any of the gmap's segments** (a reconnection starts a new
  connection: the extension has forgotten every layout, so the host sends them again before it loads the first segment). A segment
  that belongs to two gmaps gives two requests. When a gmap's file changes while the connection lasts, the host sends it again; the
  extension replaces the layout and moves nothing. A gmap whose request would not fit in the link's maximum frame, or whose name is
  also the name of a level the host serves, is not announced: the extension keeps 1.0 behaviour for the name. The reply carries no
  effects and no events.
- The extension answers `bad_args` to a request whose list is not `width * height` strings, whose numbers are out of range, or
  that repeats a non-empty segment name (compared without ASCII case, section 7), or whose `width` or `height` is more than 4,096 or
  whose `width * height` is more than 4,096 (and keeps no layout of that gmap: 1.0 behaviour for the name), and nil otherwise. A name
  the extension has no layout for is a level name.
- **Resolving.** For a gmap `G` that has a layout and a position (`x`, `y`) in its tiles (numbers, fractions allowed), the column is
  `floor(x / size)`, the row `floor(y / size)`, and the segment is `segments[column + row * width]`; the position in the segment is
  `(x - column * size, y - row * size)`. A position outside the grid, with a component that is NaN or infinite, or in a cell with an empty name, belongs to no segment: the
  operation that needed one fails as the extension's rules say for a place with no level (a drop is refused, an object is not made),
  and nothing is sent for it.
- **Where the extension uses it.** Wherever a request names a level together with a position (the `ctx.level` of a script with the
  position its operation gives, for an object made, dropped or moved to a place), the extension resolves a gmap's name as above
  before anything else. The object is then placed (`npc.place`'s `level`, `x` and `y`) with the segment's name and the position in
  the segment, and belongs to that segment: it is saved with the segment, comes back from `level.load` of that segment and goes with
  `level.unload` of it. An object stays with the segment it was placed in until the extension places it again: a position outside
  the segment's tiles (the NPC walked away) is kept as the host sent it, in the rows of `objects.blobs`, which are in the tiles of
  the NPC's own level.
- **Queries.** The extension sends segment names. For `level.near` with a gmap's name it asks every segment that the circle
  touches **and every other loaded segment that holds an object** (the host finds the NPCs of a segment by their position in that
  segment's own tiles, and an object keeps its segment when its NPC walks into the area of another one: a circle that touches only
  the other segment would miss it), with the centre written in each segment's tiles (it may lie outside `0` to `size`, and the host
  accepts any finite number), and it merges the rows (translating each row back to gmap tiles for a script that is on the gmap);
  `level.blocked` asks the one segment of the position (it depends on the tile, not on an object), and `level.loaded` the segment
  of a position or a segment name. The rows of `npc.pos` already name segments; for a script on the gmap the extension
  gives positions in the gmap's tiles (`column * size + x`, `row * size + y` of the segment `ctx.level`'s layout puts it in).
  `player.pos` answers a segment and the position in it (section 12); for a script on a gmap that has a layout (the `ctx.level` of the
  request that asks) the extension **must** translate it the same way, into the gmap's tiles.
- On a link below 1.1 nothing of this applies and the extension does what 1.0 said.

## 8. Error codes

| Status | Name | Meaning |
| --- | --- | --- |
| 0 | ok | |
| 1 | `unknown_op` | the op, or a query's name, is not known |
| 2 | `bad_args` | wrong number or types of arguments, or an id that does not fit |
| 3 | `bad_handle` | a handle is dead, unknown or nil where an object is needed |
| 4 | `unavailable` | not ready, or a query the host does not answer now (section 4.3) |
| 5 | `too_large` | the answer would not fit |
| 6 | `version` | a version the side does not speak |
| 7 | `busy` | the side cannot take the request now |
| 8 | `internal` | anything else |

Every status but 0 fails the call: the script gets the zero of its type, and the host logs it (and limits how often). An unknown
status counts as `internal`.

## 9. Protocol errors

These are protocol errors: a frame length out of range; an unknown kind or tag; a body that ends inside a field; a count or byte count
past the end; a depth over 16; bytes after the last field; a duplicate key; a varint over 10 bytes or over 64 bits; a field of the
wrong type; a frame sent when section 4 does not allow it (among them a REPLY with the wrong `reqid`, a QUERY while one is
outstanding, a REQ after `session.shutdown`'s REPLY). The side that reads one closes the connection. The host then treats the extension
as stopped (its design restarts it); the extension saves and exits (section 1).

## 10. Effects

An effect is a map whose key `k` (a string) names it. The host applies the effects of a REPLY in order after reading it. An element
that is not a map, an unknown `k`, a known effect with a missing or wrongly typed key, an effect for a player who is not online, and an
effect for a level that is not loaded are skipped and counted; the host never loads a level for an effect.

| `k` | Keys | What the host does |
| --- | --- | --- |
| `npc.place` | `handle`, `type` (a manifest type name), `level` (string), `x`, `y` (numbers, in tiles, fractions allowed), `image` (string), `classes` (list of strings), `props` (map), `blob` (string or nil) | shows the object as an NPC of the level, joined to the classes (a class the host does not have is skipped), with `props` set and its script state restored from `blob`; the NPC's `onCreated` runs in the host's next script frame. (Clarified in 1.1.) A class the NPC joins runs its first lines (the code outside its functions, where `join` of other classes may stand) as the script function `join` does, in the host's next script frame. (1.1) On a link below 1.1, and for a gmap without a layout, `level` may be a gmap's name as in 1.0; otherwise it is a segment's (section 7.1) For an object already shown it moves that NPC (level, x, y) and sets `image`, `classes` and `props`, keeping its script state and ignoring `blob`. Inside a `level.load`, `level` must be the loaded level |
| `npc.remove` | `handle` | removes the NPC that shows the object, without events |
| `npc.setprop` | `handle`, `name` (string), `value` | sets one property of the NPC that shows the object |
| `client.set` | `player` (account), `name` (string), `value` | sets the player's string `name`, the whole flag name with its prefix (`clientr.mud_hp`, `client.mud_item12`), to `value` as text |
| `client.del` | `player`, `name` | deletes that string |
| `client.text` | `player`, `text` (string) | shows the text to the player as an RPG message (what the script function `player.sendrpgmessage` does) |
| (1.1) `caller.setprop` | `name` (string), `value` | sets one property of the **caller**: the NPC whose script made the request that this REPLY answers (the level NPC that is `this` at the moment of the call: under `with (other)` the NPC that `with` names, if it is a level NPC; a database NPC or the control NPC is not one), which the host remembers per outstanding request, with the rules of `npc.setprop` for names and values. It is skipped and counted when the request was not made by the script of a level NPC (a request of the host's own: `session.*`, `player.login`, `player.logout`, `level.*`, `objects.blobs`, `gmap.layout`; or a script that has no NPC), or when the NPC is gone by the time the effect is applied. In a late REPLY (the script's call has returned already) it still applies, to the remembered NPC |
| `client.item`, `output` | | reserved: the extension must not send them in 1.0, and the host skips them |

**Property names** in `props`, `npc.setprop` and (1.1) `caller.setprop`: a name `this.<variable>` sets that script variable of the NPC (the variable's name
matching `[A-Za-z_][A-Za-z0-9_]*`); of the NPC's own properties only `image`, `ani`, `nick` and `dir` may be set. Any other name is
skipped and counted; position and level change only through `npc.place`. A `client.set` or `client.del` name that starts with neither
`client.` nor `clientr.` is skipped. An `npc.remove` or `npc.setprop` for an object no NPC shows is skipped.

## 11. Events

An event is a map: `name` (string, a manifest event), `target` (the manifest's target for it), `handle` (for target `object`, and (1.1) for
target `item`), `player` (the account, only for target `player`), (1.1) for target `item` `type` (string) and `classes` (list of
strings, *optional*, default []), `args` (list), and for an event of any target (1.1) `fallback`; an encoder writes the keys in this
order (section 3.2). The host fires the events of a
REPLY in order after its effects:

- `object`: the event runs on the level NPC that shows the object (`this` is the NPC), with `args` as the parameters.
- `player`: the event runs on the player's object (`this` is the player, through the classes the player joined), with `args`.
- `global`: the event runs on every global script object of the host (its database NPCs and control NPC) that defines it, with `args`.
- (1.1) `item`: the event runs on the **proxy** of `handle` (section 5), `this` being the proxy, as section 11.1 says.

With mode `sync` an event runs at once, before the call that caused it returns to the script; with `deferred` it runs in the host's
next script frame. The events of a late REPLY, and `sync` events nested more than 8 deep, run deferred. A script run by an event may
make requests of its own: each is a new REQ, sent after the REPLY that fired the event was read, and its own effects and events are
applied at its own REPLY, before the remaining events of the first. An event with an unknown name, a target other than the manifest's,
a missing key, an object no NPC shows (target `object`), or a player who is not online is skipped and counted. (1.1) A `sync` event
of target `item` (and of no other target) may also run deferred when the host cannot run it nested, as 11.1 says; the host counts it.

**(1.1) Typed parameters (`params`).** An event whose manifest entry has `params` (a list of type names, as for functions: `int`,
`float`, `bool`, `string`, `any`, `handle:<type>`, `ref`, `ref:<type>`, `list`, `list:<type>`; not `void`) is called with `args`
converted like the arguments of a function (section 6.3), in order. `args` must have exactly as many elements as `params`, and an
element that is nil is the zero of its type (section 6.3); otherwise the event is skipped and counted. A `handle:<type>` parameter
is resolved as section 5 says for a typed handle (a type whose `host` is `player` gives the bound player first). On a link below 1.1 `params` is ignored. An event without
`params` has the 1.0 behaviour: each element of `args` is converted as a value of type `any`, so that a handle becomes the NPC that
shows it, else the player bound to it (section 5), and a nil is the engine's nil (0).

### 11.1 (1.1) Events on items

An object in a player's inventory or a container is shown by no NPC, so an event of target `item` runs on a proxy. The manifest entry
is `{name, target: "item", mode, params}`; the event map has `handle` (a live handle of this epoch), `type` (the name of a manifest
type: the proxy's type), `classes` (the names of the script classes of the object, in order), `args` and optionally `fallback`. The
host does, in this order:

1. **The proxy.** It takes the proxy of `handle` for `type` (one per handle, section 5; made if there is none, and the same proxy
   that a script holds if it holds one). An NPC that shows the object does not change this: the event of an item runs on the proxy,
   whether or not an NPC shows the object. A handle that is nil, dead or of another epoch, a `type` that is not a manifest type, a
   `classes` that is not a list of strings, or a proxy of the handle that already has another type, skips and counts the event.
   **What the host keeps.** The state of an item's proxy (the classes joined to it, whether each one has been initialised, and its
   `this.<variable>`s) belongs to the **handle and its epoch**, not to a script object: the host keeps it until the handle is
   listed in `releases` or the epoch ends, even if no script holds the proxy for a while (the host may keep the proxy itself alive
   to do so). A later event of the handle therefore finds the classes joined, the init not repeated and the variables as the last
   event left them. A handle has one proxy type in an epoch, the type of the first proxy the host made for it; an event whose `type`
   differs skips and counts. A deferred item event that the host holds counts as a script object for the handle (section 5): the
   host does not list the handle in `releases` until the event has run or been skipped. An extension that forgets a handle anyway
   has the event skipped and counted (a dead handle).
2. **Joining and init.** It joins the proxy to each class of `classes` that is not yet joined to it (a name the host has no class
   for is skipped and counted; a class is joined as `join` does, its first lines run, and a class they join in turn counts as a new
   one). Then, for each class newly joined in this step, in the order of joining, it runs that class's `onCreated` once on the proxy
   (the **init**); a class that was already joined to the proxy is not initialised again. The init is run in the player context of
   step 3. The host does not run the init again for a later event of the same proxy.
3. **The player context.** The run's player is the player of the first element of `args` when that is a handle (or a ref that reads
   as a player) of a player bound by `player.login` and the player is online; otherwise the run has no player. In the class code
   the bare name `player` is that player and `clientr.<name>` reads and writes that player's client strings (the same as they are
   for a script that the player's own object runs); with no player context they are nil and the writes do nothing.
4. **The call.** It runs the function named `name` on the proxy, `this` being the proxy, with `args` as the parameters (typed by
   `params` when the manifest gives them), in the player context, as a script run by an event (a request that it makes is a new REQ).
5. **No function.** When no class joined to the proxy defines a function `name` (names compare without ASCII case, as script
   functions do), the event does not run, and the `fallback` below runs in its place; when the event has no `fallback` it is skipped
   and counted. (The init of step 2 has been done in either case.)

The `type` of an item event may be any manifest type: every type has a proxy form (section 6.2), also one whose `host` is `npc` or
`player`; the proxy is never the level NPC or the bound player, whatever the `host`. The player context of step 3 is only a handle
(or a `ref` that arrives as a handle) of a bound player: a string account in `args`, even one of an online player, gives no player
context.

**Re-entry.** A host may refuse to run an event of target `item` nested inside a script that is already running a class that the
event needs (two events on the same class machine in one stack). It then runs the event deferred (as a `sync` event nested more than
8 deep does) and counts it. A host that can run such an event nested may do so. Both conform: an extension must not depend on a
`sync` item event having run before the call that caused it returns, and may depend on the order of two deferred events of one
REPLY (the order of the REPLY).

**`fallback` (1.1).** The optional key `fallback` of an event map of any target is an effect map or an invoke map:

- an **effect map** is any effect of section 10 (`{k: "client.text", player, text}` is the usual one), applied as the effects of a
  REPLY are, with their skip rules;
- an **invoke map** `{invoke: name, handle, args}`: `name` a method (ASCII case folded) of the manifest type of the event (for an
  `item` event the type of `type`; for an `object` event the type the object was placed with; for a `player` event the type whose
  `host` is `player`; a `global` event has no type and its invoke fallback is skipped and counted), `handle` a live handle, `args`
  the method's arguments. The host makes the request `invoke` (op 13) of the handle with that method as a new REQ, as a script's
  call does, after the REPLY that fired the event was read: its result is not used, its effects and events are applied at its REPLY.

The host runs the fallback **only when no class joined to the event's target defines the event's function** (names compare without
ASCII case): for `object` the NPC, for `player` the player, for `item` the proxy, for `global` none of the global script objects. It
runs it only when the missing function is the **only** reason the event does not run: an event that is skipped for another reason (no
NPC shows the object, the player is offline, a wrong number of `args` for `params`, a dead handle, a host that does not run events or
scripts at that moment) runs no fallback. It runs it in the event stage, at the position and with the mode of the event (`sync` at
once, `deferred` in the next frame), and for the events of a late REPLY deferred as these are. A fallback that is not an effect or
invoke map, or that fails its rules, is skipped and counted.

- The `ctx` of the REQ of an invoke fallback is `player`: the player of the event (for `item` the player context of step 3, for
  `player` the event's player, else nil) and `level`: the level of the NPC (for `object`, a segment's name on a gmap), else nil.
- An invoke fallback that ends in an error status is counted and logged; nothing else happens, and the event is not run again.
- An invoke fallback counts as a nested call for the depth rule of section 11: the events of its REPLY, and their fallbacks, run
  deferred beyond depth 8. A host may in addition stop a chain of more than 64 fallbacks in a row and count it.
- An invoke fallback is a REQ made after the REPLY that fired the event was read. When that REPLY answers `session.shutdown` (the
  last REQ) no REQ follows: an invoke fallback is skipped and counted, an effect fallback is applied. In the REPLY of `session.init`,
  `session.flush` or `level.load` an invoke fallback is made as in any other.

## 12. Queries

| Name | Args | Result |
| --- | --- | --- |
| `level.near` | `[level: string, x, y, r]` (numbers, in tiles) | a list of rows `[handle, x, y]`, the NPCs showing objects within `r` tiles of (x, y), nearest first, at most 1,000; `[]` for a level that is not loaded |
| `level.blocked` | `[level: string, x, y]` | bool: the tile is blocked for walking; true for a level that is not loaded |
| `npc.pos` | `[handles: list]` | a list of rows `[x, y, level]` in order, nil for an object no NPC shows |
| `player.exists` | `[account: string]` | bool: the player is online |
| `level.loaded` | `[level: string]` | bool |
| (1.1) `player.pos` | `[account: string]` | `[level, x, y]`: the level's name (a segment's, on a gmap) and the player's position in that level's tiles (numbers, fractions allowed); nil for an account that is not online or whose player is in no level. Asked on a link below 1.1 the host answers status 1 |
| (1.1) `player.at` | `[account: string, index: int]` | the account (a string) of the player at position `index` (counted from 0, converted as section 3.2 says for numbers) in the `players` of the **script level** of the player `account`, in the order a script sees them (the player `account` included): that is the level the player is in, and for a segment of a gmap the whole gmap (the players of all its segments). An entry that is not one player of its own account (a guest: all guests share the account `guest`; a remote-control or NPC-Server player; a player without an account) counts in the index and is answered as nil. Nil also when `account` is not online or is in no level, and when `index` is negative or out of range. Asked on a link below 1.1 the host answers status 1 |

(Clarified in 1.1.) An account that more than one online player has (all guests have `guest`) is answered for the first of them. Level names in a query compare as section 7 says. The handles in query results name objects shown by NPCs; they are not references the host must
release. (1.1) `player.pos` and `player.at` answer for an account that is online with or without a MUD part, and their answers are
read only: they run no script. Where the player's level is a gmap segment, `level` is the segment's name and `x`, `y` are local
to it (section 7.1).

## 13. Golden vectors

The frames below are complete (length and body), written in hexadecimal. Both implementations must encode these exact bytes from the
values given and decode them back. They come from the reference encoder that made this text. REPLY 4 and REPLY 5 answer requests
that are not shown; REQ 3 releases a handle that was never issued, which the extension ignores. REPLY 5 shows the encoding of an effect and an event only:
its event is not in the example manifest, so a host would skip it.

### 13.1 Values

| Value | Bytes |
| --- | --- |
| nil | `00` |
| false | `01` |
| true | `02` |
| int 0 | `03 00` |
| int 1 | `03 02` |
| int -1 | `03 01` |
| int 63 | `03 7e` |
| int -64 | `03 7f` |
| int 64 | `03 80 01` |
| int 300 | `03 d8 04` |
| int -300 | `03 d7 04` |
| int 9223372036854775807 | `03 fe ff ff ff ff ff ff ff ff 01` |
| int -9223372036854775808 | `03 ff ff ff ff ff ff ff ff ff 01` |
| float 1.5 | `04 00 00 00 00 00 00 f8 3f` |
| float -0.25 | `04 00 00 00 00 00 00 d0 bf` |
| string "" | `05 00` |
| string "sword" | `05 05 73 77 6f 72 64` |
| string "Hé" (UTF-8 c3 a9) | `05 03 48 c3 a9` |
| handle epoch 1, tag 7 | `06 07 00 00 00 01 00 00 00` |
| list [] | `07 00` |
| list [1, "a", nil] | `07 03 03 02 05 01 61 00` |
| map {} | `08 00` |
| map {"a": 1, "b": [true]} | `08 02 01 61 03 02 01 62 07 01 02` |

### 13.2 Frames

The HELLOs hold these maps, with the keys in this order. The host's: `proto_major` 1, `proto_minor` 0, `role` "gameserver", `name`
"Alpaxis GameServer", `version` "1.0", `server_name` "Test", `epoch` 1, `data_dir` "/srv/mud-data/Test", `world_dir` "/srv/servers/Test",
`max_frame` 16777216, `call_timeout_ms` 500. The extension's: `proto_major` 1, `proto_minor` 0, `role` "extension", `name` "crossfire",
`version` "0.1.0", `features` [], `manifest` {`functions` [{`id` 64, `name` "findarchetype", `params` ["string"], `ret`
"handle:TMudArch", `slow` false}], `types` [{`id` 1, `name` "TMudArch", `host` "object", `members` [{`id` 100, `name` "mudname", `type`
"string", `access` "r", `stable` true}], `methods` []}], `events` [], `wants_tick` false, `limits` {`max_list` 256}}. A `ctx` with no
player and no level is the map {`player` nil, `level` nil}.

#### HELLO from the GameServer
```
cc 00 00 00 01 08 0b 0b 70 72 6f 74 6f 5f 6d 61 6a 6f 72 03 02 0b 70 72 6f 74 6f 5f 6d 69 6e 6f 72 03 00 04 72 6f 6c 65 05 0a 67 61 6d 65 73 65 72 76 65 72 04 6e 61 6d 65 05 12 41 6c 70 61 78 69 73 20 47 61 6d 65 53 65 72 76 65 72 07 76 65 72 73 69 6f 6e 05 03 31 2e 30 0b 73 65 72 76 65 72 5f 6e 61 6d 65 05 04 54 65 73 74 05 65 70 6f 63 68 03 02 08 64 61 74 61 5f 64 69 72 05 12 2f 73 72 76 2f 6d 75 64 2d 64 61 74 61 2f 54 65 73 74 09 77 6f 72 6c 64 5f 64 69 72 05 11 2f 73 72 76 2f 73 65 72 76 65 72 73 2f 54 65 73 74 09 6d 61 78 5f 66 72 61 6d 65 03 80 80 80 10 0f 63 61 6c 6c 5f 74 69 6d 65 6f 75 74 5f 6d 73 03 e8 07
```
#### HELLO from the extension
```
56 01 00 00 01 08 07 0b 70 72 6f 74 6f 5f 6d 61 6a 6f 72 03 02 0b 70 72 6f 74 6f 5f 6d 69 6e 6f 72 03 00 04 72 6f 6c 65 05 09 65 78 74 65 6e 73 69 6f 6e 04 6e 61 6d 65 05 09 63 72 6f 73 73 66 69 72 65 07 76 65 72 73 69 6f 6e 05 05 30 2e 31 2e 30 08 66 65 61 74 75 72 65 73 07 00 08 6d 61 6e 69 66 65 73 74 08 05 09 66 75 6e 63 74 69 6f 6e 73 07 01 08 05 02 69 64 03 80 01 04 6e 61 6d 65 05 0d 66 69 6e 64 61 72 63 68 65 74 79 70 65 06 70 61 72 61 6d 73 07 01 05 06 73 74 72 69 6e 67 03 72 65 74 05 0f 68 61 6e 64 6c 65 3a 54 4d 75 64 41 72 63 68 04 73 6c 6f 77 01 05 74 79 70 65 73 07 01 08 05 02 69 64 03 02 04 6e 61 6d 65 05 08 54 4d 75 64 41 72 63 68 04 68 6f 73 74 05 06 6f 62 6a 65 63 74 07 6d 65 6d 62 65 72 73 07 01 08 05 02 69 64 03 c8 01 04 6e 61 6d 65 05 07 6d 75 64 6e 61 6d 65 04 74 79 70 65 05 06 73 74 72 69 6e 67 06 61 63 63 65 73 73 05 01 72 06 73 74 61 62 6c 65 02 07 6d 65 74 68 6f 64 73 07 00 06 65 76 65 6e 74 73 07 00 0a 77 61 6e 74 73 5f 74 69 63 6b 01 06 6c 69 6d 69 74 73 08 01 08 6d 61 78 5f 6c 69 73 74 03 80 04
```
#### REQ 1: session.init
```
18 00 00 00 02 01 01 07 00 08 02 06 70 6c 61 79 65 72 00 05 6c 65 76 65 6c 00 07 00
```
#### REPLY 1: session.init done, 3 archetypes
```
17 00 00 00 03 01 00 08 01 0a 61 72 63 68 65 74 79 70 65 73 03 06 07 00 07 00 00
```
#### REQ 2: findarchetype("sword") for player gs1player in main.nw, releasing nothing
```
31 00 00 00 02 02 40 07 00 08 02 06 70 6c 61 79 65 72 05 09 67 73 31 70 6c 61 79 65 72 05 6c 65 76 65 6c 05 07 6d 61 69 6e 2e 6e 77 07 01 05 05 73 77 6f 72 64
```
#### REPLY 2: the handle (epoch 1, tag 7)
```
11 00 00 00 03 02 00 06 07 00 00 00 01 00 00 00 07 00 07 00 00
```
#### REQ 3: get member 100 of that handle, releasing handle (1, 5)
```
2d 00 00 00 02 03 09 07 01 06 05 00 00 00 01 00 00 00 08 02 06 70 6c 61 79 65 72 00 05 6c 65 76 65 6c 00 07 02 06 07 00 00 00 01 00 00 00 03 c8 01
```
#### REPLY 3: "sword"
```
0f 00 00 00 03 03 00 05 05 73 77 6f 72 64 07 00 07 00 00
```
#### REPLY 4: error 3 bad_handle
```
15 00 00 00 03 04 03 05 0b 64 65 61 64 20 68 61 6e 64 6c 65 07 00 07 00 00
```
#### REPLY 5: an effect and an event
```
84 00 00 00 03 05 00 00 07 01 08 03 01 6b 05 0b 63 6c 69 65 6e 74 2e 74 65 78 74 06 70 6c 61 79 65 72 05 09 67 73 31 70 6c 61 79 65 72 04 74 65 78 74 05 14 59 6f 75 20 70 69 63 6b 20 75 70 20 61 20 73 77 6f 72 64 2e 07 01 08 04 04 6e 61 6d 65 05 0e 6f 6e 55 70 64 61 74 65 4f 62 6a 65 63 74 06 74 61 72 67 65 74 05 06 6f 62 6a 65 63 74 06 68 61 6e 64 6c 65 06 07 00 00 00 01 00 00 00 04 61 72 67 73 07 00 00
```
#### QUERY 1: level.loaded("main.nw")
```
1b 00 00 00 04 01 05 0c 6c 65 76 65 6c 2e 6c 6f 61 64 65 64 07 01 05 07 6d 61 69 6e 2e 6e 77
```
#### QRESULT 1: true
```
04 00 00 00 05 01 00 02
```
#### PING 9
```
02 00 00 00 06 09
```
#### PONG 9
```
02 00 00 00 07 09
```
#### LOG level 1: "loaded 3 archetypes"
```
17 00 00 00 08 01 05 13 6c 6f 61 64 65 64 20 33 20 61 72 63 68 65 74 79 70 65 73
```

### 13.3 (1.1) Vectors of version 1.1

These frames are protocol 1.1's; an implementation of 1.1 must encode and decode them too. The maps have their keys in the order given
here. The host's HELLO is the one of 13.2 with `proto_minor` 1 and `version` "1.1". The extension's HELLO has `proto_minor` 1, `version`
"0.2.0", `features` [] and this manifest: `functions` [], `types` [{`id` 1, `name` "TMudObject", `host` "object", `members` [{`id` 100,
`name` "mudowner", `type` "ref", `access` "rw", `stable` false}], `methods` [{`id` 200, `name` "defaultapply", `params` [], `ret`
"void"}]}, {`id` 2, `name` "TMudPlayer", `host` "player", `members` [], `methods` []}], `events` [{`name` "onApply", `target` "item",
`mode` "sync", `params` ["handle:TMudPlayer", "int"]}, {`name` "onOwnerChanged", `target` "object", `mode` "deferred", `params`
["ref"]}], `wants_tick` false, `limits` {`max_list` 256}, `player_classes` ["mudplayers"]. REQ 6 is `gmap.layout("world.gmap", 2, 2,
["a.nw", "b.nw", "c.nw", ""], 64)` with the context {`player` nil, `level` nil}; REQ 7 is `set` of member 100 of handle (1, 7) to the
text "gs1player", with the context of REQ 2. REPLY 7 has one effect, `caller.setprop` {`name` "this.weapon1", `value` "sword"}, and two
events of target `item`: the first with `handle` (1, 7), `type` "TMudObject", `classes` ["specialitem"], `args` [handle (1, 9), 3]
and the `fallback` {`k` "client.text", `player` "gs1player", `text` "You cannot use that."}; the second with `handle` (1, 8), `classes`
[], `args` [handle (1, 9), 0] and the `fallback` {`invoke` "defaultapply", `handle` (1, 8), `args` []}. Its manifest events are the ones
of the extension's HELLO above. QUERY 2 asks `player.pos` ["gs1player"] and QRESULT 2 answers ["main.nw", 12.5, 30.25]; QUERY 3 asks
`player.at` ["gs1player", 1] and QRESULT 3 answers "gs2player".

#### HELLO 1.1 from the GameServer (1.1: proto_minor 1)
```
cc 00 00 00 01 08 0b 0b 70 72 6f 74 6f 5f 6d 61 6a 6f 72 03 02 0b 70 72 6f 74 6f 5f 6d 69 6e 6f 72 03 02 04 72 6f 6c 65 05 0a 67 61 6d 65 73 65 72 76 65 72 04 6e 61 6d 65 05 12 41 6c 70 61 78 69 73 20 47 61 6d 65 53 65 72 76 65 72 07 76 65 72 73 69 6f 6e 05 03 31 2e 31 0b 73 65 72 76 65 72 5f 6e 61 6d 65 05 04 54 65 73 74 05 65 70 6f 63 68 03 02 08 64 61 74 61 5f 64 69 72 05 12 2f 73 72 76 2f 6d 75 64 2d 64 61 74 61 2f 54 65 73 74 09 77 6f 72 6c 64 5f 64 69 72 05 11 2f 73 72 76 2f 73 65 72 76 65 72 73 2f 54 65 73 74 09 6d 61 78 5f 66 72 61 6d 65 03 80 80 80 10 0f 63 61 6c 6c 5f 74 69 6d 65 6f 75 74 5f 6d 73 03 e8 07
```
#### HELLO 1.1 from the extension (1.1: an item event, a ref parameter, player_classes)
```
1f 02 00 00 01 08 07 0b 70 72 6f 74 6f 5f 6d 61 6a 6f 72 03 02 0b 70 72 6f 74 6f 5f 6d 69 6e 6f 72 03 02 04 72 6f 6c 65 05 09 65 78 74 65 6e 73 69 6f 6e 04 6e 61 6d 65 05 09 63 72 6f 73 73 66 69 72 65 07 76 65 72 73 69 6f 6e 05 05 30 2e 32 2e 30 08 66 65 61 74 75 72 65 73 07 00 08 6d 61 6e 69 66 65 73 74 08 06 09 66 75 6e 63 74 69 6f 6e 73 07 00 05 74 79 70 65 73 07 02 08 05 02 69 64 03 02 04 6e 61 6d 65 05 0a 54 4d 75 64 4f 62 6a 65 63 74 04 68 6f 73 74 05 06 6f 62 6a 65 63 74 07 6d 65 6d 62 65 72 73 07 01 08 05 02 69 64 03 c8 01 04 6e 61 6d 65 05 08 6d 75 64 6f 77 6e 65 72 04 74 79 70 65 05 03 72 65 66 06 61 63 63 65 73 73 05 02 72 77 06 73 74 61 62 6c 65 01 07 6d 65 74 68 6f 64 73 07 01 08 04 02 69 64 03 90 03 04 6e 61 6d 65 05 0c 64 65 66 61 75 6c 74 61 70 70 6c 79 06 70 61 72 61 6d 73 07 00 03 72 65 74 05 04 76 6f 69 64 08 05 02 69 64 03 04 04 6e 61 6d 65 05 0a 54 4d 75 64 50 6c 61 79 65 72 04 68 6f 73 74 05 06 70 6c 61 79 65 72 07 6d 65 6d 62 65 72 73 07 00 07 6d 65 74 68 6f 64 73 07 00 06 65 76 65 6e 74 73 07 02 08 04 04 6e 61 6d 65 05 07 6f 6e 41 70 70 6c 79 06 74 61 72 67 65 74 05 04 69 74 65 6d 04 6d 6f 64 65 05 04 73 79 6e 63 06 70 61 72 61 6d 73 07 02 05 11 68 61 6e 64 6c 65 3a 54 4d 75 64 50 6c 61 79 65 72 05 03 69 6e 74 08 04 04 6e 61 6d 65 05 0e 6f 6e 4f 77 6e 65 72 43 68 61 6e 67 65 64 06 74 61 72 67 65 74 05 06 6f 62 6a 65 63 74 04 6d 6f 64 65 05 08 64 65 66 65 72 72 65 64 06 70 61 72 61 6d 73 07 01 05 03 72 65 66 0a 77 61 6e 74 73 5f 74 69 63 6b 01 06 6c 69 6d 69 74 73 08 01 08 6d 61 78 5f 6c 69 73 74 03 80 04 0e 70 6c 61 79 65 72 5f 63 6c 61 73 73 65 73 07 01 05 0a 6d 75 64 70 6c 61 79 65 72 73
```
#### REQ 6 (1.1): gmap.layout of a 2 by 2 gmap with an empty cell
```
41 00 00 00 02 06 11 07 00 08 02 06 70 6c 61 79 65 72 00 05 6c 65 76 65 6c 00 07 05 05 0a 77 6f 72 6c 64 2e 67 6d 61 70 03 04 03 04 07 04 05 04 61 2e 6e 77 05 04 62 2e 6e 77 05 04 63 2e 6e 77 05 00 03 80 01
```
#### REPLY 6 (1.1): gmap.layout done
```
09 00 00 00 03 06 00 00 07 00 07 00 00
```
#### REQ 7 (1.1): set member 100 (a ref) to the text gs1player
```
41 00 00 00 02 07 0a 07 00 08 02 06 70 6c 61 79 65 72 05 09 67 73 31 70 6c 61 79 65 72 05 6c 65 76 65 6c 05 07 6d 61 69 6e 2e 6e 77 07 03 06 07 00 00 00 01 00 00 00 03 c8 01 05 09 67 73 31 70 6c 61 79 65 72
```
#### REPLY 7 (1.1): caller.setprop, an item event with an effect fallback, an item event with an invoke fallback
```
7c 01 00 00 03 07 00 00 07 01 08 03 01 6b 05 0e 63 61 6c 6c 65 72 2e 73 65 74 70 72 6f 70 04 6e 61 6d 65 05 0c 74 68 69 73 2e 77 65 61 70 6f 6e 31 05 76 61 6c 75 65 05 05 73 77 6f 72 64 07 02 08 07 04 6e 61 6d 65 05 07 6f 6e 41 70 70 6c 79 06 74 61 72 67 65 74 05 04 69 74 65 6d 06 68 61 6e 64 6c 65 06 07 00 00 00 01 00 00 00 04 74 79 70 65 05 0a 54 4d 75 64 4f 62 6a 65 63 74 07 63 6c 61 73 73 65 73 07 01 05 0b 73 70 65 63 69 61 6c 69 74 65 6d 04 61 72 67 73 07 02 06 09 00 00 00 01 00 00 00 03 06 08 66 61 6c 6c 62 61 63 6b 08 03 01 6b 05 0b 63 6c 69 65 6e 74 2e 74 65 78 74 06 70 6c 61 79 65 72 05 09 67 73 31 70 6c 61 79 65 72 04 74 65 78 74 05 14 59 6f 75 20 63 61 6e 6e 6f 74 20 75 73 65 20 74 68 61 74 2e 08 07 04 6e 61 6d 65 05 07 6f 6e 41 70 70 6c 79 06 74 61 72 67 65 74 05 04 69 74 65 6d 06 68 61 6e 64 6c 65 06 08 00 00 00 01 00 00 00 04 74 79 70 65 05 0a 54 4d 75 64 4f 62 6a 65 63 74 07 63 6c 61 73 73 65 73 07 00 04 61 72 67 73 07 02 06 09 00 00 00 01 00 00 00 03 00 08 66 61 6c 6c 62 61 63 6b 08 03 06 69 6e 76 6f 6b 65 05 0c 64 65 66 61 75 6c 74 61 70 70 6c 79 06 68 61 6e 64 6c 65 06 08 00 00 00 01 00 00 00 04 61 72 67 73 07 00 00
```
#### QUERY 2 (1.1): player.pos for gs1player
```
1b 00 00 00 04 02 05 0a 70 6c 61 79 65 72 2e 70 6f 73 07 01 05 09 67 73 31 70 6c 61 79 65 72
```
#### QRESULT 2 (1.1): main.nw, 12.5, 30.25
```
20 00 00 00 05 02 00 07 03 05 07 6d 61 69 6e 2e 6e 77 04 00 00 00 00 00 00 29 40 04 00 00 00 00 00 40 3e 40
```
#### QUERY 3 (1.1): player.at for gs1player, index 1
```
1c 00 00 00 04 03 05 09 70 6c 61 79 65 72 2e 61 74 07 02 05 09 67 73 31 70 6c 61 79 65 72 03 02
```
#### QRESULT 3 (1.1): gs2player
```
0e 00 00 00 05 03 00 05 09 67 73 32 70 6c 61 79 65 72
```
