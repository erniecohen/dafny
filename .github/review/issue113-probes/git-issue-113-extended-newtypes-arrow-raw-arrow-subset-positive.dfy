// Ordinary-arrow counterpart; run on unchanged and feature candidate sources.
type PositiveAtZero = f: int -> int | f(0) > 0 witness (x: int) => 1

lemma IntroduceRawSubset(f: int -> int)
  requires f(0) > 0
{
  var value := f as PositiveAtZero;
  assert value(0) > 0;
}

lemma PreserveRawSubset(f: PositiveAtZero) {
  var value := f as PositiveAtZero;
  assert value(0) > 0;
}

lemma Inhabited() {
  var value := ((x: int) => 1) as PositiveAtZero;
  assert value(0) == 1;
}
