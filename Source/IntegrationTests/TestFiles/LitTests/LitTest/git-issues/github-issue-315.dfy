// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/315): a proof-form forall statement in
// a statement expression outside a method or function body (a constant initializer, a subset-type
// or newtype constraint or witness, a formal's default value, an iterator body) crashed the
// verifier with a NullReferenceException.  Each now verifies, its postcondition is checked, and
// the fact it establishes is available afterwards.

module Holds {
  const k: int := forall i: int ensures true {} 5
  class C { const k: int := forall i: int ensures true {} 5 }
  datatype D = D(y: int := forall i: int ensures true {} 5) {
    const k: int := forall i: int ensures true {} 5
  }
  type S = x: int | x >= 0 witness (forall i: int ensures true {} 5)
  type S2 = x: int | (forall i: int ensures true {} x >= 0) witness 5
  newtype N = x: int | (forall i: int ensures true {} x >= 0)
  method M(x: int := forall i: int ensures true {} 5) {}
  function F(x: int := forall i: int ensures true {} 5): int { x }
  iterator It() { forall i: int ensures true {} }

  // The established fact is usable after the statement.
  const used: int := forall i: int | 0 <= i < 3 ensures i * i < 9 {} assert 2 * 2 < 9; 5
  iterator ItUsed() { forall i: int | 0 <= i < 3 ensures i * i < 9 {} assert 2 * 2 < 9; }
}

module Fails {
  // Each postcondition is false, and each must be reported.
  const k: int := forall i: int | 0 <= i ensures i < 0 {} 5
  class C { const k: int := forall i: int | 0 <= i ensures i < 0 {} 5 }
  datatype D = D(y: int := forall i: int | 0 <= i ensures i < 0 {} 5) {
    const k: int := forall i: int | 0 <= i ensures i < 0 {} 5
  }
  type S = x: int | x >= 0 witness (forall i: int | 0 <= i ensures i < 0 {} 5)
  type S2 = x: int | (forall i: int | 0 <= i ensures i < 0 {} x >= 0) witness 5
  newtype N = x: int | (forall i: int | 0 <= i ensures i < 0 {} x >= 0)
  method M(x: int := forall i: int | 0 <= i ensures i < 0 {} 5) {}
  function F(x: int := forall i: int | 0 <= i ensures i < 0 {} 5): int { x }
  iterator It() { forall i: int | 0 <= i ensures i < 0 {} }

  // Vacuity: a true forall statement does not make false provable.
  const v: int := forall i: int ensures true {} assert false; 5
  iterator ItV() { forall i: int ensures true {} assert false; }
}
