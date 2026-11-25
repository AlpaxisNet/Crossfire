# Rules for work in this repository

- **Faithful to the base**: the C# does what Crossfire's server at `8b7ee7f90` does for the functions it ports (the same results,
  order of effects, rounding, messages); a deliberate difference is written down in `docs/differences.md` with its reason.
- **Notices**: every file ported from a Crossfire file starts with that file's copyright notices and the GPL notice, and names the
  Crossfire file and functions. Code that is not ported (bindings, tests) says so.
- **No secrets, no third-party world data**: nothing of the Alpaxis network or its servers (addresses, accounts, keys, tokens,
  `network.conf` values), none of Graal Kingdoms' files. Every push is preceded by a scan for secrets.
- **The protocol**: the extension (`src/Crossfire.Extension`) speaks [docs/protocol.md](docs/protocol.md) and nothing else to the game
  server. It is implemented here from that text alone: no code, type or library of the game server comes into this repository, and
  the text is changed only together with the suite's copy (`server/docs/mud-protocol.md`), which must stay identical. Its golden
  vectors are tests on both sides. Graal Kingdoms' names (archetype keys such as `graalnpctype`, class names) may appear in the
  extension's GK adaptation; its files never do.
- **Tests**: Method_Given_x_When_y_Then_z, //Arrange //Act //Assert; a branch counts as tested only when a mutation of it fails a test.
- **Commits**: authored `vinvicta <vinvicta@users.noreply.github.com>`, no trailers of any kind; the subject says what changed, the
  body what was wrong or missing, how it was verified and what is open. Commit after each piece; push as work progresses.
- **Builds** on the Alpaxis development machine run inside the suite's `client/scripts/capped.sh` (the suite's AGENTS.md).
