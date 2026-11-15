// A client of Lib alone.
module Client {
  import Lib

  method Main() {
    var r := Lib.Count(3);
    assert r == 3;
  }
}
