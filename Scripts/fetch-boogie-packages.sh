#!/bin/sh
# Download the Boogie packages this line builds against into Binaries/boogie-packages, the
# local NuGet source that nuget.config names, and check each against
# Scripts/boogie-packages.sha256.  Run it once before the first build; it downloads only
# what is missing or does not match.
#
# The packages are Boogie 3.5.5 with the fix for boogie-org/boogie#1168, the 14 packages of
# the release v3.5.5+review.37e4435d of https://github.com/erniecohen/boogie, which builds
# them in CI from the tag.  The release's SHA256SUMS is Scripts/boogie-packages.sha256.
set -eu
release=https://github.com/erniecohen/boogie/releases/download/v3.5.5%2Breview.37e4435d
root=$(cd "$(dirname "$0")/.." && pwd)
sums="$root/Scripts/boogie-packages.sha256"
feed="$root/Binaries/boogie-packages"
if command -v sha256sum >/dev/null 2>&1; then sha() { sha256sum "$1" | cut -c1-64; }
else sha() { shasum -a 256 "$1" | cut -c1-64; }
fi
mkdir -p "$feed"
n=0
while read -r sum name; do
  [ -n "$sum" ] || continue
  f="$feed/$name"
  if [ ! -f "$f" ] || [ "$(sha "$f")" != "$sum" ]; then
    curl -fsSL --retry 3 -o "$f.part" "$release/$name"
    got=$(sha "$f.part")
    if [ "$got" != "$sum" ]; then
      rm -f "$f.part"
      echo "$name: sha256 $got, expected $sum" >&2
      exit 1
    fi
    mv "$f.part" "$f"
  fi
  n=$((n + 1))
done < "$sums"
echo "$n Boogie packages in $feed, each matching Scripts/boogie-packages.sha256"
