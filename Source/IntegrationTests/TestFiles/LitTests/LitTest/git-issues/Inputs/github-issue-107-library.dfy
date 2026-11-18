// A library whose module LibraryImporter has copying imports.

module LibraryBase {
  export
    reveals Cell
    provides One
  class Cell {
    constructor () { }
  }
  function One(): int { 1 }
}

module LibraryImporter {
  import X := LibraryBase
  import Z := LibraryBase`LibraryBase

  function Two(): int { X.One() + Z.One() }
  method Make() returns (c: X.Cell) {
    c := new X.Cell();
  }
}
