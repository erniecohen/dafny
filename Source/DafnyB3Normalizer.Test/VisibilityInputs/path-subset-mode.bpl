// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
function F(): int;
function Guard(): bool;
axiom Guard() ==> F() == 7;
procedure P();
implementation P() {
  assume Guard(); hide *; reveal F; if (*) { reveal *; } else { } assert F() == 7; reveal *; hide F; assert F() == 7;
}
