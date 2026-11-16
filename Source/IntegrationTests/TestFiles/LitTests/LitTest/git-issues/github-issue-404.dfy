// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 0 %verify --legacy-elided-override-checks "%s" >> "%t"
// RUN: %exits-with 0 %verify "%S/Inputs/github-issue-404-positive.dfy" >> "%t"
// RUN: %exits-with 0 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-404-positive.dfy" >> "%t"
// RUN: %exits-with 0 %verify "%S/Inputs/github-issue-404-unchanged.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-elided-override-checks "%S/Inputs/github-issue-404-unchanged.dfy" >> "%t"
// RUN: %exits-with 4 %verify --general-traits=full "%S/Inputs/github-issue-404-general-traits.dfy" >> "%t"
// RUN: %exits-with 0 %verify --general-traits=full --legacy-elided-override-checks "%S/Inputs/github-issue-404-general-traits.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-404-redeclared.dfy" >> "%t"
// RUN: %exits-with 4 %verify --legacy-elided-override-checks "%S/Inputs/github-issue-404-redeclared.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false "%S/Inputs/github-issue-404-library.dfy" --output "%t.doo" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --library "%t.doo" "%S/Inputs/github-issue-404-client.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --legacy-elided-override-checks --library "%t.doo" "%S/Inputs/github-issue-404-client.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/404): a refining module could strengthen the
// contract of a trait member, or give a class a parent, while it inherited the overriding class member
// unchanged.  The override check of that class member asserts at the member's origin, which the
// refining module inherits, so its asserts became assumes and the check was never made: each method M
// below proved false.  Now a refining module makes the override check of an inherited member whenever
// it changes either member of the pair or adds the extension, under both resolvers.  Inherited pairs it
// does not change are not checked again (Inputs/github-issue-404-unchanged.dfy verifies as many tasks
// as under the legacy option), and strengthened contracts that the class member meets still verify
// (Inputs/github-issue-404-positive.dfy).  Under the hidden --legacy-elided-override-checks the checks
// are skipped again and every M verifies.  Inputs/github-issue-404-general-traits.dfy does the same for
// datatype and newtype members under --general-traits=full.  A class member that the refining module
// redeclares, even with its contract unchanged, is the module's own, so its override check is made under
// both rules (Inputs/github-issue-404-redeclared.dfy).

// The issue's program: a lemma.
module LemmaBase {
  trait S { lemma L() ensures true }
  class C extends S { constructor () {} lemma L() ensures true { } }
}
module Lemma refines LemmaBase {
  trait S ... { lemma L... ensures false }
  method M() ensures false { var s: S := new C(); s.L(); }
}

// A function.
module FunctionBase {
  trait S { function F(): int ensures true }
  class C extends S { constructor () {} function F(): int { 0 } }
}
module Function refines FunctionBase {
  trait S ... { function F... ensures F() > 0 }
  method M() ensures false { var s: S := new C(); assert s.F() > 0; assert s.F() == 0; }
}

// An abstract type with a member, refined by a class that adds a parent.
module AbstractTypeBase {
  trait S { lemma L() ensures false }
  type T {
    lemma L() ensures true { }
  }
}
module AbstractType refines AbstractTypeBase {
  class T extends S {
    constructor () {}
  }
  method M() ensures false { var s: S := new T(); s.L(); }
}

// A generic trait.
module GenericBase {
  trait S<X> { lemma L(x: X) ensures true }
  class C extends S<int> { constructor () {} lemma L(x: int) ensures true { } }
}
module Generic refines GenericBase {
  trait S<X> ... { lemma L... ensures false }
  method M() ensures false { var s: S<int> := new C(); s.L(0); }
}

// A diamond: the class reaches the strengthened trait along two paths.
module DiamondBase {
  trait Top { lemma L() ensures true }
  trait Left extends Top { }
  trait Right extends Top { }
  class C extends Left, Right { constructor () {} lemma L() ensures true { } }
}
module Diamond refines DiamondBase {
  trait Top ... { lemma L... ensures false }
  method M() ensures false { var t: Top := new C(); t.L(); }
}

