// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
datatype Box<+T> = Box(value: T)
datatype List<+T> = Nil | Cons(head: T, tail: List<T>)
datatype Callback<+T> = Callback(run: () -> T)

lemma Identity<T>(b: Box<T>) {
  var same := b as Box<T>;
  assert same.value == b.value;
}
lemma NarrowBox(b: Box<int>)
  requires 0 <= b.value
{
  var narrowed := b as Box<nat>;
  assert narrowed.value == b.value;
}
lemma ValidUpcast(b: Box<nat>) {
  var widened := b as Box<int>;
  assert widened.value == b.value;
  var returned := widened as Box<nat>;
  assert returned.value == b.value;
}
lemma NestedBox(b: Box<Box<int>>)
  requires 0 <= b.value.value
{
  var narrowed := b as Box<Box<nat>>;
  assert narrowed.value.value == b.value.value;
}
lemma SequenceField(b: Box<seq<int>>)
  requires forall i :: 0 <= i < |b.value| ==> 0 <= b.value[i]
{
  var narrowed := b as Box<seq<nat>>;
  assert narrowed.value == b.value;
}

predicate Valid(xs: List<int>) {
  match xs
  case Nil => true
  case Cons(h, t) => 0 <= h && Valid(t)
}
lemma ListMembership(xs: List<int>)
  requires Valid(xs)
  ensures xs is List<nat>
  decreases xs
{
  match xs
  case Nil =>
  case Cons(h, t) => ListMembership(t);
}
lemma NarrowList(xs: List<int>)
  requires Valid(xs)
{
  ListMembership(xs);
  var narrowed := xs as List<nat>;
  assert narrowed.Nil? == xs.Nil?;
  if xs.Cons? {
    assert narrowed.head == xs.head;
  }
}
lemma LeftTuple(p: (int, nat))
  requires 0 <= p.0
{
  var narrowed := p as (nat, nat);
  assert narrowed.0 == p.0 && narrowed.1 == p.1;
}
lemma RightTuple(p: (nat, int))
  requires 0 <= p.1
{
  var narrowed := p as (nat, nat);
  assert narrowed.0 == p.0 && narrowed.1 == p.1;
}
lemma ArrowFieldRoundTrip(c: Callback<nat>) {
  var widened := c as Callback<int>;
  var returned := widened as Callback<nat>;
  assert returned.run == c.run;
}
