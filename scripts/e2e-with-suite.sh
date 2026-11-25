#!/usr/bin/env bash
# Builds this repository's extension and runs the Alpaxis Suite's end-to-end test of the extension boundary against it.
#
#   SUITE=<checkout of the Alpaxis Suite> scripts/e2e-with-suite.sh
#
# SUITE   a checkout of the suite (the folder that holds server/tools/test_mud_e2e.sh). Without it the run is skipped, with a reason.
#
# This script holds no code of the suite: it builds here, then calls the suite's own script with CROSSFIRE_EXTENSION set to this
# build's output folder (an extension folder: the executable and extension.json side by side, docs/protocol.md section 1).
# The suite's script runs the game server's tests of the category MudEndToEnd against the real extension, over an invented world
# that the suite holds. The build and the tests run inside the suite's client/scripts/capped.sh (its memory box and build turn).
#
# Exit status: 0 the tests passed, or the run was skipped; the suite's script's status otherwise (1 a test failed, 2 the output
# folder is not an extension folder, 3 no test of the category exists); 4 the build failed; 5 SUITE is not a suite checkout.
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/.." && pwd)

if [ -z "${SUITE:-}" ]; then
	echo "SKIP: SUITE is not set (it names a checkout of the Alpaxis Suite, the folder with server/tools/test_mud_e2e.sh);"
	echo "      the end-to-end test of the extension with the game server needs the suite's side."
	exit 0
fi

suite=$(cd "$SUITE" 2>/dev/null && pwd) || {
	echo "error: SUITE=$SUITE is not a folder" >&2
	exit 5
}
test_script=$suite/server/tools/test_mud_e2e.sh
box=$suite/client/scripts/capped.sh
if [ ! -f "$test_script" ] || [ ! -x "$box" ]; then
	echo "error: $suite is not a checkout of the suite (no server/tools/test_mud_e2e.sh or client/scripts/capped.sh)" >&2
	exit 5
fi

echo "== building the extension in $repo"
if ! (cd "$repo" && "$box" -l -m 6G -t 20m dotnet build src/Crossfire.Extension/Crossfire.Extension.csproj -c Release -m:2 \
	-nodeReuse:false -p:UseSharedCompilation=false 2>&1 | tail -5; exit "${PIPESTATUS[0]}"); then
	echo "error: the build failed" >&2
	exit 4
fi

output=$repo/src/Crossfire.Extension/bin/Release/net11.0
echo "== running the suite's end-to-end test with CROSSFIRE_EXTENSION=$output"
CROSSFIRE_EXTENSION=$output "$test_script"
