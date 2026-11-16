// Member invariants (https://github.com/erniecohen/dafny/issues/105): a datatype with invariants and
// its raw type compile to the same target type, and its raw members compile as other members do.
// The program is not verified yet, so it is compiled with --no-verify.
// RUN: %run --no-verify --member-invariants "%s" > "%t"
// RUN: %run --no-verify --member-invariants --target py "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

datatype Interval = Interval(lo: int, hi: int) {
  invariant Ok()
  predicate Ok() { lo <= hi }
  function Width(): int { hi - lo }
}

datatype Pair<A(==)> = Pair(a: A, b: A) {
  invariant Different()
  predicate Different() { a != b }
  function Swap(): Pair<A> { Pair(b, a) }
}

method Widen(i: Interval) returns (j: Interval) {
  var r := i.(lo := i.lo - 1);
  j := r;
}

method Main() {
  var r: raw Interval := Interval(3, 1);
  var i: Interval := Interval(1, 3);
  var s: seq<raw Interval> := [r, i];
  print r, " ", r.Ok(), " ", i.Width(), " ", s, "\n";
  var w := Widen(i);
  print w, " ", w.Width(), "\n";
  match r
  case Interval(a, b) => print a, " ", b, "\n";
  var p: Pair<int> := Pair(1, 2);
  var q: raw Pair<int> := Pair(5, 5);
  print p.Swap(), " ", q.Different(), " ", q.(b := 6).Different(), "\n";
}
