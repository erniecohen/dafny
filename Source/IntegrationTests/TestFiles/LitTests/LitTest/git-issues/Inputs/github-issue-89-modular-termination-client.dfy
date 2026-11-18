// Part of https://github.com/erniecohen/dafny/issues/89 (see ../github-issue-89.dfy): a client of
// github-issue-89-modular-termination-library.dfy.  Under --modular-termination a library and its client agree
// on every option that changes what a program means, --inherited-trait-axioms among them, so the library does not
// load for a client that uses the option.

module Client {
  import Library
  function Twice(t: Library.T, n: nat): nat { 2 * n }
}
