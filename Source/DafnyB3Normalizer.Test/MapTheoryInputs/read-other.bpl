// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [int]int, i: int, j: int, v: int); requires i != j;
implementation P(m: [int]int, i: int, j: int, v: int) { assert m[i := v][j] == m[j]; }
