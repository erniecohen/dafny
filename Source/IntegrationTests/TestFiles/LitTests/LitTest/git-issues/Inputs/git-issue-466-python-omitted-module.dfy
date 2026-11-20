// Omitted declarations need a foreign-linkage convention for a recursive group.
module {:compile false} A { import B function F(): int { 1 } }
module B { import A function G(): int { A.F() } }
