// RUN: %verify --refinement-renaming "%s" > "%t"
// RUN: %verify --refinement-renaming --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %verify --refinement-renaming "%S/Inputs/github-issue-106-constructs.dfy" >> "%t"
// RUN: %verify --refinement-renaming --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-106-constructs.dfy" >> "%t"
// RUN: %exits-with 2 %verify --refinement-renaming "%S/Inputs/github-issue-106-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify --refinement-renaming --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-106-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify "%S/Inputs/github-issue-106-off.dfy" >> "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-106-off.dfy" >> "%t"
// RUN: %exits-with 2 %verify --refinement-renaming --legacy-inherited-name-capture "%S/Inputs/github-issue-106-off.dfy" >> "%t"
// RUN: %verify "%S/Inputs/github-issue-106-pattern.dfy" >> "%t"
// RUN: %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-106-pattern.dfy" >> "%t"
// RUN: %verify --refinement-renaming "%S/Inputs/github-issue-106-types.dfy" >> "%t"
// RUN: %verify --refinement-renaming --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-106-types.dfy" >> "%t"
// RUN: %exits-with 2 %verify --refinement-renaming "%S/Inputs/github-issue-106-types-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify --refinement-renaming --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-106-types-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify "%S/Inputs/github-issue-106-types.dfy" >> "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-106-types.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Issues https://github.com/erniecohen/dafny/issues/106 and https://github.com/erniecohen/dafny/issues/107:
// with --refinement-renaming, a declaration in a refining module may rename the type parameters,
// the out-parameters, the result and the local names of the in-parameters of the declaration it
// refines.  The inherited specification and body are renamed with it.  A named argument uses the
// parameter's label, which is the name of the first declaration and never changes.  This file
// holds renamings that are accepted, under both resolvers.  The inputs hold: a renaming through
// many kinds of inherited statements and expressions; renamings that are rejected, because they
// would change what a name in the inherited code refers to, or because the signatures disagree in
// more than names; and the same renamings without the option, which are errors as before.  The
// option cannot be combined with --legacy-inherited-name-capture, since a renaming is sound only
// under the check that the legacy option turns off.  The last input needs no option: a match
// pattern with a type annotation is inherited with a type of the refining module.  The two inputs
// about types hold signatures that spell a type differently from the signature they refine: with
// the option, the spelling is accepted if it denotes the same type, and rejected otherwise; without
// the option, each difference in spelling is an error as before.

abstract module A {
  function F(x: int): int
    ensures F(x) >= x

  function G<T>(s: seq<T>, n: nat): (r: seq<T>)
    requires n <= |s|
    ensures |r| == n
  {
    s[..n]
  }

  method M<T>(a: T, b: T) returns (c: T, d: T)
    ensures c == b && d == a
  {
    c, d := b, a;
  }

  lemma L(i: nat, j: nat)
    requires i <= j
    ensures i * 1 <= j
    decreases j - i
  {
  }

  function Sum(xs: seq<int>, acc: int := 0): int
    decreases |xs|
  {
    if xs == [] then acc else Sum(xs[1..], acc + xs[0])
  }

  function Fib(n: nat): nat {
    if n < 2 then n else Fib(n - 1) + Fib(n - 2)
  } by method {
    var a, b := 0, 1;
    for i := 0 to n
      invariant a == Fib(i) && b == Fib(i + 1)
    {
      a, b := b, a + b;
    }
    return a;
  }

  class C<U> {
    var u: U
    constructor (init: U)
      ensures u == init
    {
      u := init;
    }
    method Get() returns (v: U)
      ensures v == u
    {
      v := u;
    }
    function Same(w: U): U { w }
  }

  datatype D<X> = Leaf(x: X) | Node(l: D<X>, r: D<X>) {
    function Size(): nat {
      match this
      case Leaf(_) => 1
      case Node(a, b) => a.Size() + b.Size()
    }
  }

  trait Tr<Y> {
    function Id(y: Y): Y { y }
  }

  type Abs<Z>

  method Caller() {
    var r := F(x := 3);
    assert r >= 3;
    var s := G(s := [1, 2, 3], n := 2);
    assert |s| == 2;
    L(i := 1, j := 2);
  }
}

module B refines A {
  // an in-parameter gets a new local name
  function F(y: int): int { y + 1 }

  // a type parameter, the in-parameters and the result are renamed; the body and the specification are inherited
  function G<Elem>(items: seq<Elem>, count: nat): (prefix: seq<Elem>)

  // the two out-parameters swap their names; the inherited body and postcondition follow the positions
  method M<V>(first: V, second: V) returns (d: V, c: V)

  lemma L(lo: nat, hi: nat)

  // a default value and a recursive call in the inherited body
  function Sum(list: seq<int>, total: int): int

  // a by-method body
  function Fib(k: nat): nat

  class C<Item> ... {
    constructor (start: Item)
    method Get() returns (res: Item)
    function Same(it: Item): Item
  }

  datatype D<Val> ... {
    function Depth(): nat { 0 }
  }

  trait Tr<Arg> ... { }

  type Abs<P> = seq<P>

  method Client() {
    // a named argument uses the label, which is the name of the first declaration
    var r := F(x := 3);
    assert r == 4;
    var s := G(s := [1, 2, 3], n := 2);
    assert s == [1, 2];
    var p, q := M(a := 1, b := 2);
    assert p == 2 && q == 1;
    L(i := 1, j := 2);
    assert Sum(xs := [1, 2], acc := 0) == Sum([1, 2]);
    var c := new C<int>(init := 5);
    var g := c.Get();
    assert g == 5;
    assert c.Same(w := 2) == 2;
  }
}

// A chain: each step renames again, and the label stays that of the first declaration.
module B2 refines B {
  function F(z: int): int
  function G<E2>(xs: seq<E2>, m: nat): (out: seq<E2>)
  method M<W>(a: W, b: W) returns (c: W, d: W)

  method Client2() {
    assert F(x := 1) == 2;
    assert G(s := [true, false], n := 1) == [true];
  }
}

// Seen from outside, through an import and through an export set.
abstract module E0 {
  export provides F
  function F(x: int): int
}
module E1 refines E0 {
  export provides F
  function F(y: int): int { y }
}
module User {
  import B
  import E1
  method Test() {
    assert B.F(x := 1) == 2;
    assert E1.F(x := 1) == E1.F(1);
  }
}

// A trait's member renamed in a refinement, with an override in a class of the refining module.
abstract module T0 {
  trait Shape {
    function Area(scale: nat): nat
    method Grow(delta: nat) returns (now: nat)
      ensures now >= delta
  }
}
module T1 refines T0 {
  trait Shape ... {
    function Area(factor: nat): nat
    method Grow(amount: nat) returns (size: nat)
  }
  class Square extends Shape {
    function Area(scale: nat): nat { scale * 4 }
    method Grow(delta: nat) returns (now: nat)
      ensures now >= delta
    {
      now := delta;
    }
  }
}
