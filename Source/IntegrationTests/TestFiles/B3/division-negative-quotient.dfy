method NegativeQuotient(x: int)
  requires x < 0
{
  assert x / 2 < 0;
  assert x / (-2) > 0;
  assert 0 <= x % 2 < 2;
  assert 0 <= x % (-2) < 2;
}
