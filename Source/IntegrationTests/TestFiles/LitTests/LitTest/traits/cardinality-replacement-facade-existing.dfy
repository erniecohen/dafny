// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Ordinary resolution already forbids compiled access through an abstract
// module facade. The separate abstract-client fixture exercises admission.
abstract module A {
  trait {:termination false} V {}
  type F
}
module B refines A { type F = V -> bool }
replaceable module P {
  import M : A
  export provides M
}
module C replaces P { import M = B }
module Client {
  import Pkg = P
  datatype D extends Pkg.M.V = Ground | D(f: Pkg.M.F)
}
