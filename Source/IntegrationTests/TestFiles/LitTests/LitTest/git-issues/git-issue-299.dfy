// https://github.com/erniecohen/dafny/issues/299
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/../dafny0/DecreasesTo0.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.corpus0-false.doo" > "%t"
// RUN: %baredafny resolve "%t.corpus0-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%S/../dafny0/DecreasesTo0.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.corpus0-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.corpus0-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %baredafny build -t:lib "%S/../dafny0/DecreasesTo1.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.corpus1-false.doo" >> "%t"
// RUN: %baredafny resolve "%t.corpus1-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%S/../dafny0/DecreasesTo1.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.corpus1-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.corpus1-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.reduced-false.doo" >> "%t"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.reduced-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %diff "%s.expect" "%t"

method DecreasesToTupleComponents(x: int, y: int) {
  assert (true, (true decreases to false)) == (true, true);
  assert ((x decreases to y), false) == (true, false);
  assert (x, (y nonincreases to x), y - 1) == (x, true, y - 1);
  assert ((x, y decreases to x, y - 1), (true decreases to false)) == (true, true);
}
