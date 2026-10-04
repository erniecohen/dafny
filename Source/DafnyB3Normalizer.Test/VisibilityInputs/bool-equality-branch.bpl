// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
function F(): int; function Guard(): bool; axiom Guard() ==> F() == 7;
procedure P(a: bool, b: bool);
implementation P(a: bool, b: bool) {
  if (a == b) { assert a == b; } else { assert a != b; }
}
