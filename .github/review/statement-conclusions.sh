#!/bin/sh
# Runs the fixtures of git-issues/github-issue-96.dfy (--statement-expression-conclusions,
# https://github.com/erniecohen/dafny/issues/96) against a built Dafny and Z3, with the option on
# and off, and checks each run's exit code and output against the test's expected output.
#
#   statement-conclusions.sh <dafny> <z3> <out dir>
#
# The transcript is the test's own: the same commands, with %baredafny replaced by <dafny> and
# every solver by <z3>, so it must equal github-issue-96.dfy.expect.  Exits non-zero on any
# difference.
set -u
dafny=$1 z3=$2 out=$3
dir=Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues
mkdir -p "$out"
t="$out/transcript.txt"
: > "$t"
status=0

run() {  # run <expected exit code> <label> <args...>
  want=$1 label=$2
  shift 2
  echo "$label" >> "$t"
  (cd "$dir" && "$dafny" verify --solver-path "$z3" --use-basename-for-filename --show-snippets:false "$@") >> "$t" 2>&1
  got=$?
  echo "$label: exit $got (expected $want)" >> "$out/exits.txt"
  if [ "$got" != "$want" ]; then status=1; fi
}

opts="--allow-warnings --allow-axioms"
run 4 "positive, option off" $opts Inputs/github-issue-96-positive.dfy
run 0 "positive, option on" $opts --statement-expression-conclusions Inputs/github-issue-96-positive.dfy
run 0 "positive, option on, Z3 5.1.0" $opts --statement-expression-conclusions Inputs/github-issue-96-positive.dfy
run 0 "project file (option on)" Inputs/github-issue-96-project/dfyconfig.toml
run 4 "negative, option off" $opts Inputs/github-issue-96-negative.dfy
run 4 "negative, option on" $opts --statement-expression-conclusions Inputs/github-issue-96-negative.dfy

if ! diff -u "$dir/github-issue-96.dfy.expect" "$t" > "$out/diff.txt"; then status=1; fi
{
  echo "### --statement-expression-conclusions fixtures (#96)"
  echo
  echo '```'
  cat "$out/exits.txt"
  echo '```'
  if [ -s "$out/diff.txt" ]; then
    echo
    echo "Output differs from github-issue-96.dfy.expect:"
    echo
    echo '```diff'
    cat "$out/diff.txt"
    echo '```'
  else
    echo
    echo "Output equals github-issue-96.dfy.expect."
  fi
} > "$out/summary.md"
cat "$out/summary.md"
exit $status
