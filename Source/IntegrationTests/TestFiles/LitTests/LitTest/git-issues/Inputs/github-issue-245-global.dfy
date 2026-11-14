// The bundled standard library cannot be loaded under --modular-termination, so this file
// has no Wfo; the clause is rejected before its order is resolved as one.
ghost function Order(): (nat, nat) -> bool { (x: nat, y: nat) => x < y }

// Error: not in a clause of --modular-termination.
method Global(i: nat)
  decreases {:global} i by Order()
{
}
