// Each lemma here says something about two copies that is false, or that the verifier cannot
// know.  The verifier must reject every one of them.

module Leaf {
  class Node {
    constructor () { }
  }
}

module Base {
  export
    reveals Cell
    provides One, Y
  import Y := Leaf
  class Cell {
    constructor () { }
  }
  function One(): int { 1 }
}

module Importer {
  import Base
  import X := Base
  import Z := Base

  lemma SameAsOtherCopy(o: object)
    ensures o is X.Cell ==> o is Z.Cell  // error
  {
  }

  lemma SameAsOriginal(o: object)
    ensures o is X.Cell ==> o is Base.Cell  // error
  {
  }

  lemma InnerCopiesTheSame(o: object)
    ensures o is X.Y.Node ==> o is Z.Y.Node  // error
  {
  }

  lemma OnlyProvided()
    ensures X.One() == 1  // error: the default export set of Base does not reveal One
  {
  }
}

module Importer' refines Importer {
  import Importer

  lemma SameAsInherited(o: object)
    ensures o is X.Cell ==> o is Importer.X.Cell  // error
  {
  }

  lemma CopiesStillDiffer(o: object)
    ensures o is X.Cell <==> o is Z.Cell  // error
  {
  }
}

module Importer'' refines Importer' {
  import Importer'

  lemma SameAsInheritedAgain(o: object)
    ensures o is X.Cell ==> o is Importer'.X.Cell  // error
  {
  }
}

// An abstract import in the copied module stands for an unknown module in each copy.

abstract module WithAbstractImport {
  import A : Leaf
}

abstract module CopiesAbstractImport {
  import WithAbstractImport
  import X := WithAbstractImport
  import Z := WithAbstractImport

  lemma SameUnknown(o: object)
    ensures o is X.A.Node ==> o is Z.A.Node  // error
  {
  }

  lemma DifferentUnknown(o: object)
    ensures o is X.A.Node ==> !(o is Z.A.Node)  // error
  {
  }

  lemma SameAsOriginalUnknown(o: object)
    ensures o is X.A.Node ==> o is WithAbstractImport.A.Node  // error
  {
  }

  lemma DifferentFromOriginalUnknown(o: object)
    ensures o is X.A.Node ==> !(o is WithAbstractImport.A.Node)  // error
  {
  }
}

// Through an abstract import, a copy is known only by its signature: the module that is imported
// may be any refinement of Importer, each of which has copies of its own.

abstract module ThroughAbstractImport {
  import I : Importer
  import Importer

  lemma SameAsDeclared(o: object)
    ensures o is I.X.Cell ==> o is Importer.X.Cell  // error
  {
  }

  lemma DifferentFromDeclared(o: object)
    ensures o is I.X.Cell ==> !(o is Importer.X.Cell)  // error
  {
  }

  lemma InnerSameAsDeclared(o: object)
    ensures o is I.X.Y.Node ==> o is Importer.X.Y.Node  // error
  {
  }

  lemma InnerDifferentFromDeclared(o: object)
    ensures o is I.X.Y.Node ==> !(o is Importer.X.Y.Node)  // error
  {
  }
}
