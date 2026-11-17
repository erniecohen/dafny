include "github-issue-411-library.dfy"

// The base module of B is in an included file (or a library: github-issue-411-client.dfy).
module B refines A {
  type Empty = x: int | false witness *
  class C ... {
    var e: Empty
  }
}
