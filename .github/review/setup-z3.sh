#!/bin/sh
# Install a checksum-pinned solver for the current CI platform, and print its path.
set -eu
version=${1:-5.1.0}
dest=${2:-ci-z3}
case "$(uname -s)-$(uname -m)" in
  Darwin-arm64) platform=macos ;;
  Linux-x86_64) platform=linux ;;
  *) echo "unsupported solver platform" >&2; exit 1 ;;
esac
case "$version-$platform" in
  5.1.0-linux) archive=z3-5.1.0-x64-glibc-2.39; sha=f47be8d27d3230e823bf1eeede2fe0abaca55bb78d0b59974370e6689a92284a ;;
  5.1.0-macos) archive=z3-5.1.0-arm64-osx-13.3; sha=81d29e934fd863079a74af35eecaeaef8047e0e12414d33ca322b358d68383db ;;
  4.12.1-linux) archive=z3-4.12.1-x64-ubuntu-24.04-bin; sha=e07d4193d8e1851114fe4d507332a43576639f4aeaa33c7df15c36426d3cbf3b ;;
  4.12.1-macos) archive=z3-4.12.1-arm64-macos-15-bin; sha=5941a521e8868db9b57ed81d1cc0cc408b37e55443087bcb6eb207583b47183f ;;
  4.16.0-linux) archive=z3-4.16.0-x64-ubuntu-24.04-bin; sha=87de9c9e86c877abfccb00cf24b946c4a1bda7b9cba6f363ea58311d2573c320 ;;
  4.16.0-macos) archive=z3-4.16.0-arm64-macos-15-bin; sha=9315fa2fb5087e3e21be98b11d58e4b443e023ed6627565c12b2f33f325f77eb ;;
  *) echo "no checksum for Z3 $version on $platform" >&2; exit 1 ;;
esac
if [ "$version" = 5.1.0 ]; then
  release=https://github.com/Z3Prover/z3/releases/download/z3-5.1.0
  binary="$archive/bin/z3"
else
  release=https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03
  binary=z3-$version
fi
mkdir -p "$dest"
dest=$(cd "$dest" && pwd)
curl -fsSL --retry 3 -o "$dest/archive.zip" "$release/$archive.zip"
echo "$sha  $dest/archive.zip" | shasum -a 256 -c - >&2
unzip -oq "$dest/archive.zip" -d "$dest"
chmod +x "$dest/$binary"
"$dest/$binary" --version >&2
printf '%s\n' "$dest/$binary"
