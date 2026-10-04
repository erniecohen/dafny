// The preceding footprint facts must not make an inhabited source context inconsistent.
ghost function One(o: object?): int
  reads set q: object? | q == o :: q
{
  0
}

lemma ReadsRemainConsistent(o: object?)
{
  var named := One.reads(o);
  var f := (x: int) reads set q: object? | q == o :: q => x;
  var lambda := f.reads(0);
  assert named == {o};
  assert lambda == {o};
  assert false;
}
