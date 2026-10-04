// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
function F(): int { 7 }
lemma Hidden() { hide F; assert F() == 7; }
