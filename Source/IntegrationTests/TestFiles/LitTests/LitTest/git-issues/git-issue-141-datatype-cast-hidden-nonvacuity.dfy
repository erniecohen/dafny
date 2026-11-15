// RUN: %exits-with 4 %verify --type-system-refresh --general-traits=datatype --general-newtypes --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
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
  function Checked(b: P.Box<int>): (r: P.Box<nat>)
    requires b is P.Box<nat>
    ensures r as P.Box<int> == b
  { b as P.Box<nat> }
  lemma UniversalRoundTrip(b: P.Box<int>)
    requires b is P.Box<nat>
  {
    var r := Checked(b);
    assert r as P.Box<int> == b;
  }
  lemma InhabitedZero() {
    var zero := P.MakeZero();
    assert zero is P.Box<nat>;
    var narrowed := Checked(zero);
    assert narrowed as P.Box<int> == zero;
    assert false; // ERROR: the checked opaque type has an inhabited value
  }
}
