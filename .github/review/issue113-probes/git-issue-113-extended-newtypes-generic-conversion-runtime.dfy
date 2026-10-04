// Generic conversion control: universal contracts plus executable int/bool instantiation.
// Intended flags: --type-system-refresh=true --general-newtypes --extended-newtype-bases

datatype Pair<A(==), B(==)> = Pair(left: A, right: B)
newtype Value<A(==), B(==)> = Pair<A, B> witness *
function Decode<A(==), B(==)>(v: Value<A, B>): Pair<A, B> { v as Pair<A, B> }
method Wrap<A(==), B(==)>(p: Pair<A, B>) returns (v: Value<A, B>)
  ensures Decode(v) == p
{
  v := p as Value<A, B>;
}
method Unwrap<A(==), B(==)>(v: Value<A, B>) returns (p: Pair<A, B>)
  ensures p == Decode(v)
{
  return v as Pair<A, B>;
}
method Copy<A(==), B(==)>(v: Value<A, B>) returns (w: Value<A, B>)
  ensures Decode(w) == Decode(v)
{
  var base: Pair<A, B> := v as Pair<A, B>;
  w := base as Value<A, B>;
}
lemma GenericRoundTrip<A(==), B(==)>(p: Pair<A, B>)
  ensures ((p as Value<A, B>) as Pair<A, B>) == p
{}
method Main() {
  var original := Pair(17, true);
  var wrapped := Wrap(original);
  var copied := Copy(wrapped);
  var result := Unwrap(copied);
  expect result == original;
  print result.left, ",", result.right, "\n";
}
