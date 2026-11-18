// Checked source controls; certificate and target release gates run separately.
// RUN: %exits-with 0 python3 %S/Inputs/github-issue-211-checked-std.py %baredafny %review-z3 %t.controls "%repositoryRoot" > %t
// RUN: %diff %s.expect %t
