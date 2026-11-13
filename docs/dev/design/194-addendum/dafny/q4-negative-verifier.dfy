// PROPOSED M6 SYNTAX; EXPECTED OUTCOMES UNVERIFIED.
// --type-system-refresh --user-defined-operators
// Expected failures: unequal whole-root aliases, missing setter frame,
// missing updater precondition, and the independent assert-false control.

datatype Row = Row(s: seq<int>) {
  function {:operator "[:=]"} put(i: int, v: int): Row
    requires 0 <= i < |s|
  { Row(s[i := v]) }
}

class Cell {
  var n: int
  method {:operator "[]:="} put(i: int, v: int)
    requires i == 0
    modifies this
    ensures n == v
  { n := v; }
}

method unequal_aliases() {
  var x := Row([1,2]);
  x[0], x[1] := 7, 8;
  // EXPECTED verifier failure at the assignment: roots alias, while proposed
  // replacements Row([7,2]) and Row([1,8]) differ. Indices do not prove separation.
}

method missing_frame(c: Cell) {
  c[0] := 7; // EXPECTED verifier failure: setter modifies exceeds caller frame.
}

method missing_precondition() {
  var x := Row([1,2]);
  x[2] := 9; // EXPECTED verifier failure: updater index precondition.
}

lemma false_control() {
  var x := Row([0]);
  x[0] := 1;
  assert x == Row([1]);
  assert false; // EXPECTED verifier failure independently of the other cases.
}
