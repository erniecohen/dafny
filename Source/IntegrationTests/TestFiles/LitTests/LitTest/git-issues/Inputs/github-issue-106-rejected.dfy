// Renamings that are rejected.

abstract module A {
  const k: int
  function F(x: int): int { x + k }
  ghost function Q(x: int): bool { forall y: int {:trigger y + 0} :: y == x ==> y + 0 == x }
  function Two(x: int, y: int): int { x - y }
  method M(a: int) returns (b: int) { var c := 0; b := a + c; }
  function G<T>(t: T): T { var u: T := t; u }
  type V
  function H<W>(v: W): V
  function Tw(x: int): int
  function Let(x: int): int { var y := x + 1; y + x }
  method Out(a: int) returns (b: int) { b := a; }
  class C<U> {
    ghost function P<S>(u: U, s: S): bool { true }
    function Id<S>(s: S): S { s }
  }
}

// the new name is that of a constant that the inherited body uses
module B0 refines A { function F(k: int): int }
// the new name is that of a bound variable of the inherited body
module B1 refines A { ghost function Q(y: int): bool }
// two parameters with one name
module B2 refines A { function Two(z: int, z: int): int }
// the new name is that of a local variable of the inherited body
module B3 refines A { method M(c: int) returns (b: int) }
// a type parameter takes the name of a type that the inherited signature uses
module B4 refines A { function H<V>(v: V): V }
// the types must still agree
module B5 refines A { function Tw(y: nat): int }
// the number of parameters must still agree
module B6 refines A { function Two(x: int): int }
// the new name is that of a let-bound variable
module B7 refines A { function Let(y: int): int }
// an in-parameter takes the name of an out-parameter that keeps its name
module B8 refines A { method Out(b: int) returns (b: int) }
// a member's type parameter takes the name of the class's
module B9 refines A { class C<U> ... { ghost function P<U>(u: U, s: U): bool } }
// a class's type parameter takes the name of a member's
module B10 refines A { class C<S> ... { } }

module B11 refines A {
  function Two(p: int, q: int): int
  function G<T>(T: T): T
  method Client() {
    // the local name is not a label
    var r := Two(p := 1, y := 2);
    var s := G(T := 3);
    // a label is given once
    var t := Two(1, x := 2);
  }
}
