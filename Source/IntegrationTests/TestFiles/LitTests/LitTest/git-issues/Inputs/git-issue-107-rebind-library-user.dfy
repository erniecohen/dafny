module LibraryImpl refines LibrarySpec {
  type T = int
  function Next(t: T): T { t + 1 }
}
module LibraryConcrete refines LibraryClient {
  import X := LibraryImpl
  lemma Use() ensures Twice(0) == 2 { Same(0); }
}
