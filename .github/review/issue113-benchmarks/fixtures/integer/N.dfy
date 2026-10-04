newtype Value = int
function Encode(b: int): Value { b as Value }
function Decode(v: Value): int { v as int }
function Identity<T>(x: T): T { x }
lemma RoundTrip(b: int) ensures Decode(Encode(b)) == b {}
lemma ConcreteWitness() { var b := 0; var v := Encode(b); assert Decode(v) == b; }
method Work(n: nat) returns (checksum: int)
  ensures 2 * checksum == n * (n + 1)
{
  var value := Encode(0);
  var i := 0;
  checksum := 0;
  while i < n
    invariant 0 <= i <= n
    invariant Decode(value) == i
    invariant 2 * checksum == i * (i + 1)
  {
    value := Encode(Decode(value) + 1);
    checksum := checksum + Decode(value);
    i := i + 1;
  }
}
method Main() {
  var warm := Work(2000);
  print "warmup:", warm, "\n";
  var result := Work(20000);
  print "result:", result, "\n";
}
