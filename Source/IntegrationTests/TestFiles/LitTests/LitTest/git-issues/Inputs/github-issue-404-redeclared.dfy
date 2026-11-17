// Part of https://github.com/erniecohen/dafny/issues/404 (see ../github-issue-404.dfy): a class member
// that the refining module redeclares, with its contract unchanged, is the module's own declaration, so
// its override check is made by default and under --legacy-elided-override-checks alike.  The error has
// no related location: it is reported at the redeclaration.

module A {
  trait S { lemma L() ensures true }
  class C extends S { constructor () {} lemma L() ensures true { } }
}
module B refines A {
  trait S ... { lemma L... ensures false }
  class C ... { lemma L... }
  method M() ensures false { var s: S := new C(); s.L(); }
}
