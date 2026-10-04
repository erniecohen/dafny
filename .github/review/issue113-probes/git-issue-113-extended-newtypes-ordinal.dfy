// RUN: %verify --type-system-refresh true --general-newtypes true --extended-newtype-bases true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Ord = ORDINAL
type OrdAlias = Ord
newtype OrdLayer = OrdAlias
newtype FiniteOrd = o: ORDINAL | o.IsNat witness 0
newtype PositiveOrd = o: ORDINAL | 0 < o witness 1

lemma RoundTrip(o: ORDINAL)
  ensures ((o as Ord) as ORDINAL) == o
{
  var n := o as Ord;
  assert n.IsNat == o.IsNat;
  assert n.IsLimit == o.IsLimit;
  assert n.IsSucc == o.IsSucc;
  assert n.Offset == o.Offset;
  assert ((n as OrdLayer) as ORDINAL) == o;
  if !o.IsNat {
    assert !(n as ORDINAL).IsNat;
    assert (n as ORDINAL) != o.Offset as ORDINAL;
  }
}

lemma Backward(n: Ord)
  ensures ((n as ORDINAL) as Ord) == n
{
}

lemma FiniteCast(o: ORDINAL)
  requires o.IsNat
  ensures ((o as FiniteOrd) as int) == o.Offset
{
}

lemma IntegerToOrdinal(i: int)
  requires 0 <= i
  ensures ((i as Ord) as int) == i
{
}

lemma LiteralAndOperations()
{
  var x: Ord := 5;
  var y: Ord := 3;
  assert (x as ORDINAL).IsNat;
  assert x.Offset == 5;
  assert ((x + y) as ORDINAL) == 8;
  assert ((x - y) as ORDINAL) == 2;
  assert y < x;
  var z: Ord;
  // With strict definite assignment, do not read z here: auto-initializability
  // is a compiler property, not permission to read an unassigned local.
}

function Subtract(x: Ord, y: Ord): Ord
  requires y.IsNat
  requires y.Offset <= x.Offset
{
  x - y
}

codatatype Stream = More(head: int, tail: Stream)

lemma PrefixLimit(s: Stream, t: Stream, k: Ord)
  requires s == t
  ensures s ==#[k] t
{
}
