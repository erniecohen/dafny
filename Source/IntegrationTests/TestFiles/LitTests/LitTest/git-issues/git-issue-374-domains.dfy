// RUN: %exits-with 4 %verify --solver-path "%review-z3" --show-snippets=false "%s" > "%t"
// RUN: %verify --solver-path "%review-z3" --additional-axioms --show-snippets=false "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --function-extensionality-axiom --show-snippets=false "%s" >> "%t"
// RUN: %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false --show-snippets=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --show-snippets=false "%S/Inputs/git-issue-374-domains-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --function-extensionality-axiom --show-snippets=false "%S/Inputs/git-issue-374-domains-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false --show-snippets=false "%S/Inputs/git-issue-374-domains-negative.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --show-snippets=false "%S/Inputs/git-issue-374-domains-malformed.dfy" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --type-system-refresh=false --general-newtypes=false --show-snippets=false "%S/Inputs/git-issue-374-domains-malformed.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

include "Inputs/git-issue-374-domains-decls.dfy"

lemma Restriction0<B>(f: () -> B)
  ensures D0.ExactDom(D0.Restrict(f))
  ensures D0.ExactDom(f) <==> D0.Restrict(f) == f
  ensures D0.Restrict(f)() == f()
{}

lemma Explicit0<B>(f: () -> B, g: () -> B)
  requires D0.ExactDom(f) && D0.ExactDom(g)
  requires f() == g()
  ensures f == g
{
  assert D0.Equal(f, g);
}

lemma Restriction2<A0(!new), A1(!new), B>(f: (A0, A1) -> B)
  ensures D2.ExactDom(D2.Restrict(f))
  ensures D2.ExactDom(f) <==> D2.Restrict(f) == f
  ensures forall x0: A0, x1: A1 :: D2.Restrict(f)(x0, x1) == f(x0, x1)
{}

lemma Explicit2<A0(!new), A1(!new), B>(f: (A0, A1) -> B, g: (A0, A1) -> B)
  requires D2.ExactDom(f) && D2.ExactDom(g)
  requires forall x0: A0, x1: A1 :: f(x0, x1) == g(x0, x1)
  ensures f == g
{
  assert D2.Equal(f, g);
}

lemma Restriction5<A0(!new), A1(!new), A2(!new), A3(!new), A4(!new), B>(f: (A0, A1, A2, A3, A4) -> B)
  ensures D5.ExactDom(D5.Restrict(f))
  ensures D5.ExactDom(f) <==> D5.Restrict(f) == f
  ensures forall x0: A0, x1: A1, x2: A2, x3: A3, x4: A4 :: D5.Restrict(f)(x0, x1, x2, x3, x4) == f(x0, x1, x2, x3, x4)
{}

lemma Explicit5<A0(!new), A1(!new), A2(!new), A3(!new), A4(!new), B>(f: (A0, A1, A2, A3, A4) -> B, g: (A0, A1, A2, A3, A4) -> B)
  requires D5.ExactDom(f) && D5.ExactDom(g)
  requires forall x0: A0, x1: A1, x2: A2, x3: A3, x4: A4 :: f(x0, x1, x2, x3, x4) == g(x0, x1, x2, x3, x4)
  ensures f == g
{
  assert D5.Equal(f, g);
}

lemma CounterexampleRestrictions() {
  var f: (nat, nat) -> int := (x: int, y: int) => if x < 0 then 7 else x + y;
  var g: (nat, nat) -> int := (x: int, y: int) => x + y;
  Explicit2<nat, nat, int>(D2.Restrict<nat, nat, int>(f), D2.Restrict<nat, nat, int>(g));
}

type Empty = x: int | false witness *
lemma EmptyProducts() {
  var f: (Empty, Empty) -> int := (x: Empty, y: int) => 0;
  var g: (Empty, Empty) -> int := (x: int, y: Empty) => 1;
  assert D2.ExactDom<Empty, Empty, int>(f) && D2.ExactDom<Empty, Empty, int>(g);
  Explicit2<Empty, Empty, int>(f, g);
}

// Products of basic domains have exact domains under additional-axioms alone.
lemma Basic(f: (int, bool) -> int, g: (int, bool) -> int)
  requires forall x, y :: f(x, y) == g(x, y)
  ensures f == g
{
  assert D2.ExactDom<int, bool, int>(f) && D2.ExactDom<int, bool, int>(g);
  Explicit2<int, bool, int>(f, g);
}
