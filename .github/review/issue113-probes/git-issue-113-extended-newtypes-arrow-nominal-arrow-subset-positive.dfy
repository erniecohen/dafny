type PositiveAtZero = f: int -> int | f(0) > 0 witness (x: int) => 1
newtype Inner = PositiveAtZero witness *
newtype Outer = x: Inner | true witness *

lemma IntroduceNominal(f: int -> int)
  requires f(0) > 0
{
  var value := f as Outer;
  assert (value as int -> int)(0) > 0;
}

lemma PreserveDeclaredSubset(f: PositiveAtZero) {
  var value := f as Outer;
  assert (value as int -> int)(0) > 0;
}
