module {:extern "Foreign"} A {
  import B
  class {:extern "Actual"} Native {
    static method {:extern} Make() returns (n: Native)
    function Local(): int { B.Value() }
  }
}
module B { import A function Value(): int { 1 } }
method Main() { var n := A.Native.Make(); print n.Local(), "\n"; }
