// RUN: %exits-with 2 %verify --type-system-refresh --general-traits=datatype --general-newtypes --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

lemma UnrelatedTuplePermutation(p: (int, bool)) {
  var bad := p as (bool, int); // Corresponding arguments are unrelated.
}
