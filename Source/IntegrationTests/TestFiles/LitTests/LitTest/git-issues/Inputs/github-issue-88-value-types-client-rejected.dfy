// A client of Inputs/github-issue-88-value-types-library.dfy that the resolver rejects: a parent
// added in the client clashes with a member of a parent that the library added.

module ClientC refines LibB {
  trait Q {
    function One(): int
      ensures One() == 2
  }
  datatype D extends Q ... {} // error: One comes from both P and Q
}
