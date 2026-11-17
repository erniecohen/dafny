// The two reproducers of github-issue-409.dfy, written in one module: the same errors.
module A {
  lemma P() ensures false decreases 2 { R(); }
  lemma R() ensures false decreases 1 { P(); }
}

module Functions {
  function F(n: nat): int decreases n { G(n) + 1 }
  function G(n: nat): int decreases n, 1 { F(n) }
}
