// Cross-process equality and corruption controls use the external CLI.
// RUN: python3 "%repositoryRoot/.github/review/translation-global-inputs.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

module A {
  trait T {
    lemma Step() decreases {:global} 0
    function Get(): int decreases {:global} 0
  }
  lemma Invoke(t: T) decreases {:global} 1 { t.Step(); }
  predicate P(n: nat) { n == 0 || P(n - 1) }
  lemma {:induction n} Induction(n: nat, t: T)
    ensures P(n)
    decreases {:global} n + 1
    decreases n
  { if n > 0 { t.Step(); Induction(n - 1, t); } }
  function High(t: T): int decreases {:global} 1 { t.Get() }
  method Certify() { var h := bounded.certify(0, High); }
  function F(f: int -> int): int { 0 }
}
module M {
  import A
  lemma Good(t: A.T) decreases {:global} 2 { A.Invoke(t); }
  lemma TooLow(t: A.T) decreases {:global} 0 { A.Invoke(t); }
  lemma Bad() { assert false; }
}
