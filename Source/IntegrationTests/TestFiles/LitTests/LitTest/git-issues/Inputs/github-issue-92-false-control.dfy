datatype Box<T> = Box(value:T)
type Positive = b:Box<int> | 0 < b.value witness Box(1)
lemma FalseControl() {
  var value:Positive := Box(1) as Positive;
  assert value.value == 1;
  assert false;
}
