replaceable module Spec {
  type T(==,!new,00)
  function {:axiom} Size(t: T): int
  function {:axiom} Pick(): T
}
module Client {
  import opened Spec
  function Twice(t: T): int { 2 * Size(t) }
}
module App {
  import Client
  import opened Spec
  method Main() { print Client.Twice(Pick()), "\n"; }
}
module Impl replaces Spec {
  import Helper
  newtype T = x: int | 0 <= x < 4
  function Size(t: T): int { Helper.Value() }
  function Pick(): T { 3 }
}

module Helper { function Value(): int { 32 } }
