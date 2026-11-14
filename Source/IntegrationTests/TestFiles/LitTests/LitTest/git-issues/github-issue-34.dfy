// RUN: %verify --standard-libraries:true "%s" > "%t"
// RUN: %exits-with 4 %verify --standard-libraries:true "%S/Inputs/github-issue-34-negative.dfy" >> "%t"
// RUN: %verify --allow-axioms "%S/Inputs/github-issue-34-generic.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Function extensionality (https://github.com/erniecohen/dafny/issues/34), from the
// trusted lemmas of Std.Axioms.FunctionExtensionality, each used by an explicit call.
// Inputs/github-issue-34-negative.dfy has the controls: vacuity, a program that does not
// call the lemmas, and the arrows and domains they must not cover.
// Inputs/github-issue-34-generic.dfy records why there is no generic version: over a
// subset-type domain it proves false.

import opened Std.Axioms.FunctionExtensionality

// The issue's program.
lemma Ext(f: int -> int, g: int -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
  OnInt(f, g);
}

// Different lambdas with the same results.
lemma Lambdas()
  ensures ((x: int) => x + x) == ((x: int) => 2 * x)
{
  OnInt((x: int) => x + x, (x: int) => 2 * x);
}

// A named function and a lambda.
function Double(x: int): int { x + x }

lemma NamedAndLambda()
  ensures Double == ((x: int) => 2 * x)
{
  OnInt(Double, (x: int) => 2 * x);
}

// Equality of datatype values whose fields are total functions, one call per field.
datatype R = R(f: int -> int, g: int -> bool)

lemma Records(a: R, b: R)
  requires forall x :: a.f(x) == b.f(x)
  requires forall x :: a.g(x) == b.g(x)
  ensures a == b
{
  OnInt(a.f, b.f);
  OnInt(a.g, b.g);
}

// The result type is arbitrary: a type parameter, a subset type, a function type.
lemma GenericResult<B>(f: int -> B, g: int -> B)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
  OnInt(f, g);
}

lemma NatResult(f: int -> nat, g: int -> nat)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
  OnInt(f, g);
}

lemma Curried(f: int -> int -> int, g: int -> int -> int)
  requires forall x, y :: f(x)(y) == g(x)(y)
  ensures f == g
{
  forall x ensures f(x) == g(x) {
    OnInt(f(x), g(x));
  }
  OnInt(f, g);
}

// The other domains.
lemma Others()
  ensures (() => 1 + 1) == (() => 2)
  ensures ((x: real) => x * 2.0) == ((x: real) => x + x)
  ensures ((b: bool) => !!b) == ((b: bool) => b)
  ensures ((o: ORDINAL) => o.Offset + 0) == ((o: ORDINAL) => o.Offset)
  ensures ((x: int, y: int) => x + y) == ((x: int, y: int) => y + x)
{
  Nullary(() => 1 + 1, () => 2);
  OnReal((x: real) => x * 2.0, (x: real) => x + x);
  OnBool((b: bool) => !!b, (b: bool) => b);
  OnOrdinal((o: ORDINAL) => o.Offset + 0, (o: ORDINAL) => o.Offset);
  OnIntInt((x: int, y: int) => x + y, (x: int, y: int) => y + x);
}

// Equal functions give equal collections built from them.
lemma Images(s: set<int>, f: int -> int, g: int -> int)
  requires forall x :: f(x) == g(x)
  ensures (set x | x in s :: f(x)) == (set x | x in s :: g(x))
  ensures seq(5, i => f(i)) == seq(5, i => g(i))
  ensures map[0 := f] == map[0 := g]
{
  OnInt(f, g);
}

// A nat-indexed family given as an int -> B value: extensional on all of int.
lemma NatFamily(f: int -> int, g: int -> int)
  requires forall n: nat :: f(n) == g(n)
  requires forall x: int :: x < 0 ==> f(x) == 0 && g(x) == 0
  ensures f == g
{
  OnInt(f, g);
}

// Std.RealCompleteness is a verified wrapper of Std.Axioms.Reals.Complete.
import Std.RealCompleteness
import Std.Axioms.Reals

lemma CompleteWrapper(s: iset<real>, w: real, b: real) returns (u: real)
  requires w in s && RealCompleteness.upper(s, b)
  ensures Reals.UpperBound(s, u)
{
  u := RealCompleteness.Complete(s, w, b);
  assert RealCompleteness.upper(s, u);
}
