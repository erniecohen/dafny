// RUN: %verify --copy-imports "%s" > "%t"
// RUN: %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --copy-imports "%S/Inputs/github-issue-107-controls.dfy" >> "%t"
// RUN: %exits-with 4 %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-107-controls.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports "%S/Inputs/github-issue-107-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-107-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports "%S/Inputs/github-issue-107-names.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-107-names.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports "%S/Inputs/github-issue-107-cycle.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-107-cycle.dfy" >> "%t"
// RUN: %verify --copy-imports "%S/Inputs/github-issue-107-inherited.dfy" >> "%t"
// RUN: %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-107-inherited.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports "%S/Inputs/github-issue-107-inherited-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-107-inherited-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %verify "%s" >> "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %run --copy-imports --target cs "%S/Inputs/github-issue-107-run.dfy" >> "%t"
// RUN: %run --copy-imports --target cs --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-107-run.dfy" >> "%t"
// RUN: %run --copy-imports --target py "%S/Inputs/github-issue-107-run.dfy" >> "%t"
// RUN: %resolve --copy-imports --print:"%t.printed.dfy" "%S/Inputs/github-issue-107-run.dfy" >> "%t"
// RUN: %run --copy-imports --target cs "%t.printed.dfy" >> "%t"
// RUN: %build -t:lib --copy-imports "%S/Inputs/github-issue-107-library.dfy" --output "%t.library.doo" >> "%t"
// RUN: %exits-with 4 %verify --copy-imports "%S/Inputs/github-issue-107-library-user.dfy" --library "%t.library.doo" >> "%t"
// RUN: %exits-with 4 %verify --copy-imports --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-107-library-user.dfy" --library "%t.library.doo" >> "%t"
// RUN: %exits-with 1 %verify "%S/Inputs/github-issue-107-library-user.dfy" --library "%t.library.doo" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Issue https://github.com/erniecohen/dafny/issues/107: with --copy-imports, the declaration
// `import X := Q` gives the importing module a copy of the module Q, named X.  The copy is a
// module of its own: its classes, datatypes, newtypes and abstract types are types of their own,
// distinct from those of Q and from those of every other copy of Q, and its functions and
// constants have the definitions that Q gives them.  A module that refines the importing module
// gets copies of its own.  This file holds the uses that are accepted, under both resolvers.
// The inputs hold: lemmas that say two copies are the same, which the verifier rejects; the
// declarations that the resolver rejects; a copy that takes the place of an inherited abstract
// import, and the checks that are and are not made again for a copy; a program that is compiled and run, and printed and
// run again; and a library that uses copying imports, used by a module that refines one of the
// library's modules.  Without the option, the syntax is a parse error as before, and a library
// built with the option does not load.

// ---------- Identity: each copy has its own types

module Base {
  export
    reveals Cell, Colour
    provides One
  export Body extends Base
    reveals One
    provides Two

  datatype Colour = Red | Green
  class Cell {
    constructor () { }
  }
  function One(): int { 1 }
  function Two(): int { 2 }
}

module Importer {
  import Base
  import Revealing = Base`Body
  import X := Base
  import Z := Base`Body

  lemma CopiesDiffer(o: object)
    ensures o is X.Cell ==> !(o is Z.Cell)
  {
  }

  lemma CopyDiffersFromOriginal(o: object)
    ensures o is X.Cell ==> !(o is Base.Cell)
  {
  }

  // The definitions are the same in every copy.  X sees the default export set of Base, which
  // only provides One; Z sees the export set Body, which reveals it.
  function Sum(): int { X.One() + Z.Two() }

  lemma Revealed()
    ensures Z.One() == 1
    ensures Revealing.One() == Z.One()
  {
  }

  method Make() returns (c: X.Cell, colour: X.Colour) {
    c := new X.Cell();
    colour := X.Red;
  }
}

// A refining module gets a new copy at each copying import that it inherits.

module Importer' refines Importer {
  import Importer

  lemma NewCopy(o: object)
    ensures o is X.Cell ==> !(o is Importer.X.Cell)
    ensures o is X.Cell ==> !(o is Base.Cell)
    ensures o is X.Cell ==> !(o is Z.Cell)
  {
  }

  // The inherited members are about this module's copies.
  method MakeTwo() returns (c: X.Cell, d: X.Cell) {
    var colour;
    c, colour := Make();
    d := new X.Cell();
  }
}

module Importer'' refines Importer' {
  import Importer'

  lemma NewCopyAgain(o: object)
    ensures o is X.Cell ==> !(o is Importer'.X.Cell)
    ensures o is X.Cell ==> !(o is Importer.X.Cell)
  {
  }
}

// ---------- Depth: a copy of a module that has copying imports has copies of its own

module Leaf {
  class Node {
    constructor () { }
  }
}

module Middle {
  import Y := Leaf
  class Holder {
    constructor () { }
  }
}

module Top {
  import Leaf
  import Middle
  import X := Middle
  import Z := Middle

  lemma InnerCopiesDiffer(o: object)
    ensures o is X.Y.Node ==> !(o is Middle.Y.Node)
    ensures o is X.Y.Node ==> !(o is Z.Y.Node)
    ensures o is X.Y.Node ==> !(o is Leaf.Node)
  {
  }
}

module Top' refines Top {
  import Top

  lemma InnerCopiesAreNew(o: object)
    ensures o is X.Y.Node ==> !(o is Top.X.Y.Node)
    ensures o is X.Holder ==> !(o is Top.X.Holder)
  {
  }
}

