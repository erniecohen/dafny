// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [int]int, i: int, v: int);
implementation P(m: [int]int, i: int, v: int) { assert m[i := v][i] == v; }
