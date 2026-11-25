abstract module LibrarySpec {
  type T
  function Next(t: T): T
}
abstract module LibraryClient {
  import X := LibrarySpec
  function Twice(t: X.T): X.T { X.Next(X.Next(t)) }
  lemma Same(t: X.T) ensures Twice(t) == X.Next(X.Next(t)) { }
}
