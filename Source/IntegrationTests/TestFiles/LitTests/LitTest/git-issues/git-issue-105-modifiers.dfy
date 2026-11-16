// Member invariants (https://github.com/erniecohen/dafny/issues/105): an invariant takes no modifiers,
// a witness only 'ghost', and neither is allowed at the module level.
// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype D = D(x: int) {
  ghost invariant x > 0
  static invariant true
  opaque invariant true
  static witness D(1)
}
