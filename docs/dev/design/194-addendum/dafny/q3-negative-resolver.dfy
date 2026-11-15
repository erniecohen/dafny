// PROPOSED APPLICATION; EXPECTED OUTCOMES UNVERIFIED.
// --type-system-refresh --user-defined-operators
// Each marked case fails resolution; the embedded false control cannot run.
// q3-control.dfy is the standalone ordinary named-call control.
datatype Adder = Adder(n: int) {
  function {:operator "()"} run(x: int): int { n + x }
}

method bad_label(f: Adder) {
  label L: { }
  var y := f@L(1); // ERROR: apply slot is not a two-state function.
}

method no_synthetic_requires(f: Adder) {
  var b := f.requires(1); // ERROR: user value has no synthesized requires member.
}

lemma false_control() {
  var f := Adder(5);
  assert f.run(1) == 6;
  assert false; // Does not execute after resolver errors; see q3-control.dfy.
}
