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

// Newtypes: "raw N", where the header of N has moved into the type with its invariants, is the base type
// with no constraint, and as inhabited as the base type, so over a base that may be empty it may be empty.
module RawNewtypes {
  newtype Empty = x: int | false witness * { invariant true }
  newtype Above = x: Empty | true witness * { invariant true }
  newtype Pos = x: int | x > 0 witness 1 { invariant true }
  newtype Over = x: Pos | true witness 2 { invariant true }

  ghost method Nonempty<T(00)>() { }

  ghost method Characteristics() {
    Nonempty<raw Above>();  // error: (00)
    Nonempty<raw Over>();
  }
}

// Newtypes in the old syntax with no witness member: the header's witness clause, the default included,
// gives the type its characteristics, which a module that sees the type only as provided relies on.
module HeaderWitnesses {
  export provides Digit, Ghostly, Opt
  newtype Digit = x: int | 0 <= x < 10 { invariant this as int != 5 }
  newtype Ghostly = x: int | 0 < x < 10 ghost witness 1 { invariant this as int != 5 }
  newtype Opt = x: int | 0 < x < 10 witness * { invariant this as int != 5 }
}

module ProvidedNewtypes {
  import HeaderWitnesses

  method Zero<T(0)>() returns (t: T) { t := *; }
  method Nonempty<T(00)>() { }

  method Characteristics() {
    var a := Zero<HeaderWitnesses.Digit>();
    Nonempty<HeaderWitnesses.Ghostly>();
    var b := Zero<HeaderWitnesses.Ghostly>();  // error: (0)
    Nonempty<HeaderWitnesses.Opt>();  // error: (00)
  }
}
