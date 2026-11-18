// RUN: %exits-with 2 %resolve --lemma-bindings --modular-termination --show-hints "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: under --modular-termination, an expression that binds the
// results of a lemma calls the lemma, and the call is classified as the call in a statement expression
// "L(args); e" is.  Each function below that binds results is followed by one that calls a lemma of the same
// kind in the other form, and the two get the same hint and the same verdict.

module A {
  lemma Find(n: nat) returns (m: nat)
    ensures m > n
  {
    m := n + 1;
  }

  lemma Plain(n: nat)
    ensures n >= 0
  {
  }
}

module B {
  import A

  trait T {
    lemma Find(n: nat) returns (m: nat)
      ensures m > n

    lemma Plain(n: nat)
      ensures n >= 0
  }

  ghost function Imported(n: nat): nat {
    var m := A.Find(n); m
  }

  ghost function ImportedCall(n: nat): nat {
    A.Plain(n); n
  }

  ghost function Dispatched(t: T, n: nat): nat {
    var m := t.Find(n); m
  }

  ghost function DispatchedCall(t: T, n: nat): nat {
    t.Plain(n); n
  }

  ghost function {:termination_closed} Closed(t: T, n: nat): nat {  // error: the promise is not established
    var m := t.Find(n); m
  }

  ghost function {:termination_closed} ClosedCall(t: T, n: nat): nat {  // error: the promise is not established
    t.Plain(n); n
  }
}
