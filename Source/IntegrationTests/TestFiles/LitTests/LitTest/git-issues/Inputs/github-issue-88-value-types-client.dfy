// A client of Inputs/github-issue-88-value-types-library.dfy.  The parents that the library added
// in a refinement are there in the client, and the client can add another.

module Client {
  import LibB

  method M() {
    var d: LibB.D := LibB.X;
    var p: LibB.P := d;
    assert p.One() == 1;
    var g: LibB.G<int> := LibB.Mk(3);
    var s: LibB.S<int> := g;
    assert s.Get() == g.Get() == [3];
    assert false; // error
  }
}

module ClientB refines LibB {
  trait Q {
    function Two(): int
      ensures Two() == 2
  }
  datatype D extends Q ... {
    function Two(): int
      ensures Two() == 2
    {
      2
    }
  }

  method M() {
    var d: D := X;
    var p: P := d;
    var q: Q := d;
    assert p.One() + q.Two() == 3;
    assert false; // error
  }
}
