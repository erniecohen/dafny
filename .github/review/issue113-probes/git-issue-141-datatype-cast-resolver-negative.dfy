// Private resolver controls, isolated from verification failures.
// Use --type-system-refresh=true. No issue113 option is needed.
datatype Invariant<!T> = Invariant(callback: T -> T)
lemma Invariance(c: Invariant<int>) {
  var bad := c as Invariant<nat>; // ERROR: invariant type arguments cannot differ
}
lemma UnrelatedTuplePermutation(p: (int, bool)) {
  var bad := p as (bool, int); // ERROR: corresponding arguments are unrelated
}
