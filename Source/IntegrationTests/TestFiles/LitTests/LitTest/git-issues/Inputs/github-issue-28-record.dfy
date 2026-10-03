replaceable module Spec {
  type T(==,!new,00)
  function {:axiom} Size(t: T): int
  function {:axiom} Pick(): T
}
module Impl replaces Spec {
  datatype T = T(n: int)
  function Size(t: T): int { 32 }
  function Pick(): T { T(3) }
}
module Client {
  import opened Spec
  function Identity<X>(x: X): X { x }
  method Default<X(0)>() returns (x: X) {}
  function Twice(t: T): int { 2 * Size(Identity([t])[0]) }
  method DefaultSize() returns (n: int) {
    var t := Default<T>();
    var values := new T[1];
    n := Size(t) + Size(values[0]);
  }
}
module App {
  import Client
  import opened Spec
  method Main() { var n := Client.DefaultSize(); print if n == 64 then Client.Twice(Pick()) else 0, "\n"; }
}
