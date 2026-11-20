module {:extern "Foreign", "Foreign"} A {
  import B
  method {:extern} Plain(x: int) returns (y: int)
  method {:extern "Renamed"} Named(x: int) returns (y: int)
  function {:extern "Foreign.Tools", "Twice"} Twice(x: int, ghost ignored: int): int
  function {:extern "Foreign.Tools", "Identity"} Identity<T(0)>(x: T): T
  function {:extern "Foreign.Tools", "Default"} Default<T(0)>(): T
  method {:extern "Foreign.Tools", "Triple"} Triple(x: int) returns (y: int)
  class {:extern "Actual"} Native {
    static method {:extern} Make() returns (v: Native)
    function {:extern} Value(): int
  }
  class Local {
    static function F(): int { B.Seed() }
  }
  function F(): int { B.Seed() }
  method Test() {
    var n := Plain(F());
    print n, "\n";
  }
}
module B {
  import KeywordNative
  import A
  import C
  class {:extern "Foreign", "Actual"} Alias {
    static method {:extern} Make() returns (v: Alias)
    function {:extern} Value(): int
  }
  newtype Wrapped = int {
    function {:extern "Foreign.Tools", "AddTen"} AddTen(): int
  }
  function Seed(): int { 7 }
  method Test() {
    var n := A.Plain(A.F());
    var m := A.Named(n);
    var x := A.Native.Make();
    var alias := Alias.Make();
    var triple := A.Triple(m);
    var other := C.Get();
    var wrapped: Wrapped := 5;
    print n, " ", m, " ", x.Value(), " ", alias.Value(), " ", A.Local.F(), " ",
      A.Twice(triple, 99), " ", other, " ", KeywordNative.Five(), " ",
      A.Identity<int>(42) + 1, " ", A.Default<int>() + 2, " ", wrapped.AddTen(), "\n";
    A.Test();
  }
}
module {:extern "OtherForeign"} C {
  import A
  method {:extern} Get() returns (y: int)
}
module Test {
  import ZeroClient
  import A
  import B
  import C
  method Execute() {
    B.Test();
    ZeroClient.Test();
    var x := A.Native.Make();
    var alias := B.Alias.Make();
    var y := A.Triple(A.Twice(x.Value() + alias.Value(), 0));
    var other := C.Get();
    print y, " ", other, "\n";
  }
}
module {:extern} NativeZero {
  import ZeroClient
  method {:extern} Get() returns (y: int)
}
module ZeroClient {
  import NativeZero
  method Test() {
    var n := NativeZero.Get();
    print n, "\n";
  }
}
module {:extern "map"} KeywordNative {
  import B
  function {:extern "map.Tools", "Five"} Five(): int
}
