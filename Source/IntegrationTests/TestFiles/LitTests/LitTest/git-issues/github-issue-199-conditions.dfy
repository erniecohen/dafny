// RUN: %exits-with 2 %resolve --lemma-bindings "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: the bound results of a lemma are a function of the lemma's
// arguments, and of the heap if the postcondition reads it.  So no result may show a reference, and the
// postcondition must be an expression that a function body may contain.  Each "error" below is a binding that
// does not meet these conditions; the bindings without that comment are the controls, which are accepted.

class C {
  var f: int
  const id: int
}
datatype Box = Box(c: C?)

// ----- Results that may show a reference

lemma {:axiom} Reference(n: nat) returns (c: C?)
lemma {:axiom} Boxed(n: nat) returns (b: Box)
lemma {:axiom} Arrow() returns (f: int ~> int)
lemma {:axiom} Partial() returns (f: int --> int)
lemma {:axiom} Generic<T>(x: T) returns (y: T)
  ensures y == x
lemma {:axiom} Sets(c: C) returns (s: set<C>)

ghost function BindReference(n: nat): int {
  var c := Reference(n); 0  // error: C? is a reference type
}

ghost function BindBoxed(n: nat): int {
  var b := Boxed(n); 0  // error: Box may contain a reference
}

ghost function BindArrow(): int {
  var f := Arrow(); 0  // error: a function that may read the heap
}

ghost function BindPartial(): int {
  var f := Partial(); 0
}

ghost function BindAtClass(c: C): int {
  var d := Generic(c); 0  // error: the result type is instantiated with a class
}

ghost function BindAtInt(n: int): int {
  var m := Generic(n); m
}

ghost function BindAtAny<T>(x: T): T {
  var y := Generic(x); y  // error: T may contain references
}

ghost function BindAtNoReferences<T(!new)>(x: T): T {
  var y := Generic(x); y
}

ghost function BindSets(c: C): int {
  var s := Sets(c); 0  // error: a set of references
}

// ----- Postconditions that a function body may not contain

lemma {:axiom} Allocated(c: C) returns (b: bool)
  ensures b == allocated(c)
lemma {:axiom} Old(c: C) returns (b: int)
  ensures b == old(c.f)
lemma {:axiom} Fresh(c: C) returns (b: bool)
  ensures b == fresh(c)
lemma {:axiom} Unchanged(c: C) returns (b: bool)
  ensures b == unchanged(c)
twostate function Increase(c: C): int
  reads c
{
  c.f - old(c.f)
}
lemma {:axiom} TwoStateFunction(c: C) returns (b: int)
  ensures b == Increase(c)
lemma {:axiom} AllObjects(k: int) returns (b: bool)
  ensures b <==> forall o: C :: o.id != k
lemma {:axiom} ObjectsOfASet(k: int, s: set<C>) returns (b: bool)
  ensures b <==> forall o: C :: o in s ==> o.id != k
lemma {:axiom} AllOfAType<T>(x: T) returns (b: bool)
  ensures b <==> forall y: T :: y == x
lemma {:axiom} AllOfATypeWithoutReferences<T(!new)>(x: T) returns (b: bool)
  ensures b <==> forall y: T :: y == x
lemma {:axiom} SetOfAllObjects() returns (b: bool)
  ensures b == ((set o: C | true) == {})
lemma {:axiom} InASecondClause(c: C) returns (b: int)
  ensures b >= 0
  ensures b == old(c.f)

ghost function BindAllocated(c: C): bool {
  var b := Allocated(c); b  // error: allocated
}

ghost function BindOld(c: C): int
  reads c
{
  var b := Old(c); b  // error: old
}

ghost function BindFresh(c: C): bool {
  var b := Fresh(c); b  // error: fresh
}

ghost function BindUnchanged(c: C): bool
  reads c
{
  var b := Unchanged(c); b  // error: unchanged
}

ghost function BindTwoStateFunction(c: C): int
  reads c
{
  var b := TwoStateFunction(c); b  // error: a two-state function
}

ghost function BindAllObjects(k: int): bool {
  var b := AllObjects(k); b  // error: a quantifier over the allocated objects
}

ghost function BindObjectsOfASet(k: int, s: set<C>): bool
  reads s
{
  var b := ObjectsOfASet(k, s); b
}

ghost function BindAllOfAType(k: int): bool {
  var b := AllOfAType(k); b  // error: a quantifier over a type that may contain references
}

ghost function BindAllOfATypeWithoutReferences(k: int): bool {
  var b := AllOfATypeWithoutReferences(k); b
}

ghost function BindSetOfAllObjects(): bool {
  var b := SetOfAllObjects(); b  // error: a comprehension over the allocated objects
}

ghost function BindInASecondClause(c: C): int
  reads c
{
  var b := InASecondClause(c); b  // error: old, in the second clause
}

// The conditions are those of the lemma that is bound, whatever kind of expression binds its results.

twostate function InATwoStateFunction(c: C): int
  reads c
{
  var b := Old(c); b  // error: old
}

lemma InALemma(c: C)
{
  var x := (var b := Old(c); b);  // error: old
}

// ----- A postcondition that depends on the heap and binds results of its own lemma

lemma {:axiom} OwnResults(c: C, n: nat) returns (m: int)
  ensures m == c.f + if n == 0 then 0 else var k := OwnResults(c, n - 1); k  // error: binds its own results
lemma {:axiom} First(c: C, n: nat) returns (m: int)
  ensures m == c.f + if n == 0 then 0 else var k := Second(c, n - 1); k  // error: so does Second, through First
lemma {:axiom} Second(c: C, n: nat) returns (m: int)
  ensures m == if n == 0 then 0 else var k := First(c, n - 1); k  // error: so does First, through Second
lemma {:axiom} WithoutTheHeap(n: nat) returns (m: int)
  ensures m == if n == 0 then 0 else var k := WithoutTheHeap(n - 1); k

ghost function BindOwnResults(c: C, n: nat): int
  reads c
{
  var m := OwnResults(c, n); m  // error: the postcondition depends on the heap and binds results of OwnResults
}

ghost function BindFirst(c: C, n: nat): int
  reads c
{
  var m := First(c, n); m  // error: the postcondition depends on the heap and binds results of First
}

ghost function BindSecond(c: C, n: nat): int
  reads c
{
  var m := Second(c, n); m  // error: the postcondition depends on the heap and binds results of Second
}

ghost function BindWithoutTheHeap(n: nat): int {
  var m := WithoutTheHeap(n); m
}
