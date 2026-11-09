datatype Box<T> = Box(value:T)
type Positive = b:Box<int> | 0 < b.value witness Box(1)
type AtLeastTwo = b:Positive | 2 <= b.value witness Box(2)
type NatBox = b:Box<nat> | true witness Box(0)

function BadPredicate():Positive { Box(0) as Positive }
function BadNestedPredicate():AtLeastTwo { Box(1) as AtLeastTwo }
function BadGenericMembership():NatBox { Box<int>.Box(-1) as NatBox }
