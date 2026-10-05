// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

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
lemma GenericRoundTrip<A, B>(p: Pair<A, B>)
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
