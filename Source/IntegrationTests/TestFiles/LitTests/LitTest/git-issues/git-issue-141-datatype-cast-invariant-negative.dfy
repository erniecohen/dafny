// RUN: %exits-with 4 %verify --type-system-refresh --general-traits=datatype --general-newtypes --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Invariant<!T> = Invariant(callback: T -> T)
lemma ArbitraryNarrowing(c: Invariant<int>) {
  var unchecked := c as Invariant<nat>; // ERROR: arbitrary stored callback need not return nat
}
lemma NegativeStoredCallback() {
  var f: int -> int := (x: int) => -1;
  var source: Invariant<int> := Invariant(f);
  var unchecked := source as Invariant<nat>; // ERROR: callback is negative on every nat argument
}
