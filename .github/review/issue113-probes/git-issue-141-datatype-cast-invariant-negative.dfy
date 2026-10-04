// Private isolated invariant membership rejection control; unrun.
// Use --type-system-refresh=true. No issue113 option is needed.
datatype Invariant<!T> = Invariant(callback: T -> T)
lemma ArbitraryNarrowing(c: Invariant<int>) {
  var unchecked := c as Invariant<nat>; // ERROR: arbitrary stored callback need not return nat
}
lemma NegativeStoredCallback() {
  var f: int -> int := (x: int) => -1;
  var source: Invariant<int> := Invariant(f);
  var unchecked := source as Invariant<nat>; // ERROR: callback is negative on every nat argument
}
