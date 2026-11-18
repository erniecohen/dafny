// Renaming does not permit adding a trait axiom or changing a member's parameter type.
module Original {
  trait T<A> {
    ghost function F(a: A): A
    lemma L(a: A)
  }
}
module AddsAxioms refines Original {
  trait T<B> ... {
    ghost function {:axiom} F(b: B): B
    lemma {:axiom} L(b: B)
  }
}
module ChangesTypes refines Original {
  trait T<B> ... {
    ghost function F(b: int): B
    lemma L(b: int)
  }
}
// Providing checked bodies while renaming remains allowed.
module GivesBodies refines Original {
  trait T<B> ... {
    ghost function {:axiom} F(b: B): B { b }
    lemma {:axiom} L(b: B) { }
  }
}
