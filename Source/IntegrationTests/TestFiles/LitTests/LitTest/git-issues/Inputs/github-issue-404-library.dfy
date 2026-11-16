// Part of https://github.com/erniecohen/dafny/issues/404 (see ../github-issue-404.dfy): a library that a
// client refines.

module LibraryBase {
  trait S { lemma L() ensures true }
  class C extends S { constructor () {} lemma L() ensures true { } }
}
