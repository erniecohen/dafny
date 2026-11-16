// Member invariants (https://github.com/erniecohen/dafny/issues/105): an invariant takes no modifiers
// and cannot be {:opaque}, a witness takes only 'ghost', and neither is allowed at the module level.
// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype D = D(x: int) {
  ghost invariant x > 0
  static invariant true
  opaque invariant true
  static witness D(1)
  invariant {:opaque} x < 10
  invariant {:my_attribute} x < 20
}

invariant true
witness D(1)

module Inner {
  ghost invariant true
  witness 3
}
