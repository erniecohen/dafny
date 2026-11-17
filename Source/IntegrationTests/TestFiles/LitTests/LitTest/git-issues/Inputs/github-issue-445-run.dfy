// Used by ../github-issue-445.dfy.  An iterator that yields values of a datatype whose first
// constructor is recursive, verified, compiled and run (https://github.com/erniecohen/dafny/issues/445).

datatype L = Cons(head: int, tail: L) | Nil

// Yields Cons(1, Nil), Cons(2, Cons(1, Nil)), and so on, n values in all
iterator Stack(n: nat) yields (l: L)
  yield ensures |ls| <= n
  ensures |ls| == n
{
  l := Nil;
  var i := 0;
  while i < n
    invariant |ls| == i <= n
  {
    i := i + 1;
    yield Cons(i, l);
  }
}

method Main() {
  var it := new Stack(3);
  while true
    invariant it.Valid() && fresh(it._new)
    decreases 3 - |it.ls|
  {
    var more := it.MoveNext();
    if !more { break; }
    print it.l, "\n";
  }
}
