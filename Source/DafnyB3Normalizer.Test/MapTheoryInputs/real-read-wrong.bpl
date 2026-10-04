// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [int]real);
implementation P(m: [int]real) { assert m[0 := 0.25][0] == 0.5; }
