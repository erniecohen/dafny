// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), first stage:
// libraries.  A library built with --recursive-modules records the option, and can be used only
// with it; one built without it records nothing and may be used either way.  A group in a library
// given as unverified source is an error; stage 6 records complete groups in .doo libraries.
// Without the option cycles remain errors. Under --modular-termination a library carries a record of the
// options it was verified with, and the option is one of them: a library with a record made
// without the option is refused by a client that uses the option.
// RUN: %build -t:lib --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-library.dfy" --output "%t-on.doo" > "%t"
// RUN: %build -t:lib --no-verify "%S/Inputs/git-issue-466-library.dfy" --output "%t-off.doo" >> "%t"
// RUN: %resolve --member-invariants --recursive-modules "%s" --library "%t-on.doo" >> "%t"
// RUN: %exits-with 1 %resolve --member-invariants "%s" --library "%t-on.doo" >> "%t"
// RUN: %exits-with 1 %resolve "%s" --library "%t-on.doo" >> "%t"
// RUN: %resolve "%s" --library "%t-off.doo" >> "%t"
// RUN: %resolve --member-invariants --recursive-modules "%s" --library "%t-off.doo" >> "%t"
// RUN: %build -t:lib --modular-termination --member-invariants "%S/Inputs/git-issue-466-library.dfy" --output "%t-record.doo" >> "%t"
// RUN: %exits-with 1 %resolve --modular-termination --member-invariants --recursive-modules "%s" --library "%t-record.doo" >> "%t"
// RUN: %resolve --modular-termination --member-invariants "%s" --library "%t-record.doo" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%s" --library "%S/Inputs/git-issue-466-cyclic-library.dfy" >> "%t"
// RUN: %exits-with 2 %resolve "%s" --library "%S/Inputs/git-issue-466-cyclic-library.dfy" >> "%t"
// RUN: %build -t:lib --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-cyclic-library.dfy" --output "%t-cyclic.doo" >> "%t"
// RUN: %exits-with 2 %build -t:lib --no-verify "%S/Inputs/git-issue-466-cyclic-library.dfy" --output "%t-cyclic.doo" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Main {
  function Triple(n: nat): nat { 3 * n }
}
