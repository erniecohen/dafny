// RUN: %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Unexecuted draft: capture exact diagnostics before registration.

codatatype Stream<T> = Cons(head: T, tail: Stream<T>)
newtype Wrapped<T> = Stream<T> witness *
newtype Trivial<T> = s: Stream<T> | true witness *
newtype Bare<T(0)> = Stream<T>

function Repeat<T>(x: T): Wrapped<T> {
  Cons(x, Repeat(x) as Stream<T>) as Wrapped<T>
}

function RepeatOther<T>(x: T): Wrapped<T> {
  Cons(x, RepeatOther(x) as Stream<T>) as Wrapped<T>
}

function TrueRepeat<T>(x: T): Trivial<T> {
  Cons(x, TrueRepeat(x) as Stream<T>) as Trivial<T>
}

function {:abstemious} Copy<T>(s: Wrapped<T>): Wrapped<T> {
  Cons(s.head, Copy(s.tail as Wrapped<T>) as Stream<T>) as Wrapped<T>
}

function {:abstemious} MatchCopy<T>(s: Wrapped<T>): Wrapped<T> {
  match s case Cons(h, t) => Cons(h, MatchCopy(t as Wrapped<T>) as Stream<T>) as Wrapped<T>
}

function Whole(s: Wrapped<int>): Wrapped<int> {
  match s case Cons(0, _) => s case whole => whole
}

method Observations(s: Wrapped<int>) {
  assert s.Cons?;
  var h: int := s.head;
  var t: Stream<int> := s.tail;
  var Cons(h2, t2) := s;
  assert h == h2;
  assert t == t2;
  match s {
    case Cons(h3, t3) => assert h3 == h;
  }
}

lemma PrefixViews(s: Wrapped<int>, t: Wrapped<int>, k: nat)
  ensures (s ==#[k] t) <==> ((s as Stream<int>) ==#[k] (t as Stream<int>))
  ensures (s !=#[k] t) <==> ((s as Stream<int>) !=#[k] (t as Stream<int>))
{}

newtype Depth = ORDINAL witness 0
lemma OrdinalPrefixViews(s: Wrapped<int>, t: Wrapped<int>, k: Depth)
  ensures (s ==#[k] t) <==> ((s as Stream<int>) ==#[k] (t as Stream<int>))
{}

lemma FullViews(s: Wrapped<int>, t: Wrapped<int>)
  ensures (s == t) <==> ((s as Stream<int>) == (t as Stream<int>))
  ensures (s != t) <==> ((s as Stream<int>) != (t as Stream<int>))
{}

greatest lemma RepeatEqual(x: int)
  ensures Repeat(x) == RepeatOther(x)
{
  RepeatEqual(x);
}

codatatype Duo<A,B> = Duo(left: A, right: B, tail: Duo<A,B>)
newtype Flip<X,Y> = Duo<Y,X> witness *
newtype Deep<T> = Flip<seq<T>,bool> witness *

function Permuted<T>(xs: seq<T>): Deep<T> {
  (Duo(false, xs, (Permuted(xs) as Flip<seq<T>,bool>) as Duo<bool,seq<T>>)
    as Flip<seq<T>,bool>) as Deep<T>
}

function PairFields<T>(s: Deep<T>): (bool, seq<T>) {
  match s case Duo(b, xs, _) => (b, xs)
}

lemma GenericPrefix<T>(s: Deep<T>, t: Deep<T>, k: nat)
  ensures (s ==#[k] t) <==>
    (((s as Flip<seq<T>,bool>) as Duo<bool,seq<T>>) ==#[k]
      ((t as Flip<seq<T>,bool>) as Duo<bool,seq<T>>))
{}

// Legal acyclic nominal field path between separate codatatype families.
codatatype OuterStream = Outer(stream: Wrapped<int>, tail: OuterStream)
newtype WrappedOuter = OuterStream witness *
function OuterRepeat(x: int): WrappedOuter {
  Outer(Repeat(x), OuterRepeat(x) as OuterStream) as WrappedOuter
}
function NestedOuter(s: WrappedOuter): int {
  match s case Outer(Cons(h, _), _) => h
}
lemma OuterPrefix(s: WrappedOuter, t: WrappedOuter, k: nat)
  ensures (s ==#[k] t) <==> ((s as OuterStream) ==#[k] (t as OuterStream))
{}

newtype PositiveHead = s: Stream<int> | s.head > 0
  ghost witness Cons(1, Repeat(1) as Stream<int>)

method CheckedUpdate(s: PositiveHead) {
  var t: PositiveHead := s.(head := s.head + 1);
  assert t.head > 0;
  var raw: Stream<int> := (s as Stream<int>).(head := 0);
  assert raw.head == 0;
}
