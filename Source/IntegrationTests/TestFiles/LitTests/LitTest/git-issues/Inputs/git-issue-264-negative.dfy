method Shorter() {
  assert (1.0, 0 nonincreases to 0.5); // TOP cannot pass a proper component
}
method NegativeInt() {
  assert (-1 decreases to -2); // no further progress from a negative old value
}
method NegativeReal() {
  assert (-1.0 nonincreases to -2.0);
}
method WeakPrefix() {
  assert (1.0, -1 nonincreases to 0.5, -2); // weak progress cannot excuse a negative bound
}
function NegativeRecursion(n: int): int
  requires n < 0
  decreases n
{ NegativeRecursion(n - 1) }
method FalseControl() {
  assert (1.0, 1 decreases to 0.5, 0);
  assert false;
}
method ConjunctionLocation() {
  assert (1 decreases to 0) && (0 decreases to 1);
}
