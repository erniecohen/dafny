// Opt-in --pipeline-profile <file> (https://github.com/erniecohen/dafny/issues/241, milestone D0):
// timing events go to the file, and the verifier's output is byte-identical with and without it.
// RUN: %exits-with 4 %verify --show-snippets:false --use-basename-for-filename "%s" > "%t.off"
// RUN: %exits-with 4 %verify --show-snippets:false --use-basename-for-filename --pipeline-profile "%t.jsonl" "%s" > "%t.on"
// RUN: %diff "%t.off" "%t.on"
// RUN: python3 "%repositoryRoot/.github/review/pipeline-profile.py" check "%t.jsonl" > "%t"
// RUN: %diff "%s.expect" "%t"

module A {
  function Sum(n: nat): nat { if n == 0 then 0 else n + Sum(n - 1) }

  lemma SumFormula(n: nat)
    ensures 2 * Sum(n) == n * (n + 1)
  {
    if n > 0 { SumFormula(n - 1); }
  }
}

module B {
  import A

  method Loop(n: nat) returns (s: nat)
    ensures 2 * s == n * (n + 1)
  {
    s := 0;
    var i := 0;
    while i < n
      invariant 0 <= i <= n
      invariant s == A.Sum(i)
    {
      i := i + 1;
      s := s + i;
    }
    A.SumFormula(n);
  }

  method Wrong(n: nat) returns (s: nat)
    ensures s == n
  {
    s := A.Sum(n);
  }
}
