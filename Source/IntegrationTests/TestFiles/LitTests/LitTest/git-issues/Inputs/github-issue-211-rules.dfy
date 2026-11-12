// Overrides, the type of a bound, and well-formedness of bounds, under --modular-termination.
module Tr {
  trait {:termination false} T {
    method M(other: T, n: int) decreases {:global} n
    function F(n: nat): nat decreases {:global} n
    // An int bound must stay at or above 0 when it goes down.
    method I(other: T, n: int) decreases {:global} n
    // A bound must be well formed: 10 / n is not, at n == 0.
    method D(n: int) decreases {:global} 10 / n
    // Lexicographic bounds compare as decreases tuples do.
    method Lex(other: T, a: nat, b: nat) decreases {:global} a, b
  }
}

module Impl {
  import Tr
  class C extends Tr.T {
    // An override's global bound may not exceed the trait's: rejected.
    method M(other: Tr.T, n: int) decreases {:global} n + 1 { }
    // Equal to the trait's: fine.
    function F(n: nat): nat decreases {:global} n { 0 }
    // n - 1 may be below 0: rejected.
    method I(other: Tr.T, n: int) { other.I(this, n - 1); }
    method D(n: int) { }
    // (a, b) goes down to (a - 1, any) or (a, b - 1): verifies.
    method Lex(other: Tr.T, a: nat, b: nat) {
      if b != 0 {
        other.Lex(this, a, b - 1);
      } else if a != 0 {
        other.Lex(this, a - 1, 1000);
      }
    }
  }
}

// The defining trait declares checked extensibility. The child marker is redundant on-mode.
module Base {
  trait {:termination false} U {
    method Go(n: nat) decreases {:global} n
  }
}

module Ext {
  import Base
  @AssumeCrossModuleTermination
  class E extends Base.U {
    // Rejected: the same bound is not below it.
    method Go(n: nat) { var u: Base.U := this; u.Go(n); }
  }
}

// A trait whose implementations are all in its own module is not open: its calls are
// checked as before, and need no global bound.
module Closed {
  trait V {
    method Go(n: nat)
  }
  class W extends V {
    method Go(n: nat) decreases n { if n != 0 { var v: V := this; v.Go(n - 1); } }
  }
}
