datatype Leaf = Leaf(value: int, ghost stamp: int)
datatype Envelope = Envelope(leaf: Leaf, tag: int)
newtype Value = x: Envelope | x.tag >= 0 && x.leaf.value >= 0 witness Envelope(Leaf(0, 0), 0)
function Encode(b: Envelope): Value
  requires b.tag >= 0 && b.leaf.value >= 0
{ b as Value }
function Decode(v: Value): Envelope { v as Envelope }
lemma RoundTrip(b: Envelope)
  requires b.tag >= 0 && b.leaf.value >= 0
  ensures Decode(Encode(b)) == b
{}
method Work(n: nat) returns (checksum: int)
  ensures 2 * checksum == n * (n + 1)
{
  var value := Encode(Envelope(Leaf(0, 0), 0));
  var i := 0;
  checksum := 0;
  while i < n
    invariant 0 <= i <= n
    invariant 2 * checksum == i * (i + 1)
    invariant Decode(value).tag == i && Decode(value).leaf.value == 0
  {
    var v := value;
    var u := v.(tag := i + 1);
    value := u as Value;
    checksum := checksum + Decode(value).tag;
    i := i + 1;
  }
}
method Main() {
  var warm := Work(2000);
  print "warmup:", warm, "\n";
  var result := Work(20000);
  print "result:", result, "\n";
}
