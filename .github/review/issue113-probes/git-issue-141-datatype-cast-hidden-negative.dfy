// Private opaque-provider contract draft; not executed.
// Use --type-system-refresh=true. No issue113 option is needed.
module Provider {
  datatype Box<+T> = Box(value: T)
  function MakeNegative(): Box<int> { Box(-1) }
  function MakeZero(): Box<int> { Box(0) }
  export API provides Box, MakeNegative, MakeZero
  export Reveal reveals Box, MakeNegative, MakeZero
}
module HiddenClient {
  import P = Provider`API
  function Narrow(b: P.Box<int>): (r: P.Box<nat>)
    ensures r as P.Box<int> == b
  {
    b as P.Box<nat> // ERROR: a provided carrier must establish nominal target membership
  }
  export API provides P, Narrow
}
module RevealClient {
  import P = Provider`Reveal
  import H = HiddenClient`API
  lemma Contradiction() {
    var negative := P.MakeNegative();
    var narrowed := H.Narrow(negative);
    assert narrowed as P.Box<int> == negative;
    assert narrowed.value == negative.value;
    assert narrowed.value == -1;
    assert false;
  }
}
