// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [int]real, i: int, v: real);
implementation P(m: [int]real, i: int, v: real) { assert m[i := v][i] == v; }
