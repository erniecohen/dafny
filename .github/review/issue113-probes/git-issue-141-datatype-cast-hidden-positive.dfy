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
    requires b is P.Box<nat>
    ensures r as P.Box<int> == b
  {
    b as P.Box<nat>
  }
  export API provides Narrow
}
module RevealClient {
  import P = Provider`Reveal
  import H = HiddenClient`API
  lemma Valid() {
    var zero := P.MakeZero();
    assert zero is P.Box<nat>;
    var narrowed := H.Narrow(zero);
    assert narrowed.value == 0;
  }
}
