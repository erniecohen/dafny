// Regression: a function value is allocated where what it captures is allocated, not wherever its reads
// frame is: () => n reads nothing and returns n.  Before the fix, the original program and the seven negative
// methods that ensure false proved it, directly or through a sequence, map, iset or datatype holding the
// value, and CapturedLaterNested's assertion verified.  A general arrow (~>) also counted as a type without
// references, though its reads frame shows captured references; (!new) and a predicate's quantifier now
// reject it (resolution, bounds).  The positive cases verify before and after.
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
// RUN: echo "resolution (type-system-refresh:false)" >> "%t"
// RUN: %exits-with 2 %baredafny resolve "%S/Inputs/github-issue-132-resolution.dfy" --type-system-refresh:false --allow-axioms --show-snippets:false --use-basename-for-filename >> "%t"
// RUN: echo "resolution (type-system-refresh:true)" >> "%t"
// RUN: %exits-with 2 %baredafny resolve "%S/Inputs/github-issue-132-resolution.dfy" --type-system-refresh:true --allow-axioms --show-snippets:false --use-basename-for-filename >> "%t"
// RUN: echo "bounds (type-system-refresh:false)" >> "%t"
// RUN: %exits-with 2 %baredafny resolve "%S/Inputs/github-issue-132-bounds.dfy" --type-system-refresh:false --allow-axioms --show-snippets:false --use-basename-for-filename >> "%t"
// RUN: echo "bounds (type-system-refresh:true)" >> "%t"
// RUN: %exits-with 2 %baredafny resolve "%S/Inputs/github-issue-132-bounds.dfy" --type-system-refresh:true --allow-axioms --show-snippets:false --use-basename-for-filename >> "%t"
// RUN: %diff "%s.expect" "%t"
