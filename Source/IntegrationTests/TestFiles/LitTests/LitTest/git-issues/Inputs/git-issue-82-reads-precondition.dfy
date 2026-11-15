ghost function NeedsPositive(o: object?, n: int): int
  requires 0 < n
  reads o
{
  0
}

lemma ReadsChecksPrecondition(o: object?)
{
  var footprint := NeedsPositive.reads(o, 0);
}
