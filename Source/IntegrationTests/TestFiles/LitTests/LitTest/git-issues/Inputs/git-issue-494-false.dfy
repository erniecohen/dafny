module M {
  function F(x: bv77): bv77 { x }
  method Work() {
    var x := F(0);
    var a := new int[2, 3]((i, j) => i + j);
    assert a[0, 0] == 0;
    assert false;
  }
}
