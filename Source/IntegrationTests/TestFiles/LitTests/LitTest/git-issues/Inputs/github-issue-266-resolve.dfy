// Resolution errors: a bounded value is formed only by "bounded.certify", not from an arrow or a lambda, its
// erased function is an arrow, not a bounded value, and its order is non-variant.  (Milestone 2 adds application
// and the members "bound" and "erase"; see github-issue-266-m2.dfy.)
module N {
  method Uses(c: bounded<nat, int -> int>, f: int -> int) returns (r: int, e: bounded<nat, int -> int>) {
    r := c(3);
    e := c.erase; // error: the erased function is an arrow
    e := f; // error: an arrow is not a bounded value
    e := x => x; // error: nor is a lambda
  }
  method Ord(c: bounded<nat, int -> int>) returns (o: bounded<ORDINAL, int -> int>) {
    o := c; // error: nat and ORDINAL are different orders
  }
}
