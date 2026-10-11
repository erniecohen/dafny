replaceable module Spec {
 trait {:termination false} Base { method Value() returns (n: int) { n := 64; } }
}
module Client {
 import Spec
 class Child extends Spec.Base {}
 method Main() { var c := new Child; var n := c.Value(); print n, "\n"; }
}
module Impl replaces Spec {}
