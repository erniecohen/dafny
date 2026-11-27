module {:extern "Foreign"} A {
  import B
  method {:extern} Ping() returns (n: int)
}
module B { import A method Value() returns (n: int) { n := A.Ping(); } }
method Main() { var n := B.Value(); print n, "\n"; }
