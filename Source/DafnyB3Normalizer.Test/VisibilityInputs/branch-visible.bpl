// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
function F(): int;
function Guard(): bool;
axiom Guard() ==> F() == 7;
procedure P();
implementation P() {
  assume Guard(); hide F; if (*) { reveal F; } else { reveal F; } assert F() == 7;
}
