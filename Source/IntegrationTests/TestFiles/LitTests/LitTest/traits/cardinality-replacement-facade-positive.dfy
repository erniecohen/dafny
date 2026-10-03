// RUN: %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module A {
  trait {:termination false} V {}
  type F
}
module B refines A { type F = int }
replaceable module P {
  import M : A
  export provides M
}
module C replaces P { import M = B }
abstract module Client {
  import Pkg = P
  datatype D extends Pkg.M.V = Ground | D(f: Pkg.M.F)
}
