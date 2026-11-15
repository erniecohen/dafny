module Spec {
  datatype T = A | B
  function Size(t: T): int { 32 }
  function Pick(): T { B }
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
