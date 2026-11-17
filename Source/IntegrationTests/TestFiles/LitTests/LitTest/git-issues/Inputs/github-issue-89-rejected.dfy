// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): what
// --inherited-trait-axioms rejects.

// {:axiom} on a bodiless compiled trait member: there would be no code to run.
module Compiled {
  trait T {
    method {:axiom} M() returns (r: int) ensures r > 0  // error
    function {:axiom} F(): int ensures F() > 0  // error
    predicate {:axiom} P()  // error
  }
}

// {:axiom} on a bodiless extreme predicate or lemma.
module Extreme {
  trait T {
    least predicate {:axiom} Least(x: int)  // error
    greatest predicate {:axiom} Greatest(x: int)  // error
    least lemma {:axiom} LeastLemma(x: int) requires Least(x) ensures x == x  // error
    greatest lemma {:axiom} GreatestLemma(x: int) ensures Greatest(x)  // error
  }
}

// A subtrait redeclares an axiom without {:axiom}: classes below the subtrait must implement it again.
module Redeclared {
  trait T {
    lemma {:axiom} L() ensures 0 < 1
  }
  trait U extends T {
    lemma L() ensures 0 < 1
  }
  class BelowT extends T { }
  class BelowU extends U { }  // error: does not implement U.L
}

// Two parents declare one name: the existing clash, whether or not the members are axioms.
module Clash {
  trait A { lemma {:axiom} L() ensures 0 < 1 }
  trait B { lemma {:axiom} L() ensures 0 < 1 }
  class C extends A, B { }  // error
}

// A bodiless ghost member without {:axiom} must still be implemented.
module NoAttribute {
  trait T {
    lemma L() ensures 0 < 1
    ghost function F(): int
  }
  class C extends T { }  // error (twice)
}
