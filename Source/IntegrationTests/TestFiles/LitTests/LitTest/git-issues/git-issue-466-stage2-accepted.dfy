// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), second
// stage: programs with recursive module groups that are accepted.  The declarations of a group
// mean what they would mean in one module, so each program below verifies, and each member named
// Bad is a control that states something false: the errors of the verifier are exactly those for
// the Bad members.  One of them (Hidden.User.Bad) is false only because the function it uses is
// provided and not revealed: an export set hides from the other members of a group what it hides
// from any other importer.
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules "%s" > "%t"
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules --resolution-threads 4 "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// mutually recursive datatypes, functions and lemmas, by structural recursion
module Tree {
  import Forest
  datatype T = Leaf(n: nat) | Node(children: Forest.F)
  function Size(t: T): nat {
    match t
    case Leaf(_) => 1
    case Node(f) => 1 + Forest.Size(f)
  }
  function Sum(t: T): nat {
    match t
    case Leaf(n) => n
    case Node(f) => Forest.Sum(f)
  }
  function Double(t: T): T {
    match t
    case Leaf(n) => Leaf(2 * n)
    case Node(f) => Node(Forest.Double(f))
  }
  lemma DoubleSum(t: T)
    ensures Sum(Double(t)) == 2 * Sum(t)
  {
    match t
    case Leaf(_) =>
    case Node(f) => Forest.DoubleSum(f);
  }
  lemma Positive(t: T)
    ensures Size(t) > 0
  {
  }
  lemma Bad(t: T)
    ensures Size(t) == 1
  {
  }
}
module Forest {
  import Tree
  datatype F = Empty | Cons(head: Tree.T, tail: F)
  function Size(f: F): nat {
    match f
    case Empty => 0
    case Cons(t, rest) => Tree.Size(t) + Size(rest)
  }
  function Sum(f: F): nat {
    match f
    case Empty => 0
    case Cons(t, rest) => Tree.Sum(t) + Sum(rest)
  }
  function Double(f: F): F {
    match f
    case Empty => Empty
    case Cons(t, rest) => Cons(Tree.Double(t), Double(rest))
  }
  lemma DoubleSum(f: F)
    ensures Sum(Double(f)) == 2 * Sum(f)
  {
    match f
    case Empty =>
    case Cons(t, rest) =>
      Tree.DoubleSum(t);
      DoubleSum(rest);
  }
  lemma Bad(f: F)
    ensures Sum(Double(f)) == Sum(f)
  {
    match f
    case Empty =>
    case Cons(t, rest) =>
      Tree.DoubleSum(t);
      DoubleSum(rest);
  }
}

// a ring of three modules, with a decreases clause that orders the calls
module Ring1 {
  import Ring2
  function F(n: nat): nat
    decreases n, 2
  {
    if n == 0 then 0 else 1 + Ring2.G(n - 1)
  }
  lemma Value()
    ensures F(3) == 3
  {
  }
  lemma Bad()
    ensures F(3) == 4
  {
  }
}
module Ring2 {
  import Ring3
  function G(n: nat): nat
    decreases n, 1
  {
    Ring3.H(n)
  }
}
module Ring3 {
  import Ring1
  function H(n: nat): nat
    decreases n, 0
  {
    if n == 0 then 0 else 1 + Ring1.F(n - 1)
  }
}

// mutually recursive methods and lemmas
module Even {
  import Odd
  predicate Is(n: nat) { n % 2 == 0 }
  method Decide(n: nat) returns (b: bool)
    ensures b == Is(n)
  {
    if n == 0 {
      b := true;
    } else {
      b := Odd.Decide(n - 1);
    }
  }
  lemma Next(n: nat)
    requires Is(n)
    ensures Odd.Is(n + 1)
    decreases n
  {
    if n != 0 {
      Odd.Next(n - 1);
    }
  }
  lemma Bad(n: nat)
    ensures Is(n) && Odd.Is(n)
    decreases n
  {
    if n != 0 {
      Odd.Bad(n - 1);
    }
  }
}
module Odd {
  import Even
  predicate Is(n: nat) { n % 2 == 1 }
  method Decide(n: nat) returns (b: bool)
    ensures b == Is(n)
  {
    if n == 0 {
      b := false;
    } else {
      b := Even.Decide(n - 1);
    }
  }
  lemma Next(n: nat)
    requires Is(n)
    ensures Even.Is(n + 1)
    decreases n
  {
    if n != 0 {
      Even.Next(n - 1);
    }
  }
  lemma Bad(n: nat)
    ensures Is(n) && Even.Is(n)
    decreases n
  {
    if n != 0 {
      Even.Bad(n - 1);
    }
  }
}

