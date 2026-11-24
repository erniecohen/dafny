include "../git-issue-374-pointwise-intrinsic.dfy"
class C {}
lemma Vacuity0() {
  var f := () => 0;
  var g := () => 1 - 1;
  assert Zero.Pointwise<int>(f, g);
  assert false;
}
lemma Vacuity5(w: C) {
  var f := (a: C, b: C, c: C, d: C, e: C) => 0;
  var g := (a: C, b: C, c: C, d: C, e: C) => 1 - 1;
  assert f(w, w, w, w, w) == g(w, w, w, w, w);
  assert Five.Pointwise<C, C, C, C, C, int>(f, g);
  assert false;
}
