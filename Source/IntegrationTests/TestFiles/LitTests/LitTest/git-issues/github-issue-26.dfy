// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// A :| that binds seven variables used to overflow the stack, with exit code 134 and no
// diagnostic: its existence check had one disjunct for every combination of guesses,
// 3^7 = 2187 here, nested as deep as there were disjuncts.  github-issue-26-ten.dfy binds
// ten.

lemma Seven() {
  var x0, x1, x2, x3, x4, x5, x6 :|
    x0 == 0 && x1 == 1 && x2 == 2 && x3 == 3 && x4 == 4 && x5 == 5 && x6 == 6;
}

ghost function SevenExpr(): int {
  var x0, x1, x2, x3, x4, x5, x6 :|
    x0 == 0 && x1 == 1 && x2 == 2 && x3 == 3 && x4 == 4 && x5 == 5 && x6 == 6;
  x0 + x1 + x2 + x3 + x4 + x5 + x6
}

// The existence is still checked: with no values that satisfy the predicate, it fails.
lemma SevenNoWitness() {
  var x0, x1, x2, x3, x4, x5, x6 :|
    x0 == 0 && x1 == 1 && x2 == 2 && x3 == 3 && x4 == 4 && x5 == 5 && x6 == 6 && x6 < 0; // error
}
