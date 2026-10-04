type Pair = (int, int)
datatype Wrapper = Wrap(value: Pair)
type Value = Wrapper
function Encode(b: Pair): Value { Wrap(b) }
function Decode(v: Value): Pair { v.value }
function Identity<T>(x: T): T { x }
lemma RoundTrip(b: Pair) ensures Decode(Encode(b)) == b {}
lemma ConcreteWitness() { var b := (0, 7); var v := Encode(b); assert Decode(v) == b; }
method Work(n: nat) returns (checksum: int)
  ensures 2 * checksum == n * (n + 1) + 14 * n
{
  var value := Encode((0, 7));
  var i := 0;
  checksum := 0;
  while i < n
    invariant 0 <= i <= n
    invariant Decode(value).0 == i && Decode(value).1 == 7
    invariant 2 * checksum == i * (i + 1) + 14 * i
  {
    value := Encode((value.value.0 + 1, value.value.1));
    checksum := checksum + value.value.0 + value.value.1;
    i := i + 1;
  }
}
method Main() {
  var warm := Work(2000);
  print "warmup:", warm, "\n";
  var result := Work(20000);
  print "result:", result, "\n";
}
