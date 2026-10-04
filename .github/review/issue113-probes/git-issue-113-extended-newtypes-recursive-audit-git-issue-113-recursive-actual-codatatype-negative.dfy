// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --extended-newtype-bases
// Expected: resolver cyclic-dependency error before co-call analysis.
codatatype C<T> = Node(value: T, next: C<T>)
newtype N = C<N>
