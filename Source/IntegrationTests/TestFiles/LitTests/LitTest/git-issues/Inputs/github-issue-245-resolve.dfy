import opened Std.Termination.Orders

ghost predicate Up(x: nat, y: nat) { x < y <= 100 }
lemma {:axiom} UpWellFounded() ensures wellFounded<nat>(Up)
ghost function UpOrder(): Wfo<nat> { UpWellFounded(); Of(Up) }
ghost function UpOrderAt(k: nat): Wfo<nat> { UpOrder() }

// Error (R1): the order is a plain relation, not a Wfo.
function NotWfo(i: nat): nat
  decreases i by (x: nat, y: nat) => x < y
{
  0
}

// Error (R1): the component has type int, the order is on nat.
function WrongElement(i: int): nat
  decreases i by UpOrder()
{
  0
}

// Error (R2): the order depends on a parameter.
function NotClosed(i: nat, k: nat): nat
  decreases i by UpOrderAt(k)
{
  0
}

// Error: not in the decreases clause of an iterator.
iterator Iter(i: nat)
  decreases i by UpOrder()
{
}


// Error (R1): the order has the type of an implementation, not exactly Wfo<nat>.
function NotExactlyWfo(i: nat): nat
  decreases i by WfoOf<nat>.WfoOf(Up)
{
  0
}

// Error (R3): Helper has a component by an order, so it is treated as calling Wfo.gt, whose override
// Loopy.gt calls Helper.
datatype {:AssumeCrossModuleTermination} Loopy extends Wfo<nat> = Loopy {
  ghost predicate gt(x: nat, y: nat) { Helper(x) && x < y <= 100 }
  ghost function aha(c: Chain<nat>): (r: nat) ensures !gt(c(r), c(r + 1)) { 0 }
}

ghost function LoopyOrder(): Wfo<nat> { Loopy }

ghost function Helper(i: nat): bool
  decreases i by LoopyOrder()
{
  true
}
