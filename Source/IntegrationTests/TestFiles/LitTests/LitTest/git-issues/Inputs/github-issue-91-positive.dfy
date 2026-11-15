// The reported program: an abstract type with a member, refined by a synonym of an abstract type.
module A {
  type O { predicate i() }
}
module B refines A {
  type T { predicate i() }
  type O = T
}

// Contracts of the abstract type's members are inherited by the target's members and checked there.
// As in any refinement, a signature repeats the refined one syntactically: the result type is O, not D.
module A2 {
  type O(==) {
    function F(): int ensures F() > 0
    static function Make(): O
  }
  method M(o: O) returns (r: int) ensures r > 0 { r := o.F(); }
}
module B2 refines A2 {
  datatype D = D(n: nat) {
    function F(): int { n + 1 }
    static function Make(): O { D(0) }
  }
  type O = D
  method Use() returns (r: int) ensures r > 0 { r := M(D.Make()); }
}

// A class target, with a method.
module A3 {
  type O { method Bump() returns (r: int) ensures r >= 0 }
}
module B3 refines A3 {
  class C {
    constructor () {}
    method Bump() returns (r: int) { r := 3; }
  }
  type O = C
}

// A generic, covariant abstract type, refined by a covariant datatype.
module A4 {
  type O<+X> { function Get(): seq<X> }
}
module B4 refines A4 {
  datatype L<+X> = L(s: seq<X>) { function Get(): seq<X> { s } }
  type O<+X> = L<X>
}

// A newtype target that keeps the promised characteristics.
module A5 {
  type O(==, 0) { function Twice(): int }
}
module B5 refines A5 {
  newtype N = x: int | 0 <= x < 100 { function Twice(): int { 2 * (this as int) } }
  type O = N
}

// A member with a body that the target does not declare is copied into the target.
module A6 {
  type O { function G(): int { 7 } }
  lemma L(o: O) ensures o.G() == 7 {}
}
module B6 refines A6 {
  class K {}
  type O = K
  lemma UseG(k: K) ensures k.G() == 7 {}
}
