// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
function F(): int;
function Guard(): bool;
axiom Guard() ==> F() == 7;
procedure P();
implementation P() {
  assume Guard(); hide F; push; reveal F; assert F() == 7; pop; assert F() == 7;
}
