// Programs that --refinement-extends rejects during resolution.

// ----- A class of the base that does not implement the members of the added parent

module Unimplemented0 {
  trait T extends object {}
  class C extends T {
    constructor () {}
  }
}
module Unimplemented1 refines Unimplemented0 {
  trait S extends object {
    lemma L() ensures false
  }
  trait T ... extends S {} // error: class C does not implement S.L
}

// The same for a class of a module that imports the base abstractly, once the import is bound to
// the refinement.

module Bound0 {
  trait T extends object {}
}
module Bound1 refines Bound0 {
  trait S extends object {
    lemma L() ensures true
  }
  trait T ... extends S {}
}
abstract module Importer0 {
  import X : Bound0
  @AssumeCrossModuleTermination
  class D extends X.T {
    constructor () {}
  }
}
module Importer1 refines Importer0 {
  import X = Bound1 // error: class D does not implement X.S.L
}

// ----- A function of the added parent that has a body cannot be redeclared

module Clash0 {
  trait T extends object {
    function F(): int { 0 }
    var x: int
  }
}
module Clash1 refines Clash0 {
  trait S extends object {
    function F(): int { 1 }
    var x: int
  }
  trait T ... extends S {} // error (x2): F and x are members of both
}

// ----- A member inherited from two unrelated parents

module TwoParents0 {
  trait P extends object {
    function F(): int
  }
  trait T extends P {}
}
module TwoParents1 refines TwoParents0 {
  trait S extends object {
    function F(): int
  }
  trait T ... extends S {} // error: F is inherited from both P and S
}

// ----- A refinement cannot make a trait a reference type

module Kind0 {
  trait T {}
  trait U extends T {}
}
module Kind1 refines Kind0 {
  trait T ... extends object {} // error: T is not a reference type in Kind0
}
module Kind2 refines Kind0 {
  trait S extends object {}
  trait U ... extends S {} // error: U is not a reference type in Kind0
}

// ----- Cycles among the parents

module ParentCycle0 {
  trait T {}
  trait U extends T {}
}
module ParentCycle1 refines ParentCycle0 {
  trait T ... extends U {} // error: cycle
}

// ----- Type arguments of the added parent

module Arguments0 {
  trait T<X> extends object {}
}
module Arguments1 refines Arguments0 {
  trait S<Y> extends object {}
  trait T<X> ... extends S<Z> {} // error: Z is not declared
}

// ----- An added parent that is not a trait, and an added parent on a declaration that refines nothing

module NotATrait0 {
  class C {}
}
module NotATrait1 refines NotATrait0 {
  class D {}
  class C ... extends D {} // error: D is not a trait
  trait Fresh ... extends object {} // error: there is no Fresh to refine
}
