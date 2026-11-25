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

**What it holds**: the ported code, its tests, Crossfire's data files of that commit that the rules need (treasure lists,
artifacts, recipes, attack messages), and documentation. Nothing of Graal Kingdoms' world files (the GameServer reads them from the
world's folder), and nothing of the Alpaxis network (no addresses, accounts, keys or tokens).

The design, the decisions behind it and the phases are in the Alpaxis Suite's `server/docs/gk-mudlib.md`.

## What is here

- `src/Crossfire`: the object model (`CfObject`, `Living`, `Archetype`, `Define`), the world of globals and functions of
  `common/object.c`, `common/arch.c`, `common/loader.l` (`CfWorld`), the `.arc` reader and saver, the animation and face tables, and
  `ArchCollector` (what `lib/collect.pl` does to the archetype folder). Maps, players and their client, treasures and items are not
  ported: the code calls a `CrossfireHost` where C calls them.
- `tests/Crossfire.Tests`: xunit tests; one of them reads Graal Kingdoms' own archetypes when that folder is on the machine
  (it is not part of this repository) and reports how many `Object` blocks, unknown keys and errors it finds.
- [docs/porting.md](docs/porting.md): the table of every C function and what became of it. [docs/differences.md](docs/differences.md):
  every deliberate difference with its reason.

```bash
# on the Alpaxis development machine: inside the suite's client/scripts/capped.sh (AGENTS.md)
capped.sh -l -m 6G -t 25m dotnet build Crossfire.sln -c Release
capped.sh -l -m 6G -t 25m dotnet test Crossfire.sln -c Release --no-build
```
