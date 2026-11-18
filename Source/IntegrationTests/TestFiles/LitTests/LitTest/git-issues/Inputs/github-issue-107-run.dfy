// A program with copying imports, compiled and run.  Each copy has its own classes, datatypes,
// constants and methods in the target program.

module Leaf {
  class Node {
    var n: int
    constructor (k: int) {
      n := k;
    }
  }
}

module Base {
  import Y := Leaf
  datatype Shape = Dot | Line(length: int)
  class Counter {
    var value: int
    constructor (x: int)
      ensures value == x
    {
      value := x;
    }
    method Inc()
      modifies this
    {
      value := value + 1;
    }
  }
  function One(): int { 1 }
  const Seven := 7
  method Show(s: Shape) {
    print "Show ", s, " ", Seven, "\n";
  }
}

module Importer {
  import Base
  import X := Base
  import opened Z := Base

  method Run() {
    var c := new X.Counter(3);
    c.Inc();
    var d := new Counter(10);
    var q := new Base.Counter(20);
    print c.value, " ", d.value, " ", q.value, " ", X.One() + One() + Base.One(), "\n";
    X.Show(X.Line(1));
    Show(Dot);
    Base.Show(Base.Line(2));
    var y := new X.Y.Node(5);
    print y.n, "\n";
    var o: object := c;
    print o is X.Counter, " ", o is Z.Counter, " ", o is Base.Counter, "\n";
  }
}

module Importer' refines Importer {
  import Importer

  method RunBoth() {
    Run();
    var c := new X.Counter(30);
    var o: object := c;
    print o is X.Counter, " ", o is Importer.X.Counter, " ", o is Base.Counter, "\n";
  }
}

method Main() {
  Importer'.RunBoth();
  Importer.Run();
}
