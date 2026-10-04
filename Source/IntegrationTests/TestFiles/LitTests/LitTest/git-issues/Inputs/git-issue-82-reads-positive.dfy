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

// A function-valued clause generates an _reads finite comprehension over all int inputs.
ghost function ReadFamily(f: int -> set<object?>): int
  reads f
{
  0
}

lemma GeneratedFamilyReads(s: set<object?>)
{
  var family: int -> set<object?> := (x: int) => s;
  assert ReadFamily.reads(family) == s;
  var outer := (x: int) reads family => x;
  assert outer.reads(0) == s;
}

lemma NamedReads<T(!new)>(x: T, a: object?, b: object?, c: object?, s: iset<object?>)
{
  assert Empty.reads(0) == {};
  assert !One.requires(a, 0);
  assert One.reads(a, 1) == {a};
  assert [b][0] == b;
  assert FiniteCollections.reads(a, b, c) == {a, b, c};
  assert Generic.reads(x, a) == {a};
  assert forall q: object? :: q in InfiniteCollection.reads(s) <==> q in s;
}

lemma LambdaReads(a: object?, b: object?, s: iset<object?>)
{
  var empty := (x: int) requires false => 0;
  assert empty.reads(0) == {};
  var one := (x: int) requires 0 < x reads a => x;
  // The one-way lambda .requires encoding is exercised in a separate negative.
  assert one.reads(1) == {a};
  var finite := (x: int) reads set q: object? | q in {a, b} :: q => x;
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

  ghost function ReadInput(n: int): int
    reads this, objects
  {
    n
  }
}

// The generated union is over infinitely many inputs, with references in one heap.
ghost function ReadAll(c: ReadCell): int
  requires forall n: int :: c.ReadInput.requires(n)
  reads c, c.ReadInput.reads
{
  0
}

twostate function Previous(c: ReadCell, n: int): int
  requires old(c.value) < n
  reads c, c.objects
{
  0
}

method ChangedHeap(c: ReadCell, o: object?)
  modifies c
{
  ghost var named := c.Read.reads();
  ghost var previous := Previous;
  ghost var previousValue := c.value;
  ghost var f := (x: int) reads c, c.objects => x;
  ghost var lambda := f.reads(0);
  assert forall n: int :: c.ReadInput.requires(n);
  assert forall n: int :: c.ReadInput.reads(n) == {c} + c.objects;
  assert forall n: int, q: object? :: q in c.ReadInput.reads(n) ==> allocated(q);
  ghost var allNamed := ReadAll.reads(c);
  ghost var allLambda := (x: int) reads c, c.ReadInput.reads => x;
  ghost var allLambdaReads := allLambda.reads(0);
  label Before:
  c.objects := {o};
  c.value := c.value + 2;
  assert c.Read.reads() == {c, o};
  assert previous.requires(c, previousValue + 1);
  assert !previous.requires(c, previousValue);
  assert previous.reads(c, previousValue + 1) == {c, o};
  assert f.reads(0) == {c, o};
  assert forall n: int :: c.ReadInput.requires(n);
  assert forall n: int :: c.ReadInput.reads(n) == {c} + c.objects;
  assert forall n: int, q: object? :: q in c.ReadInput.reads(n) ==> allocated(q);
  assert ReadAll.reads(c) == {c, o};
  assert allLambda.reads(0) == {c, o};
  assert allNamed == {c} + old(c.objects);
  assert allLambdaReads == {c} + old(c.objects);
  assert named == {c} + old(c.objects);
  assert lambda == {c} + old(c.objects);
  assert old(c.Read.reads()) == {c} + old(c.objects);
  assert old@Before(c.Read.reads()) == {c} + old@Before(c.objects);
  assert old@Before(f.reads(0)) == {c} + old@Before(c.objects);
  assert old(ReadAll.reads(c)) == {c} + old(c.objects);
  assert old@Before(allLambda.reads(0)) == {c} + old@Before(c.objects);
}

// A returned pure lambda retains the previous heap used only in its body.
class OldOnlyReadCell {
  var value: int
}

twostate function OldOnlyReads(c: OldOnlyReadCell): int -> int {
  (x: int) => old(c.value) + x
}

method OldOnlyReturnedLambda(c: OldOnlyReadCell)
  modifies c
{
  label Before:
  var previous := c.value;
  c.value := previous + 1;
  ghost var f := OldOnlyReads@Before(c);
  assert f(0) == previous;
  assert f(1) == previous + 1;
}

// A pure function returns a generic callback that calls another pure function.
function ReturnedPureValue<T>(value: T): int -> T {
  (n: int) => value
}

function ReturnedPureCallback<R, U>(mapping: R -> U): (R, int) -> (int -> U) {
  (value: R, remaining: int) => ReturnedPureValue(mapping(value))
}

lemma ReturnedGenericCallback<R, U>(mapping: R -> U, value: R) {
  var callback := ReturnedPureCallback(mapping);
  assert callback(value, 0).reads(0) == {};
  assert callback(value, 0)(0) == mapping(value);
}
