// Member invariants (https://github.com/erniecohen/dafny/issues/105) are opt-in.  Without
// --member-invariants, 'invariant' in a type's members is the same parse error as before, with or
// without --type-system-refresh; and the option requires --type-system-refresh.
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false "%s" > "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh "%s" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false --member-invariants "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

datatype D = D(x: int) {
  invariant x > 0
}
