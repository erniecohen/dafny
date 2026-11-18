// https://github.com/erniecohen/dafny/issues/211
// Figs. 10-14 of Jacobs, Bosnacki and Kuiper, "Modular termination verification" (ECOOP 2015), section 4.3, in
// their own measure: a real function object holds a bag of method levels, its depth, and a call of its apply
// method spends the permission {apply} + depth.  Sum adds {createSum} to the depths of its two summands
// (Figs. 11-12), integrate holds {integrate} + depth and its helper the pair (n, {integrateIter} + depth)
// (Fig. 13), and main holds {main}.  The bounds go down in the multiset order of the orders library, by the
// inequalities of Fig. 14: a permission is replaced by finitely many lower ones.
// RUN: echo "Figs. 10-14 with bags of method levels, by default" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders "%s" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "--modular-termination: the orders library verified with the client" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders "%s" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "controls (assert false in each positive context), and bounds that do not go down" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders "%s" "%S/Inputs/github-issue-211-integrate-verify.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The levels of the methods of the paper, in its order: a method may call those below it
module Levels {
  const {:termination_closed} Apply: nat := 0
  const {:termination_closed} CreateSum: nat := 1
  const {:termination_closed} IntegrateIter: nat := 2
  const {:termination_closed} Integrate: nat := 3
  const {:termination_closed} Main: nat := 4
}

// Fig. 10(b): the predicate RealFunc(this, d), as Valid() and the depth d, and call_perm({RealFunc.apply} + d) on
// apply.  Valid() holds the permission {apply} + d at the lower second component.
module Funcs {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import L = Levels

  trait {:termination_closed} RealFunc {
    ghost const {:termination_closed} depth: multiset<nat>
    ghost predicate Valid() decreases {:global} multiset{L.Apply} + depth by Bags(), 0
    method Apply(x: int) returns (r: int)
      requires Valid()
      decreases {:global} multiset{L.Apply} + depth by Bags(), 1
  }

  // The inequalities of Fig. 14.  A permission is replaced by a lower one: {a} + d is above {b} + d when a > b
  lemma {:termination_closed} Lower(a: nat, b: nat, d: multiset<nat>)
    requires a > b
    ensures MultisetGt<nat>(Gt, multiset{a} + d, multiset{b} + d)
  {
    GtWellFounded();
    MultisetReplaceLower<nat>(Gt, a, multiset{b}, d);
  }

  // A permission is replaced by two lower ones: {a} + d is above {b, c} + d when a > b and a > c
  lemma {:termination_closed} LowerTwo(a: nat, b: nat, c: nat, d: multiset<nat>)
    requires a > b && a > c
    ensures MultisetGt<nat>(Gt, multiset{a} + d, multiset{b, c} + d)
  {
    GtWellFounded();
    MultisetReplaceLower<nat>(Gt, a, multiset{b, c}, d);
  }

  // Permissions are given up: {a, c} + e + o is above {a} + e
  lemma {:termination_closed} Part(a: nat, c: nat, e: multiset<nat>, o: multiset<nat>)
    ensures MultisetGt<nat>(Gt, multiset{a, c} + e + o, multiset{a} + e)
  {
    var big := multiset{a, c} + e + o;
    MultisetSub<nat>(Gt, multiset{a} + e, big);
    assert multiset{a} + e != big by { assert |multiset{a} + e| < |big|; }
  }
}

// Figs. 11-12: LinearFunc, with the empty depth, and Sum, whose depth is {createSum} + f's + g's
module Impl {
  import opened MultisetOrders
  import L = Levels
  import opened Funcs

  class {:termination_closed} LinearFunc extends RealFunc {
    const a: int
    const b: int
    constructor (a: int, b: int)
      ensures Valid() && depth == multiset{}
      decreases {:global} multiset{L.Apply} by Bags(), 0
    {
      this.a, this.b, depth := a, b, multiset{};
    }
    ghost predicate Valid() decreases {:global} multiset{L.Apply} + depth by Bags(), 0 { depth == multiset{} }
    method Apply(x: int) returns (r: int)
      requires Valid()
      decreases {:global} multiset{L.Apply} + depth by Bags(), 1
    {
      r := a * x + b;
    }
  }

