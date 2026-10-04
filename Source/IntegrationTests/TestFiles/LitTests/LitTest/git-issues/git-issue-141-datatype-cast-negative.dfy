// RUN: %exits-with 4 %verify --type-system-refresh --general-traits=datatype --general-newtypes --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
datatype Box<+T> = Box(value: T)
datatype List<+T> = Nil | Cons(head: T, tail: List<T>)
datatype Callback<+T> = Callback(run: () -> T)

lemma OriginalReproducer() {
  var source: Box<int> := Box(-1);
  var impossible := source as Box<nat>; // ERROR: original value lacks target membership
  assert impossible.value == -1;
  assert false;
}
lemma ArbitraryNarrowing(b: Box<int>) {
  var unchecked := b as Box<nat>; // ERROR: missing nonnegative field precondition
}
lemma RecursiveNegative() {
  var source: List<int> := Cons(1, Cons(-1, Nil));
  var unchecked := source as List<nat>; // ERROR: the recursive tail contains a negative value
}
lemma NestedNegative() {
  var source: Box<Box<int>> := Box(Box(-1));
  var unchecked := source as Box<Box<nat>>; // ERROR: nested field membership
}
lemma SequenceNegative() {
  var source: Box<seq<int>> := Box([1, -1]);
  var unchecked := source as Box<seq<nat>>; // ERROR: collection element membership
}
lemma TupleNegative() {
  var source: (nat, int) := (0, -1);
  var unchecked := source as (nat, nat); // ERROR: second tuple field membership
}
lemma ArrowNegative() {
  var f: () -> int := () => -1;
  var source: Callback<int> := Callback(f);
  var unchecked := source as Callback<nat>; // ERROR: function result membership
}
