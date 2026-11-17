// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), first stage:
// libraries.  A library built with --recursive-modules records the option, and can be used only
// with it; one built without it records nothing and may be used either way.  A group in a library
// given as source is reported like any other, and a library that has one cannot be built, with or
// without the option.
// RUN: %build -t:lib --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-library.dfy" --output "%t-on.doo" > "%t"
// RUN: %build -t:lib --no-verify "%S/Inputs/git-issue-466-library.dfy" --output "%t-off.doo" >> "%t"
// RUN: %resolve --member-invariants --recursive-modules "%s" --library "%t-on.doo" >> "%t"
// RUN: %exits-with 1 %resolve --member-invariants "%s" --library "%t-on.doo" >> "%t"
// RUN: %exits-with 1 %resolve "%s" --library "%t-on.doo" >> "%t"
// RUN: %resolve "%s" --library "%t-off.doo" >> "%t"
// RUN: %resolve --member-invariants --recursive-modules "%s" --library "%t-off.doo" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%s" --library "%S/Inputs/git-issue-466-cyclic-library.dfy" >> "%t"
// RUN: %exits-with 2 %resolve "%s" --library "%S/Inputs/git-issue-466-cyclic-library.dfy" >> "%t"
// RUN: %exits-with 2 %build -t:lib --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-cyclic-library.dfy" --output "%t-cyclic.doo" >> "%t"
// RUN: %exits-with 2 %build -t:lib --no-verify "%S/Inputs/git-issue-466-cyclic-library.dfy" --output "%t-cyclic.doo" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Main {
  function Triple(n: nat): nat { 3 * n }
}
