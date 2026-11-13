// https://github.com/erniecohen/dafny/issues/301
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/../dafny0/ReadsOnMethods.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --reads-clauses-on-methods --output "%t.corpus-false.doo" > "%t.corpus-false.build"
// RUN: %baredafny resolve "%t.corpus-false.doo" --cores 1 --allow-warnings --type-system-refresh:false --reads-clauses-on-methods > "%t.corpus-false.readback"
// RUN: %baredafny build -t:lib "%S/../dafny0/ReadsOnMethods.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --reads-clauses-on-methods --output "%t.corpus-true.doo" > "%t.corpus-true.build"
// RUN: %baredafny resolve "%t.corpus-true.doo" --cores 1 --allow-warnings --type-system-refresh:true --reads-clauses-on-methods > "%t.corpus-true.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.reduced-false.doo" > "%t"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.reduced-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %diff "%s.expect" "%t"

method LambdaOperands(f: (int, int) -> bool) {
  assert f == ((x, y) => x == y);
  assert ((x, y) => x == y) == f;
  assert f != ((x, y) => x < y);
  assert (f == ((x, y) => x == y)) && true;
}
