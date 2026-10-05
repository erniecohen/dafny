trait Tag {}
class Token extends Tag {}
type Source = Tag -> seq<Token>
type Target = Token -> seq<Tag>
// An explicit base adapter is recreated at the same point in every arm.
function Coerce(source: Source): Target { (token: Token) => source(token) }
lemma CoercionLaw(source: Source, token: Token)
  ensures |Coerce(source)(token)| == |source(token)|
  ensures forall j :: 0 <= j < |source(token)| ==> Coerce(source)(token)[j] == source(token)[j]
{}
newtype Value = Target
function Encode(b: Target): Value { b as Value }
function Decode(v: Value): Target { v as Target }
lemma RoundTrip(b: Target) ensures Decode(Encode(b)) == b {}
lemma ConcreteWitness(token: Token) {
  var source: Source := (t: Tag) => [token];
  var v := Encode(Coerce(source));
  assert Decode(v)(token) == [token];
}
method Work(n: nat) returns (checksum: int)
  ensures checksum == n
{
  var token := new Token;
  var source: Source := (t: Tag) => [token];
  var i := 0;
  checksum := 0;
  while i < n
    invariant 0 <= i <= n
    invariant checksum == i
    invariant forall t: Tag :: source(t) == [token]
  {
    var coerced: Target := Coerce(source);
    var value := Encode(coerced);
    var result := Decode(value)(token);
    assert |result| == 1 && result[0] == token;
    checksum := checksum + |result|;
    i := i + 1;
  }
}
method Main() {
  var warm := Work(2000);
  print "warmup:", warm, "\n";
  var result := Work(20000);
  print "result:", result, "\n";
}
