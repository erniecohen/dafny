module Inherited {
  lemma L(x: int) ensures x + 1 > x { }
}
module {:z3ArithmeticSolver} Empty {
  lemma L(x: int) ensures x + 1 > x { }
}
module {:z3ArithmeticSolver 2} Two {
  lemma L(x: int) ensures x + 1 > x { }
}
module {:z3ArithmeticSolver 6} Six {
  lemma L(x: int) ensures x + 1 > x { }
}
