// RUN: %exits-with 4 %verify --type-system-refresh:false --general-traits:legacy --general-newtypes:false "%s" > "%t"
// RUN: %exits-with 4 %verify "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/431, the other conditions of a let-such-that
// expression whose translation mentions a heap that the choice function did not take:
// comprehensions over the allocated objects, a bound variable of a type parameter's type,
// "old" and labeled "old" around such a quantifier, a sequence constructor whose initializer
// reads the heap, and a call of a two-state function.

class C {
  const id: int
  constructor (k: int) ensures id == k { id := k; }
}

class Cell {
  var v: int
}

method SetComprehension(k: int)
  requires forall o: C :: o.id != k
{
  ghost var y := (var x: int :| x == 0 && (set o: C | o.id == k) == {}; x);
  var c := new C(k);
  ghost var z := (var x: int :| x == 0 && c in (set o: C | o.id == k); x);
  assert false;  // error: must fail
}

method ISetComprehension(k: int)
  requires forall o: C :: o.id != k
{
  ghost var y := (var x: int :| x == 0 && (iset o: C | o.id == k) == iset{}; x);
  var c := new C(k);
  ghost var z := (var x: int :| x == 0 && c in (iset o: C | o.id == k); x);
  assert false;  // error: must fail
}

method MapComprehension(k: int)
  requires forall o: C :: o.id != k
{
  ghost var y := (var x: int :| x == 0 && (map o: C | o.id == k :: 0) == map[]; x);
  var c := new C(k);
  ghost var z := (var x: int :| x == 0 && c in (map o: C | o.id == k :: 0); x);
  assert false;  // error: must fail
}

// A bound variable of a type parameter's type ranges over the allocated values of that type.
lemma AllIn<T>(m: set<T>) returns (b: bool)
  ensures b == (var x: bool :| x <==> forall t: T :: t in m; x)
{
  b := forall t: T :: t in m;
}

method UseAllIn(m: set<C>)
  requires forall o: C :: o in m
{
  var b0 := AllIn(m);
  var c := new C(0);
  var b1 := AllIn(m);
  if * {
    assert b0 == b1;  // error: must fail
  } else {
    assert false;  // error: must fail
  }
}

// A quantifier over the allocated objects inside "old" and inside a labeled "old".  These
// crashed the verifier ("undeclared identifier: $heap$old").
method Old(k: int)
  requires forall o: C :: o.id != k
{
  label L:
  var c := new C(k);
  label M:
  var d := new C(k);
  ghost var y := (var x: int :| x == 0 && old(forall o: C :: o.id != k); x);
  ghost var y' := (var x: int :| x == 0 && old@L(forall o: C :: o.id != k); x);
  ghost var z := (var x: int :| x == 0 && old@M(exists o: C :: o.id == k); x);
  ghost var z' := (var x: int :| x == 0 && exists o: C :: o.id == k; x);
  assert y == 0 && y' == 0 && z == 0 && z' == 0;
  assert false;  // error: must fail
}

// A sequence constructor applies its initializer in the current heap.
method SeqConstructor(c: Cell)
  modifies c
{
  c.v := 0;
  var f := (i: int) reads c => c.v;
  ghost var y := (var x: seq<int> :| x == seq(1, f) && x[0] == 0; x);
  c.v := 1;
  ghost var z := (var x: seq<int> :| x == seq(1, f) && x[0] == 1; x);
  assert false;  // error: must fail
}

method SeqConstructorOnlyFirst(c: Cell)
  modifies c
{
  c.v := 0;
  var f := (i: int) reads c => c.v;
  ghost var y := (var x: seq<int> :| x == seq(1, f) && x[0] == 0; x);
  c.v := 1;
  assert false;  // error: must fail
}

method SeqConstructorOnlySecond(c: Cell)
  modifies c
{
  c.v := 0;
  var f := (i: int) reads c => c.v;
  c.v := 1;
  ghost var z := (var x: seq<int> :| x == seq(1, f) && x[0] == 1; x);
  assert false;  // error: must fail
}

// A sequence constructor whose initializer reads nothing does not depend on the heap.
lemma PickSeq(n: nat, f: int -> int) returns (s: seq<int>)
  ensures s == (var x: seq<int> :| x == seq(n, f); x)
{
  s := seq(n, f);
}

method UsePickSeq(c: Cell)
  modifies c
{
  var f := (i: int) => i + 1;
  var s0 := PickSeq(3, f);
  c.v := c.v + 1;
  var s1 := PickSeq(3, f);
  assert s0 == s1;
}

// A call of a two-state function in the condition.  This crashed the verifier
// ("undeclared identifier: $heap$old").
twostate predicate Increased(c: Cell)
  reads c
{
  old(c.v) < c.v
}

twostate predicate Same(k: int)
{
  k == k
}

twostate lemma TwoState(c: Cell)
  requires old(c.v) < c.v
{
  ghost var y := (var x: bool :| x == Increased(c); x);
  ghost var z := (var x: bool :| x == Same(3); x);
  assert y && z;
  assert false;  // error: must fail
}
