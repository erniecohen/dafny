// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [int, bool]int, i: int, j: int, v: int); requires i != j;
implementation P(m: [int, bool]int, i: int, j: int, v: int) {
  assert m[i, true := v][j, true] == m[j, true];
  assert m[i, true := v][i, false] == m[i, false];
}
