// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --extended-newtype-bases
// Expected: (!new) characteristic error at the alias; the instantiated actual contains a reference.
class Ref {}
datatype Growing<T> = End | Step(next: Growing<seq<T>>)
newtype Wrapped<T> = Growing<T>
type MustReject(!new) = Wrapped<Ref?>