// Two parents, both strengthened: each pair fails its own check.
module TwoParentsBase {
  trait S1 { lemma L1() ensures true }
  trait S2 { lemma L2() ensures true }
  class C extends S1, S2 { constructor () {} lemma L1() ensures true { } lemma L2() ensures true { } }
}
module TwoParents refines TwoParentsBase {
  trait S1 ... { lemma L1... ensures false }
  trait S2 ... { lemma L2... ensures false }
  method M1() ensures false { var s: S1 := new C(); s.L1(); }
  method M2() ensures false { var s: S2 := new C(); s.L2(); }
}

// A chain of refinements that strengthens the contract at each step.  The class member meets the first
// two strengthenings, so Chain1 and Chain2 verify their checks, and Chain3 fails its own.
module Chain0 {
  trait S { function F(): int ensures true }
  class C extends S { constructor () {} function F(): int ensures F() == 5 { 5 } }
}
module Chain1 refines Chain0 {
  trait S ... { function F... ensures F() > 0 }
}
module Chain2 refines Chain1 {
  trait S ... { function F... ensures F() > 3 }
}
module Chain3 refines Chain2 {
  trait S ... { function F... ensures F() > 10 }
  method M() ensures false { var s: S := new C(); assert s.F() > 10; assert s.F() == 5; }
}

// A trait member that overrides a member of a parent trait.
module TraitOverrideBase {
  trait Top { lemma L() ensures true }
  trait Mid extends Top { lemma L() ensures true { } }
  class C extends Mid { constructor () {} }
}
module TraitOverride refines TraitOverrideBase {
  trait Top ... { lemma L... ensures false }
  method M() ensures false { var t: Top := new C(); t.L(); }
}

// A refining module that replaces an abstract import gives the inherited class a different parent.
abstract module Interface {
  trait {:termination false} {:axiom} S { lemma L() ensures true }
}
module Strong refines Interface {
  trait {:termination false} {:axiom} S ... { lemma L... ensures false }
}
abstract module ImportBase {
  import I : Interface
  class C extends I.S { constructor () {} lemma L() ensures true { } }
}
module Import refines ImportBase {
  import I = Strong
  method M() ensures false { var s: I.S := new C(); s.L(); }
}

// A compiled method with an out-parameter.
module MethodBase {
  trait S { method N() returns (r: int) ensures r >= 0 }
  class C extends S { constructor () {} method N() returns (r: int) ensures r >= 0 { r := 0; } }
}
module Method refines MethodBase {
  trait S ... { method N... ensures false }
  method M() ensures false { var s: S := new C(); var r := s.N(); }
}

// An abstract type refined into a class that adds a parent, where the class member's precondition is
// stronger than the trait member's.
module PreconditionBase {
  trait S { method N(k: int) ensures k == 0 }
  type T {
    method N(k: int) requires k == 0 ensures k == 0 { }
  }
}
module Precondition refines PreconditionBase {
  class T extends S {
    constructor () {}
  }
  method M() ensures false { var s: S := new T(); s.N(1); }
}

// The same, where the class member's decreases is larger than the trait member's.  A call through the
// trait is checked against the trait's decreases, so Aux may call S.Le, and the cycle through T.Le never
// decreases.  Every other termination check on the cycle holds.
abstract module DecreasesBase {
  trait S { lemma Le(n: nat) requires n > 0 ensures false decreases 0 }
  type T {
    lemma Le(n: nat) requires n > 0 ensures false decreases n { Aux(this, n); }
  }
  lemma Aux(t: T, n: nat) requires n > 0 ensures false decreases n, 0
}
module Decreases refines DecreasesBase {
  class T extends S {
    constructor () {}
  }
  lemma Aux... { var s: S := t; s.Le(n); }
  method M() ensures false { var t := new T(); t.Le(1); }
}

// A twostate lemma.
module TwoStateBase {
  trait S { twostate lemma L() ensures true }
  class C extends S { constructor () {} twostate lemma L() ensures true { } }
}
module TwoState refines TwoStateBase {
  trait S ... { twostate lemma L... ensures false }
  method M(s: S) ensures false { s.L(); }
}

// A least lemma.
module LeastBase {
  trait S { least lemma L(x: nat) ensures true }
  class C extends S { constructor () {} least lemma L(x: nat) ensures true { } }
}
module Least refines LeastBase {
  trait S ... { least lemma L... ensures false }
  method M() ensures false { var s: S := new C(); s.L(0); }
}
