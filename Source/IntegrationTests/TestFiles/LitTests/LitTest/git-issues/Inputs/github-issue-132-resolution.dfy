class C { constructor () {} }

// A general arrow's values may capture references and show them through their reads frames, so it
// contains references, whatever its arguments and result.
twostate lemma AllocatedNew<T(!new)>(new x: T) ensures old(allocated(x)) {}

method GeneralArrowIsNotNew() ensures false {
  label L:
  var n := new C();
  var g := () reads n => 0;
  AllocatedNew@L(g);
  assert old@L(n in g.reads());
  assert !old@L(allocated(n));
}

type NoRefs(!new) = () ~> int
