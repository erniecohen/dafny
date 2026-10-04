type Callable = int -> int
datatype Wrapper = Wrap(value: Callable)
type Value = Wrapper
function Encode(b: Callable): Value { Wrap(b) }
function Decode(v: Value): Callable { v.value }
function Identity<T>(x: T): T { x }
lemma RoundTrip(b: Callable) ensures Decode(Encode(b)) == b {}
lemma ConcreteWitness() { var b := (x: int) => x + 1; var v := Encode(b); assert Decode(v) == b; }
method Work(n: nat) returns (checksum: int)
  ensures 2 * checksum == n * (n + 1)
{
  var value := Encode((x: int) => x + 1);
  var i := 0;
  checksum := 0;
  while i < n
    invariant 0 <= i <= n
    invariant 2 * checksum == i * (i + 1)
  {
    var captured := i;
    value := Encode((x: int) => x + captured);
    checksum := checksum + (value.value)(1);
    i := i + 1;
  }
}
method Main() {
  var warm := Work(2000);
  print "warmup:", warm, "\n";
  var result := Work(20000);
  print "result:", result, "\n";
}
