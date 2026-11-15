module Library {
  trait {:termination false} T {
    method F() decreases {:global} 0
    method {:termination_closed} BadSlot() decreases {:global} 0
  }
  method {:termination_closed} Dispatch(t: T) { t.F(); }
  function {:termination_closed} Unknown(f: nat -> nat, n: nat): nat { f(n) }
  function {:termination_closed} Callback(f: nat -> nat, n: nat): nat
    decreases {:global} 1
    decreases {:callback f} 0
  { f(n) }
  method General() { }
  function Value(): int { 0 }
}
module Client {
  import Library
  method {:termination_closed} BadImport() { Library.General(); }
}
module Evaluations {
  import Library
  const {:termination_closed} C: int := Library.Value()
  type {:termination_closed} S = x: int | Library.Value() == x witness *
  type {:termination_closed} Alias = S
  trait Base { const field: S }
  class {:termination_closed} Derived extends Base {}
  method {:termination_closed} Default(x: int := Library.Value()) {}
}
