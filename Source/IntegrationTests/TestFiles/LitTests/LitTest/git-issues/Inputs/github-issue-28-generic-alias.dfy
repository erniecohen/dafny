replaceable module Spec {
  type T(==,!new,00)<X(==,!new,0)>
  function {:axiom} Size(t: T<int>): int
  function {:axiom} Pick(): T<int>
}
module Impl replaces Spec {
  type T<X(==,!new,0)> = seq<X>
  function Size(t: T<int>): int { 32 }
  function Pick(): T<int> { [3] }
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
