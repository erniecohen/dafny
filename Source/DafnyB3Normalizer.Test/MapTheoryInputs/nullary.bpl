// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: []int, v: int);
implementation P(m: []int, v: int) { assert m[:= v][] == v; }
