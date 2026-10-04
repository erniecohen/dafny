// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [int]int);
implementation P(m: [int]int) { assert m[0 := 1][0] == 2; }
