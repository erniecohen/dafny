// Recursive two-state lemmas and functions whose decreases clauses depend on the heap.
trait Ob {
  function rep(): set<Ob> reads this
  twostate lemma l0() decreases rep() {
    if o :| o in rep() && old(allocated(o)) && o.rep() < rep() {
      o.l0();
    }
  }
}

class C {
  var s: set<int>
  function Rep(): set<int> reads this { s }

  // The measure is the previous heap's, and a labeled call starts a callee whose previous
  // heap is the current one.
  twostate lemma Down()
    decreases old(Rep())
  {
    if Rep() < old(Rep()) {
      label L:
      Down@L();
    }
  }

  // Mixed current and previous measures.
  twostate lemma Mixed(n: nat)
    decreases Rep(), n
  {
    if 0 < n {
      Mixed(n - 1);
    }
  }

  twostate function F(n: nat): int
    reads this
    decreases Rep(), n
  {
    if n == 0 then 0 else F(n - 1)
  }

  twostate function Shrink(d: C): int
    reads this, d
    decreases Rep()
  {
    if d.Rep() < Rep() then d.Shrink(d) else 0
  }

  twostate lemma CallsFunction()
    decreases Rep(), 1
  {
    var x := G();
  }

  twostate function G(): int
    reads this
    decreases Rep(), 0
  {
    0
  }
}

method Use(c: C, n: nat)
  modifies c
{
  c.s := {};
  c.Down();
  c.Mixed(n);
  var x := c.F(n);
}
