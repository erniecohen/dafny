// The target's member must satisfy the postcondition promised by the abstract type: rejected by the verifier.
module A {
  type O { function F(): int ensures F() > 0 }
}
module B refines A {
  datatype D = D { function F(): int { 0 } }
  type O = D
}
