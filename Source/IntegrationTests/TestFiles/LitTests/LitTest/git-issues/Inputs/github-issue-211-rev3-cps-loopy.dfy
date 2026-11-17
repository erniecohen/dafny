// Stand-in for stage 2(d): the Huet-Oppen multiset order, nested twice.
// Well-foundedness is an axiom here; stage 2(d) proves it.
module Ord {
  import opened Std.Termination.Orders
  type {:termination_closed} Lv = multiset<nat>
  type {:termination_closed} Stock = multiset<Lv>
  ghost predicate {:termination_closed} InnerLe(m: Lv, n: Lv) { forall x | x in m - n :: exists y | y in n - m :: x < y }
  ghost predicate {:termination_closed} InnerLt(m: Lv, n: Lv) { m != n && InnerLe(m, n) }
  ghost predicate {:termination_closed} OuterLe(s: Stock, t: Stock) { forall a | a in s - t :: exists b | b in t - s :: InnerLt(a, b) }
  ghost predicate {:termination_closed} OuterGt(t: Stock, s: Stock) { s != t && OuterLe(s, t) }
  lemma {:axiom} OuterWf() ensures forall c: Chain<Stock> :: stops<Stock>(OuterGt, c)
  ghost function {:termination_closed} StockOrder(): Wfo<Stock> { OuterWf(); Of(OuterGt) }

  lemma {:termination_closed} InnerAdd(m: Lv, k: nat) ensures InnerLt(m, multiset{k} + m)
  { assert (multiset{k} + m)[k] == m[k] + 1; assert m - (multiset{k} + m) == multiset{}; }
  lemma {:termination_closed} InnerSingle(j: nat, k: nat, x: Lv) requires j < k ensures InnerLt(multiset{j}, multiset{k} + x)
  {
    var n := multiset{k} + x;
    assert |n| == 1 + |x|;
    if multiset{j} == n { assert k in n; assert k == j; }
    forall a | a in multiset{j} - n ensures exists y | y in n - multiset{j} :: a < y {
      assert a == j; assert k in n - multiset{j};
    }
  }
  lemma {:termination_closed} InnerIrrefl(m: Lv) ensures !InnerLt(m, m) {}
  // Replace the element t of a stock by the elements of s, each lower.
  lemma {:termination_closed} OuterReplace(s: Stock, t: Lv, u: Stock)
    requires forall a | a in s :: InnerLt(a, t)
    ensures OuterGt(multiset{t} + u, s + u)
  {
    assert t !in s by { if t in s { InnerIrrefl(t); } }
    var big := multiset{t} + u;
    var small := s + u;
    assert big[t] == small[t] + 1;
    assert t in big - small;
    forall a | a in small - big ensures exists b | b in big - small :: InnerLt(a, b) {
      assert small[a] > big[a];
      assert a in s;
    }
  }
  lemma {:termination_closed} OuterDrop(s: Stock, t: Stock)
    requires exists l | l in s :: forall a | a in t :: InnerLt(a, l)
    ensures OuterGt(s, t)
  {
    var l :| l in s && forall a | a in t :: InnerLt(a, l);
    assert l !in t by { if l in t { InnerIrrefl(l); } }
    assert s != t;
    forall a | a in t - s ensures exists b | b in s - t :: InnerLt(a, b) {
      assert l in s - t;
    }
  }
  lemma {:termination_closed} OuterSub(s: Stock, t: Stock) requires s <= t ensures s == t || OuterGt(t, s)
  { assert s - t == multiset{}; }
  lemma {:termination_closed} InnerSub(m: Lv, n: Lv) requires m <= n ensures m == n || InnerLt(m, n)
  { assert m - n == multiset{}; }
  lemma {:termination_closed} ReqK(x: Lv, ks: Stock) ensures OuterGt(multiset{x} + ks, ks)
  { OuterSub(ks, multiset{x} + ks); assert (multiset{x} + ks)[x] == ks[x] + 1; }
  lemma {:termination_closed} ReqOther(d: Lv, od: Lv, ks: Stock)
    ensures multiset{od} == multiset{d + od} + ks || OuterGt(multiset{d + od} + ks, multiset{od})
  {
    InnerSub(od, d + od);
    if d + od == od { OuterSub(multiset{od}, multiset{d + od} + ks); }
    else {
      assert InnerLt(od, d + od);
      assert d + od in multiset{d + od} + ks;
      OuterDrop(multiset{d + od} + ks, multiset{od});
    }
  }
}

