// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the rows of the vacuous-types
// table (section 5.3 of docs/dev/issue105-member-invariants-plan.md) that resolution closes, for
// datatypes, generic or not.  A datatype with invariants and no witness is possibly empty, with a ghost
// witness it is nonempty, and with a compiled witness it is auto-initializable.  A type argument for a
// type parameter declared (0) or (00) must be so; a datatype without invariants is as inhabited as the
// components of a constructor that grounds it; and a class with a field of a type that is not
// auto-initializable must declare a constructor.  The classes have a module of their own, since
// resolution checks class fields after the other checks, and only in a module where those found no
// error.  git-issue-105-emptiness.dfy has the rows that the verifier closes.

module Types {
  datatype Opt<X> = None | Some(x: X, n: int) {
    invariant Some? ==> n > 0
    witness None
  }
  datatype NoWitness<X> = NW(x: X, n: int) {
    invariant n > 0
  }
  datatype Ghostly<X> = G(n: int) {
    invariant n > 0
    ghost witness G(1)
  }
  datatype Plain = P(n: int) {
    invariant n > 0
  }

  // Datatypes without invariants, grounded only through a component, or also otherwise.
  datatype Wrap<X> = Wrap(w: NoWitness<X>)
  datatype WrapG<X> = WrapG(g: Ghostly<X>)
  datatype Either<X> = Left(w: NoWitness<X>) | Right(n: int)
}

module Characteristics {
  import opened Types

  method Zero<T(0)>() returns (t: T) { t := *; }
  method Nonempty<T(00)>() { }

  method Characteristics() {
    var o := Zero<Opt<int>>();
    var a := Zero<NoWitness<int>>();  // error: (0)
    Nonempty<NoWitness<int>>();  // error: (00)
    var p := Zero<Plain>();  // error: (0)
    Nonempty<Plain>();  // error: (00)
    var b := Zero<Ghostly<int>>();  // error: (0)
    Nonempty<Ghostly<int>>();
    var c := Zero<Wrap<int>>();  // error: (0)
    Nonempty<Wrap<int>>();  // error: (00)
    var d := Zero<WrapG<int>>();  // error: (0)
    Nonempty<WrapG<int>>();
    var e := Zero<Either<int>>();
  }
}

// Class fields.
module Classes {
  import opened Types

  class Cell {  // error: needs a constructor
    var p: Plain
  }
  class CellG {  // error: needs a constructor
    var g: Ghostly<int>
  }
  class CellO {
    var o: Opt<int>
    ghost var g: Ghostly<int>
  }
  class CellP {
    var p: Plain
    constructor (p: Plain) {
      this.p := p;
    }
  }
}