// A module that the copied module declares or imports in the ordinary way is not copied.

module WithNested {
  import L = Leaf
  module Inner {
    class E { }
  }
  type Count = nat
}

module UsesNested {
  import WithNested
  import Leaf
  import X := WithNested

  lemma Shared(o: object, n: X.Count)
    ensures o is X.Inner.E <==> o is WithNested.Inner.E
    ensures o is X.L.Node <==> o is Leaf.Node
  {
    var m: WithNested.Count := n;
  }
}

// Through an abstract import, a copy is known only by its signature.

abstract module ThroughAbstractImport {
  import I : Importer

  function Sum(): int { I.X.One() + I.Z.Two() + I.Sum() }

  method Make() returns (c: I.X.Cell) {
    var colour;
    c, colour := I.Make();
  }
}

// ---------- Exports: what the name of a copy gives

module Sets {
  export
    provides A
  export More
    provides A, B
  export Other
    provides C
  function A(): int { 1 }
  function B(): int { 2 }
  function C(): int { 3 }
}

module OnlyNamed {
  export Named
    provides A
  function A(): int { 1 }
}

module ExportUser {
  export
    provides Default, Larger, Both, FromNamed

  import Default := Sets
  import Larger := Sets`More
  import Both := Sets`{Sets, Other}
  import FromNamed := OnlyNamed`Named  // a module with no default export set can be copied by naming a set

  function F(): int { Default.A() + Larger.A() + Larger.B() + Both.A() + Both.C() + FromNamed.A() }
}

module ExportClient {
  import ExportUser
  import Alias = ExportUser.Larger
  import WholeCopy = ExportUser.Default`Default  // the default export set of a copy has the copy's name

  function G(): int { ExportUser.Default.A() + ExportUser.Larger.B() + Alias.B() + WholeCopy.A() }
}

module ExportUser' refines ExportUser {
  function F'(): int { Larger.B() + Both.C() + FromNamed.A() }
}

// ---------- Opened copies

module OpenedUser {
  import opened X := Base

  function F(): int { One() + X.One() }
  function Pick(c: Colour): X.Colour { c }
}

module OpenedUser' refines OpenedUser {
  function F'(): int { One() + X.One() }
}

// ---------- Generics and the kinds of declarations

module Kinds {
  datatype List<T> = Nil | Cons(head: T, tail: List<T>)
  function Length<T>(l: List<T>): nat {
    if l.Nil? then 0 else 1 + Length(l.tail)
  }
  class Box<T(0)> {
    var value: T
    constructor (t: T)
      ensures value == t
    {
      value := t;
    }
  }
  trait {:termination false} {:axiom} Shape {
    function Sides(): int
  }
  class Triangle extends Shape {
    function Sides(): int { 3 }
    constructor () { }
  }
  newtype Digit = x: int | 0 <= x < 10
  codatatype Stream = More(rest: Stream)
  iterator Counter(n: nat) yields (x: nat) {
  }
  const Limit: int := 5
  type Opaque
  type Positive = x: int | x > 0 witness 1
  type Pair = (int, int)
}

module KindsUser {
  import X := Kinds
  import Z := Kinds

  method Use() {
    var l := X.Cons(1, X.Nil);
    assert X.Length(l) == 1;
    var b := new X.Box<int>(4);
    assert b.value == 4;
    var t := new X.Triangle();
    var s: X.Shape := t;
    assert s.Sides() == 3;
    assert X.Limit == 5 == Z.Limit;
    var d: X.Digit := 3;
    var p: X.Positive := 4;
    var q: Z.Positive := p;  // a subset type is its base type with a constraint, in every copy
    var pair: X.Pair := (1, 2);
    var pair': Z.Pair := pair;  // a type synonym is the type it stands for
  }

  lemma Differ(o: object)
    ensures o is X.Box<int> ==> !(o is Z.Box<int>)
    ensures o is X.Triangle ==> !(o is Z.Triangle)
  {
  }

  // A class of the importing module can extend a trait of a copy, under the rule for a trait of
  // another module.
  class Square extends X.Shape {
    function Sides(): int { 4 }
    constructor () { }
  }

  method UseSquare() {
    var q := new Square();
    var s: X.Shape := q;
    assert s.Sides() == 4;
  }
}

module KindsUser' refines KindsUser {
  method UseBoth() {
    var q := new Square();
    var t := new X.Triangle();
    var s: X.Shape, s': X.Shape := q, t;
    assert s.Sides() + s'.Sides() == 7;
  }
}

// ---------- Abstract modules

abstract module Spec {
  type Key
  function Hash(k: Key): nat
  lemma HashIsStable(k: Key)
    ensures Hash(k) == Hash(k)
  {
  }
}

abstract module TwoSpecs {
  import X := Spec  // a copy of an abstract module is abstract, so the importing module is too
  import Z := Spec

  function Both(a: X.Key, b: Z.Key): nat { X.Hash(a) + Z.Hash(b) }
}

// ---------- Costs: a copy is not verified again

module Costly {
  function Power(b: nat, e: nat): nat {
    if e == 0 then 1 else b * Power(b, e - 1)
  }
  lemma PowerAdds(b: nat, m: nat, n: nat)
    ensures Power(b, m + n) == Power(b, m) * Power(b, n)
  {
    if m != 0 {
      PowerAdds(b, m - 1, n);
    }
  }
}

// The six copies below add no proof to the count in the expected output.
module CostlyUser {
  import X := Costly
  import Y := Costly
  import Z := Costly
}

module CostlyUser' refines CostlyUser {
}
