// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): a library built
// under --modular-termination, without --inherited-trait-axioms.

module Library {
  trait T {
    function F(n: nat): nat
      decreases {:global} n
  }
}
