// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [bool]bool, n: [bool]bool);
  requires (forall i: bool :: m[i] == n[i]);
  requires m != n;
implementation P(m: [bool]bool, n: [bool]bool) { assert m[false := true][false] == true; assert false; }
