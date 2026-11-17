// Uses of `allocated` that https://github.com/erniecohen/dafny/issues/457 still allows, and controls.

class C { constructor () {} }

// Control: the same method with a body that does not mention `allocated` must not prove false.
method Control() ensures false {  // error: postcondition
  var c := new C();
  ghost var f: int -> bool := (x: int) => true;
  ghost var f0: int ~> bool := f;
  ghost var b := f0(0);
  assert b;
}

// `old` inside the lambda evaluates `allocated` in a fixed heap, not the heap of the application.
method OldInside(c: C) {
  ghost var f: int -> bool := (x: int) => old(allocated(c));
  ghost var f0: int ~> bool := f;
  assert f0(0);
}

method OldAtInside() {
  label L:
  var c := new C();
  ghost var f: int -> bool := (x: int) => old@L(allocated(c));
  ghost var f0: int ~> bool := f;
  assert !f0(0);
}

// `allocated` outside any lambda is unaffected.
method OutsideLambda() {
  var c := new C();
  assert allocated(c);
  ghost var b := allocated(c);
  ghost var f: int -> bool := (x: int) => b;
  assert f(0);
}

// A lambda that only reads fields is unaffected.
class D { var v: int }
method ReadsFields(d: D) {
  ghost var f := (x: int) reads d => d.v + x;
  assert f(0) == d.v;
}
