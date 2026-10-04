// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [int]int, n: [int]int);
  requires (forall i: int :: m[i] == n[i]);
  requires m != n;
implementation P(m: [int]int, n: [int]int) { assert m[0 := 1][0] == 1; assert false; }
