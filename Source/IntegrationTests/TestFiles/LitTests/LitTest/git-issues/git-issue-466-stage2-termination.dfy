// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), second
// stage: soundness gate for termination.  Each group of modules named Loop... below is a recursive
// module group whose members call each other around a cycle without a measure that decreases.
// Each would prove 'false', or run forever, if a call from one member to another were not checked
// as a call inside one module is.  Every such group gets an error that breaks its cycle: a
// termination error, or for the greatest lemmas an unproved postcondition.  The last group shows
// the errors for a cycle of datatypes that has no value.
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// functions
module LoopFunction1 {
  import LoopFunction2
  function F(n: nat): nat { LoopFunction2.G(n) }
  lemma L()
    ensures false
  {
    assert F(0) == LoopFunction2.G(0) == F(0) + 1;
  }
}
module LoopFunction2 {
  import LoopFunction1
  function G(n: nat): nat { LoopFunction1.F(n) + 1 }
}

// lemmas
module LoopLemma1 {
  import LoopLemma2
  lemma L()
    ensures false
  {
    LoopLemma2.M();
  }
}
module LoopLemma2 {
  import LoopLemma1
  lemma M()
    ensures false
  {
    LoopLemma1.L();
  }
}

// lemmas over mutually recursive datatypes, with a measure that does not decrease
module LoopData1 {
  import LoopData2
  datatype T = Leaf | Node(u: LoopData2.U)
  lemma L(t: T)
    ensures false
    decreases t
  {
    LoopData2.M(LoopData2.U(t));
  }
}
module LoopData2 {
  import LoopData1
  datatype U = U(t: LoopData1.T)
  lemma M(u: U)
    ensures false
    decreases u
  {
    LoopData1.L(u.t);
  }
}

// methods
module LoopMethod1 {
  import LoopMethod2
  method M(n: nat) returns (r: nat)
    ensures false
  {
    r := LoopMethod2.N(n);
  }
}
module LoopMethod2 {
  import LoopMethod1
  method N(n: nat) returns (r: nat)
    ensures false
  {
    r := LoopMethod1.M(n);
  }
}

// a ring of three, where the measure does not decrease along one edge
module LoopRing1 {
  import LoopRing2
  function F(n: nat): nat
    decreases n
  {
    if n == 0 then 0 else LoopRing2.G(n - 1)
  }
}
module LoopRing2 {
  import LoopRing3
  function G(n: nat): nat
    decreases n
  {
    LoopRing3.H(n + 1)
  }
}
module LoopRing3 {
  import LoopRing1
  function H(n: nat): nat
    decreases n
  {
    LoopRing1.F(n)
  }
}

// a precondition
module LoopRequires1 {
  import LoopRequires2
  function F(n: nat): nat
    requires LoopRequires2.P(n)
  {
    0
  }
}
module LoopRequires2 {
  import LoopRequires1
  predicate P(n: nat) { LoopRequires1.F(n) == 0 }
}

// postconditions
module LoopEnsures1 {
  import LoopEnsures2
  function F(n: nat): nat
    ensures LoopEnsures2.G(n) == F(n) + 1
  {
    0
  }
  lemma L()
    ensures false
  {
    var _ := F(0);
    var _ := LoopEnsures2.G(0);
  }
}
module LoopEnsures2 {
  import LoopEnsures1
  function G(n: nat): nat
    ensures LoopEnsures1.F(n) == G(n) + 1
  {
    0
  }
}

// reads clauses
module LoopReads1 {
  import LoopReads2
  function R(c: LoopReads2.C): set<object>
    reads LoopReads2.S(c)
  {
    {c}
  }
}
module LoopReads2 {
  import LoopReads1
  class C {
    var x: int
  }
  function S(c: C): set<object>
    reads LoopReads1.R(c)
  {
    {c}
  }
}

// a function called in a lambda expression
module LoopValue1 {
  import LoopValue2
  function F(n: nat): nat {
    var g := (m: nat) => LoopValue2.G(m);
    g(n) + 1
  }
  lemma L()
    ensures false
  {
    assert F(0) == LoopValue2.G(0) + 1;
  }
}
module LoopValue2 {
  import LoopValue1
  function G(n: nat): nat { LoopValue1.F(n) }
}

// the method of a function by method
module LoopByMethod1 {
  import LoopByMethod2
  function F(n: nat): nat {
    if n == 0 then 0 else LoopByMethod2.G(n - 1)
  } by method {
    return if n == 0 then 0 else LoopByMethod2.G(n);
  }
}
module LoopByMethod2 {
  import LoopByMethod1
  function G(n: nat): nat { if n == 0 then 0 else LoopByMethod1.F(n - 1) }
}

// twostate functions
module LoopTwostate1 {
  import LoopTwostate2
  twostate function F(c: LoopTwostate2.C): int
    reads c
  {
    old(c.x) + LoopTwostate2.G(c)
  }
}
module LoopTwostate2 {
  import LoopTwostate1
  class C {
    var x: int
  }
  twostate function G(c: C): int
    reads c
  {
    LoopTwostate1.F(c)
  }
}

