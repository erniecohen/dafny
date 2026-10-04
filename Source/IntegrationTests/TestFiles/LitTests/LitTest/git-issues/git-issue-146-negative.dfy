// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Provider {
  datatype Box<+T> = Box(value: T)
  function MakeZero(): (b: Box<int>)
    ensures b is Box<nat>
  { Box(0) }
  function MakeNegative(): (b: Box<int>)
    ensures !(b is Box<nat>)
  { Box(-1) }
  export API provides Box, MakeZero, MakeNegative
}
module HiddenClient {
  import P = Provider`API
  lemma MissingMembership(b: P.Box<int>) {
    assert b is P.Box<nat>;
  }
  lemma NegativeValue() {
    var negative := P.MakeNegative();
    assert negative is P.Box<nat>;
  }
  lemma InhabitedNonvacuity() {
    var zero := P.MakeZero();
    assert zero is P.Box<nat>;
    assert false;
  }
}
