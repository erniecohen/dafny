module RejectedSummaries {
  import opened Std.Termination.Orders
  datatype Box = Box(f: (nat, nat) -> bool)

  // A stored parameter is not an application-only helper.
  ghost function {:termination_closed} Stored(r: (nat, nat) -> bool): Wfo<nat>
    requires wellFounded<nat>(r)
  { var box := Box(r); Of<nat>(box.f) }

  ghost function {:termination_closed} Returned(r: (nat, nat) -> bool): (nat, nat) -> bool { r }
  ghost function {:termination_closed} Captured(r: (nat, nat) -> bool): (nat, nat) -> bool {
    (x: nat, y: nat) => r(x, y)
  }
  ghost function {:termination_closed} ViaReturned(r: (nat, nat) -> bool): Wfo<nat>
    requires wellFounded<nat>(r)
  { Of<nat>(Returned(r)) }
  ghost function {:termination_closed} ViaCaptured(r: (nat, nat) -> bool): Wfo<nat>
    requires wellFounded<nat>(r)
  { Of<nat>(Captured(r)) }

  ghost predicate Unknown(r: (nat, nat) -> bool) { r(0, 0) }
  ghost predicate {:termination_closed} Unsummarized(r: (nat, nat) -> bool) { Unknown(r) }

  // Applying r inside an escaping lambda must not hide the escape.
  ghost function {:termination_closed} Escaping(r: (nat, nat) -> bool): Box {
    Box((x: nat, y: nat) => r(x, y))
  }
  ghost function {:termination_closed} ViaEscaping(r: (nat, nat) -> bool): Wfo<nat>
    requires wellFounded<nat>(r)
  { Of<nat>(Escaping(r).f) }

  // Ordinary n termination is proved, but a cycle cannot invent an effect summary.
  ghost function {:termination_closed} A(r: (nat, nat) -> bool, n: nat): Wfo<nat>
    requires wellFounded<nat>(r)
    decreases n
  { if n == 0 then Of<nat>(r) else B(r, n - 1) }
  ghost function {:termination_closed} B(r: (nat, nat) -> bool, n: nat): Wfo<nat>
    requires wellFounded<nat>(r)
    decreases n
  { if n == 0 then Of<nat>(r) else A(r, n - 1) }

  ghost function {:termination_closed} ApplyHelper(r: nat -> nat): nat { r(0) }
  lemma {:termination_closed} ApplyLemma(r: nat -> nat) { assert r(0) == r(0); }
  // Forwarding must still account for the actual argument in an ordinary caller.
  method {:termination_closed} FunctionActual(r: nat -> nat) {
    assert ApplyHelper(r) == ApplyHelper(r);
  }
  method {:termination_closed} LemmaActual(r: nat -> nat) { ApplyLemma(r); }

}

// Previously checked callers used an unconditional Closed promise, with no argument effect.
abstract module ClosedSignature {
  ghost function {:termination_closed} Apply(r: nat -> nat): nat
  method {:termination_closed} OldCaller(r: nat -> nat) { assert Apply(r) == Apply(r); }
}
module InvalidImplementation refines ClosedSignature {
  ghost function {:termination_closed} Apply(r: nat -> nat): nat { r(0) }
}

module AppliedFunctionEscape {
  ghost function {:termination_closed} Escaped(factory: nat -> (nat -> nat)): nat -> nat { factory(0) }
}
