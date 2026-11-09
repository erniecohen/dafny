// RUN: %exits-with 4 %verify --resource-limit 1e6 "%s" > "%t"
// RUN: %exits-with 4 %verify --resource-limit 1e6 --decreases-literal-axiom-only "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/197 and https://github.com/erniecohen/dafny/issues/198.
// A literal argument unfolds a recursive function without fuel only through a literal
// definition axiom.  Without a decreases clause, the clause is inferred to name every
// parameter, and the only literal axiom needs every argument to be a literal; writing
// `decreases t` adds one that needs only `t` to be a literal.  Dafny then emits both,
// and on a call whose arguments are all literals both fire at every level, so the cost
// grows about fourfold per level: All6 runs out of resources by default.  With
// --decreases-literal-axiom-only, only the decreases-literals axiom is emitted.

datatype T = V | C(c: real) | Add(a: T, b: T)

function ev(t: T, x: real): real
  decreases t
{
  match t
  case V => x
  case C(c) => c
  case Add(a, b) => ev(a, x) + ev(b, x)
}

// x is not a literal: unfolds through the decreases-literals axiom, in both runs.
lemma Mixed6(x: real)
  ensures ev(Add(V, Add(V, Add(V, Add(V, Add(C(1.0), V))))), x) == 5.0 * x + 1.0
{}

// Every argument is a literal: out of resources by default, verified with the option.
lemma All6()
  ensures ev(Add(V, Add(V, Add(V, Add(V, Add(C(1.0), V))))), 2.0) == 11.0
{}

// Vacuity control: a false equation still fails, in both runs.
lemma Wrong(x: real)
  ensures ev(Add(V, C(1.0)), x) == x + 2.0
{}

// The member-predicate form of #198: with `decreases this`, a depth-3 literal receiver
// unfolds although the other argument, |s|, is not a literal.
datatype Op = P | M
datatype Term = Var(i: nat) | Bin(op: Op, l: Term, r: Term) {
  predicate Scoped(n: nat)
    decreases this
  {
    match this
    case Var(i) => i < n
    case Bin(_, l, r) => l.Scoped(n) && r.Scoped(n)
  }
}

function Size(t: Term, s: seq<int>): int
  requires t.Scoped(|s|)
{
  |s|
}

function Literal3(x: int, y: int, z: int): int {
  Size(Bin(P, Var(0), Bin(M, Var(1), Var(2))), [x, y, z])
}
