predicate P(x: int)
const Stored := (x: int) requires P(x) => x

lemma StoredDomain(x: int)
  ensures Stored.requires(x) ==> P(x)
{}

lemma Forward(x: int)
  ensures P(x) ==> Stored.requires(x)
{}

lemma Local(x: int) {
  var f := (y: int) requires y > 0 => y;
  assert f.requires(x) <==> x > 0;
}

lemma Multiple(x: int, y: int) {
  var f := (a: int, b: int) requires a < b => a;
  assert f.requires(x, y) <==> x < y;
}

lemma Nullary(b: bool) {
  var f := () requires b => 0;
  assert f.requires() <==> b;
}

lemma EmptyDomain() {
  var f := (x: int) requires false => x / 0;
  assert !f.requires(0);
}

function Returned(offset: int): int --> int {
  (x: int) requires x > offset => x
}

lemma ReturnedDomain(offset: int, x: int)
  ensures Returned(offset).requires(x) <==> x > offset
{}

function Generic<T(==)>(value: T): T --> T {
  (x: T) requires x == value => x
}

lemma GenericDomain<T>(value: T, x: T)
  ensures Generic(value).requires(x) <==> x == value
{}

lemma Nested(x: int, y: int) {
  var outer := (a: int) => (b: int) requires b > a => b;
  assert outer(x).requires(y) <==> y > x;
}

lemma Subset(x: nat) {
  var f := (y: nat) requires y > 0 => y;
  assert f.requires(x) <==> x > 0;
}

class Cell {
  var n: int
}

method HeapDomain(c: Cell, x: int)
  modifies c
{
  var f := (y: int) requires y > c.n reads c => y;
  assert f.requires(x) <==> x > c.n;
  c.n := x;
  assert !f.requires(x);
  assert f.requires(x + 1);
}

twostate lemma PreviousDomain(c: Cell, x: int) {
  var f := (y: int) requires y > old(c.n) => y;
  assert f.requires(x) <==> x > old(c.n);
}

method LabelledDomain(c: Cell)
  modifies c
{
  var n := c.n;
  label Here:
  var f := (x: int) requires x > old@Here(c.n) => x;
  c.n := n + 10;
  assert f.requires(n + 1);
  assert !f.requires(n);
}

predicate Partial(x: int)
  requires x != 0
{ true }

lemma GuardedRange() {
  var f := (x: int) requires x != 0 && Partial(x) => x;
  assert f.requires(1);
  assert !f.requires(0);
}
