// Member invariants (https://github.com/erniecohen/dafny/issues/105): the declaration rule covers the
// whole program, libraries included, whether given as source or as .doo files.  A library that
// declares neither an invariant nor a codatatype may be used with either.  The rule's error is
// reported in the program's own source where it can be.  A library built with --member-invariants
// can be used only with the option; one built without it records nothing and may be used either way.
// RUN: %resolve --member-invariants "%s" --library "%S/Inputs/git-issue-105-neutral-library.dfy" > "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%s" --library "%S/Inputs/git-issue-105-codatatype-library.dfy" --library "%S/Inputs/git-issue-105-neutral-library.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%S/Inputs/git-issue-105-uses-codatatype.dfy" --library "%S/Inputs/git-issue-105-invariant-library.dfy" --library "%S/Inputs/git-issue-105-neutral-library.dfy" >> "%t"
// RUN: %resolve --member-invariants "%S/Inputs/git-issue-105-uses-codatatype.dfy" --library "%S/Inputs/git-issue-105-neutral-library.dfy" >> "%t"
// RUN: %build -t:lib --no-verify --member-invariants "%S/Inputs/git-issue-105-invariant-library.dfy" --output "%t-invariant.doo" >> "%t"
// RUN: %build -t:lib --no-verify "%S/Inputs/git-issue-105-neutral-library.dfy" --output "%t-neutral.doo" >> "%t"
// RUN: %build -t:lib --no-verify "%S/Inputs/git-issue-105-codatatype-library.dfy" --output "%t-codatatype.doo" >> "%t"
// RUN: %resolve --member-invariants "%s" --library "%t-neutral.doo" >> "%t"
// RUN: %resolve --member-invariants "%s" --library "%t-invariant.doo" --library "%t-neutral.doo" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%s" --library "%t-codatatype.doo" --library "%t-neutral.doo" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%S/Inputs/git-issue-105-uses-codatatype.dfy" --library "%t-invariant.doo" --library "%t-neutral.doo" >> "%t"
// RUN: %exits-with 1 %resolve "%S/Inputs/git-issue-105-uses-codatatype.dfy" --library "%t-invariant.doo" --library "%t-neutral.doo" >> "%t"
// RUN: %resolve "%S/Inputs/git-issue-105-uses-codatatype.dfy" --library "%t-neutral.doo" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Main {
  import Neutral
  datatype Range = Range(lo: int, hi: int) {
    invariant lo <= hi
  }
  function Width(r: Range, p: Neutral.Pair): int { r.hi - r.lo + Neutral.Sum(p) }
}
