lemma WordRoundTrip(x: bv3) {
  assert ((x as int) as bv3) == x;
}

lemma IntegerRoundTrip(i: int)
  requires 0 <= i < 8
{
  assert ((i as bv3) as int) == i;
}
