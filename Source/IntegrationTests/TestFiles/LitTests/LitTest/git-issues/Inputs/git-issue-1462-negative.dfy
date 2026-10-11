// Recursions that do not terminate: each would prove false if its decreases check passed.
class C {
  var s: set<int>
  function Rep(): set<int> reads this { s }

  // A call with the same heaps and receiver, whose measure is smaller in the current heap
  // than in the previous one.
  twostate lemma Bad()
    requires Rep() < old(Rep())
    ensures false
    decreases Rep()
  {
    Bad();
  }

  // The same, through a two-state function.
  twostate function F(): int
    requires Rep() < old(Rep())
    reads this
    ensures false
    decreases Rep(), 0
  {
    G(); 0
  }

  twostate lemma G()
    requires Rep() < old(Rep())
    ensures false
    decreases Rep(), 1
  {
    var x := F();
  }

  // A labeled call: the callee's measure is in its own previous heap, the heap at the label,
  // which is the current heap.  The call to UpFrom does not decrease, and neither does the
  // call back to Up.
  twostate lemma Up()
    requires old(Rep()) < Rep()
    ensures false
    decreases old(Rep())
  {
    label L:
    UpFrom@L();
  }

  twostate lemma UpFrom()
    ensures false
    decreases old(Rep()), 1
  {
    Up();
  }
}

method Exploit(c: C)
  requires c.s == {1}
  modifies c
  ensures false
{
  c.s := {};
  c.Bad();
}

method ExploitThroughFunction(c: C)
  requires c.s == {1}
  modifies c
  ensures false
{
  c.s := {};
  c.G();
}
