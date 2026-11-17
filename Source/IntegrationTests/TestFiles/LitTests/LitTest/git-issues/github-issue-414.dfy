// Regression for https://github.com/erniecohen/dafny/issues/414.
// For an integer measure, the call's termination check is emitted in the shape it had before the corrected
// decreases order of https://github.com/erniecohen/dafny/issues/264: the Boogie is the same in both modes, and
// the induction step stays well inside a resource cap that the conjoined check exceeded.
// Only the lemma Skip is verified.  The input is kept as found, with a lambda in T that is not well-formed
// (k is not known to be a nat), because small edits to it change the solver's cost by more than the gap.
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-414.dfy" --filter-symbol Skip --resource-limit:500000 --solver-path "%review-z3" --cores:1 --show-snippets:false --use-basename-for-filename --bprint "%t.default.bpl" > "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-414.dfy" --filter-symbol Skip --resource-limit:500000 --legacy-decreases-order --solver-path "%review-z3" --cores:1 --show-snippets:false --use-basename-for-filename --bprint "%t.legacy.bpl" >> "%t"
// RUN: %diff "%t.default.bpl" "%t.legacy.bpl"
// RUN: %diff "%s.expect" "%t"