// an iterator
module LoopIterator1 {
  import LoopIterator2
  iterator It(n: nat) yields (x: nat)
    ensures false
  {
    LoopIterator2.M(n);
  }
}
module LoopIterator2 {
  import LoopIterator1
  method M(n: nat)
    ensures false
  {
    var it := new LoopIterator1.It(n);
    var more := it.MoveNext();
  }
}

// least predicates and lemmas
module LoopLeast1 {
  import LoopLeast2
  least predicate P(n: nat) { LoopLeast2.Q(n) }
  least lemma L(n: nat)
    requires P(n)
    ensures false
  {
    LoopLeast2.M(n);
  }
  lemma Prefix(k: ORDINAL, n: nat)
    requires P#[k](n)
    ensures false
  {
    LoopLeast2.Prefix(k, n);
  }
}
module LoopLeast2 {
  import LoopLeast1
  least predicate Q(n: nat) { LoopLeast1.P(n) }
  least lemma M(n: nat)
    requires Q(n)
    ensures false
  {
    LoopLeast1.L(n);
  }
  lemma Prefix(k: ORDINAL, n: nat)
    requires Q#[k](n)
    ensures false
  {
    LoopLeast1.Prefix(k, n);
  }
}

// an explicit call of the prefix lemma of another member, at the same prefix
module LoopPrefix1 {
  import LoopPrefix2
  least lemma L(n: nat)
    ensures false
  {
    LoopPrefix2.M#[_k](n);
  }
}
module LoopPrefix2 {
  import LoopPrefix1
  least lemma M(n: nat)
    ensures false
  {
    LoopPrefix1.L#[_k](n);
  }
}

// greatest lemmas: a call of the greatest lemma of another member is a co-call, which gives the
// postcondition for a smaller prefix only, so the part of the postcondition that is not a
// greatest predicate is not proved
module LoopGreatest1 {
  import LoopGreatest2
  greatest predicate P(n: nat) { LoopGreatest2.Q(n) }
  greatest lemma L(n: nat)
    ensures P(n) && n != n
  {
    LoopGreatest2.M(n);
  }
}
module LoopGreatest2 {
  import LoopGreatest1
  greatest predicate Q(n: nat) { LoopGreatest1.P(n) }
  greatest lemma M(n: nat)
    ensures Q(n) && n != n
  {
    LoopGreatest1.L(n);
  }
}

// a call through an opened import and one through a renamed import
module LoopOpened1 {
  import opened LoopOpened2
  lemma L()
    ensures false
  {
    M();
  }
}
module LoopOpened2 {
  import X = LoopOpened1
  lemma M()
    ensures false
  {
    X.L();
  }
}

// a call through a nested module of another member
module LoopNested1 {
  import LoopNested2
  lemma L()
    ensures false
  {
    LoopNested2.Inner.Step();
    LoopNested2.M();
  }
}
module LoopNested2 {
  module Inner {
    lemma Step() { }
  }
  import LoopNested1
  lemma M()
    ensures false
  {
    LoopNested1.L();
  }
}

// members of classes
module LoopClass1 {
  import LoopClass2
  class C {
    ghost function F(k: LoopClass2.K): nat { k.G(this) + 1 }
    lemma L(k: LoopClass2.K)
      ensures false
    {
      assert F(k) == k.G(this) + 1;
    }
  }
}
module LoopClass2 {
  import LoopClass1
  class K {
    ghost function G(c: LoopClass1.C): nat { c.F(this) }
  }
}

// calls through a trait of one member from another member
module LoopDispatch1 {
  import LoopDispatch2
  trait T {
    function F(): nat
    lemma P()
      ensures false
  }
  class C extends T {
    function F(): nat { LoopDispatch2.G(this) + 1 }
    lemma P()
      ensures false
    {
      LoopDispatch2.Q(this);
    }
  }
  lemma L(c: C)
    ensures false
  {
    assert c.F() == LoopDispatch2.G(c) + 1;
  }
}
module LoopDispatch2 {
  import LoopDispatch1
  function G(t: LoopDispatch1.T): nat { t.F() }
  lemma Q(t: LoopDispatch1.T)
    ensures false
  {
    t.P();
  }
}

// a default value in a cycle of calls
module LoopDefault1 {
  import LoopDefault2
  function F(n: nat, m: nat := LoopDefault2.G(n)): nat { 0 }
}
module LoopDefault2 {
  import LoopDefault1
  function G(n: nat): nat { LoopDefault1.F(n) }
}

// a cycle of datatypes with no value: the types are reported as possibly empty, and a variable of
// such a type is not assigned
module Empty1 {
  import Empty2
  datatype D = D(e: Empty2.E)
  method M() {
    var x: D;
    var y := x;
  }
}
module Empty2 {
  import Empty1
  datatype E = E(d: Empty1.D)
}
