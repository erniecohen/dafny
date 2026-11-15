// Proposed () syntax; expected outcomes are UNVERIFIED.
// --user-defined-operators --type-system-refresh
// bad_application must fail at run's precondition. q3_false_control must
// separately fail at assert false. No laws or automatic requires property exist.

datatype NonnegativeAdder = NonnegativeAdder(n: int) {
  function {:operator "()"} run(x: int): int
    requires 0 <= x
  { n + x }
}

method bad_application()
{
  var f := NonnegativeAdder(5);
  var x := f(-1); // Expected verifier error: run's precondition.
}

lemma q3_false_control()
{
  var f := NonnegativeAdder(5);
  assert f(0) == 5;
  assert false; // Must fail independently of the bad precondition above.
}
