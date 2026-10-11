// RUN: %baredafny resolve --type-system-refresh:true "%s" > "%t"
// RUN: %baredafny resolve --type-system-refresh:false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Original crash: the inferred constant needs the constructor signature.
datatype Foo = Foo(x: int)
const f := Foo.Foo(0)
const annotated: Foo := Foo.Foo(0)

module Unqualified {
  const inferred := Make(1)
  datatype Box<T> = Make(value: T)
}

module Qualified {
  const inferred := Box.Make(1)
  const explicit := Box<int>.Make(2)
  datatype Box<T> = Make(value: T)
}

module NamedAndDefault {
  const named := Pair.Pair(right := 2, left := 1)
  const defaulted := Pair.Pair(1)
  datatype Pair<T> = Pair(left: T, right: int := 0)
}

module DatatypeFirst {
  datatype Box<T> = Make(value: T)
  const inferred := Box.Make(1)
}

module ScopedGeneric {
  datatype Box<T> = Make(value: T)
  function Wrap<U>(value: U): Box<U> { Box.Make(value) }
}