// type parameters
module Gen1 {
  import Gen2
  datatype D<T> = Nil | Cons(t: T, rest: Gen2.E<T>)
  function Len<T>(d: D<T>): nat {
    match d
    case Nil => 0
    case Cons(_, r) => 1 + Gen2.Len(r)
  }
  function Map<T, U>(d: D<T>, f: T -> U): D<U> {
    match d
    case Nil => Nil
    case Cons(t, r) => Cons(f(t), Gen2.Map(r, f))
  }
  lemma MapLen<T, U>(d: D<T>, f: T -> U)
    ensures Len(Map(d, f)) == Len(d)
  {
    match d
    case Nil =>
    case Cons(_, r) => Gen2.MapLen(r, f);
  }
}
module Gen2 {
  import Gen1
  datatype E<T> = E(d: Gen1.D<T>)
  function Len<T>(e: E<T>): nat {
    Gen1.Len(e.d)
  }
  function Map<T, U>(e: E<T>, f: T -> U): E<U> {
    E(Gen1.Map(e.d, f))
  }
  lemma MapLen<T, U>(e: E<T>, f: T -> U)
    ensures Len(Map(e, f)) == Len(e)
  {
    Gen1.MapLen(e.d, f);
  }
  lemma Bad<T>(e: E<T>)
    ensures Len(e) == 0
  {
  }
}

// two groups, a module below both and a module above both
module Base {
  function Zero(): int { 0 }
}
module Up1 {
  import Up2
  import Base
  function F(n: nat): int { if n == 0 then Base.Zero() else Up2.G(n - 1) }
}
module Up2 {
  import Up1
  import Base
  function G(n: nat): int { if n == 0 then Base.Zero() else Up1.F(n - 1) }
}
module Side1 {
  import Side2
  function P(): int { Side2.Q() }
}
module Side2 {
  import Side1
  function Q(): int { 1 }
  function R(): int { Side1.P() }
}
module Top {
  import Up1
  import Up2
  import Side2
  lemma Values()
    ensures Up1.F(1) == 0 && Up2.G(0) == 0 && Side2.R() == 1
  {
  }
  lemma Bad()
    ensures Up1.F(1) == Side2.R()
  {
  }
}

// opened imports in both directions, and renamed imports
module Open1 {
  import opened Open2
  function F(n: nat): nat { if n == 0 then 0 else G(n - 1) }
  function Same(): int { 1 }
}
module Open2 {
  import opened Open1
  function G(n: nat): nat { if n == 0 then 0 else F(n - 1) }
  function Same(): int { 2 }
  lemma Local()
    ensures Same() == 2 && Open1.Same() == 1
  {
  }
  lemma Bad()
    ensures Same() == 1
  {
  }
}
module Alias1 {
  import X = Alias2
  function F(n: nat): nat { if n == 0 then 0 else X.G(n - 1) }
}
module Alias2 {
  import Y = Alias1
  import Z = Alias1
  function G(n: nat): nat { if n == 0 then 1 else Y.F(n - 1) + Z.F(n - 1) }
  lemma Value()
    ensures G(1) == 0 && Y.F(1) == 1
  {
  }
  lemma Bad()
    ensures G(1) == 1
  {
  }
}

// a nested module of a member, imported by another member
module Nest1 {
  module Inner {
    function F(): nat { 7 }
  }
  import Nest2
  function H(n: nat): nat { if n == 0 then 0 else Nest2.G(n - 1) }
}
module Nest2 {
  import Nest1
  import Nest1.Inner
  function G(n: nat): nat { if n == 0 then Inner.F() else Nest1.H(n - 1) }
  lemma Value()
    ensures Nest1.H(1) == 7 && Nest1.Inner.F() == 7
  {
  }
  lemma Bad()
    ensures Nest1.H(1) == 8
  {
  }
}

// least and greatest predicates and lemmas
module Least1 {
  import Least2
  least predicate P(n: nat) { n == 0 || (n > 0 && Least2.Q(n - 1)) }
  least lemma L(n: nat)
    requires P(n)
    ensures n % 2 == 0
  {
    if n != 0 {
      Least2.M(n - 1);
    }
  }
  lemma Two()
    ensures P(2)
  {
    assert Least2.Q(1);
  }
  lemma Prefix(k: ORDINAL, n: nat)
    requires Least2.Q#[k](n)
    ensures n % 2 == 1
  {
    Least2.M#[k](n);
  }
  lemma Bad()
    ensures P(3)
  {
  }
  lemma Bad2(n: nat)
    requires P(n)
    ensures n == 0
  {
    L(n);
  }
}
module Least2 {
  import Least1
  least predicate Q(n: nat) { n > 0 && Least1.P(n - 1) }
  least lemma M(n: nat)
    requires Q(n)
    ensures n % 2 == 1
  {
    Least1.L(n - 1);
  }
}
module Greatest1 {
  import Greatest2
  greatest predicate P(n: nat) { Greatest2.Q(n + 1) }
  greatest lemma L(n: nat)
    ensures P(n)
  {
    Greatest2.M(n + 1);
  }
  greatest predicate Never(n: nat) { n != 0 && Greatest2.Never(n) }
  greatest lemma Bad(n: nat)
    ensures Never(n)
  {
    Greatest2.Bad(n);
  }
}
module Greatest2 {
  import Greatest1
  greatest predicate Q(n: nat) { Greatest1.P(n + 1) }
  greatest lemma M(n: nat)
    ensures Q(n)
  {
    Greatest1.L(n + 1);
  }
  greatest predicate Never(n: nat) { n != 0 && Greatest1.Never(n) }
  greatest lemma Bad(n: nat)
    ensures Never(n)
  {
    Greatest1.Bad(n);
  }
}

