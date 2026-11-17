// The base module of B is in a library (or an included file: github-issue-409-include.dfy).
module B refines A {
  lemma P() ensures false { R(); }
}
