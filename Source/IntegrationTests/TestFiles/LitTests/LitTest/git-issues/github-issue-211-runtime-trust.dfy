// Explicit runtime trust is separate from the ghost-premise list.
// RUN: %exits-with 0 python3 %S/Inputs/github-issue-211-runtime-trust.py %baredafny %review-z3 %t.controls > %t
// RUN: %diff %s.expect %t
