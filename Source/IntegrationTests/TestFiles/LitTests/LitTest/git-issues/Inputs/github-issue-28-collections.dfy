replaceable module Spec {
  type T(==,!new,00)
  function {:axiom} Size(t: T): int
  function {:axiom} Pick(): T
}
module Impl replaces Spec {
  datatype T = A | B
  function Size(t: T): int { 32 }
  function Pick(): T { B }
}
module Client {
  import opened Spec
  function Identity<X>(x: X): X { x }
  function Twice(t: T): int { 2 * Size(Identity([t])[0]) }
  method DefaultValue() returns (t: T) { var values := new T[1]; t := values[0]; }

}
module App {
  import Client
  import opened Spec
  method Main() { var d := Client.DefaultValue(); print Client.Twice(Pick()), "\n"; }
}
