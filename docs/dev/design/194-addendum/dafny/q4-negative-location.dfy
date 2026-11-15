// PROPOSED SURFACES; EXPECTED OUTCOME UNVERIFIED.
// --type-system-refresh --user-defined-operators --referrers
// ERROR at modifies: backtick-index locations require intrinsic arrays.
// An ordinary user [] getter, even returning an object, never defines storage.
// This resolution failure prevents the embedded control from running;
// q4-control.dfy is the standalone named-call control.

class Cell { var n: int }
datatype RefBox = RefBox(c: Cell) {
  function {:operator "[]"} at(i: int): Cell
    requires i == 0
  { c }
}

method bad_location(x: RefBox)
  modifies x`[0] // EXPECTED resolver error: RefBox is not an array.
{ }

lemma false_control(x: RefBox) {
  assert x.at(0) == x.c;
  assert false;
}
