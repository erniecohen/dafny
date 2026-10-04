// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: <T>[T]T);
implementation P(m: <T>[T]T) { assert m[0 := 1][0] == 1; }
