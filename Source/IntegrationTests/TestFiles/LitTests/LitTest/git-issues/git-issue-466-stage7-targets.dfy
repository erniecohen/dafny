// Each unqualified backend is rejected before emitting executable target code.
// NONUNIFORM: These are admission controls for backends without recursive-group support.
// RUN: %exits-with 3 %build --no-verify --target js --member-invariants --recursive-modules "%s" --output "%t-js" > "%t"
// RUN: %exits-with 3 %build --no-verify --target cpp --member-invariants --recursive-modules "%s" --output "%t-cpp" >> "%t"
// RUN: %exits-with 3 %build --no-verify --target dfy --member-invariants --recursive-modules "%s" --output "%t-dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
module A { import B function F(): int { 1 } }
module B { import A function G(): int { A.F() } }
