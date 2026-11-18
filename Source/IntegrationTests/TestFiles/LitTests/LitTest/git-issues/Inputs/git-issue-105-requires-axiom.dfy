// Member invariants (https://github.com/erniecohen/dafny/issues/105): the precondition of each
// invariant is the invariants before it, which its well-formedness check assumes, so that its
// #canCall, and those of its callees, hold only where they do.  git-issue-105-false-proofs.dfy
// checks the #requires axioms in the Boogie text.
function H(n: int): bool requires n > 0 { true }
datatype D = D(n: int) {
  invariant n > 0
  invariant H(n)
  invariant H(n + 1)
}
