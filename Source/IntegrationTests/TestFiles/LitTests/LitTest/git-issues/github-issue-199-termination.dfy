// RUN: %exits-with 4 %verify --type-system-refresh --lemma-bindings "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: the results of a lemma that are bound in an
// expression are sound only because the binding is a call of the lemma, which must terminate.  A
// lemma whose specification or proof depends on its own bound results must be rejected, as the
// same cycle through a call statement is.  Each module below has one such cycle, which would
// otherwise prove false, and each must get a termination error on at least one of its calls, as
// the same cycle written with a call statement does.  The modules keep the cycles apart: what a
// rejected cycle says may be inconsistent, and must not hide the error of another.

// ---------- The body of a lemma binds the lemma's own results

module OwnResults {
  lemma Own(n: nat) returns (m: nat)
    ensures m > n
  {
    var k := (var m := Own(n); m);  // error: termination
    m := k;
  }
}

module ThroughAFunction {
  lemma ThroughFunction(n: nat) returns (m: nat)
    ensures m > n
  {
    m := BindThroughFunction(n);  // error: termination
  }

  ghost function BindThroughFunction(n: nat): nat {
    var m := ThroughFunction(n); m  // error: termination
  }
}

// ---------- The postcondition of a lemma mentions a function that binds the lemma's results

module PostconditionWithBody {
  ghost function F(n: nat): nat {
    var m := L(n); m  // error: termination
  }

  lemma L(n: nat) returns (m: nat)
    ensures m == F(n) + 1  // error: termination
  {
    m := F(n) + 1;  // error: termination
  }
}

module PostconditionWithoutBody {
  ghost function F(n: nat): nat {
    var m := L(n); m  // error: termination
  }

  lemma {:axiom} L(n: nat) returns (m: nat)
    ensures m == F(n) + 1  // error: termination
}

// ---------- The postcondition of a lemma binds the lemma's own results

module OwnPostconditionWithoutBody {
  lemma {:axiom} L(n: nat) returns (m: nat)
    ensures m == (var k := L(n); k) + 1  // error: termination
}

module OwnPostconditionWithBody {
  lemma L(n: nat) returns (m: nat)
    ensures (var k := L(n); k) < m  // error: termination
  {
    m := 0;
  }
}

// ---------- The precondition of a lemma mentions a function that binds the lemma's results

module Precondition {
  ghost function F(n: nat): nat {
    var m := L(n); m  // error: termination
  }

  lemma L(n: nat) returns (m: nat)
    requires F(n) < F(n)  // error: termination
    ensures false
  {
  }
}

// ---------- The cycle goes through a function's postcondition, a lambda, a default value, a third lemma

module FunctionPostcondition {
  ghost function F(n: nat): nat
    ensures (var m := L(n); m) == F(n) + 1  // error: termination
  {
    0
  }

  lemma {:axiom} L(n: nat) returns (m: nat)
    ensures m == F(n) + 1  // error: termination
}

module Lambda {
  ghost function F(): nat -> nat {
    (n: nat) => var m: nat := L(n); m  // (the call from the lemma back to F is the one that is reported)
  }

  lemma {:axiom} L(n: nat) returns (m: nat)
    ensures m == F()(n) + 1  // error: termination
}

module DefaultValue {
  ghost function F(n: nat, k: nat := (var m := L(n); m)): nat {  // error: termination
    k
  }

  lemma {:axiom} L(n: nat) returns (m: nat)
    ensures m == F(n) + 1  // (the call from F to the lemma is the one that is reported)
}

module ThirdLemma {
  lemma {:axiom} L(n: nat) returns (m: nat)
    ensures (Third(n); m > m)  // error: termination

  lemma {:axiom} Third(n: nat)
    requires (var m := L(n); m) >= 0  // error: termination
}

// ---------- With decreases clauses, the calls that decrease are accepted and the others are not

module Decreases {
  ghost function F(n: nat): nat
    decreases n, 1
  {
    var m := L(n); m + 1
  }

  lemma L(n: nat) returns (m: nat)
    decreases n, 0
    ensures m == F(n)  // error: termination
  {
    m := 0;
  }
}

module DecreasingPostcondition {
  // The postcondition binds results of the lemma at a smaller argument, which is fine.
  lemma Chain(n: nat) returns (m: nat)
    ensures m >= n
    ensures n > 0 ==> (var k := Chain(n - 1); k) < m
  {
    if n == 0 {
      m := 0;
    } else {
      m := (var k := Chain(n - 1); k) + 1;
    }
  }

  ghost function ChainResult(n: nat): nat {
    var m := Chain(n); m
  }

  lemma ChainIncreases(n: nat)
    requires n > 0
    ensures ChainResult(n - 1) < ChainResult(n)
  {
  }

  lemma ChainIsNotKnown()
    ensures ChainResult(1) == 1  // error
  {
  }
}
