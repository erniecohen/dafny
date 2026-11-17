// RUN: %testDafnyForEachResolver --expect-exit-code=2 "%s"


module GiveT {
  type T = int
}

module Base {
  import opened GiveT

  ghost function f(x : T):int { 0 }
}

module Refined refines Base {
  type T = int // error: captures the T of the inherited f, which is GiveT.T (issue 410)

}

module ImportBase {
  import opened Base
}

module RefineImportBase refines ImportBase {
  import opened GiveT
}

module GiveT2 {
  type T = bool
}

module Refined2 refines GiveT {
  import opened GiveT2

  ghost function f(x: T): int // OK: T is GiveT.T (refinement is preferred to import)
}

module GiveF{
  ghost function {:opaque} f(): bool { true }
}

module BaseF{
  import opened GiveF

  lemma Test()
    ensures f() == true
  { reveal f(); }

}

module RefinedF refines BaseF {
  ghost function f(): bool { false } // error: captures the f of the inherited Test,
                               // which is GiveT.f; otherwise False proves false (issue 410)

  lemma False()
    ensures false
  { reveal f(); Test(); }
}
