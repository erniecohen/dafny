// Under --legacy-allocated-in-lambdas, the reproducer of https://github.com/erniecohen/dafny/issues/457
// still proves false.

class C { constructor () {} }

method Reproducer() ensures false {
  var c := new C();
  ghost var f: int -> bool := (x: int) => allocated(c);
  ghost var f0: int ~> bool := f;
  ghost var b := f0(0);
  assert b;
}
