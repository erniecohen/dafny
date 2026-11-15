// Regression for https://github.com/erniecohen/dafny/issues/147: what is sent to the solver.
// At widths that are not powers of two (3, 6, 12, 65) the rotations are inline definitions by
// shifts; at powers of two (1, 32) they keep Z3's extended rotations; width 0 is unchanged.
// One check file per width, since the order of the widths' declarations is not fixed.
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-widths.dfy" --solver-path "%review-z3" --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --bprint "%t.widths.bpl" > "%t"
// RUN: %OutputCheck --file-to-check "%t.widths.bpl" "%S/Inputs/github-issue-147-bpl-0.check"
// RUN: %OutputCheck --file-to-check "%t.widths.bpl" "%S/Inputs/github-issue-147-bpl-1.check"
// RUN: %OutputCheck --file-to-check "%t.widths.bpl" "%S/Inputs/github-issue-147-bpl-3.check"
// RUN: %OutputCheck --file-to-check "%t.widths.bpl" "%S/Inputs/github-issue-147-bpl-6.check"
// RUN: %OutputCheck --file-to-check "%t.widths.bpl" "%S/Inputs/github-issue-147-bpl-12.check"
// RUN: %OutputCheck --file-to-check "%t.widths.bpl" "%S/Inputs/github-issue-147-bpl-32.check"
// RUN: %OutputCheck --file-to-check "%t.widths.bpl" "%S/Inputs/github-issue-147-bpl-65.check"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-portable.dfy" --solver-path "%review-z3" --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --solver-log "%t.portable.smt2" >> "%t"
// RUN: %OutputCheck --file-to-check "%t.portable.smt2" "%S/Inputs/github-issue-147-smt-portable.check"
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/github-issue-147-native.dfy" --solver-path "%review-z3" --cores:1 --resource-limit:200000 --verification-time-limit:0 --show-snippets:false --use-basename-for-filename --solver-log "%t.native.smt2" >> "%t"
// RUN: %OutputCheck --file-to-check "%t.native.smt2" "%S/Inputs/github-issue-147-smt-native.check"
// RUN: %OutputCheck --file-to-check "%t" "%s"
// CHECK: Dafny program verifier finished with 1 verified, 0 errors
// CHECK: Dafny program verifier finished with 1 verified, 0 errors
// CHECK: Dafny program verifier finished with 1 verified, 0 errors
