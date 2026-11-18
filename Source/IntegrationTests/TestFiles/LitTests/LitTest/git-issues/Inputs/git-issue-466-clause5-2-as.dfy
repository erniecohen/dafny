// Clause 5 follows the invariant cone through all members of the recursive group.
module A {
  import B
  datatype T = T(n: int) {
    invariant B.Bad(this)
  }
}
module B {
  import A
  ghost predicate Bad(r: raw A.T) { (r as A.T).n == 0 }
}
