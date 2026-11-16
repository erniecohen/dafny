// The scanner's tokens for the keywords of member invariants are U+0001 followed by the word.  The
// parser makes them only under --member-invariants, from the words 'invariant', 'witness' and 'raw';
// written in a program, they are rejected, with or without the option.
datatype D = D(x: int) {
  invariant x > 0
  witness D(1)
}
method M(r: raw D) { }
