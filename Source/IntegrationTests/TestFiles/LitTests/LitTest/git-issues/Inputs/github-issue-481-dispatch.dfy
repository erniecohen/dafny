// Part of github-issue-481.dfy: overriding members and refining modules.

// A trait method without a body stores nothing, so an override may not store its type parameter
module Override {
  trait Tr {
    method Put<U>(a: array<U>, u: U) modifies a requires a.Length > 0
  }
  class C extends Tr {
    method Put<U>(a: array<U>, u: U) modifies a requires a.Length > 0 { a[0] := u; } // error
  }
}

// The characteristic (!reads) on the trait method allows the store, and is checked at calls through the trait
module OverrideMarked {
  trait Tr {
    method Put<U(!reads)>(a: array<U>, u: U) modifies a requires a.Length > 0
  }
  class C extends Tr {
    method Put<U>(a: array<U>, u: U) modifies a requires a.Length > 0 { a[0] := u; }
  }
  method Client(t: Tr, a: array<() ~> bool>) modifies a requires a.Length > 0 {
    t.Put<() ~> bool>(a, () => true); // error
  }
}

// An override may not add the characteristic either
module OverrideAddsMarker {
  trait Tr {
    method Put<U>(a: array<U>, u: U) modifies a requires a.Length > 0
  }
  class C extends Tr {
    method Put<U(!reads)>(a: array<U>, u: U) modifies a requires a.Length > 0 { } // error
  }
}

// A class, a method and an abstract type of an abstract module, refined so that they store
abstract module A {
  class Ref<T> {
    method Set(t: T)
  }
  method Put<U>(a: array<U>, u: U) modifies a requires a.Length > 0
  type Box<T>
  type Opaque(!reads) // error: the characteristic applies only to type parameters
}
module B refines A {
  class Ref<T> ... {
    var w: T // error (reported at the class): the type parameter of A.Ref is not stored
    method Set(t: T) { }
  }
  method Put<U>(a: array<U>, u: U) { a[0] := u; } // error
  class Box<T> { var v: T } // error
  type Opaque = int
}

// The same, with the characteristic in the abstract module
abstract module AM {
  class Ref<T(!reads)> {
    method Set(t: T)
  }
  method Put<U(!reads)>(a: array<U>, u: U) modifies a requires a.Length > 0
  type Box<T(!reads)>
  method Client(a: array<() ~> bool>) modifies a requires a.Length > 0 {
    Put(a, () => true); // error, here and in the copy in BM, where Put stores its type parameter
  }
}
module BM refines AM {
  class Ref<T> ... {
    var w: T
    method Set(t: T) { }
  }
  method Put<U>(a: array<U>, u: U) { a[0] := u; }
  class Box<T> { var v: T }
}

// An abstract import keeps what the bodies of the imported module store
abstract module SCore {
  method Store<A>(a: array<A>, x: A) modifies a requires a.Length > 0 { a[0] := x; }
  datatype Box<R> = Box(r: R) {
    method Keep(a: array<R>) modifies a requires a.Length > 0 { Store(a, r); }
  }
}
abstract module SClient {
  import P : SCore
  method M() {
    var b: P.Box<() ~> bool> := P.Box(() => true); // error
  }
}
