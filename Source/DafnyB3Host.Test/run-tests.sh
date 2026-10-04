#!/usr/bin/env bash
# Runtime integration checks against an already verified B3 library.
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
if [ "$#" -lt 2 ] || [ "$#" -gt 3 ]; then
  echo "Usage: $0 VERIFIED_B3_LIBRARY Z3_EXECUTABLE [OUTPUT_DIRECTORY]" >&2
  exit 64
fi
library=$(cd "$(dirname "$1")" && pwd)/$(basename "$1")
solver=$(cd "$(dirname "$2")" && pwd)/$(basename "$2")
output=${3:-"$root/build/b3-host-tests"}
mkdir -p "$output"
output=$(cd "$output" && pwd)
dotnet publish "$root/Source/DafnyB3Host/DafnyB3Host.csproj" -c Release \
  -p:B3LibraryPath="$library" -o "$output/package" --nologo
python3 - "$root/ThirdParty/B3/source-manifest.json" "$output/package" <<'MANIFEST'
import hashlib, json, pathlib, sys
source, package = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
files = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(package.iterdir())
         if p.is_file() and p.suffix in ('.dll', '.json') and p.name != 'b3-worker-manifest.json'}
manifest = {'version': 2, 'b3Commit': 'ea6e8a18dfe9e317d313de769291f989957dc5f2',
            'normalizerVersion': 'experimental-2', 'bootstrapCompiler': '4.11.0+fcb2042d.review.a171069d',
            'sourceFingerprint': hashlib.sha256(source.read_bytes()).hexdigest(), 'files': files}
(package / 'b3-worker-manifest.json').write_text(json.dumps(manifest, separators=(',', ':')) + '\n')
MANIFEST
dotnet build "$root/Source/DafnyB3Protocol.Test/WorkerFixture/WorkerFixture.csproj" -c Release --nologo
export B3_WORKER_FIXTURE="$root/Source/DafnyB3Protocol.Test/WorkerFixture/bin/Release/net8.0/WorkerFixture.dll"
export B3_TEST_WORKER_PACKAGE="$output/package"
export B3_TEST_SOLVER="$solver"
dotnet test "$root/Source/DafnyB3Protocol.Test/DafnyB3Protocol.Test.csproj" -c Release --nologo
dotnet test "$root/Source/DafnyB3Host.Test/DafnyB3Host.Test.csproj" -c Release -p:B3LibraryPath="$library" --nologo
