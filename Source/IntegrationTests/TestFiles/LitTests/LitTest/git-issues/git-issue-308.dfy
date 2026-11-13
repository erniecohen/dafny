// https://github.com/erniecohen/dafny/issues/308
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/git-issue-2597-verification.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.corpus-false.doo" > "%t"
// RUN: %baredafny resolve "%t.corpus-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%S/git-issue-2597-verification.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.corpus-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.corpus-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.reduced-false.doo" >> "%t"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.reduced-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %baredafny build -t:lib "%S/git-issue-851.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.map-false.doo" > "%t.map-false.build"
// RUN: %baredafny resolve "%t.map-false.doo" --cores 1 --allow-warnings --type-system-refresh:false > "%t.map-false.readback"
// RUN: %baredafny build -t:lib "%S/git-issue-851.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.map-true.doo" > "%t.map-true.build"
// RUN: %baredafny resolve "%t.map-true.doo" --cores 1 --allow-warnings --type-system-refresh:true > "%t.map-true.readback"
// RUN: %diff "%s.expect" "%t"

lemma AFact() {}
function Id(i: int): int { i }

method CallStatementOperands() {
  ghost var x := old(5 + (AFact(); 0));
  ghost var y := 5 * (AFact(); 1);
  ghost var m := map i: int | 0 <= i < 1 :: (AFact(); Id(i)) := i;
}
