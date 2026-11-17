// Part of https://github.com/erniecohen/dafny/issues/88 (see ../github-issue-88-modular-termination.dfy): a library built
// under --modular-termination, without --refinement-extends.

module Library {
  trait T {
    function F(n: nat): nat
      decreases {:global} n
  }
}
