// A conversion that is not a subtype of the payload's arrow type cannot be proved.
module Conversions {
  method Narrow(c: bounded<nat, int -> int>) returns (d: bounded<nat, int -> nat>) {
    d := c; // error: int -> int is not a subtype of int -> nat
  }
  method Total(c: bounded<nat, int --> int>) returns (d: bounded<nat, int -> int>) {
    d := c; // error: a partial function is not total
  }
  method Reads(c: bounded<nat, int ~> int>) returns (d: bounded<nat, int --> int>) {
    d := c; // error: a function that reads the heap does not read nothing
  }
}
