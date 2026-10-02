// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %exits-with 2 %verify --filter-symbol=Selected "%s" >> "%t"
// RUN: %exits-with 2 %verify --dont-verify-dependencies "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

include "Inputs/cardinality-library.dfy"
module Client {
  import A = Api
  import S = Storage
  datatype D extends A.V = D(b: S.B)
  method Selected() { assert true; }
}
