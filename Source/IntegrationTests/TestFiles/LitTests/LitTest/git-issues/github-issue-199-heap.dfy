// RUN: %exits-with 4 %verify --lemma-bindings "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: binding the results of a lemma whose
// postcondition reads the heap.  The bound results are then a function of the heap too.  A function
// that binds them must be allowed to read what the postcondition reads at results that satisfy it,
// and the binding is checked for that.

class Cell {
  var f: int
  var g: int
}

lemma Get(c: Cell) returns (x: int)
  ensures x == c.f
{
  x := c.f;
}

ghost function F(c: Cell): int
  reads c
{
  var x := Get(c); x
}

lemma FIs(c: Cell)
  ensures F(c) == c.f
{
}

ghost function NoReads(c: Cell): int {
  var x := Get(c); x  // error: insufficient reads clause
}

ghost function WrongReads(c: Cell, d: Cell): int
  reads d
{
  var x := Get(c); x  // error: insufficient reads clause
}

ghost function WrongField(c: Cell): int
  reads c`g
{
  var x := Get(c); x  // error: insufficient reads clause
}

// ---------- What the postcondition reads only at values that do not satisfy it is not read

lemma OnlyZero(c: Cell) returns (x: int)
  ensures x == 0
  ensures x != 0 ==> c.f == 0
{
  x := 0;
}

ghost function Zero(c: Cell): int {
  var x := OnlyZero(c); x
}

lemma ZeroIs(c: Cell)
  ensures Zero(c) == 0
{
}

lemma ZeroOrField(c: Cell) returns (x: int)
  ensures x == 0 || (x == 1 && c.f == 0)
{
  x := 0;
}

ghost function ZeroOrOne(c: Cell): int {
  var x := ZeroOrField(c); x  // error: insufficient reads clause
}

// ---------- A function that binds the results satisfies its frame

lemma Above(c: Cell) returns (x: int)
  ensures x > c.f
{
  x := c.f + 1;
}

ghost function AboveF(c: Cell): int
  reads c`f
{
  var x := Above(c); x
}

method Frame(c: Cell)
  modifies c
{
  ghost var before := AboveF(c);
  c.g := 7;
  assert before == AboveF(c);  // the function reads only c.f
  c.f := c.f + 1;
  assert AboveF(c) > c.f;
  if * {
    assert before == AboveF(c);  // error
  } else {
    assert false;  // error
  }
}

method WholeHeap(c: Cell)
  modifies c
{
  ghost var before := (var x := Above(c); x);
  c.g := 7;
  ghost var after := (var x := Above(c); x);
  assert before > c.f && after > c.f;
  assert before == after;  // error: outside a function, nothing says what the results depend on
}

// ---------- In "old", the call is checked, and the results are chosen, in the old heap

lemma PositiveField(c: Cell) returns (x: int)
  requires c.f > 0
  ensures 0 < x <= c.f
{
  x := c.f;
}

method Old(c: Cell)
  requires c.f == 5
  modifies c
{
  c.f := 0;
  ghost var o := old(var x := PositiveField(c); x);
  assert 0 < o <= 5;
  ghost var n := (var x := PositiveField(c); x);  // error: precondition
}

method OldAndNew(c: Cell)
  modifies c
{
  c.f := c.f + 1;
  ghost var o := old(var x := Get(c); x);
  ghost var n := (var x := Get(c); x);
  assert o == old(c.f) && n == c.f;
  assert o == n;  // error
}

twostate lemma InTwoState(c: Cell)
  ensures old(var x := Get(c); x) == old(c.f)
  ensures (var x := Get(c); x) == c.f
{
}

twostate function TwoStateBinder(c: Cell): int
  reads c
{
  old(var x := Get(c); x) + (var x := Get(c); x)
}

twostate lemma TwoStateBinderIs(c: Cell)
  ensures TwoStateBinder(c) == old(c.f) + c.f
{
}

// ---------- In a reads clause

lemma IsZero(c: Cell) returns (b: bool)
  ensures b == (c.f == 0)
{
  b := c.f == 0;
}

ghost function ReadsBinding(c: Cell): int
  reads c
  reads var b := IsZero(c); if b then {} else {c}
{
  if c.f == 0 then 0 else c.g
}

ghost function ReadsBindingAlone(c: Cell): int
  reads var b := IsZero(c); if b then {} else {c}  // error: the reads clause reads c.f
{
  0
}

// ---------- An instance lemma, and a heap-dependent precondition with a heap-independent postcondition

class Counter {
  var n: nat

  lemma Next() returns (m: nat)
    ensures m > n
  {
    m := n + 1;
  }

  ghost function AboveN(): nat
    reads this
  {
    var m := Next(); m
  }

  lemma Small(k: nat) returns (m: nat)
    requires n > 0
    ensures m <= k
  {
    m := 0;
  }
}

method Counters(c: Counter)
  requires c.n > 0
  modifies c
{
  ghost var a := c.AboveN();
  assert a > c.n;
  ghost var s := (var m := c.Small(3); m);
  c.n := c.n + 1;
  assert c.AboveN() > c.n;
  ghost var s' := (var m := c.Small(3); m);
  assert s == s' <= 3;  // the postcondition of Small does not depend on the heap, so neither do its results
  assert a == c.AboveN();  // error
}

// ---------- A postcondition that binds the results of another lemma reads what that lemma's postcondition reads

lemma GetTwice(c: Cell) returns (x: int)
  ensures var y := Get(c); x == 2 * y
{
  x := 2 * c.f;
}

ghost function Double(c: Cell): int
  reads c
{
  var x := GetTwice(c); x
}

lemma DoubleIs(c: Cell)
  ensures Double(c) == 2 * c.f
{
}

ghost function DoubleNoReads(c: Cell): int {
  var x := GetTwice(c); x  // error: insufficient reads clause
}

// ---------- None of the above proves false

method Vacuity(c: Cell, k: Counter)
  requires c.f == 5
  modifies c
{
  ghost var x := F(c) + Zero(c) + AboveF(c) + ReadsBinding(c) + k.AboveN() + Double(c);
  c.f := 6;
  ghost var y := F(c) + AboveF(c) + old(var x := PositiveField(c); x) + TwoStateBinder(c);
  assert false;  // error
}
