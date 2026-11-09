// Regression for https://github.com/erniecohen/dafny/issues/147: Z3 composed the counts of nested
// rotations modulo 2^W instead of modulo W, and so proved false facts at widths W that are not
// powers of two.  Each fixture runs with Z3 5.1.0 (%review-z3) and Z3 4.12.1 (%review-z3-4.12.1),
// in both resolver modes, with the issue's resource limit of 200,000.  The original program stays
// alone in its file, with its symbolic counts.
// RUN: echo "original (review-z3, type-system-refresh:false)" > "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-original.dfy" --solver-path "%review-z3" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "good (review-z3, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-good.dfy" --solver-path "%review-z3" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "controls (review-z3, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-controls.dfy" --solver-path "%review-z3" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "true (review-z3, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-true.dfy" --solver-path "%review-z3" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "false (review-z3, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-false.dfy" --solver-path "%review-z3" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "bounds (review-z3, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-bounds.dfy" --solver-path "%review-z3" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "original (review-z3, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-original.dfy" --solver-path "%review-z3" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "good (review-z3, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-good.dfy" --solver-path "%review-z3" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "controls (review-z3, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-controls.dfy" --solver-path "%review-z3" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "true (review-z3, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-true.dfy" --solver-path "%review-z3" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "false (review-z3, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-false.dfy" --solver-path "%review-z3" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "bounds (review-z3, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-bounds.dfy" --solver-path "%review-z3" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "original (review-z3-4.12.1, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-original.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "good (review-z3-4.12.1, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-good.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "controls (review-z3-4.12.1, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-controls.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "true (review-z3-4.12.1, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-true.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "false (review-z3-4.12.1, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-false.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "bounds (review-z3-4.12.1, type-system-refresh:false)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-bounds.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:false --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "original (review-z3-4.12.1, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-original.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "good (review-z3-4.12.1, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-good.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "controls (review-z3-4.12.1, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-controls.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "true (review-z3-4.12.1, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-true.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "false (review-z3-4.12.1, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-false.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: echo "bounds (review-z3-4.12.1, type-system-refresh:true)" >> "%t"
// RUN: %exits-with 4 %baredafny verify "%S/Inputs/github-issue-147-bounds.dfy" --solver-path "%review-z3-4.12.1" --type-system-refresh:true --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --error-limit:0 >> "%t"
// RUN: %diff "%s.expect" "%t"

// Fixtures, in Inputs/:
//   github-issue-147-original.dfy: the false postcondition of the issue must FAIL.
//   github-issue-147-good.dfy: its true counterpart must verify.
//   github-issue-147-controls.dfy: assert false under the same preconditions, and in branches on the correct values, must FAIL.
//   github-issue-147-true.dfy: true compositions at widths 0, 1, 3, 6, 12, 32 and 64 must verify.
//   github-issue-147-false.dfy: their false companions must each FAIL.
//   github-issue-147-bounds.dfy: negative and over-width counts must still be reported.
