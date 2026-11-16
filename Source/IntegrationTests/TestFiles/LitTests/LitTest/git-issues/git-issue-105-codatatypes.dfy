// Member invariants (https://github.com/erniecohen/dafny/issues/105): a codatatype may not declare an
// invariant or a witness, and a program that declares a member invariant may not declare a codatatype
// (the declaration rule).  Witness members are not yet supported.
// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Co {
  codatatype Stream = Cons(head: int, tail: Stream) {
    invariant head > 0
    ghost witness Cons(1, this)
  }
}

module Data {
  datatype Pos = Pos(n: int) {
    invariant n > 0
    witness Pos(1)
  }
  datatype Neg = Neg(n: int) {
    invariant n < 0
    ghost witness Neg(-1)
  }
}
