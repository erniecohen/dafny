newtype Id<T> = x: T | true witness *
newtype IntTwice = Id<Id<int>>
newtype RealTwice = Id<Id<real>>
function Add(x: IntTwice, y: IntTwice): IntTwice { x + y }
function Modulo(x: IntTwice, y: IntTwice): IntTwice { x % y }
function RealAdd(x: RealTwice, y: RealTwice): RealTwice { x + y }
