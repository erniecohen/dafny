// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module A { trait {:termination false} V {} type F }
module B1 refines A { type F = int }
module B2 refines A { type F = V -> bool }
replaceable module P1 { import M : A export provides M }
replaceable module P2 { import M : A export provides M }
module C1 replaces P1 { import M = B1 }
module C2 replaces P2 { import M = B2 }
abstract module Client {
  import Pkg1 = P1
  import Pkg2 = P2
  datatype D1 extends Pkg1.M.V = Ground1 | D1(f: Pkg1.M.F)
  datatype D2 extends Pkg2.M.V = Ground2 | D2(f: Pkg2.M.F)
}
