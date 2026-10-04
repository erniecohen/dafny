// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --extended-newtype-bases
// Expected: resolver cyclic-dependency error; a grounded constructor does not admit a recursive newtype.
datatype List<T> = Nil | Cons(head: T, tail: List<T>)
newtype N = List<N>
