datatype Tree = Tip | Fork(left: Tree, right: Tree)
function Pow2(k: nat): nat { if k == 0 then 1 else 2 * Pow2(k - 1) }
function Count(t: Tree): nat { match t case Tip => 1 case Fork(a, b) => Count(a) + Count(b) }
function Build(k: nat): Tree
  ensures Count(Build(k)) == Pow2(k)
{ if k == 0 then Tip else Fork(Build(k - 1), Build(k - 1)) }
newtype Value = Tree
function Encode(b: Tree): Value { b as Value }
function Decode(v: Value): Tree { v as Tree }
lemma RoundTrip(b: Tree) ensures Decode(Encode(b)) == b {}
function Observe(v: Value): nat
  ensures Observe(v) == Count(Decode(v))
{ match v case Tip => 1 case Fork(a, b) => Count(a) + Count(b) }
method Work(n: nat) returns (checksum: int)
  ensures checksum == 32 * n
{
  var i := 0;
  checksum := 0;
  while i < n
    invariant 0 <= i <= n
    invariant checksum == 32 * i
  {
    var value := Encode(Build(5));
    checksum := checksum + Observe(value);
    i := i + 1;
  }
}
method Main() {
  var warm := Work(2000);
  print "warmup:", warm, "\n";
  var result := Work(20000);
  print "result:", result, "\n";
}
