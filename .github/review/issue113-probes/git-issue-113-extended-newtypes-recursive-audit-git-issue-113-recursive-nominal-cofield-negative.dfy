// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --extended-newtype-bases
// Expected: recursive constraint-dependency rejection in the call graph.
codatatype Ring<T> = Ring(value: T, next: N<T>)
newtype N<T> = Ring<T>
