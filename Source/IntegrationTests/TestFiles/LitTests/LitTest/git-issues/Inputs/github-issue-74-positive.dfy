// Native integer calls, without bitvector conversions.
module IntegerCalls {
  function Identity(x: int): int { x }
  lemma Numerals()
    ensures Identity(42) == 42 && Identity(42) == Identity(42)
    ensures Identity(0) == 0 && Identity(7) == 7
    ensures Identity(-7) == -7
    ensures Identity(340282366920938463463374607431768211457) == 340282366920938463463374607431768211457
    ensures Identity(-340282366920938463463374607431768211457) == -340282366920938463463374607431768211457
  { }
  lemma ManyCalls()
    ensures Identity(1) + Identity(2) + Identity(3) + Identity(4) + Identity(5) == 15
    ensures Identity(6) + Identity(7) + Identity(8) + Identity(9) + Identity(10) == 40
    ensures Identity(11) + Identity(12) + Identity(13) + Identity(14) + Identity(15) == 65
    ensures Identity(16) + Identity(17) + Identity(18) + Identity(19) + Identity(20) == 90
  { }
}

module AnotherModule {
  function Identity(x: int): int { x }
  lemma Numerals() ensures Identity(7) == 7 && Identity(0) == 0 { }
}

// These calls are deliberately outside the collected shape.
module ExcludedArguments {
  function Identity(x: int): int { x }
  function RealIdentity(x: real): real { x }
  function BoxedIdentity<T>(x: T): T { x }
  lemma Arguments(x: int)
    ensures Identity(x) == x
    ensures Identity(x + 101) == x + 101
    ensures RealIdentity(1.25) == 1.25
    ensures BoxedIdentity<int>(999) == 999
  { }
}
