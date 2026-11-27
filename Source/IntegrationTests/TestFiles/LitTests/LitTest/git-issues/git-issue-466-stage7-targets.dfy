// Capability-specific diagnostics keep unqualified backends rejected before emission.
// NONUNIFORM: These are admission controls for backends without recursive-group support.
// RUN: %exits-with 3 %build --cores 2 --solver-path %review-z3 --no-verify --target cpp --member-invariants --recursive-modules "%s" --output "%t-cpp" > "%t"
// RUN: %exits-with 3 %build --cores 2 --solver-path %review-z3 --no-verify --target dfy --member-invariants --recursive-modules "%s" --output "%t-dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
module A { import B function F(): int { 1 } }
module B { import A function G(): int { A.F() } }
