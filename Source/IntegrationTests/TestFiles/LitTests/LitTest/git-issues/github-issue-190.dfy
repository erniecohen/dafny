// Regression: Z3's preprocessing option smt.refine_inj_axioms is unsound (Z3Prover/z3#11435),
// and with it on, Dafny proved `ensures false` in MustFail below (#190).  Its preconditions hold
// at a = 0, b = 1, since F is the identity on the diagonal z == w and 0 elsewhere.  Dafny now
// gives Z3 smt.refine_inj_axioms=false, both as a global prover option and as an smt_option of
// each implementation, and the postcondition is rejected, with Z3 4.12.1 and with Z3 5.1.0.
//
// Default: rejected under both solvers.
// RUN: %exits-with 4 %baredafny verify --solver-path "%z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" > "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" >> "%t"
// Control: turning the option back on, as before the fix, proves `ensures false` (both paths
// take the value given with --solver-option).
// RUN: %baredafny verify --solver-path "%z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-option O:smt.refine_inj_axioms=true "%s" >> "%t"
// RUN: %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-option O:smt.refine_inj_axioms=true "%s" >> "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s"
//
// Each path on its own, in the printed Boogie program, verified by Boogie with Z3 5.1.0.
// RUN: %exits-with 4 %baredafny verify --solver-path "%review-z3" --use-basename-for-filename --show-snippets:false --standard-libraries:false --bprint "%t.bpl" "%s" > "%t.dafny"
// RUN: %OutputCheck --file-to-check "%t.bpl" "%S/Inputs/github-issue-190-bpl.check"
// RUN: %sed 's#\{:smt_option .smt\.refine_inj_axioms., .false.\} ?##g' "%t.bpl" > "%t.global.bpl"
// The per-implementation option alone, against a global true: rejected.
// RUN: dotnet tool run boogie -typeEncoding:a -useBaseNameForFileName '-proc:*MustFail' -timeLimit:60 -proverOpt:PROVER_PATH=%review-z3 -proverOpt:O:auto_config=false -proverOpt:O:smt.refine_inj_axioms=true "%t.bpl" > "%t.boogie"
// The global option alone (the per-implementation one deleted): rejected when false ...
// RUN: dotnet tool run boogie -typeEncoding:a -useBaseNameForFileName '-proc:*MustFail' -timeLimit:60 -proverOpt:PROVER_PATH=%review-z3 -proverOpt:O:auto_config=false -proverOpt:O:smt.refine_inj_axioms=false "%t.global.bpl" >> "%t.boogie"
// ... and `ensures false` proved when true.
// RUN: dotnet tool run boogie -typeEncoding:a -useBaseNameForFileName '-proc:*MustFail' -timeLimit:60 -proverOpt:PROVER_PATH=%review-z3 -proverOpt:O:auto_config=false -proverOpt:O:smt.refine_inj_axioms=true "%t.global.bpl" >> "%t.boogie"
// RUN: %OutputCheck --file-to-check "%t.boogie" "%S/Inputs/github-issue-190-boogie.check"
//
// CHECK: github-issue-190\.dfy\(\d+,\d+\): Error: a postcondition could not be proved on this return path
// CHECK: Dafny program verifier finished with \d+ verified, 1 error
// CHECK: github-issue-190\.dfy\(\d+,\d+\): Error: a postcondition could not be proved on this return path
// CHECK: Dafny program verifier finished with \d+ verified, 1 error
// CHECK-NOT: Error
// CHECK: Dafny program verifier finished with \d+ verified, 0 errors
// CHECK-NOT: Error
// CHECK: Dafny program verifier finished with \d+ verified, 0 errors

opaque function F(x: int, z: int, w: int): int {
  if z == w then x else 0
}

lemma {:induction false} MustFail(a: int, b: int)
  requires a != b
  requires forall x: int, y: int, z: int ::
    F(x,z,z) == F(y,z,z) ==> x == y
  requires F(a,a,b) == F(b,a,b)
  ensures false
{
}
