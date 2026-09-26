#!/bin/sh
# Print the version that a build of this tree reports, as GitHub step outputs:
#
#   revision=<what follows the + in `dafny --version`; pass it as -p:SourceRevisionId>
#   version=<VersionPrefix>+<revision>
#
# On a line based on an upstream release, .github/review/base names that release's
# tag, and the revision is <its commit, 8 digits>.review.<hash>, where <hash> is the
# first 8 hex digits of the sha256 of `git diff --diff-filter=M --abbrev=7 <tag> HEAD`:
# the change the line makes to files the release already has (REVIEW.md says why).
# Elsewhere the revision is the commit.
set -eu
prefix=$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' Source/Directory.Build.props)
if [ -f .github/review/base ]; then
  base=$(tr -d ' \n' < .github/review/base)
  # A shallow fetch keeps the repository small, so that 7-digit object ids stay unambiguous.
  git fetch --quiet --no-tags --depth=1 origin "+refs/tags/$base:refs/tags/$base"
  hash=$(git diff --no-ext-diff --no-color --diff-filter=M --abbrev=7 "$base" HEAD | sha256sum | cut -c1-8)
  revision="$(git rev-parse --short=8 "$base^{commit}").review.$hash"
else
  revision=$(git rev-parse --short=8 HEAD)
fi
echo "revision=$revision"
echo "version=$prefix+$revision"
