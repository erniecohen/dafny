// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Api { trait {:termination false} V {} }
module Storage {
  import A = Api
  export Opaque provides B, A
  export Transparent provides A reveals B, F
  type F = A.V -> bool
  datatype B = B(f: F)
}
module Client {
  import A = Api
  import opened O = Storage`Opaque
  import opened R = Storage`Transparent
  datatype D extends A.V = D(hidden: O.B, transparent: R.B)
}
