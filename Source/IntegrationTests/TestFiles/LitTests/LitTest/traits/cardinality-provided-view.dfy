// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Api { trait {:termination false} V {} }
module Storage {
  import A = Api
  export provides B
  datatype B = B(f: A.V -> bool)
}
module Client {
  import A = Api
  import S = Storage
  datatype D extends A.V = D(b: S.B)
}
