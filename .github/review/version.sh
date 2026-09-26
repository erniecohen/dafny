#!/bin/sh
# Print the version that a build of this tree reports, as GitHub step outputs:
#
#   revision=<what follows the + in `dafny --version`; pass it as -p:SourceRevisionId>
#   version=<VersionPrefix>+<revision>
#
# On a line based on an upstream release, .github/review/base holds two words: that
# release's tag, and the line's last product commit, the last commit that changes a
# file the build compiles.  The revision is then <the release's commit, 8 digits>
# .review.<hash>, where <hash> is the first 8 hex digits of the sha256 of
#
#   git diff --diff-filter=M --abbrev=7 <tag> <last product commit>
#
# and the script fails if any commit after the last product commit changes a product
# file: such a change needs a new product commit named here, and so a new version.
# REVIEW.md says why.  Elsewhere the revision is the commit.
set -eu
prefix=$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' Source/Directory.Build.props)
if [ -f .github/review/base ]; then
  read -r base product < .github/review/base
  # Shallow fetches keep the repository small, so that 7-digit object ids stay unambiguous.
  git fetch --quiet --no-tags --depth=1 origin "+refs/tags/$base:refs/tags/$base"
  git cat-file -e "$product^{commit}" 2>/dev/null || git fetch --quiet --no-tags --depth=1 origin "$product"
  changed=$(git diff --name-only "$product" HEAD -- . \
    ':(exclude)Source/IntegrationTests' ':(exclude)docs' ':(exclude).github' ':(exclude)REVIEW.md')
  if [ -n "$changed" ]; then
    echo "product files changed after $product, the last product commit in .github/review/base:" >&2
    echo "$changed" >&2
    exit 1
  fi
  hash=$(git diff --no-ext-diff --no-color --diff-filter=M --abbrev=7 "$base" "$product" | sha256sum | cut -c1-8)
  revision="$(git rev-parse --short=8 "$base^{commit}").review.$hash"
else
  revision=$(git rev-parse --short=8 HEAD)
fi
echo "revision=$revision"
echo "version=$prefix+$revision"
