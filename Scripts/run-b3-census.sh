#!/bin/sh
# Build and inspect Dafny's typed pre-VC output without running a verifier.
set -eu
root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$root"
output=${1:-docs/dev/b3/emitted-features.json}
source_revision=${DAFNY_CENSUS_SOURCE_REVISION:-$(git rev-parse HEAD)}
dependency_revision=${DAFNY_CENSUS_DEPENDENCY_REVISION:-$source_revision}
if [ -n "${DAFNY_CENSUS_REFERENCE_DIRECTORY:-}" ]; then
  dotnet build Source/DafnyB3.Census/DafnyB3.Census.csproj -c Release \
    -p:DafnyCensusReferenceDirectory="$DAFNY_CENSUS_REFERENCE_DIRECTORY" --nologo
else
  sh Scripts/fetch-boogie-packages.sh
  dotnet build Source/DafnyB3.Census/DafnyB3.Census.csproj -c Release --nologo
fi
dotnet Source/DafnyB3.Census/bin/Release/net8.0/DafnyB3.Census.dll \
  "$root" "$output" "$source_revision" "$dependency_revision"
