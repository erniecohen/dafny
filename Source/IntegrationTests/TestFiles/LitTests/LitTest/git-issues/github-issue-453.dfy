// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-453-newtype.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-453-accepted.dfy" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-453-accepted.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-stored-function-callbacks "%S/Inputs/github-issue-453-knot.dfy" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-453-knot.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-stored-function-callbacks "%S/Inputs/github-issue-453-nested-knot.dfy" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-453-nested-knot.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-453-nested-knot.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/453): a function value that a constructor
// stores in a field could be applied, by a function of the object, to a value from which it reaches
// the object again without reading the heap, through a datatype argument or the constant of another
// object.  Applying a function value gets no termination check, so the two functions were a cycle that
// proved false (Inputs/github-issue-453-knot.dfy) and overflowed the stack when run.  The check of a
// stored function now follows, left of an arrow, the arguments of datatype constructors, the
// components of tuples, the constants of classes and traits, and the bases of newtypes and subset
// types, and it fails at the class of the object, a trait type or an abstract type.  It also looks
// for function values inside the stored value: in datatypes, in the constants of other objects, and
// in the type arguments of a trait type.  Each module below stores such a function and is rejected.
// Inputs/github-issue-453-accepted.dfy has stored functions that do not reach the object, and an
// "assert false" that must fail.  The hidden --legacy-stored-function-callbacks restores the old
// search, under which the knot verifies.  A nested datatype, whose recursion makes a larger
// instantiation at every step, is expanded once on each path of the search, so the search ends: a
// store of one is accepted (Inputs/github-issue-453-accepted.dfy), and a knot through one is rejected
// (Inputs/github-issue-453-nested-knot.dfy, which also verifies under the legacy option).

// The reproducer of the issue: a datatype argument
module Datatype {
  datatype Box = Box(c: C)
  class C {
    const g: Box -> nat
    constructor() { g := (b: Box) => b.c.F(0); }
    function F(n: nat): nat { g(Box(this)) + 1 }
  }
}

// A generic datatype
module GenericDatatype {
  datatype Box<X> = Box(x: X)
  class C {
    const g: Box<C> -> nat
    constructor() { g := (b: Box<C>) => b.x.F(0); }
    function F(n: nat): nat { g(Box(this)) + 1 }
  }
}

// A generic datatype whose argument is the class only through another datatype
module NestedGenericDatatype {
  datatype Box<X> = Box(x: X)
  datatype Wrap = Wrap(c: C)
  class C {
    const g: Box<Wrap> -> nat
    constructor() { g := (b: Box<Wrap>) => b.x.c.F(0); }
    function F(n: nat): nat { g(Box(Wrap(this))) + 1 }
  }
}

// A sequence, a set and a map of the class type, inside a datatype
module Collections {
  datatype Bag = Bag(s: seq<C>, t: set<C>, m: map<int, C>)
  class C {
    const g: Bag -> nat
    const h: seq<Bag> -> nat
    const k: map<Bag, int> -> nat
    constructor() {
      g := (b: Bag) => if |b.s| == 0 then 0 else b.s[0].F(0);
      h := (bs: seq<Bag>) => 0;
      k := (m: map<Bag, int>) => 0;
    }
    function F(n: nat): nat { g(Bag([this], {}, map[])) + 1 }
  }
}

// A tuple component
module Tuple {
  datatype Box = Box(c: C)
  class C {
    const g: ((int, Box)) -> nat
    constructor() { g := (p: (int, Box)) => p.1.c.F(0); }
    function F(n: nat): nat { g((0, Box(this))) + 1 }
  }
}

// A subset type of a datatype that holds the class
module SubsetType {
  datatype Box = Box(c: C)
  type SubBox = b: Box | true
  class C {
    const g: SubBox -> nat
    constructor() { g := (b: SubBox) => b.c.F(0); }
    function F(n: nat): nat { g(Box(this)) + 1 }
  }
}

// A codatatype
module Codatatype {
  codatatype Stream = Cons(c: C, tail: Stream)
  class C {
    const g: Stream -> nat
    constructor() { g := (s: Stream) => s.c.F(0); }
    function F(n: nat): nat { g(Rep(this)) + 1 }
  }
  function Rep(c: C): Stream { Cons(c, Rep(c)) }
}

// A trait the class extends, inside a datatype
module Trait {
  trait T { function F(n: nat): nat }
  datatype Box = Box(t: T)
  class C extends T {
    const g: Box -> nat
    constructor() { g := (b: Box) => b.t.F(0); }
    function F(n: nat): nat { g(Box(this)) + 1 }
  }
}

// The constant of another class (the second example of the issue)
module OtherClass {
  class D { const c: C  constructor(c: C) { this.c := c; } }
  class C {
    ghost const g: D -> nat
    constructor() { g := (d: D) => d.c.F(0); }
    ghost function F(n: nat): nat { g(Mk(this)) + 1 }
    ghost function Mk(c: C): D
  }
}

// The constant of a trait that another class extends
module InheritedConstant {
  trait P extends object { const c: C }
  class D extends P { constructor(c: C) { this.c := c; } }
  class C {
    ghost const g: D -> nat
    constructor() { g := (d: D) => d.c.F(0); }
    ghost function F(n: nat): nat { g(Mk(this)) + 1 }
    ghost function Mk(c: C): D
  }
}

// A function stored in another object, whose argument reaches that object
module Holder {
  class E {
    const h: C -> nat
    constructor (h: C -> nat) { this.h := h; }
  }
  class C {
    const e: E
    constructor (e: E) { this.e := e; }
    function F(): nat { e.h(this) + 1 }
  }
}

// A function inside a datatype
module FunctionInDatatype {
  datatype Wrap = Wrap(f: C -> nat)
  class C {
    const w: Wrap
    constructor (w: Wrap) { this.w := w; }
    function F(): nat { w.f(this) + 1 }
  }
}

// A permissive type parameter instantiated with the type of the object that stores the instance
module PermissiveClass {
  class C<!T> {
    const g: T -> nat
    constructor (h: T -> nat) { g := h; }
    function F(x: T): nat { g(x) }
  }
  class D {
    const c: C<D>
    constructor (c: C<D>) { this.c := c; }
    function F(): nat { c.F(this) + 1 }
  }
}

// The same, through a trait whose implementation stores the function
module PermissiveTrait {
  trait Ac<!I> { function Apply(i: I): nat }
  class FA<!I> extends Ac<I> {
    const f: I -> nat
    constructor (f: I -> nat) { this.f := f; }
    function Apply(i: I): nat { f(i) }
  }
  class D {
    const a: Ac<D>
    constructor (a: Ac<D>) { this.a := a; }
    function F(): nat { a.Apply(this) + 1 }
  }
}

// An abstract type, which a refining module may define as a datatype that holds the class
abstract module Abstract {
  type X
  class C {
    const g: X -> nat
    constructor (g: X -> nat) { this.g := g; }
  }
}
