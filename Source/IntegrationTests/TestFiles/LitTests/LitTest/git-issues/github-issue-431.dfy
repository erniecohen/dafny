// RUN: %exits-with 4 %verify --type-system-refresh:false --general-traits:legacy --general-newtypes:false "%s" > "%t"
// RUN: %exits-with 4 %verify "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/431: a let-such-that expression whose condition
// depends on which objects are allocated, without reading a mutable field, got a choice function
// that takes no heap.  Its axiom then stated the condition about the placeholder heap of
// heap-independent definitions, the same heap at every use, and two such expressions around an
// allocation proved false.  The choice function of such an expression now takes the heap.

class C {
  const id: int
  constructor (k: int) ensures id == k { id := k; }
}

// The reproduction from the issue.
method Repro(k: int)
  requires forall o: C :: o.id != k
{
  ghost var y := (var x: int :| x == 0 && forall o: C :: o.id != k; x);
  var c := new C(k);
  ghost var z := (var x: int :| x == 0 && exists o: C :: o.id == k; x);
  assert y == 0 && z == 0;
  assert false;  // error: must fail
}

// Controls: the same method with only the first let, with only the second, and with neither.
method OnlyFirst(k: int)
  requires forall o: C :: o.id != k
{
  ghost var y := (var x: int :| x == 0 && forall o: C :: o.id != k; x);
  var c := new C(k);
  assert false;  // error: must fail
}

method OnlySecond(k: int)
  requires forall o: C :: o.id != k
{
  var c := new C(k);
  ghost var z := (var x: int :| x == 0 && exists o: C :: o.id == k; x);
  assert false;  // error: must fail
}

method NoLets(k: int)
  requires forall o: C :: o.id != k
{
  var c := new C(k);
  assert false;  // error: must fail
}

// The same let in a lemma's postcondition.  The lemma verifies, and its result before an
// allocation is not known to be its result after.
lemma Pick(k: int) returns (b: bool)
  ensures b == (var x: bool :| x <==> forall o: C :: o.id != k; x)
{
  b := forall o: C :: o.id != k;
}

method UsePick(k: int)
  requires forall o: C :: o.id != k
{
  var b0 := Pick(k);
  var c := new C(k);
  var b1 := Pick(k);
  if * {
    assert b0 == b1;  // error: must fail
  } else {
    assert false;  // error: must fail
  }
}

// A condition whose quantifier is bounded by a set does not depend on allocation.  Its choice
// function still takes no heap, so the value is the same in every heap.
class Cell {
  var v: int
}

lemma PickBounded(k: int, s: set<C>) returns (b: bool)
  ensures b == (var x: bool :| x <==> forall o: C :: o in s ==> o.id != k; x)
{
  b := forall o: C :: o in s ==> o.id != k;
}

method UsePickBounded(k: int, s: set<C>, d: Cell)
  modifies d
{
  var b0 := PickBounded(k, s);
  var c := new C(k);
  d.v := d.v + 1;
  var b1 := PickBounded(k, s);
  assert b0 == b1;
}
