// Controls for github-issue-211-integrate.dfy, which is verified with this file: an assert false in each positive
// context, and bounds that do not go down.

module Controls {
  import opened MultisetOrders
  import L = Levels
  import opened Funcs
  import Impl
  import Client

  // Positive contexts: each assert false is reported
  method SumApplyControl(s: Impl.Sum, x: int) returns (r: int)
    requires (Part(L.Apply, L.CreateSum, s.f.depth, s.g.depth); s.Valid())
  {
    r := s.Apply(x);
    assert false; // error: control
  }

  method IntegrateControl(f: RealFunc) returns (r: int)
    requires (Lower(L.Integrate, L.Apply, f.depth); f.Valid())
    decreases {:global} multiset{L.Integrate} + f.depth by Bags()
  {
    Lower(L.Integrate, L.IntegrateIter, f.depth);
    r := Client.IntegrateIter(f, 0, 3, 0);
    assert false; // error: control
  }

  method MainControl()
    decreases {:global} multiset{L.Main} by Bags()
  {
    Lower(L.Main, L.Apply, multiset{});
    assert multiset{L.Main} + multiset{} == multiset{L.Main} && multiset{L.Apply} + multiset{} == multiset{L.Apply};
    var f := new Impl.LinearFunc(2, 1);
    Lower(L.Main, L.Integrate, multiset{});
    assert multiset{L.Integrate} + multiset{} == multiset{L.Integrate} + f.depth;
    var r := Client.Integrate(f, 0, 10);
    assert false; // error: control
  }
}

// A sum that does not add {createSum} to the depths of its summands: when g's depth is empty, f.apply runs at
// the caller's own bound
module NoCreateSum {
  import opened MultisetOrders
  import L = Levels
  import opened Funcs

  class {:termination_closed} FlatSum extends RealFunc {
    const f: RealFunc
    const g: RealFunc
    constructor (f: RealFunc, g: RealFunc) ensures depth == f.depth + g.depth
      decreases {:global} multiset{L.Apply} + f.depth + g.depth by Bags(), 0
    {
      this.f, this.g, depth := f, g, f.depth + g.depth;
    }
    ghost predicate Valid() decreases {:global} multiset{L.Apply} + depth by Bags(), 0 {
      depth == f.depth + g.depth && f.Valid() // error: f.Valid's bound {apply} + f.depth is not below {apply} + f.depth + g.depth
    }
    method Apply(x: int) returns (r: int)
      requires Valid()
      decreases {:global} multiset{L.Apply} + depth by Bags(), 1
    {
      r := f.Apply(x); // error: f.apply's bound {apply} + f.depth is not below {apply} + f.depth + g.depth
    }
  }
}

module Rejected {
  import opened MultisetOrders
  import L = Levels
  import opened Funcs
  import Impl
  import Client

  // integrate at the level of apply: its call of f.apply is at its own bound
  method FlatIntegrate(f: RealFunc, x: int) returns (r: int)
    requires f.Valid()
    decreases {:global} multiset{L.Apply} + f.depth by Bags(), 1
  {
    r := f.Apply(x); // error: the same bound
  }

  // main below integrate: {integrate} is not above {integrate, createSum}
  method LowMain(s: Impl.Sum)
    requires s.depth == multiset{L.CreateSum}
    requires (LowerTwo(L.Integrate, L.Apply, L.CreateSum, multiset{});
              assert multiset{L.Integrate} + multiset{} == multiset{L.Integrate};
              assert multiset{L.Apply, L.CreateSum} + multiset{} == multiset{L.Apply} + s.depth; s.Valid())
    decreases {:global} multiset{L.Integrate} by Bags()
  {
    var r := Client.Integrate(s, 0, 10); // error: {integrate, createSum} is above {integrate}
  }

  // The global bound does not order the recursion: without an ordinary decreases clause, the default one is
  // the parameters, and x goes up
  method LoopIter(f: RealFunc, x: int, n: nat) returns (r: int)
    requires (Lower(L.IntegrateIter, L.Apply, f.depth); f.Valid())
    decreases {:global} multiset{L.IntegrateIter} + f.depth by Bags()
  {
    if n == 0 {
      return 0;
    }
    r := LoopIter(f, x + 1, n - 1); // error: x + 1 is not below x
  }
}
