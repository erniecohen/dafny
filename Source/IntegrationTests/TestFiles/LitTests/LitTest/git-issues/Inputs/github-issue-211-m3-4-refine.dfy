// Inherits M1.A's body, which is not part of M1's termination contract: refused under --modular-termination.
module M2 refines M1 {
  method B() { A(); }
}
