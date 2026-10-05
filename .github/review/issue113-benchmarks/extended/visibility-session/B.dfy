module Provider0 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client0 {
  import Hidden = Provider0`API
  import Visible = Provider0`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider1 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client1 {
  import Hidden = Provider1`API
  import Visible = Provider1`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider2 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client2 {
  import Hidden = Provider2`API
  import Visible = Provider2`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider3 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client3 {
  import Hidden = Provider3`API
  import Visible = Provider3`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider4 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client4 {
  import Hidden = Provider4`API
  import Visible = Provider4`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider5 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client5 {
  import Hidden = Provider5`API
  import Visible = Provider5`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider6 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client6 {
  import Hidden = Provider6`API
  import Visible = Provider6`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider7 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client7 {
  import Hidden = Provider7`API
  import Visible = Provider7`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider8 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client8 {
  import Hidden = Provider8`API
  import Visible = Provider8`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider9 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client9 {
  import Hidden = Provider9`API
  import Visible = Provider9`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider10 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client10 {
  import Hidden = Provider10`API
  import Visible = Provider10`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider11 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client11 {
  import Hidden = Provider11`API
  import Visible = Provider11`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider12 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client12 {
  import Hidden = Provider12`API
  import Visible = Provider12`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider13 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client13 {
  import Hidden = Provider13`API
  import Visible = Provider13`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider14 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client14 {
  import Hidden = Provider14`API
  import Visible = Provider14`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider15 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client15 {
  import Hidden = Provider15`API
  import Visible = Provider15`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider16 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client16 {
  import Hidden = Provider16`API
  import Visible = Provider16`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider17 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client17 {
  import Hidden = Provider17`API
  import Visible = Provider17`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider18 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client18 {
  import Hidden = Provider18`API
  import Visible = Provider18`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider19 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client19 {
  import Hidden = Provider19`API
  import Visible = Provider19`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider20 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client20 {
  import Hidden = Provider20`API
  import Visible = Provider20`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider21 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client21 {
  import Hidden = Provider21`API
  import Visible = Provider21`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider22 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client22 {
  import Hidden = Provider22`API
  import Visible = Provider22`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider23 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client23 {
  import Hidden = Provider23`API
  import Visible = Provider23`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider24 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client24 {
  import Hidden = Provider24`API
  import Visible = Provider24`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider25 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client25 {
  import Hidden = Provider25`API
  import Visible = Provider25`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider26 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client26 {
  import Hidden = Provider26`API
  import Visible = Provider26`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider27 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client27 {
  import Hidden = Provider27`API
  import Visible = Provider27`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider28 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client28 {
  import Hidden = Provider28`API
  import Visible = Provider28`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider29 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client29 {
  import Hidden = Provider29`API
  import Visible = Provider29`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider30 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client30 {
  import Hidden = Provider30`API
  import Visible = Provider30`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider31 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client31 {
  import Hidden = Provider31`API
  import Visible = Provider31`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider32 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client32 {
  import Hidden = Provider32`API
  import Visible = Provider32`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider33 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client33 {
  import Hidden = Provider33`API
  import Visible = Provider33`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider34 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client34 {
  import Hidden = Provider34`API
  import Visible = Provider34`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider35 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client35 {
  import Hidden = Provider35`API
  import Visible = Provider35`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider36 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client36 {
  import Hidden = Provider36`API
  import Visible = Provider36`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider37 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client37 {
  import Hidden = Provider37`API
  import Visible = Provider37`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider38 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client38 {
  import Hidden = Provider38`API
  import Visible = Provider38`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider39 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client39 {
  import Hidden = Provider39`API
  import Visible = Provider39`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider40 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client40 {
  import Hidden = Provider40`API
  import Visible = Provider40`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider41 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client41 {
  import Hidden = Provider41`API
  import Visible = Provider41`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider42 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client42 {
  import Hidden = Provider42`API
  import Visible = Provider42`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider43 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client43 {
  import Hidden = Provider43`API
  import Visible = Provider43`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider44 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client44 {
  import Hidden = Provider44`API
  import Visible = Provider44`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider45 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client45 {
  import Hidden = Provider45`API
  import Visible = Provider45`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider46 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client46 {
  import Hidden = Provider46`API
  import Visible = Provider46`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider47 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client47 {
  import Hidden = Provider47`API
  import Visible = Provider47`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider48 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client48 {
  import Hidden = Provider48`API
  import Visible = Provider48`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
module Provider49 {
  export API provides Value, Decode, Encode, RoundTrip reveals Box
  export Visible reveals Value, Box, Decode, Encode provides RoundTrip
  datatype Box = Box(value: int)
  type Value = Box
  function Encode(b: Box): Value { b }
  function Decode(v: Value): Box { v }
  lemma RoundTrip(b: Box) ensures Decode(Encode(b)) == b {}
}
module Client49 {
  import Hidden = Provider49`API
  import Visible = Provider49`Visible
  lemma HiddenRoundTrip(b: Hidden.Box) ensures Hidden.Decode(Hidden.Encode(b)) == b { Hidden.RoundTrip(b); }
  lemma VisibleRoundTrip(b: Visible.Box) ensures Visible.Decode(Visible.Encode(b)) == b {}
}
