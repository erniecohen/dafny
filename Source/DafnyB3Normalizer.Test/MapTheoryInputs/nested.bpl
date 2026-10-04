// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [int][bool]int, n: [bool]int);
implementation P(m: [int][bool]int, n: [bool]int) {
  assert m[0 := n][0] == n;
  assert m[0 := n][0][false := 1][false] == 1;
}
