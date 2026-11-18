// Declarations for the function-domain tests of
// https://github.com/erniecohen/dafny/issues/374, included by github-issue-374*.dfy.
// Restrict and ExactDom are given meaning by the verifier when function domains are on
// (--additional-axioms or --function-extensionality-axiom); otherwise they are opaque.
// FunExt is extensionality as a lemma: the configuration that has no extensionality axiom.
module FunctionDomains {
  ghost function {:axiom} {:functionDomain "restrict"} Restrict<A, B>(f: A -> B): A -> B
  ghost predicate {:axiom} {:functionDomain "exactDom"} ExactDom<A, B>(f: A -> B)
  lemma {:axiom} FunExt<A, B>(f: A -> B, g: A -> B)
    requires ExactDom(f) && ExactDom(g)
    requires forall x: A :: f(x) == g(x)
    ensures f == g
}
