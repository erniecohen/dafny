// General-map witnesses must retain source types and every translated capture.
type Small = x: int | 0 <= x < 2 witness 0
type Empty = x: int | false witness *

ghost function CapturedMap(n: int): map<int, int> {
  map x: int | x == n :: 0 := x
}

ghost function CapturedTuple(a: int, b: bool): map<int, (int, bool)> {
  map x: int, y: bool | x == a && y == b :: 0 := (x, y)
}

ghost function CapturedGeneric<T(!new)>(v: T): map<int, T> {
  map x: T | x == v :: 0 := x
}

ghost function CapturedIMap(n: int): imap<int, int> {
  imap x: int | x == n :: 0 := x
}

// The domain-trigger mode needs a source tuple term to instantiate its
// existential witness. This contract proves membership for every captured pair.
lemma CapturedTupleDomain(a: int, b: bool)
  ensures 0 in CapturedTuple(a, b).Keys
{
  var witnessPair := (a, b);
  assert witnessPair.0 == a;
  assert witnessPair.1 == b;
}

lemma TwoCapturedEnvironments(a: int, b: int) {
  CapturedTupleDomain(a, false);
  CapturedTupleDomain(b, true);
  assert CapturedMap(a).Keys == {0};
  assert CapturedMap(a)[0] == a;
  assert CapturedMap(b)[0] == b;
  assert CapturedTuple(a, false)[0] == (a, false);
  assert CapturedTuple(b, true)[0] == (b, true);
  assert CapturedIMap(a)[0] == a;
  assert CapturedIMap(b)[0] == b;
}

// Lambda translation substitutes its argument in the key alone. This used to
// leave the comprehension unchanged when range and value had no such capture.
lemma KeyOnlySubstitution(n: int) {
  var keyOnly := (k: int) => map x: int | x == 0 :: k := x;
  assert keyOnly(n).Keys == {n};
  assert keyOnly(n)[n] == 0;
  var booleanKey := (k: bool) => map x: int | x == 0 :: k := x;
  assert booleanKey(false).Keys == {false};
  assert booleanKey(true).Keys == {true};
}

// Rebuilding outer predicates must keep the same nested choice symbols, even
// when substitution creates fresh nodes for the inner comprehension.
lemma LocalArithmeticCapture(n: int, i: int) {
  var m := map j: int | j == n + i :: 0 := j;
  assert m.Keys == {0};
  assert m[0] == n + i;
}

// Name the inner computation so a source witness uses the same key term as
// the outer relation. The nested source comprehensions stay in these bodies.
ghost function NestedInnerValue82(v: int): (value: int)
  ensures value == v
{
  (map j: int | j == v :: 0 := j)[0]
}

ghost function NestedRangeMap82(n: int): map<int, int> {
  map i: int | 0 <= i < 2 &&
    (map j: int | j == n + i :: 0 := j)[0] == n + i :: i + 10 := i
}

ghost function NestedKeyMap82(n: int): map<int, int> {
  map i: int | 0 <= i < 2 :: NestedInnerValue82(n + i) := i
}

lemma NestedRangeWitness82(n: int, i: int)
  requires 0 <= i < 2
  ensures i + 10 in NestedRangeMap82(n).Keys
{
  // This tuple supplies the native source witness to the domain trigger.
  var witnessTuple := (i, false);
  assert witnessTuple.0 == i;
}

lemma NestedRangeMapContract82(n: int)
  ensures NestedRangeMap82(n).Keys == {10, 11}
  ensures NestedRangeMap82(n)[10] == 0
  ensures NestedRangeMap82(n)[11] == 1
{
  NestedRangeWitness82(n, 0);
  NestedRangeWitness82(n, 1);
}

// Use the same named inner computation as the actual key predicate.
lemma NestedKeyWitness82(n: int, i: int)
  requires 0 <= i < 2
  ensures NestedInnerValue82(n + i) in NestedKeyMap82(n).Keys
  ensures NestedKeyMap82(n)[NestedInnerValue82(n + i)] == i
{
  NestedInnerIdentity82();
  assert NestedInnerValue82(n + i) == n + i;
  // This tuple supplies the native source witness to the domain trigger.
  var witnessTuple := (i, false);
  assert witnessTuple.0 == i;
}

lemma NestedInnerIdentity82()
  ensures forall value: int :: NestedInnerValue82(value) == value
{
  forall value: int
    ensures NestedInnerValue82(value) == value
  {
    assert NestedInnerValue82(value) == value;
  }
}

lemma NestedKeyMembership82(n: int, key: int)
  ensures key in NestedKeyMap82(n).Keys <==> key == n || key == n + 1
{
  NestedInnerIdentity82();
  if key == n { NestedKeyWitness82(n, 0); }
  if key == n + 1 { NestedKeyWitness82(n, 1); }
}

