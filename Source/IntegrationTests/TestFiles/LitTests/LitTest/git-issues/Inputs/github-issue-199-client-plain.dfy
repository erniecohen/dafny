// A client of github-issue-199-lib-plain.dfy that binds the results of the library's lemma.

module ClientOfPlain {
  import Plain

  ghost function Above(n: nat): nat {
    var m := Plain.Find(n); m
  }

  lemma AboveIsAbove(n: nat)
    ensures Above(n) > n
  {
  }

  lemma NotTheBody(n: nat)
    ensures Above(n) == n + 1  // error: only the postcondition of Find is known
  {
  }
}