  class {:termination_closed} Sum extends RealFunc {
    const f: RealFunc
    const g: RealFunc
    // createSum: the depth of the result is {createSum} + f's + g's (Fig. 12)
    constructor (f: RealFunc, g: RealFunc)
      requires (Part(L.Apply, L.CreateSum, f.depth, g.depth); f.Valid())
      requires (Part(L.Apply, L.CreateSum, g.depth, f.depth); g.Valid())
      ensures depth == multiset{L.CreateSum} + f.depth + g.depth
      ensures (assert multiset{L.Apply} + depth == multiset{L.Apply, L.CreateSum} + f.depth + g.depth; Valid())
      decreases {:global} multiset{L.Apply, L.CreateSum} + f.depth + g.depth by Bags(), 0
    {
      this.f, this.g := f, g;
      depth := multiset{L.CreateSum} + f.depth + g.depth;
    }
    ghost predicate Valid() decreases {:global} multiset{L.Apply} + depth by Bags(), 0 {
      depth == multiset{L.CreateSum} + f.depth + g.depth &&
      (Part(L.Apply, L.CreateSum, f.depth, g.depth); f.Valid()) &&
      (Part(L.Apply, L.CreateSum, g.depth, f.depth); g.Valid())
    }
    // {apply, createSum} + f.depth + g.depth is above {apply} + f.depth, and above {apply} + g.depth
    method Apply(x: int) returns (r: int)
      requires Valid()
      decreases {:global} multiset{L.Apply} + depth by Bags(), 1
    {
      Part(L.Apply, L.CreateSum, f.depth, g.depth);
      var u := f.Apply(x);
      Part(L.Apply, L.CreateSum, g.depth, f.depth);
      var v := g.Apply(x);
      r := u + v;
    }
  }
}

// Figs. 13-14: integrate, its loop as the recursive integrateIter, and main
module Client {
  import opened MultisetOrders
  import L = Levels
  import opened Funcs
  import Impl

  // call_perm((n, {integrateIter} + d)) of Fig. 13: the global bound {integrateIter} + d, and the ordinary
  // decreases clause n for the recursion.  {integrateIter} + d is above {apply} + d: integrateIter > apply.
  method IntegrateIter(f: RealFunc, x: int, n: nat, acc: int) returns (r: int)
    requires (Lower(L.IntegrateIter, L.Apply, f.depth); f.Valid())
    decreases {:global} multiset{L.IntegrateIter} + f.depth by Bags()
    decreases n
  {
    if n == 0 {
      return acc;
    }
    Lower(L.IntegrateIter, L.Apply, f.depth);
    var y := f.Apply(x);
    r := IntegrateIter(f, x + 1, n - 1, acc + y);
  }

  // {integrate} + d is above {integrateIter} + d
  method Integrate(f: RealFunc, lo: int, n: nat) returns (r: int)
    requires (Lower(L.Integrate, L.Apply, f.depth); f.Valid())
    decreases {:global} multiset{L.Integrate} + f.depth by Bags()
  {
    Lower(L.Integrate, L.IntegrateIter, f.depth);
    r := IntegrateIter(f, lo, n, 0);
  }

  // {main} is above {apply}, {apply, createSum} and {integrate, createSum}
  method Main()
    decreases {:global} multiset{L.Main} by Bags()
  {
    Lower(L.Main, L.Apply, multiset{});
    assert multiset{L.Main} + multiset{} == multiset{L.Main} && multiset{L.Apply} + multiset{} == multiset{L.Apply};
    var f := new Impl.LinearFunc(2, 1);
    var g := new Impl.LinearFunc(0, 3);
    LowerTwo(L.Main, L.Apply, L.CreateSum, multiset{});
    assert multiset{L.Apply, L.CreateSum} + multiset{} == multiset{L.Apply, L.CreateSum} + f.depth + g.depth;
    var s := new Impl.Sum(f, g);
    LowerTwo(L.Main, L.Integrate, L.CreateSum, multiset{});
    assert multiset{L.Integrate, L.CreateSum} + multiset{} == multiset{L.Integrate} + s.depth;
    var r := Integrate(s, 0, 10);
  }
}
