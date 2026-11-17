// Part of https://github.com/erniecohen/dafny/issues/404 (see ../github-issue-404.dfy): a client that
// refines a library module makes the override check that its change requires, although the class member
// comes from the library.  Unchanged refines it without changing the pair, and checks nothing again.

module Client refines LibraryBase {
  trait S ... { lemma L... ensures false }
  method M() ensures false { var s: S := new C(); s.L(); }
}

module Unchanged refines LibraryBase {
  method M() { var s: S := new C(); s.L(); }
}
