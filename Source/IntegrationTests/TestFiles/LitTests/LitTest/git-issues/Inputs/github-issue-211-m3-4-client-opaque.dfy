// A client of the opaque library.
module Client {
  import Opaque

  method Main() {
    Opaque.L();
    assert Opaque.F() + Opaque.G() == 3;
  }
}
