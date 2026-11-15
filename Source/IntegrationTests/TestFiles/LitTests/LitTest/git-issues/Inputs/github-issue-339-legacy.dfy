// The program of https://github.com/erniecohen/dafny/issues/339, which proves false under the hidden
// --legacy-first-phase-const-reads.  The control below must still fail.
datatype D = Zero | One(D)

class C {
  const t: D
  const s: D := t
  constructor () ensures false {
    t := One(s);
    new;
    assert s == t;
    assert s < t;
  }
}

method Control() {
  assert false;  // error
}
