// A client of the admitted libraries.
module Client {
  import Lib
  import Dependent

  method Main() {
    var r := Dependent.Triple(2);
    assert r == 6;
    Lib.DoubleIsEven(4);
  }
}
