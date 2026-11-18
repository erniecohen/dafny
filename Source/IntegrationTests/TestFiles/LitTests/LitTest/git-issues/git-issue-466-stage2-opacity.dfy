// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), second
// stage: the default opacity of functions.  With autoRevealDependencies, a function of another
// member of the group is revealed where the importing member sees its body, and is not revealed
// where the export set only provides it, as for a function of a module that is imported from
// outside a group.  With opaque, nothing is revealed.  Each error below is at a lemma named Bad.
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules --default-function-opacity autoRevealDependencies "%s" > "%t"
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules --default-function-opacity opaque "%s" >> "%t"
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// every body is seen: the dependencies in the other member are revealed
module Rev1 {
  import Rev2
  function F(n: nat): nat { if n == 0 then 0 else 1 + Rev2.G(n - 1) }
  lemma LF(n: nat)
    ensures F(n) == n
    decreases n, 1
  { reveal F(); if n != 0 { Rev2.LG(n - 1); } }
  lemma Bad()
    ensures F(3) == 4
  { }
}
module Rev2 {
  import Rev1
  function G(n: nat): nat { if n == 0 then 0 else 1 + Rev1.F(n - 1) }
  lemma LG(n: nat)
    ensures G(n) == n
    decreases n, 1
  { reveal G(); if n != 0 { Rev1.LF(n - 1); } }
  lemma Use()
    ensures Rev1.F(2) == 2
  { reveal Rev1.F(), G(); }
}

// Hide2 provides G and reveals K: Hide1 gets K and never G
module Hide1 {
  import Hide2
  function F(n: nat): nat { if n == 0 then 0 else 1 + Hide2.G(n - 1) }
  lemma Bad()
    ensures F(1) == 1 // the value of G(0) is not known here
  { reveal F(); }
  lemma Zero()
    ensures F(0) == 0
  { reveal F(); }
  function H(n: nat): nat { Hide2.K(n) }
  lemma ViaRevealed()
    ensures H(3) == 4
  { reveal H(), Hide2.K(); }
}
module Hide2 {
  export provides G, Hide1 reveals K
  import Hide1
  function G(n: nat): nat { if n == 0 then 0 else 1 + Hide1.F(n - 1) }
  function K(n: nat): nat { n + 1 }
  lemma Inside()
    ensures G(1) == 1
  { reveal G(), Hide1.F(); }
}
