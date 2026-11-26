import P = Std.PartialFunctionDomains
lemma DifferentPreconditions() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x > 0 => x;
  P.Arity1.Extensionality(f, g); // error
}
lemma MoreDefined() {
  var f: int --> int := (x: int) requires x > 0 => x;
  var g: int --> int := (x: int) requires x >= 0 => x;
  P.Arity1.Extensionality(f, g); // error
}
lemma OverlapOnly() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x <= 0 => x;
  assert forall x :: f.requires(x) && g.requires(x) ==> f(x) == g(x);
  P.Arity1.Extensionality(f, g); // error
}
lemma DifferentResults() {
  var f: int --> int := (x: int) requires x >= 0 => 0;
  var g: int --> int := (x: int) requires x >= 0 => 1;
  P.Arity1.Extensionality(f, g); // error
}
lemma WiderDomain() {
  var f: nat --> int := (x: int) requires x >= 0 => x;
  var g: nat --> int := (x: nat) requires true => x;
  assert P.Arity1.Pointwise<nat, int>(f, g);
  P.Arity1.Extensionality<nat, int>(f, g); // error: exactness
}
lemma WiderDirect() {
  var f: nat --> int := (x: int) requires x >= 0 => x;
  var g: nat --> int := (x: nat) requires true => x;
  assert P.Arity1.Pointwise<nat, int>(f, g);
  assert P.Arity1.Equal<nat, int>(f, g); // error: exactness is required by the intrinsic too
}
// A contravariant view need not have the declared subset as its exact domain.
lemma NonBasicNotForced(f: nat --> int)
  requires !P.Arity1.ExactDom<nat, int>(f)
{
  assert false; // error: partial-arrow typing alone does not imply exactness
}
lemma NoCall(f: int --> int, g: int --> int)
  requires forall x :: f.requires(x) == g.requires(x)
  requires forall x :: f.requires(x) ==> f(x) == g(x)
  ensures f == g // error: explicit call required
{}
lemma VacuityInt() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x >= 0 => if x < 0 then 7 else x;
  P.Arity1.Extensionality(f, g);
  assert f(0) == 0;
  assert false; // error
}
lemma VacuitySubset() {
  var f: nat --> int := (x: nat) requires x >= 2 => x;
  var g: nat --> int := (x: nat) requires x >= 2 => if x == 1 then 7 else x;
  P.Arity1.Extensionality<nat, int>(f, g);
  assert f(2) == 2;
  assert false; // error
}
function Named(x: int): int requires x >= 0 { x }
lemma VacuityNamed() {
  var g: int --> int := (x: int) requires x >= 0 => x + 0;
  P.Arity1.Extensionality(Named, g);
  assert Named(0) == 0;
  assert false; // error
}
lemma InvalidCall() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x >= 0 => x + 0;
  P.Arity1.Extensionality(f, g);
  assert f(-1) == g(-1); // error: neither precondition holds
}
class Cell { var n: int }
lemma Reading(c: Cell) {
  var f: int ~> int := (x: int) reads c => c.n;
  var ignored := P.Arity1.Pointwise<int, int>(f, (x: int) => 0); // error
}
lemma AllocatedOnly(f: Cell --> int, g: Cell --> int)
  requires forall c: Cell :: f.requires(c) == g.requires(c)
  requires forall c: Cell :: f.requires(c) ==> f(c) == g(c)
{
  assert P.Arity1.Pointwise<Cell, int>(f, g); // error
}
lemma Vacuity0() {
  var f: () --> int := () requires false => 0;
  var g: () --> int := () requires false => 1;
  P.Arity0.Extensionality(f, g);
  assert false; // error
}
lemma Vacuity1() {
  var f: int --> int := (x0: int) requires false => 0;
  var g: int --> int := (x0: int) requires false => 1;
  P.Arity1.Extensionality(f, g);
  assert false; // error
}
lemma Vacuity2() {
  var f: (int, int) --> int := (x0: int, x1: int) requires false => 0;
  var g: (int, int) --> int := (x0: int, x1: int) requires false => 1;
  P.Arity2.Extensionality(f, g);
  assert false; // error
}
lemma Vacuity3() {
  var f: (int, int, int) --> int := (x0: int, x1: int, x2: int) requires false => 0;
  var g: (int, int, int) --> int := (x0: int, x1: int, x2: int) requires false => 1;
  P.Arity3.Extensionality(f, g);
  assert false; // error
}
lemma Vacuity4() {
  var f: (int, int, int, int) --> int := (x0: int, x1: int, x2: int, x3: int) requires false => 0;
  var g: (int, int, int, int) --> int := (x0: int, x1: int, x2: int, x3: int) requires false => 1;
  P.Arity4.Extensionality(f, g);
  assert false; // error
}
