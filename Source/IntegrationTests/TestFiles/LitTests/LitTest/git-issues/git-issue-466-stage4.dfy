// Group invariants use one extension check, including private aliases and recursive dependencies.
// RUN: %verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage4-positive.dfy" > "%t"
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage4-false.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage4-hidden.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage4-dependence.dfy" >> "%t"
// RUN: %verify --member-invariants --recursive-modules --legacy-recursive-module-invariants "%S/Inputs/git-issue-466-stage4-hidden.dfy" >> "%t"
// RUN: %verify --member-invariants --recursive-modules --legacy-recursive-module-invariants "%S/Inputs/git-issue-466-stage4-dependence.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
