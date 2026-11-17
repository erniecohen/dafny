// The base module of B is in a library (or an included file: github-issue-411-include.dfy).
module B refines A {
  type Empty = x: int | false witness *
  class C ... {
    var e: Empty
  }
}
