datatype Box<T> = Box(value:T)
type Positive = b:Box<int> | 0 < b.value witness Box(1)
type AtLeastTwo = b:Positive | 2 <= b.value witness Box(2)
type NatBox = b:Box<nat> | true witness Box(0)
method Main() {
  var first := Box(1) as Positive;
  var nested := Box(2) as AtLeastTwo;
  var generic := Box<int>.Box(3) as NatBox;
  print first.value, " ", nested.value, " ", generic.value, "\n";
}
