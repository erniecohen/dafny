// Used by ../github-issue-445.dfy, under the hidden --legacy-early-yield-parameter-check, which restores
// the check of yield parameters made before the resolver knows grounding constructors
// (https://github.com/erniecohen/dafny/issues/445).  That check followed the first constructor, C1,
// so it accepted D<int>.  The compiled code initializes a yield parameter with the default value of
// its type, made with the grounding constructor, C2, which has none.  So this verified program prints
// null for a value of type D<int>.

type S = x: int | x > 0 witness *
datatype D<T> = C1(t: T) | C2(s: S)

iterator I() yields (x: D<int>) {
  yield;
}

method Main() {
  var i := new I();
  var more := i.MoveNext();
  if more {
    print i.x, "\n";
  }
}
