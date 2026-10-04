// RUN: %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Draft expectations must be captured from the patched compiler before registration.

datatype L = Nil | Cons(head: int, tail: L)
newtype NonEmpty = xs: L | xs.Cons? witness Cons(1, Nil)

datatype Pair<A,B> = Pair(left: A, right: B)
newtype Flip<X,Y> = p: Pair<Y,X> | true witness *
newtype Deep<T> = p: Flip<seq<T>,bool> | true witness *
datatype GenericContainer<X,Y> = GC(pair: Flip<X,Y>)
newtype WrappedGeneric<X,Y> = GenericContainer<X,Y> witness *

datatype Cell = Cell(value: int)
newtype PositiveCell = c: Cell | c.value > 0 witness Cell(1)
datatype Container = Container(cell: PositiveCell, count: int)
newtype WrappedContainer = Container witness Container(Cell(1) as PositiveCell, 0)

newtype PositiveTuple = p: (bool, int) | p.1 > 0 witness (false, 1)
newtype GhostTuple = p: (ghost int, bool) | true witness (ghost 1, false)

method Observe(n: NonEmpty, d: Deep<int>) {
  assert n.Cons?;
  var tail: L := n.tail;
  var left: bool := d.left;
  var right: seq<int> := d.right;
  var h := match n case Nil => 0 case Cons(x, xs) => x;
  assert h == n.head;
  match n {
    case Nil => assert false;
    case Cons(x, xs) => assert xs == n.tail;
  }
  var Cons(x, xs) := n;
  assert xs == n.tail;
}

function Whole(n: PositiveCell): PositiveCell {
  match n
  case Cell(0) => n
  case whole => whole
}

function WholeOnly(n: PositiveCell): PositiveCell {
  match n case whole => whole
}

function Nested(c: WrappedContainer): int {
  match c case Container(Cell(v), count) => v + count
}

function NestedLet(c: WrappedContainer): int {
  var Container(Cell(v), count) := c;
  v + count
}

function NestedPermutation<T>(g: WrappedGeneric<seq<T>, bool>): (bool, seq<T>) {
  match g case GC(Pair(b, s)) => (b, s)
}

function NestedPermutationLet<T>(g: WrappedGeneric<seq<T>, bool>): (bool, seq<T>) {
  var GC(Pair(b, s)) := g;
  (b, s)
}

method NestedStatement(c: WrappedContainer) returns (value: int) {
  match c {
    case Container(Cell(v), count) => value := v + count;
  }
  var Container(Cell(v), count) := c;
  assert value == v + count;
}

method TupleObservation(t: PositiveTuple, g: GhostTuple) {
  var flag: bool := t.0;
  var value: int := t.1;
  var (b, i) := t;
  assert b == t.0 && i == t.1;
  ghost var first: int := g.0;
  var second: bool := g.1;
}

datatype R = R(value: int, ghost proof: int)
newtype Positive = r: R | r.value > 0 witness R(1, 0)
newtype ZeroProof = r: R | r.proof == 0 ghost witness R(1, 0)

method ValidUpdate(p: Positive) {
  var q: Positive := p.(value := p.value + 1);
  assert q.value > 0;
  var b: R := (p as R).(value := 0);
  assert b.value == 0;
  var constructed := R(1, 0) as Positive;
  assert constructed.value == 1;
}

lemma ValidGhostUpdate(p: ZeroProof) {
  var q: ZeroProof := p.(proof := 0);
  assert q.proof == 0;
}

class C {}
datatype RefBox = RefBox(value: C)
newtype NR = RefBox witness *

method ReferencePreserved(c: C) {
  var n := RefBox(c) as NR;
  assert n.value == c;
}
