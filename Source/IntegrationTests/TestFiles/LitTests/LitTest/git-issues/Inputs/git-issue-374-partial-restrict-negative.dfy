import P = Std.PartialFunctionDomains
lemma Vacuity0() {
  var f: () --> int := () requires false => 3;
  var r := P.Arity0.Restrict(f);
  P.Arity0.Restriction(f);
  assert r == f;
  assert false;
}
lemma Vacuity1() {
  var f: int --> int := (x0: int) requires false => 3;
  var r := P.Arity1.Restrict(f);
  P.Arity1.Restriction(f);
  assert r == f;
  assert false;
}
lemma Vacuity2() {
  var f: (int, int) --> int := (x0: int, x1: int) requires false => 3;
  var r := P.Arity2.Restrict(f);
  P.Arity2.Restriction(f);
  assert r == f;
  assert false;
}
lemma Vacuity3() {
  var f: (int, int, int) --> int := (x0: int, x1: int, x2: int) requires false => 3;
  var r := P.Arity3.Restrict(f);
  P.Arity3.Restriction(f);
  assert r == f;
  assert false;
}
lemma Vacuity4() {
  var f: (int, int, int, int) --> int := (x0: int, x1: int, x2: int, x3: int) requires false => 3;
  var r := P.Arity4.Restrict(f);
  P.Arity4.Restriction(f);
  assert r == f;
  assert false;
}
lemma NoIdentityOnWiderDomain() {
  var f: nat --> int := (x: int) requires x >= 0 => x;
  var r := P.Arity1.Restrict<nat, int>(f);
  P.Arity1.Restriction<nat, int>(f);
  assert r == f;
}
lemma NotTotal() {
  var f: int --> int := (x: int) requires x > 0 => x;
  var r := P.Arity1.Restrict(f);
  P.Arity1.Restriction(f);
  assert r.requires(0);
}
lemma DifferentPreconditions() {
  var f: nat --> int := (x: int) requires x > 0 => x;
  var g: nat --> int := (x: int) requires x >= 0 => x;
  var r := P.Arity1.Restrict<nat, int>(f);
  var t := P.Arity1.Restrict<nat, int>(g);
  P.Arity1.Restriction<nat, int>(f);
  P.Arity1.Restriction<nat, int>(g);
  P.Arity1.Extensionality<nat, int>(r, t);
}
lemma InvalidCall() {
  var f: int --> int := (x: int) requires x > 0 => x;
  var r := P.Arity1.Restrict(f);
  P.Arity1.Restriction(f);
  assert r(0) == 0;
}
