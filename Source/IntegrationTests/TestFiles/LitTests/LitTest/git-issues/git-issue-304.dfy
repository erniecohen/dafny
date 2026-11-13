// https://github.com/erniecohen/dafny/issues/304
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/../dafny2/Calculations.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.corpus-false.doo" > "%t.corpus-false.build"
// RUN: %baredafny resolve "%t.corpus-false.doo" --cores 1 --allow-warnings --type-system-refresh:false > "%t.corpus-false.readback"
// RUN: %baredafny build -t:lib "%S/../dafny2/Calculations.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.corpus-true.doo" > "%t.corpus-true.build"
// RUN: %baredafny resolve "%t.corpus-true.doo" --cores 1 --allow-warnings --type-system-refresh:true > "%t.corpus-true.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.reduced-false.doo" > "%t.reduced-false.build"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false > "%t.reduced-false.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.reduced-true.doo" > "%t.reduced-true.build"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true > "%t.reduced-true.readback"
// RUN: echo "Both resolver round-trips completed." > "%t"
// RUN: %diff "%s.expect" "%t"

// An explicit annotation that names an implicitly generated type parameter
// (here the `_T0` of `L`'s formal `xs: List`) is kept in the library.

datatype List<T> = Nil | Cons(T, List)

function concat<T>(a: List<T>, b: List<T>): List<T> {
  match a
  case Nil => b
  case Cons(x, xs) => Cons(x, concat(xs, b))
}

lemma L(xs: List)
{
  assert forall g: _T0, gs :: concat(Cons(g, Nil), gs) == Cons(g, gs);
}
