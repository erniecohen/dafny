// Vacuity control: with the inherited contract in scope and used, false is still not provable.
module A {
  type O { function F(): int ensures F() > 0 }
  lemma L(o: O) ensures o.F() > 0 {}
}
module B refines A {
  datatype D = D { function F(): int { 1 } }
  type O = D
  lemma Vacuous(o: O) ensures false { L(o); assert o.F() > 0; }
}
