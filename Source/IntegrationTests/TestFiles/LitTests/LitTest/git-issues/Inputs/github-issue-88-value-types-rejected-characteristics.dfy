// A program in which a datatype gains a parent in a refinement, rejected when type characteristics
// are checked.  The type parameter of a datatype gets the characteristics its parents need, so the
// added parent makes X an equality-supporting parameter, and an inherited use of H at a type
// without equality is an error in the refining module.

module Characteristics0 {
  trait Eq<Y(==)> {}
  datatype H<X> = MkH(x: X)
  method Use() returns (h: H<int -> int>) { // error (in Characteristics1)
    h := MkH(x => x); // error (in Characteristics1)
  }
}

module Characteristics1 refines Characteristics0 {
  datatype H<X> extends Eq<X> ... {}
}
