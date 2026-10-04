// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
function F(): int;
function Guard(b: bool): bool;
axiom (forall b: bool :: Guard(b) ==> F() == 7);
procedure P();
implementation P() {
  assert F() == 7;
}
