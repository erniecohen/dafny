// https://github.com/erniecohen/dafny/issues/305
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/../dafny4/Bug108.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.corpus-false.doo" > "%t.corpus-false.build"
// RUN: %baredafny resolve "%t.corpus-false.doo" --cores 1 --allow-warnings --type-system-refresh:false > "%t.corpus-false.readback"
// RUN: %baredafny build -t:lib "%S/../dafny4/Bug108.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.corpus-true.doo" > "%t.corpus-true.build"
// RUN: %baredafny resolve "%t.corpus-true.doo" --cores 1 --allow-warnings --type-system-refresh:true > "%t.corpus-true.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.reduced-false.doo" > "%t.reduced-false.build"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false > "%t.reduced-false.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.reduced-true.doo" > "%t.reduced-true.build"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true > "%t.reduced-true.readback"
// RUN: echo "Both resolver round-trips completed." > "%t"
// RUN: %diff "%s.expect" "%t"

// A set comprehension without a term ends with its range, so a following
// `::` or `, identifier` must not be absorbed by it on read-back.

function F(s: set<int>, t: int): int { t }
method M(A: set<int>, y: int) {
  var a := F((set x | x in A), y);
  var b := [(set x | x in A), {y}];
  var c := forall x | x in (set z | z in A) :: x > 0;
  var d := set x | x in (set z | z in A) :: x + 1;
  var e := map x | x in (set z | z in A) :: x;
  var f := iset x | x in (iset z | z in A) :: x;
  print (set x | x in A), y, "\n";
}
class C { var r: set<object> }
function G(cs: set<C>, c: C): int
  reads (set b | b in cs), c
  reads (set b | b in cs)`r, c
{ 0 }
method R(A: set<int>, y: int) returns (s: set<int>, t: int) {
  var u, v := (set x | x in A), y;
  s, t := (set x | x in A), y;
  return (set x | x in A), y;
}
