// Regression: the Boogie program that Dafny prints verifies like the program Dafny verified (#182).
// Dafny marks function definition axioms hideable.  Boogie's printer dropped `hideable`, so in the
// printed program `hide *` hid nothing, and Boogie proved the assertion in Hidden that Dafny
// reports.  The printer now writes `hideable`, and both report the same error.
// RUN: %exits-with 4 %baredafny verify "%s" --show-snippets:false --use-basename-for-filename --solver-path "%review-z3" --verification-time-limit:60 --cores:1 --allow-warnings --bprint "%t.bpl" > "%t"
// RUN: dotnet tool run boogie -prune:1 -typeEncoding:a -useBaseNameForFileName '-proc:*(correctness)' -timeLimit:60 -proverOpt:PROVER_PATH=%review-z3 "%t.bpl" >> "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s"
// CHECK: github-issue-182\.dfy\(19,\d+\): Error: assertion could not be proved
// CHECK: Dafny program verifier finished with \d+ verified, 1 error
// CHECK: Error: this assertion could not be proved
// CHECK-L: Boogie program verifier finished with 1 verified, 1 error

function F(x: int): int { x + 1 }

// Fails in Dafny, and in the printed program: F's definition is hidden.
lemma Hidden(y: int)
{
  hide *;
  assert F(y) == y + 1;
}

// Verifies in both: F's definition is revealed again.
lemma Revealed(y: int)
{
  hide *;
  reveal F;
  assert F(y) == y + 1;
}
