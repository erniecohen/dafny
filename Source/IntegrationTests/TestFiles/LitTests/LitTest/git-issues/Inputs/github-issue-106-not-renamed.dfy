// With --refinement-renaming, two kinds of declaration still take no renaming: an abstract type with
// members that a type synonym refines (--synonym-refines-abstract-type), and an iterator.

abstract module Boxes {
  type Box<T> {
    function Get(): T
  }
}

module SameBoxes refines Boxes {
  class Cell<T> {
    const value: T
    constructor (t: T) { value := t; }
    function Get(): T { value }
  }
  type Box<T> = Cell<T>
}

module RenamedBoxes refines Boxes {
  class Cell<U> {
    const value: U
    constructor (u: U) { value := u; }
    function Get(): U { value }
  }
  type Box<U> = Cell<U>  // error: the type parameter is renamed
}

abstract module Iterators {
  iterator Count<T(0)>(x: T, n: nat) yields (y: T)
}

module SameIterators refines Iterators {
  iterator Count ... { }
}

module RenamedIterators refines Iterators {
  iterator Count<U(0)>(a: U, n: nat) yields (b: U) { }  // error: an iterator is refined only with "..."
}
