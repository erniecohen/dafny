// EXISTING NAMED-CALL SYNTAX; EXPECTED OUTCOME UNVERIFIED.
// Run separately from resolver-negative examples. No new feature is required.
// Ordinary update/assertion should verify; exactly assert false must fail.

datatype Row = Row(s: seq<int>) {
  function put(i: int, v: int): Row
    requires 0 <= i < |s|
  { Row(s[i := v]) }
}

lemma false_control() {
  var x := Row([0]);
  x := x.put(0,1);
  assert x == Row([1]);
  assert false;
}
