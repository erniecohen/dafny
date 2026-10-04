// Only finite ORDINAL values are constructed or observed by compiled methods.
newtype Value = ORDINAL
function Encode(b: ORDINAL): Value { b as Value }
function Decode(v: Value): ORDINAL { v as ORDINAL }
lemma RoundTrip(b: ORDINAL) ensures Decode(Encode(b)) == b {}
lemma ConcreteWitness() { var v := Encode(0); assert Decode(v) == 0; }
method Work(n: nat) returns (checksum: int)
  ensures 2 * checksum == n * (n + 1)
{
  var value := Encode(0);
  var i := 0;
  checksum := 0;
  while i < n
    invariant 0 <= i <= n
    invariant Decode(value).IsNat
    invariant Decode(value).Offset == i
    invariant 2 * checksum == i * (i + 1)
  {
    value := Encode(Decode(value) + 1);
    checksum := checksum + Decode(value).Offset;
    i := i + 1;
  }
}
method Main() {
  var warm := Work(2000);
  print "warmup:", warm, "\n";
  var result := Work(20000);
  print "result:", result, "\n";
}
