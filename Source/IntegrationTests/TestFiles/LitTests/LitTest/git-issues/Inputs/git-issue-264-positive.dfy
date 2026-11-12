// Fractional real progress is weak nonincrease and permits an unchanged suffix.
trait T {
  method M(r: real)
    requires r >= 0.0
    decreases r, 0
}
class C extends T {
  method M(r: real)
    requires r >= 0.0
    decreases r / 2.0, 0
  {}
}

method Tuples() {
  assert (1.0, 0 nonincreases to 0.5, 0);
  assert (1.0, 1 decreases to 0.5, 0);
  assert (1.0, 0 decreases to 0.5, 0, 0); // proper < TOP
  assert (1, -100 decreases to 0, 1000);  // early strict progress permits a reset
  assert (-2 nonincreases to -2);         // negative values remain reflexive
  assert (0 decreases to -1);             // a terminal negative value
  assert (0.0 decreases to -1.0);
  assert (2 nonincreases to 2, 0);        // proper < TOP in a weak check too
}

function IntTerminal(n: int): int
  decreases n
{ if n < 0 then 0 else IntTerminal(n - 1) }

function RealTerminal(r: real): int
  decreases r
{ if r < 0.0 then 0 else RealTerminal(r - 1.0) }

function EarlyStrict(n: nat, suffix: int): int
  decreases n, suffix
{ if n == 0 then suffix else EarlyStrict(n - 1, suffix + 100) }

method FractionalLoop(r0: real, n0: nat)
  requires r0 >= 0.0
{
  var r, n := r0, n0;
  while n > 0
    invariant r >= 0.0 && n >= 0
    decreases r, n
  {
    r := r / 2.0;
    n := n - 1;
  }
}

method NegativeTerminalLoop() {
  var n := 0;
  while n >= 0
    decreases n
  { n := n - 1; }
}
