#!/bin/bash
# No implicit refspec, branch wildcard, or tag auto-follow is permitted here.
set -euo pipefail
refspecs=()
while IFS= read -r ref; do
  [ -n "$ref" ] || continue
  if [[ ! "$ref" =~ ^[0-9a-fA-F]{40}$ ]]; then
    case "$ref" in
      refs/heads/*|refs/pull/*|refs/tags/*) git check-ref-format "$ref" ;;
      *) echo 'Expected a commit ID or fully qualified ref' >&2; exit 1 ;;
    esac
  fi
  refspecs+=("+$ref:refs/ci/required/${#refspecs[@]}")
done <<< "$REQUIRED_REFS"
[ "${#refspecs[@]}" -gt 0 ] || { echo 'No required refs supplied' >&2; exit 1; }
options=(--no-tags --no-recurse-submodules --refmap=)
case "$FULL_HISTORY" in
  true)
    # A retry may find a complete repository after a successful earlier fetch.
    # Never pass --unshallow then, and never expand the remote's configured glob.
    if [ "$(git rev-parse --is-shallow-repository)" = true ]; then
      options+=(--unshallow)
    fi
    ;;
  false) options+=(--depth=1) ;;
  *) echo 'full-history must be true or false' >&2; exit 1 ;;
esac
git -c fetch.prune=false -c fetch.pruneTags=false fetch "${options[@]}" origin "${refspecs[@]}"
