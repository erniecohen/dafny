import D = Std.FunctionDomains
import P = Std.FunctionPointwise
class C { var n: int }

lemma Vacuity1(w: C) {
  var f := (x0: C) => 0;
  var g := (x0: C) => 1 - 1;
  assert f(w) == g(w);
  assert P.Arity1.Pointwise<C, int>(f, g);
  P.Arity1.Extensionality<C, int>(f, g);
  assert false;
}
lemma Distinct1(w: C) {
  var f := (x0: C) => 0;
  var g := (x0: C) => 1;
  assert f(w) != g(w);
  assert P.Arity1.Pointwise<C, int>(f, g);
}

lemma Vacuity2(w: C) {
  var f := (x0: C, x1: C) => 0;
  var g := (x0: C, x1: C) => 1 - 1;
  assert f(w, w) == g(w, w);
  assert P.Arity2.Pointwise<C, C, int>(f, g);
  P.Arity2.Extensionality<C, C, int>(f, g);
  assert false;
}
lemma Distinct2(w: C) {
  var f := (x0: C, x1: C) => 0;
  var g := (x0: C, x1: C) => 1;
  assert f(w, w) != g(w, w);
  assert P.Arity2.Pointwise<C, C, int>(f, g);
}

lemma Vacuity3(w: C) {
  var f := (x0: C, x1: C, x2: C) => 0;
  var g := (x0: C, x1: C, x2: C) => 1 - 1;
  assert f(w, w, w) == g(w, w, w);
  assert P.Arity3.Pointwise<C, C, C, int>(f, g);
  P.Arity3.Extensionality<C, C, C, int>(f, g);
  assert false;
}
lemma Distinct3(w: C) {
  var f := (x0: C, x1: C, x2: C) => 0;
  var g := (x0: C, x1: C, x2: C) => 1;
  assert f(w, w, w) != g(w, w, w);
  assert P.Arity3.Pointwise<C, C, C, int>(f, g);
}

lemma Vacuity4(w: C) {
  var f := (x0: C, x1: C, x2: C, x3: C) => 0;
  var g := (x0: C, x1: C, x2: C, x3: C) => 1 - 1;
  assert f(w, w, w, w) == g(w, w, w, w);
  assert P.Arity4.Pointwise<C, C, C, C, int>(f, g);
  P.Arity4.Extensionality<C, C, C, C, int>(f, g);
  assert false;
}
lemma Distinct4(w: C) {
  var f := (x0: C, x1: C, x2: C, x3: C) => 0;
  var g := (x0: C, x1: C, x2: C, x3: C) => 1;
  assert f(w, w, w, w) != g(w, w, w, w);
  assert P.Arity4.Pointwise<C, C, C, C, int>(f, g);
}

lemma AllocatedAgreementIsInsufficient(f: C -> int, g: C -> int)
  requires forall c: C :: f(c) == g(c)
  ensures P.Arity1.Pointwise<C, int>(f, g)
{}
lemma WiderDomain() {
  var f: C -> int := (o: object) => 0;
  var g := (c: C) => 0;
  assert P.Arity1.Pointwise<C, int>(f, g);
  P.Arity1.Extensionality<C, int>(f, g);
}
type EmptyRef = c: C | false witness *
lemma EmptyVacuity(w: C) {
  var f := (c: C, e: EmptyRef) => 0;
  var g := (c: C, e: EmptyRef) => 1;
  assert P.Arity2.Pointwise<C, EmptyRef, int>(f, g);
  P.Arity2.Extensionality<C, EmptyRef, int>(f, g);
  assert false;
}
method HeapReading(c: C)
  modifies c
{
  var before := c.n;
  var f: C ~> int := (x: C) reads c => c.n;
  var g := (x: C) => before;
  assert forall x: C :: f(x) == g(x);
  c.n := before + 1;
  assert f(c) != g(c);
  assert f == g;
}

lemma FirstClassVacuity(w: C) {
  var p := P.Arity1.Pointwise<C, int>;
  var f := (c: C) => 0;
  var g := (c: C) => 1 - 1;
  assert f(w) == g(w);
  assert p(f, g);
  P.Arity1.Extensionality<C, int>(f, g);
  assert false;
}
