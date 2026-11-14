// Resolution errors: in milestone 1 a bounded value cannot be applied, projected, or formed from an arrow or a
// lambda, and its order is non-variant.
module N {
  method Uses(c: bounded<nat, int -> int>, f: int -> int) returns (r: int, e: bounded<nat, int -> int>) {
    r := c(3); // error: not a function
    var b := c.bound; // error: no member
    var g := c.erase; // error: no member
    e := f; // error: an arrow is not a bounded value
    e := x => x; // error: nor is a lambda
  }
  method Ord(c: bounded<nat, int -> int>) returns (o: bounded<ORDINAL, int -> int>) {
    o := c; // error: nat and ORDINAL are different orders
  }
}