lemma NestedKeyMapContract82(n: int)
  ensures NestedKeyMap82(n).Keys == {n, n + 1}
  ensures NestedKeyMap82(n)[n] == 0
  ensures NestedKeyMap82(n)[n + 1] == 1
{
  NestedInnerIdentity82();
  NestedKeyWitness82(n, 0);
  NestedKeyWitness82(n, 1);
  forall key: int
    ensures key in NestedKeyMap82(n).Keys <==> key == n || key == n + 1
  {
    NestedKeyMembership82(n, key);
  }
}

lemma NestedGeneralMaps(n: int) {
  NestedRangeMapContract82(n);
  NestedKeyMapContract82(n);
  var s := set i: int | 0 <= i < 2 &&
    (map j: int | j == n + i :: 0 := j)[0] == n + i;
  assert s == {0, 1};
  var m := NestedRangeMap82(n);
  assert m.Keys == {10, 11};
  assert m[10] == 0;
  assert m[11] == 1;
  var nestedKey := NestedKeyMap82(n);
  assert nestedKey.Keys == {n, n + 1};
  assert nestedKey[n] == 0;
  assert nestedKey[n + 1] == 1;
}

lemma GenericWitnesses<T(!new)>(a: T, b: T) {
  assert CapturedGeneric(a).Keys == {0};
  assert CapturedGeneric(a)[0] == a;
  assert CapturedGeneric(b)[0] == b;
}

lemma SmallTransformedKey(x: Small)
  ensures x + 10 in (map y: Small | true :: y + 10 := y).Keys
{
}

lemma SubsetWitnesses() {
  var m := map x: Small | true :: x + 10 := x;
  SmallTransformedKey(0);
  SmallTransformedKey(1);
  assert m.Keys == {10, 11};
  assert m[10] == 0;
  assert m[11] == 1;
  var empty := map x: Empty | 0 <= x < 1 :: 0 := x;
  assert empty == map[];
}

// One order needs bounds discovery to reverse the binders. Each projection
// component must still refer to its corresponding binder after that reversal.
ghost opaque function DependentForward82(): (m: map<(int, int), int>)
  ensures m.Keys == {(0, 0), (1, 0), (1, 1)}
  ensures forall key: (int, int) | key in m.Keys :: m[key] == key.0 + key.1
{
  map i: int, j: int | 0 <= i < 2 && 0 <= j <= i :: (i, j) := i + j
}

ghost opaque function DependentReverse82(): (m: map<(int, int), int>)
  ensures m.Keys == {(0, 0), (1, 0), (1, 1)}
  ensures forall key: (int, int) | key in m.Keys :: m[key] == key.0 + key.1
{
  map j: int, i: int | 0 <= i < 2 && 0 <= j <= i :: (i, j) := i + j
}

lemma DependentBinders() {
  var forward := DependentForward82();
  var reverse := DependentReverse82();
  assert forward.Keys == {(0, 0), (1, 0), (1, 1)};
  assert forward[(1, 0)] == 1;
  assert forward[(1, 1)] == 2;
  assert forward == reverse;
}

lemma DuplicateTransformedKey(x: int)
  requires 0 <= x < 4
  ensures x % 2 in (map y: int | 0 <= y < 4 :: y % 2 := y % 2).Keys
{
}

lemma EqualValueDuplicateKeys() {
  var m := map i: int | 0 <= i < 4 :: i % 2 := i % 2;
  DuplicateTransformedKey(0);
  DuplicateTransformedKey(1);
  assert m.Keys == {0, 1};
  assert m[0] == 0;
  assert m[1] == 1;
}

lemma InfiniteTransformedKeys(n: int) {
  var m := imap x: int | true :: x + 1 := x;
  assert n + 1 in m;
  assert m[n + 1] == n;
}

class Cell {
  var value: int
}

ghost function HeapMap(c: Cell): map<int, int>
  reads c
{
  map x: int | x == c.value :: 0 := x
}

method HeapCaptures(c: Cell)
  modifies c
{
  label Before:
  ghost var before := HeapMap(c);
  var original := c.value;
  c.value := original + 1;
  assert before[0] == original;
  assert HeapMap(c)[0] == original + 1;
  assert old(HeapMap(c))[0] == original;
  assert old@Before(HeapMap(c))[0] == original;
}

ghost function ConditionalCapturedMap(b: bool): map<int, int> {
  var n := if b then 0 else 1;
  map x: int | x == n :: 0 := x
}

lemma ConditionalCaptures(b: bool) {
  assert ConditionalCapturedMap(b).Keys == {0};
  assert ConditionalCapturedMap(b)[0] == (if b then 0 else 1);
}
