// Clause 5 follows the invariant cone through all members of the recursive group.
module A {
  import B
  datatype T = T(n: int) {
    invariant B.Bad(this)
  }
}
module B {
  import A
  import C
  ghost predicate Bad(r: raw A.T) { C.Bad(r) }
}
module C {
  import A
  ghost predicate Bad(r: raw A.T) { var t: A.T := r; t.n == 0 }
}
