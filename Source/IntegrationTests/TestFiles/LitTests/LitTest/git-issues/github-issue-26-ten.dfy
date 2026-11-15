// RUN: %verify --boogie "/proc:__NoProcedureMatches__" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Ten variables bound by one :|, 3^10 = 59049 partial guesses.  On Linux the thread stack
// first overflowed here, in Boogie's type checker.  The program is translated and
// type-checked, but not sent to the solver: /proc matches no procedure.

lemma Ten() {
  var x0, x1, x2, x3, x4, x5, x6, x7, x8, x9 :|
    x0 == 0 && x1 == 1 && x2 == 2 && x3 == 3 && x4 == 4 && x5 == 5 && x6 == 6 && x7 == 7 && x8 == 8 && x9 == 9;
}
