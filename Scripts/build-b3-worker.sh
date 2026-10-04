#!/usr/bin/env bash
# Verifying bootstrap of the pinned source component; no stock B3 CLI output scraping.
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
if [ "$#" -lt 2 ] || [ "$#" -gt 3 ]; then
  echo "Usage: $0 PINNED_COMPILER_ARCHIVE Z3_EXECUTABLE [OUTPUT_DIRECTORY]" >&2
  exit 64
fi
archive=$(cd "$(dirname "$1")" && pwd)/$(basename "$1")
solver=$(cd "$(dirname "$2")" && pwd)/$(basename "$2")
output=${3:-"$root/build/b3-worker"}
mkdir -p "$output"
output=$(cd "$output" && pwd)
python3 - "$archive" "$root/ThirdParty/B3" <<'CHECK'
import hashlib, json, pathlib, sys
archive, source = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
expected = 'ba06f4d5048ecf0cc40f79281230eb44b429fd73a8bbed5c4377a3d0ff010331'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == expected, 'Wrong bootstrap compiler archive'
source_manifest = source / 'source-manifest.json'
assert hashlib.sha256(source_manifest.read_bytes()).hexdigest() == 'd5c5283d5145b55d5807bf066a70d4b411ca3962499c6edad32cbfc71750832a', 'Wrong B3 source manifest'
manifest = json.loads(source_manifest.read_text())
assert manifest['upstreamCommit'] == 'ea6e8a18dfe9e317d313de769291f989957dc5f2'
actual = set()
for name in manifest['vendoredPaths']:
    path = source / name
    if path.is_dir():
        actual.update(str(p.relative_to(source)) for p in path.rglob('*') if p.is_file())
    else:
        actual.add(name)
actual.update(p['file'] for p in manifest['patches'])
assert actual == set(manifest['files']), 'Unlisted or missing B3 source files'
for name, digest in manifest['files'].items():
    assert hashlib.sha256((source / name).read_bytes()).hexdigest() == digest, 'Changed B3 source: ' + name
CHECK
mkdir -p "$output/compiler" "$output/library"
tar -xzf "$archive" -C "$output/compiler"
compiler="$output/compiler/dafny/Dafny.dll"
[ "$(dotnet "$compiler" --version)" = '4.11.0+fcb2042d.review.a171069d' ]
[ "$("$solver" -version)" = 'Z3 version 5.1.0 - 64 bit' ]
(
  cd "$root/ThirdParty/B3"
  dotnet "$compiler" build library/dfyconfig.toml --solver-path "$solver" \
    --cores "${B3_BUILD_CORES:-2}" --resource-limit 100000000 --verification-time-limit 120 \
    --output "$output/library/B3Library" --spill-translation \
    --log-format "csv;LogFileName=$output/library/resources.csv"
)
dotnet publish "$root/Source/DafnyB3Host/DafnyB3Host.csproj" -c Release \
  -p:B3LibraryPath="$output/library/B3Library.dll" -o "$output/package" --nologo
python3 - "$root/ThirdParty/B3/source-manifest.json" "$output/package" <<'MANIFEST'
import hashlib, json, pathlib, sys
source, package = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
files = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(package.iterdir()) if p.is_file() and p.suffix in ('.dll', '.json') and p.name != 'b3-worker-manifest.json'}
manifest = {'version': 3, 'b3Commit': 'ea6e8a18dfe9e317d313de769291f989957dc5f2', 'normalizerVersion': 'experimental-3',
 'bootstrapCompiler': '4.11.0+fcb2042d.review.a171069d', 'sourceFingerprint': hashlib.sha256(source.read_bytes()).hexdigest(), 'files': files}
(package / 'b3-worker-manifest.json').write_text(json.dumps(manifest, separators=(',', ':')) + '\n')
MANIFEST
echo "Built B3 worker package: $output/package/DafnyB3Host.dll"
