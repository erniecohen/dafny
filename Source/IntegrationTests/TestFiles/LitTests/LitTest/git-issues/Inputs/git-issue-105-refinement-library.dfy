// Member invariants (https://github.com/erniecohen/dafny/issues/105): a library whose module a program
// refines, adding invariants (git-issue-105-refinement-verify.dfy).

abstract module LibraryBank {
  datatype Account = Account(balance: int) {
    invariant balance >= 0
  }
  function Deposit(a: Account, n: nat): Account {
    Account(a.balance + n)                             // error in the refining module of the program
  }
  function Clear(a: Account): Account { Account(0) }
}
