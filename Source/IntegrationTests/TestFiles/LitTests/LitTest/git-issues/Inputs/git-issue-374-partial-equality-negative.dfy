// Each assertion must fail with automatic partial extensionality enabled.
lemma DifferentPreconditions() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x > 0 => x;
  assert f == g;
}
lemma DifferentResults() {
  var f: int --> int := (x: int) requires x >= 0 => 0;
  var g: int --> int := (x: int) requires x >= 0 => 1;
  assert f == g;
}
lemma Overlap() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x <= 0 => x;
  assert f == g;
}
lemma WiderDomain() {
  var f: nat --> int := (x: int) requires x >= -1 => x;
  var g: nat --> int := (x: int) requires x >= 0 => x;
  assert forall x: nat :: f.requires(x) == g.requires(x);
  assert forall x: nat :: f.requires(x) ==> f(x) == g(x);
  assert f == g;
}
class Cell { var n: int }
lemma AllocatedOnly(f: Cell --> int, g: Cell --> int)
  requires forall c: Cell :: f.requires(c) == g.requires(c)
  requires forall c: Cell :: f.requires(c) ==> f(c) == g(c)
  ensures f == g
{}
lemma HeapReading(f: int ~> int, g: int ~> int)
  requires forall x :: f.requires(x) == g.requires(x)
  requires forall x :: f.requires(x) ==> f(x) == g(x)
  ensures f == g
{}
lemma InvalidCall() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x >= 0 => x + 0;
  assert forall x: int :: f.requires(x) == g.requires(x);
  assert forall x: int :: f.requires(x) ==> f(x) == g(x);
  assert f == g;
  assert f(-1) == g(-1);
}
lemma Vacuity0() {
  var f: () --> int := () requires false => 0;
  var g: () --> int := () requires false => 1;
  assert !f.requires() && !g.requires();
  assert f == g;
  assert false;
}
lemma Vacuity1() {
  var f: int --> int := (x0: int) requires false => 0;
  var g: int --> int := (x0: int) requires false => 1;
  assert forall x0: int :: !f.requires(x0) && !g.requires(x0);
  assert f == g;
  assert false;
}
lemma Vacuity2() {
  var f: (int, int) --> int := (x0: int, x1: int) requires false => 0;
  var g: (int, int) --> int := (x0: int, x1: int) requires false => 1;
  assert forall x0: int, x1: int :: !f.requires(x0, x1) && !g.requires(x0, x1);
  assert f == g;
  assert false;
}
lemma Vacuity3() {
  var f: (int, int, int) --> int := (x0: int, x1: int, x2: int) requires false => 0;
  var g: (int, int, int) --> int := (x0: int, x1: int, x2: int) requires false => 1;
  assert forall x0: int, x1: int, x2: int :: !f.requires(x0, x1, x2) && !g.requires(x0, x1, x2);
  assert f == g;
  assert false;
}
lemma Vacuity4() {
  var f: (int, int, int, int) --> int := (x0: int, x1: int, x2: int, x3: int) requires false => 0;
  var g: (int, int, int, int) --> int := (x0: int, x1: int, x2: int, x3: int) requires false => 1;
  assert forall x0: int, x1: int, x2: int, x3: int :: !f.requires(x0, x1, x2, x3) && !g.requires(x0, x1, x2, x3);
  assert f == g;
  assert false;
}
lemma Vacuity5() {
  var f: (int, int, int, int, int) --> int := (x0: int, x1: int, x2: int, x3: int, x4: int) requires false => 0;
  var g: (int, int, int, int, int) --> int := (x0: int, x1: int, x2: int, x3: int, x4: int) requires false => 1;
  assert forall x0: int, x1: int, x2: int, x3: int, x4: int :: !f.requires(x0, x1, x2, x3, x4) && !g.requires(x0, x1, x2, x3, x4);
  assert f == g;
  assert false;
}
