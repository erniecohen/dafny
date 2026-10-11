class OldOnlyReadCell {
  var value: int
}

twostate function OldOnlyReads(c: OldOnlyReadCell): int -> int {
  (x: int) => old(c.value) + x
}

method OldOnlyReturnedLambda(c: OldOnlyReadCell)
  modifies c
{
  label Before:
  var previous := c.value;
  c.value := previous + 1;
  ghost var f := OldOnlyReads@Before(c);
  assert f(0) == previous;
  assert f(1) == previous + 1;
  assert false;
}
