// Member invariants (https://github.com/erniecohen/dafny/issues/105): a program that refines a module
// of a library and adds invariants, so the code it inherits from the library is checked here for them
// (git-issue-105-refinement-verify.dfy).

module Bank refines LibraryBank {
  datatype Account ... {
    invariant balance < 100
  }
}
