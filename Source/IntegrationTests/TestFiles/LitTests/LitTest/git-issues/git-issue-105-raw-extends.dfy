// Member invariants (https://github.com/erniecohen/dafny/issues/105): "raw D" and raw members of a
// datatype that extends traits, directly, through a diamond and generically, and of mutually
// recursive datatypes. A raw value converts to a parent trait as to D itself: the resolver accepts
// the conversion, and the verifier is to check it.
// RUN: %resolve --member-invariants --print-mode NoIncludes --rprint:- "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Extends {
  trait Shape {
    function Area(): int
    predicate Valid()
  }

  // Valid overrides a trait member and is raw; Area is neither applied to a raw value nor raw.
  datatype Square extends Shape = Square(side: int) {
    invariant Valid()
    function Area(): int { side * side }
    predicate Valid() { side >= 0 }
  }

  method M(s: Square, r: raw Square) returns (a: Shape, b: Shape, n: int) {
    a := s;
    b := r;
    n := s.Area() + a.Area();
    var v := r.Valid() && a.Valid();
  }
}

module Diamond {
  trait Top {
    function Id(): int
  }
  trait Left extends Top { }
  trait Right extends Top { }

  datatype Both extends Left, Right = Both(x: int, y: int) {
    invariant Id() == x
    function Id(): int { x }
    predicate Sym() { x == y }
  }

  method M(b: Both, r: raw Both) returns (t: Top, l: Left, rt: Right, s: seq<raw Both>) {
    t, l, rt := b, b, r;
    s := [r, b];
    var i := r.Id() + t.Id() + l.Id();
    var q := b.Sym();
  }
}

module GenericExtends {
  trait Container<T> {
    function Elems(): seq<T>
  }

  datatype Bounded<T> extends Container<T> = Bounded(s: seq<T>, cap: nat) {
    invariant |Elems()| <= cap
    function Elems(): seq<T> { s }
  }

  method M<T>(b: Bounded<T>, r: raw Bounded<T>) returns (c: Container<T>, d: Container<T>) {
    c := b;
    d := r;
  }
}

// Mutually recursive datatypes, each with an invariant. Only the invariants and raw members of a type
// make its members raw: an invariant of Tree applies Forest.Count to a raw Forest, but Count is not raw.
// Applying it there is a conversion of the raw value to Forest, which the verifier is to check.
module Mutual {
  datatype Tree = Leaf(key: int) | Node(key: int, kids: Forest) {
    invariant Node? ==> kids.Above(key)
    invariant Forest.Nil.Count() == 0
    function Size(): nat { if Leaf? then 1 else 1 + kids.Size() }
  }

  datatype Forest = Nil | Cons(head: Tree, tail: Forest) {
    invariant Cons? ==> tail.Count() < 100
    predicate Above(n: int) { Cons? ==> head.key > n && tail.Above(n) }
    function Count(): nat { if Nil? then 0 else 1 + tail.Count() }
    function Size(): nat { if Nil? then 0 else head.Size() + tail.Size() }
  }

  method M(t: Tree, f: Forest) returns (r: raw Forest, u: raw Tree) {
    r := Cons(t, f);
    u := Node(0, r);
  }
}
