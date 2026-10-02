module A { function F(): int { 64 } }
module B { import A method Main() { print A.F(), "\n"; } }
