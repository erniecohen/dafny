// A global bound survives module refinement: the refining module B keeps A's abstract trait,
// and its clone of T.M keeps 'decreases {:global} n'.
abstract module A {
  trait {:termination false} T {
    method M(other: T, n: nat) decreases {:global} n
  }
}

module B refines A {
}

module C {
  import B
  class Same extends B.T {
    // The same bound is not below it: rejected.
    method M(other: B.T, n: nat) { other.M(this, n); }
  }
  class Down extends B.T {
    // n - 1 is: verifies.
    method M(other: B.T, n: nat) { if n != 0 { other.M(this, n - 1); } }
  }
}
