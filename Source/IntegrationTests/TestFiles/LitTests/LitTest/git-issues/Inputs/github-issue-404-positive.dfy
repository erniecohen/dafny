// Part of https://github.com/erniecohen/dafny/issues/404 (see ../github-issue-404.dfy): a refining module
// that strengthens a trait member makes the override check of the inherited class member, and the check
// passes when the class member meets the stronger contract.

module LemmaBase {
  trait S { lemma L(x: int) ensures true }
  class C extends S { constructor () {} lemma L(x: int) ensures x * 0 == 0 { } }
}
module Lemma refines LemmaBase {
  trait S ... { lemma L... ensures x * 0 == 0 }
  method M() { var s: S := new C(); s.L(3); assert 3 * 0 == 0; }
}

module ChainBase {
  trait S { function F(): int ensures true }
  class C extends S { constructor () {} function F(): int ensures F() == 5 { 5 } }
}
module Chain1 refines ChainBase {
  trait S ... { function F... ensures F() > 0 }
}
module Chain2 refines Chain1 {
  trait S ... { function F... ensures F() > 3 }
  method M() { var s: S := new C(); assert s.F() > 3; }
}

module GenericBase {
  trait S<X> { lemma L(x: X) ensures true }
  class C extends S<int> { constructor () {} lemma L(x: int) ensures x + 0 == x { } }
}
module Generic refines GenericBase {
  trait S<X> ... { lemma L... ensures x == x }
}

module TraitOverrideBase {
  trait Top { lemma L(x: int) ensures true }
  trait Mid extends Top { lemma L(x: int) ensures x - x == 0 { } }
  class C extends Mid { constructor () {} }
}
module TraitOverride refines TraitOverrideBase {
  trait Top ... { lemma L... ensures x - x == 0 }
}
