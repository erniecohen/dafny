// RUN: %audit --allow-axioms "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Instance contracts must be implemented and are not assumptions.
trait T {
  lemma L(x: int) ensures x + 0 == x
  method M() returns (r: int) ensures r == 0
  ghost function G(): (r: int) ensures r == 0
  function F(): (r: int) ensures r == 0
  ghost function N(): nat
}

class C extends T {
  lemma L(x: int) ensures x + 0 == x { }
  method M() returns (r: int) ensures r == 0 { r := 0; }
  ghost function G(): (r: int) ensures r == 0 { 0 }
  function F(): (r: int) ensures r == 0 { 0 }
  ghost function N(): nat { 0 }
}

// Static trait members cannot be implemented by an overriding class.
trait StaticMembers {
  static lemma StaticLemma() ensures true
  static ghost function StaticFunction(): (r: int) ensures r == 0
}

// Bodyless class members still introduce assumptions.
class BodylessClass {
  lemma ClassLemma() ensures true
  ghost function ClassFunction(): (r: int) ensures r == 0
}

// Explicit axioms on instance members are still reported.
trait ExplicitAssumptions {
  lemma {:axiom} AxiomLemma() ensures true
  ghost function {:axiom} AxiomFunction(): (r: int) ensures r == 0
}
