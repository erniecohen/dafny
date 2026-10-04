datatype Payload = P(count: int, items: seq<int>)
predicate Valid(p: Payload) {
  p.count >= 0 && forall j :: 0 <= j < |p.items| ==> p.items[j] >= 0
}
datatype Wrapper = Wrap(value: Payload)
type Value = w: Wrapper | Valid(w.value) witness Wrap(P(0, []))
function Encode(b: Payload): Value
  requires Valid(b)
{ Wrap(b) as Value }
function Decode(v: Value): Payload { v.value }
lemma RoundTrip(b: Payload)
  requires Valid(b)
  ensures Decode(Encode(b)) == b
{}
lemma IntroductionFacts(b: Payload)
  requires Valid(b)
  ensures Valid(Decode(Encode(b)))
{}
lemma ConcreteWitness() { var v := Encode(P(0, [])); assert Decode(v) == P(0, []); }
method Work(n: nat) returns (checksum: int)
  ensures 2 * checksum == n * (n + 1) + 16 * n
{
  var seed: seq<int> := [0, 1, 2, 3, 4, 5, 6, 7];
  assert Valid(P(0, seed));
  var value := Encode(P(0, seed));
  var i := 0;
  checksum := 0;
  while i < n
    invariant 0 <= i <= n
    invariant Decode(value).count == i
    invariant Decode(value).items == seed
    invariant Valid(Decode(value))
    invariant 2 * checksum == i * (i + 1) + 16 * i
  {
    var prior := Decode(value);
    var candidate := P(prior.count + 1, prior.items);
    assert Valid(candidate);
    value := Encode(candidate);
    checksum := checksum + Decode(value).count + |Decode(value).items|;
    i := i + 1;
  }
}
method Main() {
  var warm := Work(2000);
  print "warmup:", warm, "\n";
  var result := Work(20000);
  print "result:", result, "\n";
}
