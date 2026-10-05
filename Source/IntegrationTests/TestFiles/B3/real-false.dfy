lemma FalseWithRealContext(x: real)
  requires x == 0.1 + 0.2
{
  assert false;
}
