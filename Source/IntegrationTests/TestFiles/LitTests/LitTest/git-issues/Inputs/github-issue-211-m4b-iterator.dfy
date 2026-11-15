module Library {
  iterator {:termination_closed} Once() yields (x: int) { x := 0; yield; }
}
module Client {
  import Library
  method Use() decreases {:global} 0 {
    var i := new Library.Once();
    var more := i.MoveNext();
  }
}
