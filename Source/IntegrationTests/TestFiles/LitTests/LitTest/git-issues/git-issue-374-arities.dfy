// RUN: %exits-with 4 %verify --show-snippets=false "%s" > "%t"
// RUN: %exits-with 4 %verify --additional-axioms --show-snippets=false "%s" >> "%t"
// RUN: %verify --function-extensionality-axiom --show-snippets=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --function-extensionality-axiom --show-snippets=false "%S/Inputs/git-issue-374-arities-negative.dfy" >> "%t"
// RUN: %verify --function-extensionality-axiom --type-system-refresh=false --general-newtypes=false --show-snippets=false "%s" >> "%t"
// RUN: %exits-with 4 %verify --function-extensionality-axiom --type-system-refresh=false --general-newtypes=false --show-snippets=false "%S/Inputs/git-issue-374-arities-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Extensionality at every arity, without a library axiom or an explicit lemma call.
lemma Nullary<B>(f: () -> B, g: () -> B)
  requires f() == g()
  ensures f == g
{}

lemma Binary<B>(f: (int, bool) -> B, g: (int, bool) -> B)
  requires forall x, y :: f(x, y) == g(x, y)
  ensures f == g
{}

// Exercise every basic domain, and a result type that is not a basic domain.
lemma FourArguments(f: (int, real, bool, ORDINAL) -> char,
                    g: (int, real, bool, ORDINAL) -> char)
  requires forall a, b, c, d :: f(a, b, c, d) == g(a, b, c, d)
  ensures f == g
{}

lemma SubsetLambdas() {
  var f := (x: nat, y: nat) => x + y;
  var g := (x: nat, y: nat) => y + x;
  assert f == g;
}

datatype D = A | B
function Named(x: D, y: nat): int { if x.A? then y else 0 }
lemma NamedAndLambda() {
  var f := (x: D, y: nat) => if x.B? then 0 else y;
  var g: (D, nat) -> int := Named;
  assert forall x: D, y: nat :: f(x, y) == g(x, y);
  assert f == g;
}

datatype Pair = Pair(x: int, b: bool)
function MakePair(x: int, b: bool): Pair { Pair(x, b) }
lemma Constructor() {
  var f := (x: int, b: bool) => Pair(x, b);
  var g: (int, bool) -> Pair := MakePair;
  assert forall x: int, b: bool :: f(x, b) == g(x, b);
  assert f == g;
}

lemma Disequality(f: () -> int, g: () -> int)
  requires f() == g()
  ensures !(f != g)
{}

// Equality is Leibniz equality, including when a function occurs in a collection.
lemma Substitution(f: (int, bool) -> int, g: (int, bool) -> int)
  requires forall x, y :: f(x, y) == g(x, y)
  ensures [f] == [g]
  ensures {f} == {g}
  ensures map[0 := f] == map[0 := g]
{
  assert f == g;
}

// Exact domains are sets of tuples, rather than tuples of type identities.
// Both of these domains are empty, though their parameter types differ.
type Empty = x: int | false witness *
lemma EmptyProducts() {
  var f: (Empty, Empty) -> int := (x: Empty, y: int) => 0;
  var g: (Empty, Empty) -> int := (x: int, y: Empty) => 1;
  assert f == g;
}
