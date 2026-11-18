// Newtypes of https://github.com/erniecohen/dafny/issues/453, which need --general-newtypes.  A newtype
// cannot be based on a class or a datatype, but it can be based on a sequence of them.

// A newtype of a sequence of the class type
module NewtypeOfClass {
  newtype N = s: seq<C> | true
  class C {
    const g: N -> nat
    constructor() { g := (n: N) => if |n| == 0 then 0 else n[0].F(0); }
    function F(n: nat): nat { g([this] as N) + 1 }
  }
}

// A newtype of a sequence of a datatype that holds the class
module NewtypeOfDatatype {
  datatype Box = Box(c: C)
  newtype NB = seq<Box>
  class C {
    const g: NB -> nat
    constructor() { g := (n: NB) => if |n| == 0 then 0 else n[0].c.F(0); }
    function F(n: nat): nat { g([Box(this)] as NB) + 1 }
  }
}

// A generic newtype
module GenericNewtype {
  datatype Box = Box(c: C)
  newtype NS<X> = seq<X>
  class C {
    const g: NS<Box> -> nat
    constructor() { g := (n: NS<Box>) => if |n| == 0 then 0 else n[0].c.F(0); }
    function F(n: nat): nat { g([Box(this)] as NS<Box>) + 1 }
  }
}
