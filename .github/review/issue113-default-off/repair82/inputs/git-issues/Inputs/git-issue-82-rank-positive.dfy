// The rank inequality remains legal with a genuinely finite witness bound.
datatype Leaf = Leaf(i: int)
datatype Container = Container(children: iset<Leaf>)

ghost function InfiniteChildren(): iset<Leaf> { iset i: int | true :: Leaf(i) }

lemma InfiniteLowerRanks(i: int)
  ensures Leaf(i) < Container(InfiniteChildren())
{
  var children := InfiniteChildren();
  assert Leaf(i) in children;
  assert Leaf(i) < Container(children);
}

lemma ExplicitFiniteBounds() {
  var c := Container(InfiniteChildren());
  InfiniteLowerRanks(0);
  InfiniteLowerRanks(1);
  var s := set d: Leaf | d in {Leaf(0), Leaf(1)} && d < c;
  assert s == {Leaf(0), Leaf(1)};
  var m := map d: Leaf | d in {Leaf(0), Leaf(1)} && d < c :: d.i;
  assert m.Keys == s;
  assert m[Leaf(0)] == 0;
  assert m[Leaf(1)] == 1;
}

lemma FiniteImageAndInfiniteResults() {
  var c := Container(InfiniteChildren());
  InfiniteLowerRanks(0);
  InfiniteLowerRanks(1);
  var s := set d: Leaf | d < c :: d.i == 0;
  forall b: bool
    ensures b in s
  {
    var sourceValue := Leaf(if b then 0 else 1);
    InfiniteLowerRanks(sourceValue.i);
    assert (sourceValue.i == 0) == b;
  }
  assert s == {false, true};
  var inf := iset d: Leaf | d < c;
  assert Leaf(0) in inf;
  assert Leaf(1) in inf;
  var im := imap d: Leaf | d < c :: d.i;
  assert im[Leaf(0)] == 0;
  assert im[Leaf(1)] == 1;
}
