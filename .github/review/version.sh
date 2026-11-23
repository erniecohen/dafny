#!/bin/sh
# Print the version that a build of this tree reports, as GitHub step outputs:
#
#   revision=<what follows the + in `dafny --version`; pass it as -p:SourceRevisionId>
#   version=<VersionPrefix>+<revision>
#
# On a line based on an upstream release, the first word of .github/review/base
# is that release's tag. An optional second word (the historical product commit)
# is ignored, so existing two-word files remain compatible without restamping.
# The revision is <the release's commit, 8 digits>.review.<hash>, where <hash>
# is the first 8 hex digits of the sha256 of
#
#   git diff --no-renames --abbrev=7 <tag> HEAD -- <product files>
#
# Every product file counts, added ones too. Documents, tests and CI do not.
# REVIEW.md defines the product. Elsewhere the revision is the commit.
set -eu
prefix=$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' Source/Directory.Build.props)
if [ -f .github/review/base ]; then
  read -r base ignored < .github/review/base
  # Shallow fetches keep the repository small, so that 7-digit object ids stay unambiguous.
  # A shallow fetch can fail transiently ("shallow file has changed since we read it"), so
  # each is tried three times.
  fetch() {
    git fetch --quiet --no-tags --refmap= --depth=1 origin "$1" && return 0
    sleep 5; git fetch --quiet --no-tags --refmap= --depth=1 origin "$1" && return 0
    sleep 15; git fetch --quiet --no-tags --refmap= --depth=1 origin "$1"
  }
  fetch "+refs/tags/$base:refs/tags/$base"
  # Not product: tests (the integration tests and the unit-test projects, Source/*.Test and
  # Source/*.Tests, which no release ships), documents and CI, the friction cases, and the
  # documents at the top of the tree.
  set -- . ':(exclude)Source/IntegrationTests' ':(exclude,glob)Source/*.Test/**' \
    ':(exclude,glob)Source/*.Tests/**' ':(exclude)docs' ':(exclude).github' ':(exclude)friction' \
    ':(exclude)REVIEW.md' ':(exclude)ROADMAP.md' ':(exclude)AGENTS.md' ':(exclude)CLAUDE.md'
  # Every product file counts, added ones too: with only the files that the tag already has,
  # a change confined to a file the fork added kept the version, so two products shared one.
  hash=$(git diff --no-ext-diff --no-color --no-renames --abbrev=7 "$base" HEAD -- "$@" | shasum -a 256 | cut -c1-8)
  revision="$(git rev-parse --short=8 "$base^{commit}").review.$hash"
else
  revision=$(git rev-parse --short=8 HEAD)
fi
echo "revision=$revision"
echo "version=$prefix+$revision"
