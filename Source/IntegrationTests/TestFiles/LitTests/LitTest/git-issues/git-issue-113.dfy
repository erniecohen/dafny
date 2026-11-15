// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// General newtype value carriers preserve explicit identity conversions and base operations.
newtype Ord = ORDINAL
newtype F = f: (int -> int) | true witness (x: int) => x
newtype Partial = int --> int witness ((x: int) => x)
newtype General = int ~> int witness ((x: int) => x)

datatype List<T> = Nil | Node(head: T, tail: List<T>)
newtype ListView<T> = List<T> witness *
newtype Nonempty = s: List<int> | s.Node? witness Node(0, Nil)
newtype TupleView<T> = (T, int) witness *

codatatype Stream = More(head: int, tail: Stream)
newtype StreamView = Stream witness *
function Repeat(x: int): StreamView {
  More(x, Repeat(x) as Stream) as StreamView
}

lemma OrdinalIdentity(o: ORDINAL)
  ensures ((o as Ord) as ORDINAL) == o
{
  var n := o as Ord;
  assert n.IsNat == o.IsNat;
  assert n.IsLimit == o.IsLimit;
  assert n.IsSucc == o.IsSucc;
  assert n.Offset == o.Offset;
  if !o.IsNat {
    assert !(n as ORDINAL).IsNat;
    assert (n as ORDINAL) != o.Offset as ORDINAL;
  }
}

lemma TotalIdentity(f: int -> int, x: int)
  ensures (f as F)(x) == f(x)
  ensures ((f as F) as int -> int)(x) == f(x)
{}

lemma PartialIdentity(f: int --> int, x: int)
  requires f.requires(x)
  ensures (f as Partial).requires(x)
  ensures (f as Partial)(x) == f(x)
{}

ghost function GeneralIdentity(f: int ~> int, x: int): int
  requires f.requires(x)
  reads f.reads(x)
{
  (f as General)(x)
}

lemma GeneralFrame(f: int ~> int, x: int)
  requires f.requires(x)
  ensures (f as General).requires(x)
  ensures (f as General).reads(x) == f.reads(x)
{}

lemma ListIdentity<T>(s: List<T>)
  ensures ((s as ListView<T>) as List<T>) == s
{}

lemma TupleIdentity<T>(p: (T, int))
  ensures ((p as TupleView<T>) as (T, int)) == p
{}

lemma BaseDestructor(s: List<int>)
  requires s.Node?
{
  var n := s as Nonempty;
  var tail: List<int> := n.tail;
  assert tail == s.tail;
  assert n.head == s.head;
}

lemma FiniteObservation(x: int)
  ensures Repeat(x).head == x
  ensures Repeat(x).tail.head == x
{}

lemma PrefixIdentity(s: StreamView, t: StreamView, k: nat)
  ensures (s ==#[k] t) <==> ((s as Stream) ==#[k] (t as Stream))
  ensures (s !=#[k] t) <==> ((s as Stream) !=#[k] (t as Stream))
{}

method IssueExample() {
  var f := ((x: int) => x + 1) as F;
  assert f(4) == 5;
}
