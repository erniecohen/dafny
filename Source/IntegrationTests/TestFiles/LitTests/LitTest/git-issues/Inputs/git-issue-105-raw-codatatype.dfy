// Used by git-issue-105-raw-errors.dfy.  A program without member invariants may declare a
// codatatype, but 'raw' does not apply to one, since a codatatype cannot declare an invariant.
// 'raw' of a datatype without invariants is the datatype itself.

codatatype Stream = Cons(head: int, tail: Stream)
datatype Plain = Plain(x: int)

method M(s: raw Stream, p: raw Plain) returns (q: Plain) {
  q := p;
}
