// A library whose datatypes gain parents in a refinement.

module LibA {
  datatype D = X {
    function One(): int
      ensures One() == 1
    {
      1
    }
  }
  datatype G<X> = Mk(x: X) {
    function Get(): seq<X>
      ensures Get() == [x]
    {
      [x]
    }
  }
}

module LibB refines LibA {
  trait P {
    function One(): int
      ensures One() == 1
  }
  trait S<Y> {
    function Get(): seq<Y>
  }
  datatype D extends P ... {}
  datatype G<X> extends S<X> ... {}
}
