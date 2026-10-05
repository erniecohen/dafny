// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
opaque function F(): bool { true }
lemma Use() { reveal F(); assert F(); hide F; assert F(); }