// classes that refer to each other
module Class1 {
  import Class2
  class C {
    var k: Class2.K?
    var n: nat
    constructor ()
      ensures k == null && n == 0
    {
      k := null;
      n := 0;
    }
    method Attach()
      modifies this
      ensures k != null && k.c == this && fresh(k)
    {
      k := new Class2.K(this);
    }
    method Bad()
      modifies this
      ensures k != null
    {
    }
  }
}
module Class2 {
  import Class1
  class K {
    var c: Class1.C
    constructor (c: Class1.C)
      ensures this.c == c
    {
      this.c := c;
    }
    method Bump()
      modifies c
      ensures c.n == old(c.n) + 1
    {
      c.n := c.n + 1;
    }
  }
}

// a trait and a class of one member, with calls through the trait from another member
module Dispatch1 {
  import Dispatch2
  trait T {
    function F(n: nat): nat
      decreases n, 0
  }
  class C extends T {
    function F(n: nat): nat
      decreases n, 0
    {
      if n == 0 then 0 else Dispatch2.G(this, n - 1)
    }
  }
  lemma Value(c: C)
    ensures c.F(1) == 0
  {
  }
  lemma Bad(c: C)
    ensures c.F(1) == 1
  {
  }
}
module Dispatch2 {
  import Dispatch1
  function G(t: Dispatch1.T, n: nat): nat
    decreases n, 1
  {
    t.F(n)
  }
}

// constants whose types are inferred across the group, subset types and newtypes
module Const1 {
  import Const2
  const c := 0
  const d := Const2.e + 1
  type Small = x: Const2.Byte | x < 16
  function F(): nat { Const2.e }
  method M() {
    var x := Const2.e;
    var y: nat := x;
    var s: Small := 3;
    var b: Const2.Byte := s;
  }
  lemma Value()
    ensures d == 1 && Const2.g == 1
  {
  }
  lemma Bad()
    ensures d == 2
  {
  }
}
module Const2 {
  import Const1
  const e := Const1.c
  const f := Const1.d
  const g: nat := f
  type Byte = x: int | 0 <= x < 256
  newtype Index = x: int | 0 <= x < 16
  function OfSmall(s: Const1.Small): Index { s as Index }
  method Bad(b: Byte) returns (s: Const1.Small) {
    s := b;
  }
}

// opaque functions
module Opaque1 {
  import Opaque2
  opaque function F(n: nat): nat { if n == 0 then 0 else Opaque2.G(n - 1) }
  lemma Revealed()
    ensures F(0) == 0
  {
    reveal F();
  }
  lemma Bad()
    ensures F(0) == 0
  {
  }
}
module Opaque2 {
  import Opaque1
  opaque function G(n: nat): nat { if n == 0 then 0 else Opaque1.F(n - 1) }
}

// export sets: what a member provides without revealing is hidden from the other members
module Hidden {
  module Lib {
    export provides F, D, Mk, User
    export Body reveals F, D provides Mk, User
    import User
    function F(n: nat): nat { if n == 0 then 0 else User.G(n - 1) }
    datatype D = D(n: nat)
    function Mk(): D { D(0) }
  }
  module User {
    export reveals G, Get provides Lib
    import Lib
    function G(n: nat): nat { if n == 0 then 0 else Lib.F(n - 1) }
    function Get(): Lib.D { Lib.Mk() }
    lemma Bad()
      ensures Lib.F(0) == 0
    {
    }
  }
  module Seer {
    import Lib`Body
    import User
    lemma Sees()
      ensures Lib.F(0) == 0 && User.G(1) == 0 && User.Get() == Lib.Mk()
    {
    }
  }
}

// the type of a constant that is inferred from a constant of another member, in three steps: the
// subset type nat reaches Refined1.a only when the members repeat the inference, and the export
// sets hide the values, so G verifies only because a has that type
module Refined1 {
  import Refined2
  const a := Refined2.b
  function G(): nat { a }
  lemma Bad()
    ensures a == 0
  {
  }
}
module Refined2 {
  export provides b, Refined3
  import Refined3
  const b := Refined3.c
}
module Refined3 {
  export provides c, Refined1
  import Refined1
  function F(): nat
  const c := F()
}
