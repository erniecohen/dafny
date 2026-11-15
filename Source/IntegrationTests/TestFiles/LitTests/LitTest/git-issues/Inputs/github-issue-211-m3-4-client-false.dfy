// Control: admitting a library does not make its client's false assertion provable.
module Client {
  import Lib

  method Main() {
    var r := Lib.Count(3);
    assert r == 4; // error
  }
}
