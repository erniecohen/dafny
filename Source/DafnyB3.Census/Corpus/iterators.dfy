iterator Count(n: nat) yields (x: int)
 yield ensures 0 <= x < n
{ var i := 0; while i < n invariant 0 <= i <= n { x := i; yield; i := i + 1; } }
