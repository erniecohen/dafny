import P = Std.FunctionPointwise
class C { var n: int }
lemma Reading(c: C) {
  var f: C ~> int := (x: C) reads c => c.n;
  var g := (x: C) => 0;
  assert P.Arity1.Pointwise<C, int>(f, g);
  P.Arity1.Extensionality<C, int>(f, g);
}
lemma Partial(f: C --> int, g: C --> int) {
  assert P.Arity1.Pointwise<C, int>(f, g);
  P.Arity1.Extensionality<C, int>(f, g);
}
