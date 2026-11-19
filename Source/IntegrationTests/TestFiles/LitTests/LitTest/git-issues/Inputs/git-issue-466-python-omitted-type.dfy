// Omitted declarations need a foreign-linkage convention for a recursive group.
module A { import B class {:compile false} C { } function F(): int { 1 } }
module B { import A function G(): int { A.F() } }
