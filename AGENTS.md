# Rules for work in this repository

- **Faithful to the base**: the C# does what Crossfire's server at `8b7ee7f90` does for the functions it ports (the same results,
  order of effects, rounding, messages); a deliberate difference is written down in `docs/differences.md` with its reason.
- **Notices**: every file ported from a Crossfire file starts with that file's copyright notices and the GPL notice, and names the
  Crossfire file and functions. Code that is not ported (bindings, tests) says so.
- **No secrets, no third-party world data**: nothing of the Alpaxis network or its servers (addresses, accounts, keys, tokens,
  `network.conf` values), none of Graal Kingdoms' files. Every push is preceded by a scan for secrets.
- **Tests**: Method_Given_x_When_y_Then_z, //Arrange //Act //Assert; a branch counts as tested only when a mutation of it fails a test.
- **Commits**: authored `vinvicta <vinvicta@users.noreply.github.com>`, no trailers of any kind; the subject says what changed, the
  body what was wrong or missing, how it was verified and what is open. Commit after each piece; push as work progresses.
- **Builds** on the Alpaxis development machine run inside the suite's `client/scripts/capped.sh` (the suite's AGENTS.md).
