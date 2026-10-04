// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --extended-newtype-bases
// Expected: recursive constraint-dependency rejection in the call graph, even with a base constructor.
datatype Grounded = Base | Recursive(value: N)
newtype N = Grounded
