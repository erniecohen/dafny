import opened Std.Termination.Orders

ghost predicate Up(x: nat, y: nat) { x < y <= 100 }
lemma {:axiom} UpWellFounded() ensures wellFounded<nat>(Up)
ghost function UpOrder(): Wfo<nat> { UpWellFounded(); Of(Up) }

ghost predicate Down(x: nat, y: nat) { y < x }
lemma {:axiom} DownWellFounded() ensures wellFounded<nat>(Down)
ghost function DownOrder(): Wfo<nat> { DownWellFounded(); Of(Down) }

// Error: the argument goes down, but UpOrder wants it to go up.
function WrongWay(i: nat): nat
  requires i <= 100
  decreases i by UpOrder()
{
  if i == 0 then 0 else WrongWay(i - 1)
}

// Error: the caller and the callee compare by different orders.
function A(i: nat): nat
  requires i <= 100
  decreases i by UpOrder()
{
  if i == 100 then 0 else B(i + 1)
}

function B(i: nat): nat
  requires i <= 100
  decreases i by DownOrder()
{
  if i == 0 then 0 else A(i - 1)
}

// Error: every pair is related, so the relation is not well-founded, and WfoOf relates nothing by it.
ghost function Always(): Wfo<nat> {
  WfoOf((x: nat, y: nat) => true)
}

function Forever(i: nat): nat
  decreases i by Always()
{
  Forever(i)
}

// Error: an order whose precondition does not hold.
ghost function OrderAt(k: nat): Wfo<nat>
  requires k == 0
{
  UpOrder()
}

function IllFormed(i: nat): nat
  requires i <= 100
  decreases i by OrderAt(1)
{
  if i == 100 then 0 else IllFormed(i + 1)
}

// Error: a loop whose variable goes the wrong way.
method LoopWrongWay() {
  var i: nat := 100;
  while 0 < i
    decreases i by UpOrder()
  {
    i := i - 1;
  }
}
