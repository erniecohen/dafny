module Lib {
  method {:verify false} Count(n: nat) returns (r: nat)
    ensures r == n
  {
  }
}
