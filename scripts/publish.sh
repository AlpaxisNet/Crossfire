#!/usr/bin/env bash
# Publishes the extension for a game server's machine: a self-contained build (the .NET runtime inside it, since the machine need not
# have one, and a self-contained game server has none to lend), laid out as an extension folder: <out>/crossfire/ with the executable
# and extension.json side by side (docs/protocol.md section 1). A host installs that folder as extensions/crossfire/ beside its game
# server; a network that deploys its own game servers takes it from the folder its deployment names.
#
#   scripts/publish.sh <out folder> [<runtime identifier>]
#
# <runtime identifier>: the machine's, a Linux one (extension.json names the executable without .exe), default linux-x64. With
# SUITE naming a checkout of the Alpaxis Suite the build runs inside its client/scripts/capped.sh (its memory box and build turn), as
# every build on the Alpaxis development machine must; without it the build runs as it is. The publish goes to <out>/crossfire.new
# first and replaces <out>/crossfire only when it is whole.
#
# Exit status: 0 published; 1 the arguments are wrong; 4 the build failed; 5 SUITE is not a suite checkout; 6 the result is not a
# whole extension folder.
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/.." && pwd)

if [ $# -lt 1 ] || [ $# -gt 2 ] || [ -z "$1" ]; then
	echo "usage: scripts/publish.sh <out folder> [<runtime identifier>]" >&2
	exit 1
fi
runtime=${2:-linux-x64}
if ! [[ $runtime =~ ^linux-[a-z0-9.-]+$ ]]; then
	echo "error: $runtime is not a Linux runtime identifier (for example linux-x64 or linux-arm64)" >&2
	exit 1
fi
mkdir -p "$1" || exit 1
out=$(cd "$1" && pwd)

box=()
if [ -n "${SUITE:-}" ]; then
	box=("$SUITE/client/scripts/capped.sh" -l -m 6G -t 20m)
	if [ ! -x "${box[0]}" ]; then
		echo "error: $SUITE is not a checkout of the suite (no client/scripts/capped.sh)" >&2
		exit 5
	fi
fi

rm -rf "$out/crossfire.new"
echo "== publishing the extension for $runtime into $out/crossfire"
if ! (cd "$repo" && "${box[@]}" dotnet publish src/Crossfire.Extension/Crossfire.Extension.csproj -c Release -r "$runtime" \
	--self-contained -o "$out/crossfire.new" -m:2 -nodeReuse:false -p:UseSharedCompilation=false -v:q -nologo 2>&1 | tail -5
	exit "${PIPESTATUS[0]}"); then
	echo "error: the build failed" >&2
	rm -rf "$out/crossfire.new"
	exit 4
fi

# The folder must be what a host checks: extension.json and its executable, and a runtime of its own.
executable=$out/crossfire.new/Crossfire.Extension
if [ ! -f "$out/crossfire.new/extension.json" ] || [ ! -x "$executable" ] ||
	grep -q '"frameworks\?"' "$out/crossfire.new/Crossfire.Extension.runtimeconfig.json" 2>/dev/null; then
	echo "error: $out/crossfire.new is not a whole self-contained extension folder" >&2
	exit 6
fi
rm -rf "$out/crossfire" && mv "$out/crossfire.new" "$out/crossfire"
echo "published: $out/crossfire ($(du -sh "$out/crossfire" | cut -f1))"
