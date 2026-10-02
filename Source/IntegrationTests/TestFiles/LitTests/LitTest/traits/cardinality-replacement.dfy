// RUN: %exits-with 2 %resolve --type-system-refresh --general-traits=datatype "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Api { trait {:termination false} V {} }
replaceable module Storage {
  import A = Api
  type F
}
module ConcreteStorage replaces Storage {
  type F = A.V -> bool
}
module Client {
  import A = Api
  import S = Storage
  datatype D extends A.V = Ground | D(f: S.F)
}
