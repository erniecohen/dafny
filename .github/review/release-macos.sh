#!/bin/sh
# Build the osx-arm64 binary of a release tag on an Apple-silicon Mac and add it to the
# tag's GitHub release, which review.yml creates with the linux-x64 zip only.
#
#   release-macos.sh <tag> <work dir>
#
# Needs gh (logged in), git, curl, zip, shasum, a .NET 10 SDK with the .NET 8 runtime
# (DOTNET_ROOT), and a JDK 17 (JAVA_HOME).  The steps are those of review.yml's
# `binaries` job: publish self-contained, check the version and one verification with
# Z3 5.1.0, zip.  Then the zip is uploaded, and SHA256SUMS and the sha256 block of the
# notes are rewritten to list both zips.
set -eu
tag=$1
work=$2
repo=erniecohen/dafny
version=${tag#v}
revision=${version#*+}
z3=z3-5.1.0-arm64-osx-13.3
z3sum=81d29e934fd863079a74af35eecaeaef8047e0e12414d33ca322b358d68383db
name=dafny-$version-osx-arm64.zip

test "$(uname -s)-$(uname -m)" = Darwin-arm64
gh release view "$tag" --repo "$repo" > /dev/null  # the release CI made
mkdir -p "$work"
cd "$work"
rm -rf src out
git clone -q --filter=blob:none --depth 1 --branch "$tag" "https://github.com/$repo.git" src
(cd src && sh .github/review/version.sh) | grep -qx "version=$version"
(cd src && sh Scripts/fetch-boogie-packages.sh)
(cd src && dotnet publish Source/Dafny/Dafny.csproj -c Release -r osx-arm64 --self-contained \
  -o ../out/dafny -p:GeneratePackageOnBuild=false -p:SourceRevisionId="$revision")
rm -f out/dafny/*.pdb
mv out/dafny/Dafny out/dafny/dafny
for f in LICENSE.txt NOTICES.txt REVIEW.md; do if [ -f "src/$f" ]; then cp "src/$f" out/dafny/; fi; done
if [ ! -x "$z3/bin/z3" ]; then
  curl -fsSLo z3.zip "https://github.com/Z3Prover/z3/releases/download/z3-5.1.0/$z3.zip"
  echo "$z3sum  z3.zip" | shasum -a 256 -c -
  unzip -qo z3.zip && chmod +x "$z3/bin/z3"
fi
test "$(out/dafny/dafny --version | head -1)" = "$version"
out/dafny/dafny verify --solver-path "$z3/bin/z3" \
  src/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-6537.dfy
rm -f "$name"
(cd out && zip -qr "../$name" dafny)

rm -rf assets && mkdir assets
gh release download "$tag" --repo "$repo" --pattern '*linux-x64.zip' --dir assets
cp "$name" assets/
(cd assets && shasum -a 256 dafny-*.zip > ../SHA256SUMS)
gh release upload "$tag" --repo "$repo" --clobber "$name" SHA256SUMS
gh release view "$tag" --repo "$repo" --json body --jq .body |
  python3 -c '
import re, sys
body = sys.stdin.read()
sums = open("SHA256SUMS").read()
print(re.sub(r"(sha256:\n\n```\n).*?(```)", lambda m: m.group(1) + sums + m.group(2), body, flags=re.S), end="")
' > notes.md
gh release edit "$tag" --repo "$repo" --notes-file notes.md
cat SHA256SUMS
