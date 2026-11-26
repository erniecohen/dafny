module {:compile false} A { import B function F(): int { 1 } }
module {:compile false} B { import A function G(): int { A.F() } }
