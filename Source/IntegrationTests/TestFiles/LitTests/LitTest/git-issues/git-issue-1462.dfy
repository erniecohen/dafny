// Regression: a recursive call in a two-state lemma or function compares the callee's decreases clause with
// the caller's in its state on entry, not in its previous state (dafny-lang/dafny#1462).  The negative cases
// recurse forever, and Exploit and ExploitThroughFunction proved false before the fix.
// RUN: echo "original (type-system-refresh:false)" > "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/git-issue-1462-original.dfy" --type-system-refresh:false --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "original (type-system-refresh:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/git-issue-1462-original.dfy" --type-system-refresh:true --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "positive (type-system-refresh:false)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/git-issue-1462-positive.dfy" --type-system-refresh:false --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "positive (type-system-refresh:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/git-issue-1462-positive.dfy" --type-system-refresh:true --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "negative (type-system-refresh:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-1462-negative.dfy" --type-system-refresh:false --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: echo "negative (type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/git-issue-1462-negative.dfy" --type-system-refresh:true --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 >> "%t"
// RUN: %diff "%s.expect" "%t"
