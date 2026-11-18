// Programs in which a value type gains a parent in a refinement, rejected by the resolver.

// ----- The refined type must implement the members of the added parent

module Missing0 {
  datatype D = X
  newtype N = x: int | 0 <= x < 10
}

module Missing1 refines Missing0 {
  trait S {
    function F(): int
  }
  datatype D extends S ... {} // error: F is not implemented
  newtype N extends S ... {} // error: F is not implemented
}

// ----- A value type cannot get a reference type as a parent

module Reference0 {
  datatype D = X
}

module Reference1 refines Reference0 {
  trait R extends object {}
  datatype D extends R ... {} // error: R is a reference type
}

// ----- One generic parent at two instantiations

module Twice0 {
  trait S<Y> {}
  datatype G<X> extends S<X> = Mk(x: X)
}

module Twice1 refines Twice0 {
  datatype G<X> extends S<int> ... {} // error: S<X> and S<int>
}

// ----- The same parent again is not an error

module Again0 {
  trait S {}
  datatype D extends S = X
}

module Again1 refines Again0 {
  datatype D extends S ... {}
}
