// Member invariants (https://github.com/erniecohen/dafny/issues/105): the scanner produces the tokens of
// 'invariant' and 'witness' members and of 'raw T' from the control character U+0001 followed by the
// word, but a program may not contain them, with or without --member-invariants.
// RUN: %exits-with 2 %resolve "%S/Inputs/git-issue-105-control.dfy" > "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%S/Inputs/git-issue-105-control.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false "%S/Inputs/git-issue-105-control.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
