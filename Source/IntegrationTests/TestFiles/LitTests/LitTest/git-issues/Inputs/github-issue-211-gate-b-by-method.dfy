// Gate (b) of https://github.com/erniecohen/dafny/issues/211: a function without a bound whose by-method
// body dispatches, called under a bound.  Every member Bnn must get an error on its own line.
module Lib {
  trait {:termination false} T {
    method M(n: nat) decreases {:global} n
    lemma L(n: nat) decreases {:global} n
  }
}
module Client {
  import opened Lib
  function H(t: T, n: nat): nat { 0 } by method { t.M(n); return 0; }
  function H2(t: T, n: nat): nat { 0 } by method { t.L(n); return 0; }
  method B01(t: T, n: nat) decreases {:global} n { var x := H(t, n); }
  method B02(t: T, n: nat) decreases {:global} n { var x := H2(t, n); }
}
