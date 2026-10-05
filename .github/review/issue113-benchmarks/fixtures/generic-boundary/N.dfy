type Pair = (int, int)
newtype Value = Pair
function Encode(b: Pair): Value { b as Value }
function Decode(v: Value): Pair { v as Pair }
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
    value := Encode((value.0 + 1, value.1));
    var entries: seq<Value> := [value];
    var table: map<int, Value> := map[0 := entries[0]];
    value := Identity<Value>(table[0]);
    checksum := checksum + value.0 + value.1;
    i := i + 1;
  }
}
method Main() {
  var warm := Work(2000);
  print "warmup:", warm, "\n";
  var result := Work(20000);
  print "result:", result, "\n";
}
