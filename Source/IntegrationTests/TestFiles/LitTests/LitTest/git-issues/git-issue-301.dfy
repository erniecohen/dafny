// https://github.com/erniecohen/dafny/issues/301
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/../dafny0/ReadsOnMethods.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --reads-clauses-on-methods --output "%t.corpus-false.doo" > "%t.corpus-false.build"
// RUN: %baredafny resolve "%t.corpus-false.doo" --cores 1 --allow-warnings --type-system-refresh:false --reads-clauses-on-methods > "%t.corpus-false.readback"
// RUN: %baredafny build -t:lib "%S/../dafny0/ReadsOnMethods.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --reads-clauses-on-methods --output "%t.corpus-true.doo" > "%t.corpus-true.build"
// RUN: %baredafny resolve "%t.corpus-true.doo" --cores 1 --allow-warnings --type-system-refresh:true --reads-clauses-on-methods > "%t.corpus-true.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --reads-clauses-on-methods --output "%t.reduced-false.doo" > "%t.reduced-false.build"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false --reads-clauses-on-methods > "%t.reduced-false.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --reads-clauses-on-methods --output "%t.reduced-true.doo" > "%t.reduced-true.build"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true --reads-clauses-on-methods > "%t.reduced-true.readback"
// RUN: echo "Both resolver round-trips completed." > "%t"
// RUN: %diff "%s.expect" "%t"

// Lambdas in clauses whose grammar does not admit an unparenthesized lambda
// keep their parentheses; assert and invariant expressions stay unchanged.

class Cache { var inv: (int, int) -> bool }
method M(cache: Cache, f: int -> int)
  requires cache.inv == ((key, value) => value == key * key)
  requires f == ((x: int) => x + 1)
  requires forall g: int -> int :: g == f ==> g == (x => x + 1)
  ensures f == (x => x + 1)
{
  assert f == (x => x + 1);
  var i := 0;
  while i < 3
    invariant f == (x => x + 1)
  {
    i := i + 1;
  }
}
function F(f: int -> int): int
  requires f == ((x: int) => x)
  ensures F(f) == 0 && f == (x => x)
{ 0 }
