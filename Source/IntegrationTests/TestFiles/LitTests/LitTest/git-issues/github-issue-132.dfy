// Regression: a function value is allocated where what it captures is allocated, not wherever its reads
// frame is: () => n reads nothing and returns n.  Before the fix, the original program and the first
// negative method proved false, and the third negative method's assertion verified; the positive cases
// verify before and after.
// RUN: echo "original (type-system-refresh:false)" > "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-132-original.dfy" --type-system-refresh:false --allow-axioms --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "original (type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-132-original.dfy" --type-system-refresh:true --allow-axioms --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "negative (type-system-refresh:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-132-negative.dfy" --type-system-refresh:false --allow-axioms --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "negative (type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-132-negative.dfy" --type-system-refresh:true --allow-axioms --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "positive (type-system-refresh:false)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-132-positive.dfy" --type-system-refresh:false --allow-axioms --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "positive (type-system-refresh:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-132-positive.dfy" --type-system-refresh:true --allow-axioms --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: %diff "%s.expect" "%t"
