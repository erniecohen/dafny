abstract module Spec {
  type T
  function Next(t: T): T
}
module Impl refines Spec {
  type T = int
  function Next(t: T): T { t + 1 }
}
abstract module Client {
  import X := Spec
  function Twice(t: X.T): X.T { X.Next(X.Next(t)) }
}
module Concrete refines Client {
  import X := Impl
  method Main() { print Twice(3), "\n"; }
}
