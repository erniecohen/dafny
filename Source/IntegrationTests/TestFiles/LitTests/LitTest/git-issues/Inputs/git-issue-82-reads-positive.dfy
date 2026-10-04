// Finite reads values must be available through .reads without invoking the function.
ghost function Empty(n: int): int
  requires false
{
  0
}

ghost function One(o: object?, n: int): int
  requires 0 < n
  reads o
{
  0
}

ghost function FiniteCollections(a: object?, b: object?, c: object?): int
  reads {a}, [b], multiset{c}
{
  0
}

ghost function InfiniteCollection(s: iset<object?>): int
  reads s
{
  0
}

ghost function All(): int
  reads *
{
  0
}

ghost function Generic<T(!new)>(x: T, o: object?): int
  reads set q: object? | q == o :: q
{
  0
}

lemma NamedReads<T(!new)>(x: T, a: object?, b: object?, c: object?, s: iset<object?>)
{
  assert Empty.reads(0) == {};
  assert !One.requires(a, 0);
  assert One.reads(a, 1) == {a};
  assert FiniteCollections.reads(a, b, c) == {a, b, c};
  assert Generic.reads(x, a) == {a};
  assert forall q: object? :: q in InfiniteCollection.reads(s) <==> q in s;
}

lemma LambdaReads(a: object?, b: object?, s: iset<object?>)
{
  var empty := (x: int) requires false => 0;
  assert empty.reads(0) == {};
  var one := (x: int) requires 0 < x reads a => x;
  assert !one.requires(0);
  assert one.reads(1) == {a};
  var finite := (x: int) reads set q: object? | q == a || q == b :: q => x;
  assert finite.reads(0) == {a, b};
  var captured := (x: int) reads s => x;
  assert forall q: object? :: q in captured.reads(0) <==> q in s;
  var argument := (s: iset<object?>) reads s => 0;
  assert forall q: object? :: q in argument.reads(s) <==> q in s;
}

lemma WildcardReads(o: object)
{
  assert o in All.reads();
  var all := (x: int) reads * => x;
  assert o in all.reads(0);
}

class ReadCell {
  var objects: set<object?>
  var value: int

  ghost function Read(): int
    reads this, objects
  {
    value
  }
}

twostate function Previous(c: ReadCell): int
  reads old(c.objects)
{
  0
}

method ChangedHeap(c: ReadCell, o: object?)
  modifies c
{
  ghost var named := c.Read.reads();
  ghost var f := (x: int) reads c, c.objects => x;
  ghost var lambda := f.reads(0);
  label Before:
  c.objects := {o};
  assert c.Read.reads() == {c, o};
  assert Previous.reads(c) == old(c.objects);
  assert f.reads(0) == {c, o};
  assert named == {c} + old(c.objects);
  assert lambda == {c} + old(c.objects);
  assert old(c.Read.reads()) == {c} + old(c.objects);
  assert old@Before(c.Read.reads()) == {c} + old@Before(c.objects);
  assert old@Before(f.reads(0)) == {c} + old@Before(c.objects);
}