// Figs. 16-17: sets with continuation-passing containsCPS and intersectsCPS.
// The bound of a continuation is its stock: one level per frame it will resume.
module CPS {
  import opened Ord
  trait {:termination_closed} Cont {
    ghost const {:termination_closed} stock: Stock
    ghost predicate Valid() decreases {:global} stock by StockOrder(), 0
    method Invoke(b: bool) returns (r: bool) requires Valid()
      decreases {:global} stock by StockOrder(), 1
  }
  trait {:termination_closed} IntSet {
    ghost const {:termination_closed} depth: Lv
    ghost predicate Valid() decreases {:global} multiset{depth} by StockOrder(), 0
    method ContainsCPS(x: int, k: Cont) returns (r: bool) requires Valid() && k.Valid()
      decreases {:global} multiset{depth} + k.stock by StockOrder(), 1
    method IntersectsCPS(other: IntSet, k: Cont) returns (r: bool)
      requires (ReqOther(other.depth, depth, k.stock); assert other.depth + depth == depth + other.depth; Valid())
        && (ReqOther(depth, other.depth, k.stock); other.Valid())
        && (ReqK(depth + other.depth, k.stock); k.Valid())
      decreases {:global} multiset{depth + other.depth} + k.stock by StockOrder(), 1
  }
}

