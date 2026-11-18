// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %exits-with 2 %resolve --show-hints "%s" >> "%t"
// RUN: %exits-with 2 %resolve --lemma-bindings:false "%s" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --lemma-bindings "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: without --lemma-bindings, a call of a lemma in the
// right-hand side of a let expression is the error it has always been.  With --show-hints, a hint names the
// option where it would apply.  The option needs --type-system-refresh, which the last command does not give.

lemma Find(n: nat) returns (m: nat)
  ensures m > n
{
  m := n + 1;
}

method M(n: nat) returns (m: nat)

datatype Result = Success(value: nat) | Failure {
  predicate IsFailure() { Failure? }
  function PropagateFailure(): Result { this }
  function Extract(): nat requires Success? { value }
}
lemma Fallible(n: nat) returns (r: Result)

ghost function Above(n: nat): nat {
  var m := Find(n); m  // error: a lemma call in an expression
}

ghost function WithGhost(n: nat): nat {
  ghost var m := Find(n); m  // error: a lemma call in an expression
}

ghost function Method(n: nat): nat {
  var m := M(n); m  // error: a method call in an expression (and no hint)
}

ghost function ElephantOperator(n: nat): Result {
  var a :- Fallible(n); Success(a)  // error: a lemma call in an expression (and no hint)
}

ghost function NotTheRightHandSide(n: nat): nat {
  var m := 1 + Find(n); m  // error: a lemma call in an expression (and no hint)
}
