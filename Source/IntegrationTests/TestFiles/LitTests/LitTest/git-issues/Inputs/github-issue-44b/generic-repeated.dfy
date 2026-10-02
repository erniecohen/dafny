newtype G<T> = x: T | true witness *
newtype IntTwice = G<G<int>>
newtype RealTwice = G<G<real>>
function IntegerIdentity(x: IntTwice): IntTwice { x }
function RealIdentity(x: RealTwice): RealTwice { x }
