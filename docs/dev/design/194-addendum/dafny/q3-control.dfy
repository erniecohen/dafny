// EXISTING NAMED-CALL SYNTAX; EXPECTED OUTCOME UNVERIFIED.
// No new feature is needed. Only assert false should fail.
datatype Adder = Adder(n: int) {
  function run(x: int): int { n + x }
}
lemma false_control() {
  var f := Adder(5);
  assert f.run(1) == 6;
  assert false;
}
