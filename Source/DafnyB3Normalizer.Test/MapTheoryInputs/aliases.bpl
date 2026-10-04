// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
type M = [int]int;
procedure P(m: M, n: [int]int);
implementation P(m: M, n: [int]int) { assert m[0 := 1][0] == 1; assert n[0 := 2][0] == 2; }
