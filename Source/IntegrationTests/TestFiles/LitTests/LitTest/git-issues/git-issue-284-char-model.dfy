// https://github.com/erniecohen/dafny/issues/284
// RUN: %review-z3 "%S/Inputs/issue-284-char-model.smt2" > "%t"
// RUN: %diff "%s.expect" "%t"
