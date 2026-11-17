// The reproducer of https://github.com/erniecohen/dafny/issues/479
module M {
  datatype W = W(f: () ~> bool)
  class Ref {
    ghost var w: W
    constructor (ghost w: W) ensures this.w == w { this.w := w; }
  }
  method Main() ensures false {
    var r := new Ref(W(() => true));
    ghost var knot := () reads * => if r.w.f.requires() then !r.w.f() else false;
    r.w := W(knot);
    assert knot() == !knot();
  }
}
