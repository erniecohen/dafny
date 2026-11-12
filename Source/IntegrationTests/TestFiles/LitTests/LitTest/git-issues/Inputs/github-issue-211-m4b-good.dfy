module Library {
  trait {:termination false} T {
    method Step(n: nat) decreases {:global} n
  }
  trait {:termination false} Left { method L() decreases {:global} 0 }
  trait {:termination false} Right { method R() decreases {:global} 0 }
  trait SealedChild extends T { }
  method Forward(t: T, n: nat) decreases {:global} n + 1 { t.Step(n); }
  method Leaf(n: nat) decreases {:global} n { }
  method {:termination_closed} Closed() { }
  function {:termination_closed} Rank(n: nat): nat { n }
  const {:termination_closed} Weight: nat := 1
  type {:termination_closed} Small = n: nat | n < 10
  type {:termination_closed} Alias = Small
  datatype {:termination_closed} D = D(n: nat)
  trait Sealed {
    method {:termination_closed} Done()
  }
  class Local extends Sealed {
    method Done() { }
  }
}
module Client {
  import Library
  class Multiple extends Library.Left, Library.Right {
    method L() {}
    method R() {}
  }
  class Implementation extends Library.T {
    method Step(n: nat) { if n > 1 { Library.Forward(this, n - 2); } }
  }
  method General(t: Library.T) decreases {:global} 4 {
    Library.Leaf(4);
    Library.Forward(t, 2);
  }
  method Closed() decreases {:global} 0 { Library.Closed(); }
  function Ranked(n: nat): nat decreases {:global} Library.Rank(n) { n }
  function Constant(): nat decreases {:global} Library.Weight { 0 }
  method Types(s: Library.Alias, d: Library.D) decreases {:global} 0 { }
  method LocalInference() decreases {:global} 0 { Private(); }
  method Private() { }
  function Apply(f: nat -> nat, n: nat): nat
    decreases {:global} 1
    decreases {:callback f} 0
  { f(n) }
  function Named(n: nat): nat decreases {:global} 1 { Apply(Library.Rank, n) }
}
