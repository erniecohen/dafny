module Library {
  trait {:termination false} T {
    method F(n: nat) decreases {:global} n
  }
  method H(t: T, n: nat) decreases {:global} n { t.F(n); }
}
