# The MUD extension protocol, version 1.0

This text is the contract between a game server (the **host**, the Alpaxis GameServer) and a MUD engine that runs as a separate
program (the **extension**, such as the Crossfire extension). Each side implements it from this text alone; no code is shared. The
host's design around it (threads, deadlines, failure handling, persistence, costs) is in the suite's `server/docs/mud-extension.md`.

**Licence of this text:** CC0 1.0 Universal (public domain dedication). To the extent possible under law, the authors have waived
all copyright and related rights to this text, so that any implementation, under any licence, may carry it unchanged. The suite
keeps it as `server/docs/mud-protocol.md` and the Crossfire repository as `docs/protocol.md`; the two copies are identical.

Words: **must** is a requirement whose breach is a protocol error (section 9); **should** is advice. Every number is decimal unless
it is written as bytes (`0a`) or with `0x`.

## 1. Finding and starting the extension

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
  `executable` is a file name in the same folder (no `/` or `\`). `licence` and `source` are shown to the host's operator.
- The host chooses an extension by name from its own configuration, never from a path given by a script or a remote user.
- The host creates a private directory (mode 0700), listens there on a Unix domain stream socket (`AF_UNIX`, `SOCK_STREAM`), and
  starts the executable with exactly two arguments, `--connect <absolute socket path>`, with the extension's folder as the working
  directory. The extension connects within 15 s; the host then removes the socket's path. There is one connection for the life of
  the extension's process. When the connection ends (EOF or an error), the extension saves what it must and exits.
- Standard input, output and error carry no protocol. The extension sends its log lines in `LOG` frames (section 4.8) and should
  write nothing to standard output.

## 2. Frames

A frame is a 4-byte **length** `N` (unsigned, little endian) followed by `N` bytes of **body**. `N` is at least 1 and at most the
link's maximum (16,777,216 bytes, lowered by either HELLO's `max_frame`: the smaller wins for both directions once both HELLOs are
read). The body is one **kind** byte and then the kind's fields, in the order section 4 gives, with no bytes after the last field.

A field is either a **varint** (section 3.1, untagged) or a **value** (section 3.2, a tag byte and its body). Section 4 says which, and
which types a value field may have.

## 3. Encoding

### 3.1 Varints

An unsigned integer below 2^64 in LEB128: seven bits per byte, least significant group first, the high bit set on every byte but the
last. Encoders write the shortest form. A decoder must reject a varint of more than 10 bytes or one whose value does not fit in 64
bits; it may accept longer forms of a small number.

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

- An unknown tag is a protocol error.
- **Maps:** keys are unique, compared as bytes (a duplicate is a protocol error). Encoders write keys in the order this text lists
  them; decoders accept any order and ignore keys they do not know.
- **Depth:** a value that is not a list or a map has depth 1; a list or map has depth 1 plus the largest depth of its elements (1 when
  empty). A field's value may have depth 16 at most.
- **Counts:** a count larger than the number of bytes left in the body is a protocol error (every element takes a byte at least).
- **Numbers:** where this text asks for a number, a receiver must accept an int or a float and convert (a float to an int by
  truncation toward zero, clamped to the 64-bit range; NaN gives 0).

## 4. Frame kinds

| Kind | Name | Sent by | Fields |
| --- | --- | --- | --- |
| 1 | HELLO | both, once, first | value `hello` (map) |
| 2 | REQ | host | varint `reqid`, varint `op`, value `releases` (list of handles), value `ctx` (map), value `args` (list) |
| 3 | REPLY | extension | varint `reqid`, varint `status`, value `result`, value `effects` (list of maps), value `events` (list of maps), value `touched` (nil, or list of handles) |
| 4 | QUERY | extension | varint `qid`, value `name` (string), value `args` (list) |
| 5 | QRESULT | host | varint `qid`, varint `status`, value `result` |
| 6 | PING | both | varint `n` |
| 7 | PONG | both | varint `n` |
| 8 | LOG | extension | varint `level` (0 error, 1 warning, 2 information, 3 debug), value `text` (string) |

An unknown kind is a protocol error. A frame sent at a time this section does not allow is a protocol error too.

### 4.1 HELLO

After the extension connects, **the host sends its HELLO first**; the extension answers with its own. Nothing else is sent before both
are read, except that the host may close the link.

The host's map: `proto_major` (int, 1), `proto_minor` (int, 0), `role` ("gameserver"), `name` (string), `version` (string),
`server_name` (string), `epoch` (int, 1 for the first connection of the host's process, one more for each later one), `data_dir`
(string: an absolute path the host created, where the extension keeps its files), `world_dir` (string: an absolute path the
extension reads and never writes), `max_frame` (int), `call_timeout_ms` (int: how long the host waits for a reply, for information).

The extension's map: `proto_major`, `proto_minor`, `role` ("extension"), `name`, `version`, `features` (list of strings, optional
capabilities; none in 1.0), `manifest` (map, section 6), and optionally `max_frame` (int).

**Versions.** A different `proto_major` on either side: the host closes the link and runs without the extension. A higher
`proto_minor` is accepted; a side ignores map keys it does not know and answers an op it does not know with `unknown_op`.

### 4.2 REQ and REPLY

- `reqid` starts at 1 on each connection and grows by 1 with every REQ. The REPLY to a REQ carries its `reqid`.
- **One request at a time.** The host sends a REQ only when the REPLY to its previous REQ has been read. The extension answers REQs
  in the order they arrive, each with exactly one REPLY.
- `op` is an operation (section 7). `releases` lists handles the host no longer refers to (section 5). `ctx` is a map with `player`
  (the account name of the player the call is made for, a string, or nil) and `level` (the level's name, or nil); other keys are
  ignored. `args` are the operation's arguments.
- `status` is 0 for success or an error code (section 8). On success `result` is the operation's value. On an error `result` is a
  string that says what went wrong, `effects` and `events` are empty and `touched` is nil.
- `effects` (section 10) and `events` (section 11) are what the host does after it has read the whole REPLY: first every effect in
  order, then every event in order.
- `touched` is nil in 1.0. (A later minor version may list the handles whose changeable members this request changed.)

### 4.3 QUERY and QRESULT

While a REQ is waiting for its REPLY, the extension may ask the host questions; the host answers each with a QRESULT before the
extension's REPLY can arrive (the extension waits). `qid` starts at 1 on each connection and grows by 1 with every QUERY; the QRESULT
carries it. A QUERY sent while no REQ is waiting is answered with status 4 (`unavailable`). Queries are reads: answering one changes
nothing on the host and runs no script. The names are listed in section 12; an unknown name is answered with status 1.

### 4.4 PING and PONG

Either side may send PING at any time after the HELLOs; the other answers PONG with the same `n` as soon as it reads it, even while it
is busy with a request. The host uses it to tell a busy extension from a dead one.

### 4.5 LOG

The extension may send LOG at any time after the HELLOs. The host writes the text to its log at that level and may limit how many
lines it keeps.

## 5. Handles

A handle names an object of the extension: an unsigned 64-bit integer whose high 32 bits are the **epoch** the host sent in its HELLO
and whose low 32 bits are the extension's **tag** for the object. Tag 0 is never used; the extension never reuses a tag within an
epoch. A handle of another epoch is dead: the host treats it as nil, and the extension answers a request that names one with
`bad_handle`.

- The host may keep any handle it was given. When it no longer refers to one, it lists it in the `releases` of a later REQ (any REQ;
  no request is sent for releases alone). The extension may then forget objects that nothing else holds; releasing an object that
  still exists in the world (in a level, an inventory, an archetype) changes nothing.
- A handle may appear only where this text or the manifest asks for one. The host turns a handle into a script object: the level NPC
  that shows it, if the object has one (section 10, `npc.place`), else a proxy object of the type the manifest names. A script object
  passed as an argument of a handle type is sent as its handle (a proxy's, a bound NPC's, or a player's from `player.login`); any
  other value is sent as nil.

## 6. The manifest

The extension's HELLO carries a map `manifest` that tells the host which script functions, types, members, methods and events exist.
The host registers them once per process; after a reconnection the manifest must be equal to the registered one, or the host runs
without the extension until it restarts.

| Key | Value |
| --- | --- |
| `functions` | list of maps `{id, name, params, ret, slow}`: `id` an int of at least 64 (the op of the call, section 7); `name` the script function's name; `params` a list of type names; `ret` a type name; `slow` a bool (optional, false by default: the host allows a slow function a longer deadline) |
| `types` | list of maps `{id, name, host, members, methods}`: `id` an int; `name` the script type's name (`objecttype()`); `host` one of `object` (a proxy script object of this type), `player` (members and methods on the host's player object; the handle is the one `player.login` gave), `npc` (members and methods on a level NPC that shows an object); `members` a list of maps `{id, name, type, access, stable}` (`access` "r" or "rw"; `stable` a bool, section 6.1); `methods` a list of maps `{id, name, params, ret}` |
| `events` | list of maps `{name, target, mode}`: `target` one of `object`, `player`, `global`; `mode` `sync` or `deferred` (section 11) |
| `wants_tick` | bool: the host sends `session.tick` (section 7) |
| `limits` | map `{max_list}`: the most elements `get_list` returns |

Member and method ids are ints, unique within the whole manifest. Names are compared without regard to case by scripts; a manifest
must not hold two functions, or two members or methods of one type, whose names differ only in case.

**Type names:** `int`, `float`, `bool`, `string`, `any`, `void` (for `ret` only), `handle:<type>` (a handle whose script object is of
the named manifest type), `list` (a list of `any`), `list:<type name>` (a list whose elements have that type; `list:handle:<type>`
is allowed). The host converts a script's arguments to the declared types (numbers to int or float, other values to their text for
`string`, truthiness for `bool`), sends exactly as many arguments as `params` names (nil for those the script left out; extra ones
are dropped), and converts the result back.

### 6.1 Stable members

A member is **stable** when only a `set` through this link changes it: the extension promises never to change it by its own rules,
timers or other requests. The host may answer a read of a stable member from what it last read or wrote, without a request. A member
the extension's rules recompute (hit points, carried weight, a value that depends on other fields) must not be stable.

## 7. Operations

Ops 1 to 63 are fixed; ops from 64 up are the manifest's functions. `args` and `result` are as the table says.

| Op | Name | Args | Result |
| --- | --- | --- | --- |
| 1 | `session.init` | `[]` | map `{archetypes: int}`; the first REQ of every connection; the extension loads its world |
| 2 | `session.tick` | `[now_ms: int]` | nil; only with `wants_tick` |
| 3 | `session.flush` | `[blobs]` | nil; the extension saves a snapshot (it may reply before the files are written, and must write them) |
| 4 | `session.shutdown` | `[save: bool, blobs]` | nil, after the files are written when `save` is true; the last REQ |
| 5 | `player.login` | `[account: string]` | the player's handle |
| 6 | `player.logout` | `[account: string]` | nil; the effects remove every string the extension set for the player |
| 7 | `level.load` | `[level: string, cursor]` | map `{more: bool, cursor}`; the first call has cursor nil; each call places at most 64 objects (as `npc.place` effects); the host calls again with the returned cursor until `more` is false |
| 8 | `level.unload` | `[level: string, blobs]` | nil |
| 9 | `get` | `[handle, member: int]` | the member's value |
| 10 | `set` | `[handle, member: int, value]` | the value stored (the extension may clamp it) |
| 11 | `get_many` | `[handle, members: list of int]` | list of the values, in order |
| 12 | `get_list` | `[handle, member: int, fields: list of int]` | list of lists `[element handle, value of each field...]` for the list-typed member, at most `max_list` |
| 13 | `invoke` | `[handle, method: int, args: list]` | the method's value |
| 14 | `object.destroyed` | `[handle]` | nil; a script destroyed the NPC that showed the object |
| 15 | `stats` | `[]` | map of the extension's counters (names and meaning are its own) |
| 64+ | a manifest function | as its `params` | as its `ret` |

`blobs` is a list of lists `[handle, x: float, y: float, blob: string]`: the position of each level NPC that shows an object, and the
NPC's script state as text, which the extension stores with the object without reading it and gives back in the `npc.place` effect
that shows the object again.

## 8. Error codes

| Status | Name | Meaning |
| --- | --- | --- |
| 0 | ok | |
| 1 | `unknown_op` | the op, or a query's name, is not known |
| 2 | `bad_args` | wrong number or types of arguments |
| 3 | `bad_handle` | a handle is dead or unknown |
| 4 | `unavailable` | not ready (before `session.init` finished), or a query with no request waiting |
| 5 | `too_large` | the answer would exceed the frame's maximum |
| 6 | `version` | a version the side does not speak |
| 7 | `busy` | the side cannot take the request now |
| 8 | `internal` | anything else |

## 9. Protocol errors

A malformed frame (a length out of range, an unknown kind or tag, a count past the end, a depth over 16, bytes after the last field, a
duplicate key, a varint too long) or a frame sent when section 4 does not allow it is a **protocol error**: the receiver closes the
connection. The host then treats the extension as stopped (its design restarts it); the extension saves and exits.

## 10. Effects

An effect is a map whose key `k` names it. The host applies the effects of a REPLY in order after reading it. An effect with an
unknown `k` is skipped (and counted).

| `k` | Keys | What the host does |
| --- | --- | --- |
| `npc.place` | `handle`, `level` (string), `x`, `y` (numbers, in tiles), `image` (string), `classes` (list of strings), `props` (map of script property names to values), `blob` (string or nil) | shows the object as an NPC of the level, joined to the classes, with the props set and the script state restored from `blob`; the NPC's `onCreated` runs in the host's next script frame |
| `npc.remove` | `handle` | removes the NPC that shows the object, without events |
| `npc.setprop` | `handle`, `name`, `value` | sets a property of the NPC that shows the object |
| `client.set` | `player` (account), `name`, `value` (string) | sets the player's string `name`, the whole flag name with its prefix (`clientr.mud_hp`, `client.mud_item12`); a name that starts with neither `client.` nor `clientr.` is skipped |
| `client.del` | `player`, `name` | deletes that client string |
| `client.text` | `player`, `text` | shows the text to the player (its chat or information line) |
| `client.item` | `player`, `fields` (map) | an item update for the player's client in the host's own form (reserved in 1.0) |
| `output` | `player`, `text` | as `client.text` (reserved) |

## 11. Events

An event is a map `{name, target, handle | player, args}`: `name` a manifest event, `target` as the manifest says, `handle` the
object (for `object`), `player` the account (for `player`), `args` a list. The host fires the events of a REPLY in order after its
effects, as script events (`onUpdateObject` and so on) of the NPC that shows the object, of the player, or of the global script
objects. With mode `sync` they run at once, before the call that caused them returns to the script; with `deferred` in the host's
next script frame. An event for an object no NPC shows is dropped. A script run by an event may make requests of its own (new REQs,
after this REPLY).

## 12. Queries

| Name | Args | Result |
| --- | --- | --- |
| `level.near` | `[level: string, x, y, r]` | list of `[handle, x, y]`: the NPCs showing objects within `r` tiles of (x, y) |
| `level.blocked` | `[level: string, x, y]` | bool: the tile is blocked for walking |
| `npc.pos` | `[handles: list]` | list of `[x, y, level]` (nil for an object no NPC shows), in order |
| `player.exists` | `[account: string]` | bool: the player is online |
| `level.loaded` | `[level: string]` | bool |

## 13. Golden vectors

The frames below are complete (length and body), written in hexadecimal. Both implementations must encode these exact bytes from the
values given and decode them back. They come from the reference encoder that made this text.

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
