function F(n: nat): nat decreases n { if n == 0 then 0 else F(n-1) + 1 }
method Loop(n: nat) returns (i: nat) ensures i == n {
 i := 0; while i < n invariant i <= n { i := i + 1; }
}