module Impl {
  import opened Ord
  import opened CPS
  class {:termination_closed} Empty extends IntSet {
    constructor () ensures Valid() && depth == multiset{} { depth := multiset{}; }
    ghost predicate Valid() decreases {:global} multiset{depth} by StockOrder(), 0 { depth == multiset{} }
    method ContainsCPS(x: int, k: Cont) returns (r: bool) requires Valid() && k.Valid()
      decreases {:global} multiset{depth} + k.stock by StockOrder(), 1
    { OuterReplace(multiset{}, depth, k.stock); r := k.Invoke(false); }
    method IntersectsCPS(other: IntSet, k: Cont) returns (r: bool)
      requires (ReqOther(other.depth, depth, k.stock); assert other.depth + depth == depth + other.depth; Valid())
        && (ReqOther(depth, other.depth, k.stock); other.Valid())
        && (ReqK(depth + other.depth, k.stock); k.Valid())
      decreases {:global} multiset{depth + other.depth} + k.stock by StockOrder(), 1
    { OuterReplace(multiset{}, depth + other.depth, k.stock); r := k.Invoke(false); }
  }
  class {:termination_closed} Insert extends IntSet {
    const elem: int
    const inner: IntSet
    constructor (e: int, s: IntSet) requires s.Valid() ensures Valid() && depth == multiset{3} + s.depth
    { elem := e; inner := s; depth := multiset{3} + s.depth; }
    ghost predicate Valid() decreases {:global} multiset{depth} by StockOrder(), 0
    { depth == multiset{3} + inner.depth && (InnerAdd(inner.depth, 3); OuterReplace(multiset{inner.depth}, depth, multiset{}); inner.Valid()) }
    method ContainsCPS(x: int, k: Cont) returns (r: bool) requires Valid() && k.Valid()
      decreases {:global} multiset{depth} + k.stock by StockOrder(), 1
    {
      if x == elem {
        OuterReplace(multiset{}, depth, k.stock);
        r := k.Invoke(true);
      } else {
        InnerAdd(inner.depth, 3);
        OuterReplace(multiset{inner.depth}, depth, k.stock);
        r := inner.ContainsCPS(x, k);
      }
    }
    method IntersectsCPS(other: IntSet, k: Cont) returns (r: bool)
      requires (ReqOther(other.depth, depth, k.stock); assert other.depth + depth == depth + other.depth; Valid())
        && (ReqOther(depth, other.depth, k.stock); other.Valid())
        && (ReqK(depth + other.depth, k.stock); k.Valid())
      decreases {:global} multiset{depth + other.depth} + k.stock by StockOrder(), 1
    {
      // The bound of other.ContainsCPS(elem, c) is {other.depth, L3} + k.stock,
      // where L3 = {2} + inner.depth + other.depth: both are below depth + other.depth.
      var d := depth + other.depth;
      var l3 := multiset{2} + inner.depth + other.depth;
      assert d == multiset{3} + (inner.depth + other.depth);
      LevelsBelow(other.depth, l3, inner.depth, d);
      OuterReplace(multiset{l3}, d, k.stock);
      var c := new IntersectsCont(inner, other, k);
      OuterReplace(multiset{other.depth, l3}, d, k.stock);
      assert multiset{other.depth} + c.stock == multiset{other.depth, l3} + k.stock;
      r := other.ContainsCPS(elem, c);
    }
  }
  lemma {:termination_closed} LevelsBelow(od: Lv, l3: Lv, id: Lv, d: Lv)
    requires d == multiset{3} + (id + od) && l3 == multiset{2} + id + od
    ensures InnerLt(od, d) && InnerLt(l3, d)
  {
    assert d[3] == (id + od)[3] + 1;
    assert od - d == multiset{} by { forall z | z in od ensures od[z] <= d[z] {} }
    assert InnerLt(od, d);
    var big := d; var small := l3;
    assert small - big <= multiset{2};
    assert 3 in big - small;
  }
  class {:termination_closed} IntersectsCont extends Cont {
    const inner: IntSet
    const other: IntSet
    const k: Cont
    constructor (s: IntSet, o: IntSet, k: Cont)
      requires (ValidBelow(s.depth, o.depth, k.stock); s.Valid() && o.Valid() && k.Valid())
      decreases {:global} multiset{multiset{2} + s.depth + o.depth} + k.stock by StockOrder(), 0
      ensures stock == multiset{multiset{2} + s.depth + o.depth} + k.stock && Valid()
    { inner := s; other := o; this.k := k; stock := multiset{multiset{2} + s.depth + o.depth} + k.stock; }
    ghost predicate Valid() decreases {:global} stock by StockOrder(), 0
    {
      stock == multiset{multiset{2} + inner.depth + other.depth} + k.stock &&
      (ValidBelow(inner.depth, other.depth, k.stock); inner.Valid() && other.Valid() && k.Valid())
    }
    method Invoke(b: bool) returns (r: bool) requires Valid()
      decreases {:global} stock by StockOrder(), 1
    {
      var l3 := multiset{2} + inner.depth + other.depth;
      if b {
        OuterReplace(multiset{}, l3, k.stock);
        r := k.Invoke(true);
      } else {
        assert InnerLt(inner.depth + other.depth, l3) by { InnerAdd(inner.depth + other.depth, 2); }
        OuterReplace(multiset{inner.depth + other.depth}, l3, k.stock);
        r := inner.IntersectsCPS(other, this);
      }
    }
  }
  lemma {:termination_closed} ValidBelow(id: Lv, od: Lv, ks: Stock)
    ensures var s := multiset{multiset{2} + id + od} + ks;
      OuterGt(s, multiset{id}) && OuterGt(s, multiset{od}) && OuterGt(s, ks)
  {
    var l3 := multiset{2} + id + od;
    assert InnerLt(id, l3) && InnerLt(od, l3) by {
      assert l3[2] == (id + od)[2] + 1;
      assert id - l3 == multiset{} by { forall z | z in id ensures id[z] <= l3[z] {} }
      assert od - l3 == multiset{} by { forall z | z in od ensures od[z] <= l3[z] {} }
    }
    OuterReplace(multiset{id}, l3, multiset{});
    OuterReplace(multiset{od}, l3, multiset{});
    OuterReplace(multiset{}, l3, ks);
    var s := multiset{l3} + ks;
    // drop ks from the first two by monotonicity
    OuterDrop(s, multiset{id}); OuterDrop(s, multiset{od});
  }
  class {:termination_closed} Done extends Cont {
    constructor () ensures Valid() && stock == multiset{} { stock := multiset{}; }
    ghost predicate Valid() decreases {:global} stock by StockOrder(), 0 { stock == multiset{} }
    method Invoke(b: bool) returns (r: bool) requires Valid()
      decreases {:global} stock by StockOrder(), 1 { r := b; }
  }
  method Main() {
    var e := new Empty();
    var s1 := new Insert(1, e);
    var s2 := new Insert(2, e);
    var d := new Done();
    var r := s1.IntersectsCPS(s2, d);
  }
}
