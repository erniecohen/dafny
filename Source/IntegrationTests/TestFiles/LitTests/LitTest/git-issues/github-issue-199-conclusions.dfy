// RUN: %exits-with 4 %verify --type-system-refresh --lemma-bindings --statement-expression-conclusions "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: --statement-expression-conclusions asks each statement of a
// boolean statement expression in an assertion for its conclusion.  The call of a lemma whose first result is a
// boolean is such a statement.  Its conclusion is "true": what the call establishes is known from the binding
// itself, under quantifiers and connectives, too.

lemma IsEven(n: nat) returns (b: bool)
  ensures b <==> n % 2 == 0
{
  b := n % 2 == 0;
}

lemma Halve(n: nat) returns (even: bool, half: nat)
  ensures even ==> n == 2 * half
{
  even, half := n % 2 == 0, n / 2;
}

method Asserts(k: nat) {
  assert var b := IsEven(4); b;
  assert forall n: nat :: (var b := IsEven(2 * n); b);
  assert forall n: nat :: (var b := IsEven(n); b) || (var b := IsEven(n + 1); b);
  assert !(var b := IsEven(2 * k + 1); b);
  assert forall n: nat :: (var even, half := Halve(n); even ==> n == 2 * half);
}

method StillChecked(k: nat) {
  assert forall n: nat :: (var b := IsEven(n); b);  // error: not every number is even
  assert false;  // error: the bindings above do not make everything provable
}

lemma {:axiom} EvenAbove(n: nat) returns (b: bool)
  requires n > 10
  ensures b

method PreconditionStillChecked() {
  assert forall n: nat :: (var b := EvenAbove(n); b);  // error: precondition of EvenAbove
}
