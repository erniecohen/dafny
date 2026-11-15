// Both contracts are valid for every n. Their comprehension definitions must
// remain usable without making the common function theory contradictory.
opaque ghost function OpaqueSuccessor(n: int): (s: set<int>)
  ensures s == (set i: int | i == n :: i + 1)
{
  {n + 1}
}

opaque ghost function OpaqueConstantKey(n: int): (m: map<int, int>)
  ensures m == (map i: int | i == n :: 0 := i)
{
  map[0 := n]
}

lemma CommonContractsRemainConsistent(n: int) {
  var s := OpaqueSuccessor(n);
  assert n + 1 in s;
  assert n !in s;
  var m := OpaqueConstantKey(n);
  assert m[0] == n;
  var next := OpaqueConstantKey(n + 1);
  assert next[0] == n + 1;
  assert false; // intended sole failing obligation
}
