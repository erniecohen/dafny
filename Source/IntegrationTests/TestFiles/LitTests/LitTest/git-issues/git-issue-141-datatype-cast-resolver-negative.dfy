// RUN: %exits-with 2 %verify --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

lemma UnrelatedTuplePermutation(p: (int, bool)) {
  var bad := p as (bool, int); // Corresponding arguments are unrelated.
}
