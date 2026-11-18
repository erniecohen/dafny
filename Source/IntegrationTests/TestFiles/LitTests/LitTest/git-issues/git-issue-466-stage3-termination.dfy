// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), third
// stage: soundness gate for traits in a recursive module group.  In each group of modules named
// Tie... below, a type of one member extends a trait of another member, and a cycle of calls goes
// through the trait without a measure that decreases.  Each would prove 'false', or run forever, if
// a call dispatched through a trait of the group to an override in another member were not checked
// as it is when trait and override are in one module.  Every such group gets an error that breaks
// its cycle.  The second run adds --modular-termination, where the members of a group count as one
// module: every group gets an error there too.
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules "%s" > "%t"
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules --modular-termination "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// two classes of two members that override each other's way around: neither override calls itself,
// and no member has a cycle of its own
module TieFunction1 {
  import TieFunction2
  import TieFunction3
  trait T {
    function A(other: T): int
    function B(other: T): int
  }
  function UseX(x: TieFunction2.X): int { 0 }
  function UseY(y: TieFunction3.Y): int { 0 }
}
module TieFunction2 {
  import TieFunction1
  class X extends TieFunction1.T {
    constructor () { }
    function A(other: TieFunction1.T): int { 0 }
    function B(other: TieFunction1.T): int { other.A(this) }
  }
}
module TieFunction3 {
  import TieFunction1
  class Y extends TieFunction1.T {
    constructor () { }
    function A(other: TieFunction1.T): int { other.B(this) + 1 }
    function B(other: TieFunction1.T): int { 1 }
  }
}
module TieFunction4 {
  import TieFunction1
  import TieFunction2
  import TieFunction3
  method Bad()
    ensures false
  {
    var x := new TieFunction2.X();
    var y := new TieFunction3.Y();
    assert x.B(y) == y.A(x) == x.B(y) + 1;
  }
}

// methods
module TieMethod1 {
  import TieMethod2
  trait T {
    method M(n: nat)
      decreases n
  }
  method Run(c: TieMethod2.C) {
    c.M(3);
  }
}
module TieMethod2 {
  import TieMethod1
  class C extends TieMethod1.T {
    method M(n: nat)
      decreases n
    {
      var t: TieMethod1.T := this;
      t.M(n);
    }
  }
}

// lemmas
module TieLemma1 {
  import TieLemma2
  trait T {
    lemma L()
      ensures false
  }
  lemma Use(c: TieLemma2.C)
    ensures false
  {
    c.L();
  }
}
module TieLemma2 {
  import TieLemma1
  class C extends TieLemma1.T {
    lemma L()
      ensures false
    {
      var t: TieLemma1.T := this;
      t.L();
    }
  }
}

// a datatype that extends a trait of another member
module TieDatatype1 {
  import TieDatatype2
  trait T {
    function F(n: nat): nat
  }
  function Use(d: TieDatatype2.D): nat { d.F(0) }
}
module TieDatatype2 {
  import TieDatatype1
  datatype D extends TieDatatype1.T = D(x: nat) {
    function F(n: nat): nat {
      var t: TieDatatype1.T := this;
      t.F(n) + 1
    }
  }
  lemma Bad(d: D)
    ensures false
  {
    var t: TieDatatype1.T := d;
    assert t.F(0) == d.F(0) == t.F(0) + 1;
  }
}

// {:termination false} on a trait says nothing about the types of its own group that extend it
module TieLegacy1 {
  import TieLegacy2
  trait {:termination false} T {
    function F(): nat
  }
  function Use(c: TieLegacy2.C): nat { c.F() }
}
module TieLegacy2 {
  import TieLegacy1
  class C extends TieLegacy1.T {
    function F(): nat {
      var t: TieLegacy1.T := this;
      t.F() + 1
    }
  }
}

// the cycle goes through a member that the class inherits from the trait, and through a third
// member of the group
module TieInherited1 {
  import TieInherited2
  import TieInherited3
  trait T {
    function F(): nat
    function G(): nat { TieInherited3.H(this) }
  }
  function Use(c: TieInherited2.C): nat { c.G() }
}
module TieInherited2 {
  import TieInherited1
  class C extends TieInherited1.T {
    function F(): nat { G() + 1 }
  }
}
module TieInherited3 {
  import TieInherited1
  function H(t: TieInherited1.T): nat { t.F() }
}

// the trait of one member extends a trait of another member, and the class is in a third
module TieParent1 {
  import TieParent2
  trait T {
    lemma L(n: nat)
      ensures false
      decreases n
  }
}
module TieParent2 {
  import TieParent1
  import TieParent3
  trait U extends TieParent1.T {
    lemma K(c: TieParent3.C)
      ensures false
    {
      c.L(0);
    }
  }
}
module TieParent3 {
  import TieParent1
  import TieParent2
  class C extends TieParent2.U {
    lemma L(n: nat)
      ensures false
      decreases n
    {
      var u: TieParent2.U := this;
      u.L(n);
    }
  }
}

// a class that extends traits of two other members: the cycle uses one override of each
module TieTwo1 {
  import TieTwo3
  trait T {
    function F(n: nat): nat
      decreases n, 0
  }
  function Use(c: TieTwo3.C): nat { c.F(0) }
}
module TieTwo2 {
  import TieTwo3
  trait U {
    function G(n: nat): nat
      decreases n, 0
  }
  function Use(c: TieTwo3.C): nat { c.G(0) }
}
module TieTwo3 {
  import TieTwo1
  import TieTwo2
  class C extends TieTwo1.T, TieTwo2.U {
    function F(n: nat): nat
      decreases n, 0
    {
      var u: TieTwo2.U := this;
      u.G(n)
    }
    function G(n: nat): nat
      decreases n, 0
    {
      var t: TieTwo1.T := this;
      t.F(n) + 1
    }
  }
}

// a generic trait, an opened import and a renamed import
module TieGeneric1 {
  import opened TieGeneric2
  trait T<X> {
    function F(x: X): X
  }
  function Use(c: C): int { c.F(0) }
}
module TieGeneric2 {
  import Tr = TieGeneric1
  class C extends Tr.T<int> {
    function F(x: int): int {
      var t: Tr.T<int> := this;
      t.F(x) + 1
    }
  }
}

// the override's measure is larger than the one the trait declares
module TieMeasure1 {
  import TieMeasure2
  trait T {
    function F(n: nat): nat
      decreases n
  }
  function Use(t: T, c: TieMeasure2.C, n: nat): nat { c.F(n) }
}
module TieMeasure2 {
  import TieMeasure1
  class C extends TieMeasure1.T {
    function F(n: nat): nat
      decreases n + 1
    {
      var t: TieMeasure1.T := this;
      if n == 0 then 0 else t.F(n - 1)
    }
  }
}

// a reference trait and a method that the override makes loop through a method of the trait's
// member
module TieObject1 {
  import TieObject2
  trait T extends object {
    var count: nat
    method Step()
      modifies this
  }
  method Run(t: T)
    modifies t
  {
    t.Step();
  }
  method Start() {
    var c := new TieObject2.C();
    Run(c);
  }
}
module TieObject2 {
  import TieObject1
  class C extends TieObject1.T {
    constructor () { }
    method Step()
      modifies this
    {
      count := count + 1;
      TieObject1.Run(this);
    }
  }
}
