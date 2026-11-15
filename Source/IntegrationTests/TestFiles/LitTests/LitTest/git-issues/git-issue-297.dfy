// https://github.com/erniecohen/dafny/issues/297
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/../comp/Extern.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.corpus-false.doo" > "%t"
// RUN: %baredafny resolve "%t.corpus-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%S/../comp/Extern.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.corpus-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.corpus-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.reduced-false.doo" >> "%t"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.reduced-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %diff "%s.expect" "%t"

method TakeLeft(t: (ghost int, int)) { }
method TakeRight(t: (int, ghost int)) { }
method TakeNested(t: ((ghost int, int), ghost int)) { }

method GhostTupleArguments() {
  TakeLeft((ghost 10, 2));
  TakeRight((1, ghost 20));
  TakeNested(((ghost 10, 2), ghost 30));
  var t: (ghost int, int) := (ghost 10, 2);
  TakeLeft(t);
}
