// NONUNIFORM: verifies the opt-in source API and its checked restriction helpers.
// RUN: %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%s" > "%t"
// RUN: %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts --type-system-refresh=false --general-newtypes=false --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --function-extensionality-axiom --additional-axioms --heap-free-lambda-facts --verify-included-files "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%S/Inputs/git-issue-374-partial-restrict-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms "%S/Inputs/git-issue-374-partial-restrict-malformed.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-374-partial-restrict-malformed.dfy" >> "%t"
// RUN: %audit --report-file "%t.audit.md" "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

import P = Std.PartialFunctionDomains
lemma Generic0<B>(f: () --> B)
  ensures P.Arity0.ExactDom(P.Arity0.Restrict(f))
  ensures P.Arity0.Pointwise(P.Arity0.Restrict(f), f)
  ensures P.Arity0.ExactDom(f) <==> P.Arity0.Restrict(f) == f
{
  P.Arity0.Restriction(f);
}
lemma Generic1<A0, B>(f: A0 --> B)
  ensures P.Arity1.ExactDom(P.Arity1.Restrict(f))
  ensures P.Arity1.Pointwise(P.Arity1.Restrict(f), f)
  ensures P.Arity1.ExactDom(f) <==> P.Arity1.Restrict(f) == f
{
  P.Arity1.Restriction(f);
}
lemma Generic2<A0, A1, B>(f: (A0, A1) --> B)
  ensures P.Arity2.ExactDom(P.Arity2.Restrict(f))
  ensures P.Arity2.Pointwise(P.Arity2.Restrict(f), f)
  ensures P.Arity2.ExactDom(f) <==> P.Arity2.Restrict(f) == f
{
  P.Arity2.Restriction(f);
}
lemma Generic3<A0, A1, A2, B>(f: (A0, A1, A2) --> B)
  ensures P.Arity3.ExactDom(P.Arity3.Restrict(f))
  ensures P.Arity3.Pointwise(P.Arity3.Restrict(f), f)
  ensures P.Arity3.ExactDom(f) <==> P.Arity3.Restrict(f) == f
{
  P.Arity3.Restriction(f);
}
lemma Generic4<A0, A1, A2, A3, B>(f: (A0, A1, A2, A3) --> B)
  ensures P.Arity4.ExactDom(P.Arity4.Restrict(f))
  ensures P.Arity4.Pointwise(P.Arity4.Restrict(f), f)
  ensures P.Arity4.ExactDom(f) <==> P.Arity4.Restrict(f) == f
{
  P.Arity4.Restriction(f);
}
lemma Narrowing() {
  var f: nat --> int := (x: int) requires x >= -1 => x;
  var g: nat --> int := (x: int) requires x >= 0 => if x < 0 then 7 else x;
  var r := P.Arity1.Restrict<nat, int>(f);
  var t := P.Arity1.Restrict<nat, int>(g);
  P.Arity1.Restriction<nat, int>(f);
  P.Arity1.Restriction<nat, int>(g);
  P.Arity1.Extensionality<nat, int>(r, t);
  assert r == t;
  assert r.requires(0) && r(0) == 0;
}
lemma Idempotent<A, B>(f: A --> B) {
  var r := P.Arity1.Restrict(f);
  P.Arity1.Restriction(f);
  P.Arity1.Restriction(r);
  assert P.Arity1.Restrict(r) == r;
}
lemma DefinedAndUndefined() {
  var f: int --> int := (x: int) requires x > 0 => x + 1;
  var r := P.Arity1.Restrict(f);
  P.Arity1.Restriction(f);
  assert !r.requires(0);
  assert r.requires(1) && r(1) == 2;
}
class Cell {}
lemma References(f: Cell --> Cell) {
  P.Arity1.Restriction<Cell, Cell>(f);
}
module Five {
  ghost function {:transparent} {:functionDomain "partialRestrict"} Restrict<A, B, C, D, E, R>(f: (A, B, C, D, E) --> R): (A, B, C, D, E) --> R {
    (a: A, b: B, c: C, d: D, e: E) requires f.requires(a, b, c, d, e) => f(a, b, c, d, e)
  }
  ghost predicate {:functionDomain "partialExactDom"} ExactDom<A, B, C, D, E, R>(f: (A, B, C, D, E) --> R)
  ghost predicate {:functionDomain "partialPointwise"} Pointwise<A, B, C, D, E, R>(f: (A, B, C, D, E) --> R, g: (A, B, C, D, E) --> R)
  lemma Generic<A, B, C, D, E, R>(f: (A, B, C, D, E) --> R)
    ensures ExactDom(Restrict(f)) && Pointwise(Restrict(f), f)
    ensures ExactDom(f) <==> Restrict(f) == f
  {}
}
