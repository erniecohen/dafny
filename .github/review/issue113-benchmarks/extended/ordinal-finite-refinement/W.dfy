predicate Valid(o: ORDINAL) { o.IsNat && o.Offset % 2 == 0 }
datatype Wrapper = Wrap(value: ORDINAL)
type Value = w: Wrapper | Valid(w.value) witness Wrap(0)
function Encode(b: ORDINAL): Value
  requires Valid(b)
{ Wrap(b) as Value }
function Decode(v: Value): ORDINAL { v.value }
lemma RoundTrip(b: ORDINAL)
  requires Valid(b)
  ensures Decode(Encode(b)) == b
{}
lemma IntroductionFacts(b: ORDINAL)
  requires Valid(b)
  ensures Valid(Decode(Encode(b)))
{}
lemma ConcreteWitness() { var v := Encode(0); assert Decode(v) == 0; }
method Work(n: nat) returns (checksum: int)
  ensures checksum == n * (n + 1)
{
  var value := Encode(0);
  var i := 0;
  checksum := 0;
  while i < n
    invariant 0 <= i <= n
    invariant Decode(value).IsNat
    invariant Decode(value).Offset == 2 * i
    invariant Valid(Decode(value))
    invariant checksum == i * (i + 1)
  {
    var candidate := Decode(value) + 2;
    assert Valid(candidate);
    value := Encode(candidate);
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
