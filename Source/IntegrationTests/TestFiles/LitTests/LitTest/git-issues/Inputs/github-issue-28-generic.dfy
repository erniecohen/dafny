replaceable module Spec {
  type T<X(==,!new,0)>(==,!new,00)
  function {:axiom} Size(t: T<int>): int
  function {:axiom} Pick(): T<int>
}
module Impl replaces Spec {
  datatype T<X(==,!new,0)> = T(n: X)
  function Size(t: T<int>): int { 32 }
  function Pick(): T<int> { T(3) }
}
module Client {
  import opened Spec
  function Twice(t: T<int>): int { 2 * Size(t) }
}
module App {
  import Client
  import opened Spec
  method Main() { print Client.Twice(Pick()), "\n"; }
}
