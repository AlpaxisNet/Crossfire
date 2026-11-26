# Crossfire for Alpaxis

A port to C# of the parts of the server of [Crossfire](https://crossfire.real-time.com/) 1.x that Graal Kingdoms' MUD library
was built on, for the Alpaxis GameServer, so that Graal Kingdoms' scripts find the MUD functions and objects they call
(`mudaddobject`, `TMudObject`, inventories, apply, skills, spells, parties).

**The base** is Crossfire's server at commit `8b7ee7f90` of 2002-06-27 (its official repository,
`git.code.sf.net/p/crossfire/crossfire-server`), five days before Crossfire 1.3.0: Graal Kingdoms' own message catalogue
(`graalmudlib.po`) cites Crossfire's sources, and its 1,405 file and line references fit that commit best. Every ported file names
the Crossfire file and functions it comes from and keeps their copyright notices.

**The licence** is Crossfire's: the GNU General Public License, version 2 or (at your option) any later version ([LICENSE](LICENSE)).
Crossfire is copyright (C) 1992 Frank Tore Johansen, (C) 2001 Mark Wedel and the Crossfire Development Team, and the authors named
in each file.

**How it runs**: as the **Crossfire extension**, a program of its own that a game server starts and talks to over a Unix socket,
with the protocol in [docs/protocol.md](docs/protocol.md). The game server does not link this code, and this repository holds none
of the game server's: both sides implement that text. The protocol's text is under CC0 1.0 (its own header says so), so that any
implementation may carry it; everything else here is under the GPL as above.

**What it holds**: the ported code, its tests, Crossfire's data files of that commit that the rules need (treasure lists,
artifacts, recipes, attack messages), and documentation. Nothing of Graal Kingdoms' world files (the GameServer reads them from the
world's folder), and nothing of the Alpaxis network (no addresses, accounts, keys or tokens).

The design, the decisions behind it and the phases are in the Alpaxis Suite's `server/docs/gk-mudlib.md` and
`server/docs/mud-extension.md` (the game server's side of the protocol).

## What is here

- `src/Crossfire`: the object model (`CfObject`, `Living`, `Archetype`, `Define`), the world of globals and functions of
  `common/object.c`, `common/arch.c`, `common/loader.l` (`CfWorld`), the `.arc` reader and saver, the animation and face tables, and
  `ArchCollector` (what `lib/collect.pl` does to the archetype folder). Maps, players and their client, treasures and items are not
  ported (`common/map.c`'s map header and object list, `server/player.c`'s `get_player_archetype` are, as far as the object model
  needs them): the code calls a `CrossfireHost` where C calls the rest.
- `src/Crossfire.Extension`: the extension itself (the executable `Crossfire.Extension --connect <socket>`): its own codec and link
  for [docs/protocol.md](docs/protocol.md), the loading of the archetypes, the functions `findarchetype`, `findarchetypebyrealname`,
  `mudaddobject` and `mudaddspecialobject`, the types `TMudArch` (an archetype), `TMudObject` (an object in a level, shown by the
  host as an NPC) and `TMudPlayer` (a logged-in player), the levels (`level.load`, `level.unload`, the level files) and the accounts
  (`player.login`, `player.logout`, the account files); the Graal Kingdoms adaptation (classes, image, properties) is in its `Gk`
  folder. What it does and the tables of its members: [docs/extension.md](docs/extension.md). Its lifecycle (the flock on `<data_dir>/.lock`, the file writer
  and the state file, flush, shutdown and the save at the exit, ticks, the counters of `stats`, the paging helpers) is described there too.
  Tests: `tests/Crossfire.Extension.Tests` (with `tests/Crossfire.WriterChild`, the child process of the write-order fault tests).
- `tests/Crossfire.Tests`: xunit tests; two of them read Graal Kingdoms' own archetypes when the environment variable
  `CROSSFIRE_GK_ARCH` names their folder (the `Arch` folder of its world, not part of this repository) and report how many
  `Object` blocks, unknown keys and errors they find. The extension's tests also read the world's `mudlevels` and `scripts` folders when
  `CROSSFIRE_GK_MUDLEVELS` and `CROSSFIRE_GK_SCRIPTS` name them (docs/extension.md, "The GK adaptation").
- [docs/porting.md](docs/porting.md): the table of every C function and what became of it. [docs/differences.md](docs/differences.md):
  every deliberate difference with its reason.

```bash
# on the Alpaxis development machine: inside the suite's client/scripts/capped.sh (AGENTS.md)
capped.sh -l -m 6G -t 25m dotnet build Crossfire.sln -c Release
capped.sh -l -m 6G -t 25m dotnet test Crossfire.sln -c Release --no-build

# the suite's end-to-end test of the extension with the game server (SUITE: a checkout of the Alpaxis Suite; skipped without it)
SUITE=/path/to/Alpaxis.Suite scripts/e2e-with-suite.sh

# the extension for a game server's machine: self-contained for Linux x64, as the folder <out>/crossfire/ (about 82 MB)
SUITE=/path/to/Alpaxis.Suite scripts/publish.sh <out>
```

**Installing it.** A game server runs the extension from `extensions/crossfire/` beside its executable, the folder
`scripts/publish.sh` makes. The game server's machine needs no .NET of its own: the published folder carries the runtime. A
`dotnet build` output does not, so it starts only where a shared .NET 11 runtime is installed. A network that deploys its own
game servers with the Alpaxis Suite names the folder that holds `crossfire/` as `HOSTING_EXTENSIONS` in its `network.conf`;
`deploy.sh hosting` then installs it on the hosting machine.
