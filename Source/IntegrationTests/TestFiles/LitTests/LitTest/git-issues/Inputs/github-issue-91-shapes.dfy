// Synonyms whose right-hand side is not a new member-bearing declaration of the refining module.
module A { type O { function F(): int } }
module NotADeclaration refines A {
  type O = int
}
module NotItsParameters refines A {
  datatype D<X> = D { function F(): int { 0 } }
  type O = D<int>
}
module A2 { type O { function F(): int } type T { function F(): int } }
module DeclaredInBase refines A2 {
  type O = T
}
module A3 { type O { function F(): int } type P { function F(): int } }
module TwoSynonyms refines A3 {
  datatype D = D { function F(): int { 0 } }
  type O = D
  type P = D
}
module Subset refines A {
  datatype D = D { function F(): int { 0 } }
  type O = d: D | true
}
