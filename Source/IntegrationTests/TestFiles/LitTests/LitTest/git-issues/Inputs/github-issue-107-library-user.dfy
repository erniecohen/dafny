// A user of the library of github-issue-107-library.dfy.  The module refines a module of the
// library that has copying imports, and so gets copies of its own.

module LibraryUser refines LibraryImporter {
  import LibraryImporter
  import LibraryBase
  import W := LibraryBase

  lemma NewCopies(o: object)
    ensures o is X.Cell ==> !(o is LibraryImporter.X.Cell)
    ensures o is W.Cell ==> !(o is LibraryImporter.X.Cell) && !(o is LibraryImporter.Z.Cell)
    ensures o is W.Cell ==> !(o is LibraryBase.Cell)
  {
  }

  lemma SameAsLibraryCopy(o: object)
    ensures o is X.Cell ==> o is LibraryImporter.X.Cell  // error
  {
  }

  method MakeTwo() returns (c: X.Cell, d: X.Cell) {
    c := Make();
    d := new X.Cell();
  }
}
