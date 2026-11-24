import FD = Std.FunctionDomains

lemma Vacuity0() {
  var f := () => 0;
  FD.Arity0.Restriction<int>(f);
  FD.Arity0.Extensionality<int>(FD.Arity0.Restrict<int>(f), f);
  assert false;
}

lemma Vacuity1() {
  var f := (x0: nat) => 0;
  FD.Arity1.Restriction<nat, int>(f);
  FD.Arity1.Extensionality<nat, int>(FD.Arity1.Restrict<nat, int>(f), f);
  assert false;
}

lemma Vacuity2() {
  var f := (x0: nat, x1: nat) => 0;
  FD.Arity2.Restriction<nat, nat, int>(f);
  FD.Arity2.Extensionality<nat, nat, int>(FD.Arity2.Restrict<nat, nat, int>(f), f);
  assert false;
}

lemma Vacuity3() {
  var f := (x0: nat, x1: nat, x2: nat) => 0;
  FD.Arity3.Restriction<nat, nat, nat, int>(f);
  FD.Arity3.Extensionality<nat, nat, nat, int>(FD.Arity3.Restrict<nat, nat, nat, int>(f), f);
  assert false;
}

lemma Vacuity4() {
  var f := (x0: nat, x1: nat, x2: nat, x3: nat) => 0;
  FD.Arity4.Restriction<nat, nat, nat, nat, int>(f);
  FD.Arity4.Extensionality<nat, nat, nat, nat, int>(FD.Arity4.Restrict<nat, nat, nat, nat, int>(f), f);
  assert false;
}

lemma WiderDomain() {
  var f: (nat, bool) -> int := (x: int, b: bool) => x;
  var g: (nat, bool) -> int := (x: nat, b: bool) => x;
  FD.Arity2.Extensionality<nat, bool, int>(f, g);
}
lemma Distinct() {
  var f := (x: int) => 0;
  var g := (x: int) => 1;
  FD.Arity1.Extensionality(f, g);
}
