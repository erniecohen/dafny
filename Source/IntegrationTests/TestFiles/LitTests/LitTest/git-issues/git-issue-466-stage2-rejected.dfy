// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), second
// stage: programs with recursive module groups that the resolver rejects.  Each is rejected for
// the reason its declarations would be rejected in one module.
// RUN: %exits-with 2 %resolve --member-invariants --recursive-modules "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// a cycle of constants
module Const1 {
  import Const2
  const x: int := Const2.y + 1
  lemma L()
    ensures false
  {
    assert x == Const2.y + 1;
    assert Const2.y == x;
  }
}
module Const2 {
  import Const1
  const y: int := Const1.x
}

// a constraint that depends on its own type
module Subset1 {
  import Subset2
  type Pos = x: int | Subset2.Ok(x) witness *
}
module Subset2 {
  import Subset1
  predicate Ok(x: int) {
    var p: Subset1.Pos :| true;
    p == x
  }
}
module Newtype1 {
  import Newtype2
  newtype N = x: int | Newtype2.Ok(x as int)
}
module Newtype2 {
  import Newtype1
  predicate Ok(x: int) { forall n: Newtype1.N :: n as int == x }
}

// a function used as a value in its own recursion
module Naked1 {
  import Naked2
  function F(n: nat): nat {
    var g := Naked2.G;
    g(n) + 1
  }
}
module Naked2 {
  import Naked1
  function G(n: nat): nat { Naked1.F(n) }
}

// a datatype used left of an arrow in its own definition
module Negative1 {
  import Negative2
  datatype D = D(f: Negative2.E -> int)
}
module Negative2 {
  import Negative1
  datatype E = E(d: Negative1.D)
}

// mutual recursion in a tail-recursive function
module Tail1 {
  import Tail2
  function {:tailrecursion} F(n: nat, acc: nat): nat {
    if n == 0 then acc else Tail2.G(n - 1, acc + 1)
  }
}
module Tail2 {
  import Tail1
  function G(n: nat, acc: nat): nat {
    if n == 0 then acc else Tail1.F(n - 1, acc + 1)
  }
}

// equality of a type that does not support it
module Equality1 {
  import Equality2
  datatype D<T> = D0 | D1(e: Equality2.E<T>)
  method M(f: D<int -> int>) {
    var b := f == f;
  }
  method N<U>(a: D<U>) {
    var b := a == a;
  }
}
module Equality2 {
  import Equality1
  datatype E<T> = E(d: Equality1.D<T>, t: T)
}

// equality of a datatype that does not support it because of a datatype of another member
module Equality3 {
  import Equality4
  datatype NoEq = NoEq(f: int -> int, v: Equality4.V)
}
module Equality4 {
  import Equality3
  datatype V = V(n: int)
  datatype W = W(n: Equality3.NoEq)
  method M(x: W, y: W) returns (b: bool) {
    b := x == y;
  }
}

// a type parameter that must support equality because of a datatype of another member, which is
// found only when the members repeat the inference
module Equality5 {
  import Equality6
  datatype D<T> = D(e: Equality6.E<T>)
  method M(d: D<int -> int>) { }
}
module Equality6 {
  import Equality5
  datatype E<T> = E(s: set<T>)
}
