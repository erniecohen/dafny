datatype Box<T> = Box(value:T)
type Positive = b:Box<int> | 0 < b.value witness Box(1)
type AtLeastTwo = b:Positive | 2 <= b.value witness Box(2)
type Alias = AtLeastTwo
type NatBox = b:Box<nat> | true witness Box(0)
type Nonempty<T> = b:Box<seq<T>> | 0 < |b.value| witness *
datatype Pair<A,B> = Pair(first:A, second:B)
type NatSecond<A> = p:Pair<A,nat> | true witness *

function PositiveCast():Positive { Box(1) as Positive }
function NestedCast():AtLeastTwo { Box(2) as AtLeastTwo }
function AliasCast():Alias { Box(3) as Alias }
function GenericCast():NatBox { Box<int>.Box(0) as NatBox }
function GenericSubset<T>(value:T):Nonempty<T> { Box([value]) as Nonempty<T> }
function Reordered(value:int):NatSecond<int> { Pair<int,int>.Pair(value,0) as NatSecond<int> }
