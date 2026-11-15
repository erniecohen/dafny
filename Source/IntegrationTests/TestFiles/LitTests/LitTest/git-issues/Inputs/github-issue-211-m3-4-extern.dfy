module Lib {
  method {:extern "Lib", "Count"} Count(n: nat) returns (r: nat)
    ensures r == n
}
