replaceable module Spec {
  type T(==,!new,00)
  function {:axiom} Size(t: T): int
  function {:axiom} Pick(): T
}
abstract module Shared {
  import opened Spec
  function Twice(t: T): int { 2 * Size(t) }
}
module Client refines Shared {}

module App {
  import Client
  import opened Spec
  method Main() { print Client.Twice(Pick()), "\n"; }
}
module Implementations {
module Impl replaces Spec {
  newtype T = x: int | 0 <= x < 4
  function Size(t: T): int { 32 }
  function Pick(): T { 3 }
}
}
