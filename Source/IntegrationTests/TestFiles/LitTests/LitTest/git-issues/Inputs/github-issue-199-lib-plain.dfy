// A library that is built without --lemma-bindings, for github-issue-199-library.dfy.

module Plain {
  lemma Find(n: nat) returns (m: nat)
    ensures m > n
  {
    m := n + 1;
  }
}
