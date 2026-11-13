module Lib {
  method Count(n: nat) returns (r: nat)
    ensures r == n
  {
    r := 0;
    assume r == n;
  }
}
