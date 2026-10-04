// RUN: %exits-with 2 %baredafny resolve --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun isolated native carrier restriction; no other resolver errors mask it.

newtype {:nativeType "uint"} NativeOrd = ORDINAL
