// RUN: %exits-with 4 %verify --type-system-refresh --general-traits=datatype --general-newtypes --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Provider {
  datatype Pair<+A, +B> = Pair(left: A, right: B)
  function Mixed(): (p: Pair<int, int>)
    ensures p is Pair<nat, int>
    ensures !(p is Pair<int, nat>)
  { Pair(0, -1) }
  export API provides Pair, Mixed
}
module HiddenClient {
  import P = Provider`API
  lemma SwappedArguments() {
    var p := P.Mixed();
    assert p is P.Pair<int, nat>;
  }
}
