// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
opaque function F(): int { 7 }
lemma UseOpaque() { reveal F(); assert F() == 7; hide F; assert F() == 7; }
