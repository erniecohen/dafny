// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --extended-newtype-bases
// Feature wrapper control for forwarding generalArrows; no independent #132 product change.
datatype Growing<T> = End | Step(next: Growing<seq<T>>)
newtype Wrapped<T> = Growing<T>
type MustReject(!new) = Wrapped<int ~> int>
