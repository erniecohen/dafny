class AllocationCell82 {
  var count: nat
  constructor(n: nat)
    ensures count == n
  {
    count := n;
  }
}

ghost function NonnegativeRange82(n: int): set<int>
  requires 0 <= n
{
  set i: int | 0 <= i < n
}

method LambdaFormalFactsRemainConsistent() {
  ghost var f := (q: AllocationCell82) reads q => NonnegativeRange82(q.count);
  var c := new AllocationCell82(1);
  assert fresh(c);
  assert c in f.reads(c);
  assert f(c) == {0};
  assert false; // intended sole failing obligation
}
