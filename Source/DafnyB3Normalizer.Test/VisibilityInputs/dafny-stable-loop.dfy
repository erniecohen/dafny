// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
function F(): int { 7 }
lemma StableLoop() {
  assert F() == 7; hide F; var i := 0;
  while i < 1 invariant 0 <= i <= 1 invariant F() == 7 { i := i + 1; }
  assert F() == 7;
}
