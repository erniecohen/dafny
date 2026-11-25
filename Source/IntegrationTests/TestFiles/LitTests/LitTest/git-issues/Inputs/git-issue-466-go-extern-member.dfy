module A {
  import B
  method {:extern} Plain(x: int) returns (y: int)
  function {:extern "Renamed"} Named(x: int): int
  method {:extern} Echo(x: B.Box) returns (y: B.Box)
  class {:extern "Actual"} Foreign {
    constructor() {}
    function Local(): int { B.Seed() }
    function {:extern} Value(): int
  }
  class Mixed {
    constructor() {}
    method {:extern "RenamedInstance"} ForeignValue() returns (y: int)
  }
  function F(): int { B.Seed() }
  method Test() {
    var b := new B.Box(11);
    var same := Echo(b);
    var n := Plain(Named(same.value));
    var x := new Foreign();
    var m := new Mixed();
    var y := m.ForeignValue();
    print n, " ", x.Value(), " ", x.Local(), " ", y, " ", F(), "\n";
  }
}
module B {
  import A
  class Box {
    var value: int
    constructor(v: int) { value := v; }
  }
  function Seed(): int { 7 }
  method Test() {
    var n := A.Plain(A.Named(2));
    print n, " ", A.F(), "\n";
    A.Test();
  }
}
module Test {
  import A
  import B
  method Main() {
    B.Test();
    var n := A.Plain(A.Named(5));
    print n, "\n";
  }
}
