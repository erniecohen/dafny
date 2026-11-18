// RUN: %verify --type-system-refresh --solver-path %review-z3 "%s" > "%t"
// RUN: %verify --type-system-refresh --solver-path %review-z3 "%S/git-issue-611.dfy" >> "%t"
// RUN: %verify --type-system-refresh --solver-path %review-z3 "%S/git-issue-623.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Normalizing a type need not imply that its declaration is visible in the current scope.
// The export check in 611 encounters an invisible abstract type. The inferred quantifier
// domain in Library.M0 in 623 is MyTrait, which is not itself exported to Client.
// Both must remain opaque when not revealed, without triggering a Debug assertion.
