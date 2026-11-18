// Member invariants (https://github.com/erniecohen/dafny/issues/105): the syntax of 'invariant'
// members, its printing, and resolution of invariants as ghost predicates of the enclosing type.  The
// program is printed and the printed program resolved again.  'raw' and 'witness' remain ordinary
// identifiers wherever they were before.
// RUN: %resolve --member-invariants --print "%t.dfy" "%s" > "%t"
// RUN: %resolve --member-invariants "%t.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Basics {
  datatype Interval = Interval(lo: int, hi: int) {
    invariant lo <= hi
    invariant {:my_attribute} hi - lo < 1000
    predicate Contains(x: int) { lo <= x <= hi }
    function Width(): nat { hi - lo }
  }

  // An invariant may use the type's own members, const fields, quantifiers and matches.
  datatype Shape = Circle(r: int) | Rect(w: int, h: int) {
    const Big := 100
    invariant match this
      case Circle(r) => 0 < r < Big
      case Rect(w, h) => 0 < w && 0 < h
    invariant forall i :: 0 <= i < 3 ==> Scaled(i) >= 0
    function Scaled(k: int): int { match this case Circle(r) => k * r case Rect(w, h) => k * w * h }
  }

  // Mutually recursive datatypes, each with an invariant.
  datatype Tree = Node(value: int, kids: Forest) {
    invariant value >= 0
  }
  datatype Forest = Nil | Cons(head: Tree, tail: Forest) {
    invariant Cons? ==> head.value <= 100
  }

  // Generic datatypes.
  datatype Bounded<T> = Bounded(xs: seq<T>) {
    invariant |xs| < 10
  }
  datatype Same<T(==)> = Same(a: T, b: T) {
    invariant a == b
  }

  // Types whose invariant no value satisfies.
  datatype Empty = Empty(x: int) {
    invariant false
  }
  newtype EmptyNat = x: int | 0 <= x {
    invariant false
  }

  // Other kinds of types.
  newtype Small = x: int | 0 <= x < 100 witness 7 {
    invariant this != 13
  }
  type Abstract {
    invariant true
  }
  class Cell {
    const k: int
    const a: array<int>
    invariant k > 0 && a.Length > k
    constructor () {
      k := 1;
      a := new int[2];
    }
  }

  // 'raw' is still an identifier, and 'witness' is still a clause of a subset type.
  type raw = int
  type Witnessed = x: int | x != 0 witness 1
  method M(raw: raw) returns (r: raw) {
    r := raw;
  }
}

// Traits, with an 'extends' diamond.
module Traits {
  trait Top {
    invariant true
  }
  trait Left extends Top {
    const l: int
    invariant l >= 0
  }
  trait Right extends Top {
    const r: int
    invariant r >= 0
  }
  class Bottom extends Left, Right {
    invariant l + r < 10
    constructor () {
      l, r := 1, 2;
    }
  }
  datatype Pt extends Top = Pt(x: int) {
    invariant x != 0
  }
}

// A refining declaration inherits the invariants of the one it refines and may add more: an invariant
// is anonymous, so it never refines another.  (Datatype refinement is in git-issue-105-refinement.dfy.)
abstract module Abstract {
  datatype Positive = Positive(n: int) {
    invariant n > 0
  }
  type Hidden {
    invariant true
  }
  class Counter {
    const limit: int
    invariant limit > 0
    constructor () {
      limit := 1;
    }
  }
  trait Named {
    const name: string
    invariant |name| > 0
  }
}
module Concrete refines Abstract {
  datatype Hidden = Hidden(b: bool) {
    invariant b || !b
    invariant true
  }
  class Counter ... {
    invariant limit < 10
  }
  trait Named ... {
    invariant |name| < 10
  }
  function Get(p: Positive): int { p.n }
}

// An invariant or a witness may follow a method, a function or a lemma without a body.
module Bodiless {
  datatype D = D(x: int) {
    method M()
    invariant x > 0
    function F(): int
    invariant x > 1
    predicate P()
    witness D(2)
  }
  trait T {
    const k: int
    lemma L()
    invariant k > 0
    twostate predicate Q()
    invariant k > 1
  }
}
