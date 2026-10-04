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
  function Unchecked(b: P.Box<int>): (r: P.Box<nat>)
    ensures r as P.Box<int> == b
  { b as P.Box<nat> } // ERROR: opaque target membership is unproved
  lemma NegativeIntroduction() {
    var negative := P.MakeNegative();
    assert !(negative is P.Box<nat>);
    var invalid := negative as P.Box<nat>; // ERROR: provider excludes target membership
  }
}
