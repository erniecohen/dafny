// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
function F(): int { 7 }
lemma LearnThenHide() { assert F() == 7; hide F; assert F() == 7; }
