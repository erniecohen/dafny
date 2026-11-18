// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), first stage:
// a program that has a recursive module group may not declare a codatatype, wherever the codatatype
// is.
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module A {
  import B
  datatype T = Leaf | Node(u: B.U)
}

module B {
  import A
  datatype U = U(t: A.T)
}

module Elsewhere {
  codatatype Stream = Cons(head: int, tail: Stream)
}
