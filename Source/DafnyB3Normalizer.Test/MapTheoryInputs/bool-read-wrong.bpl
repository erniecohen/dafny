// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
procedure P(m: [bool]bool);
implementation P(m: [bool]bool) { assert m[false := true][false] == false; }
