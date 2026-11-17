// Ghost errors: the bound of a bounded value is ghost; its function is not.
module R {
  method Ghost(c: bounded<nat, int -> int>) {
    var b := c.bound; // error: ghost in compiled code
    print b;
  }
  method Erase2(c: bounded<nat, int -> int>) {
    var b: nat := c.erase(0);
    var g := c.bound; // error: ghost
    print g;
  }
  datatype D = D(h: bounded<nat, D -> bool>) // error: D occurs in its own payload's domain
}
