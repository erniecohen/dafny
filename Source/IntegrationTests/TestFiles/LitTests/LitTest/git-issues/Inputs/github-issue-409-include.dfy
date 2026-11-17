include "github-issue-409-library.dfy"

// The base module of B is in an included file (or a library: github-issue-409-client.dfy).
module B refines A {
  lemma P() ensures false { R(); }
}
