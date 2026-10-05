lemma SatisfiableInputsFalse(x: bv3, a: nat, b: nat)
  requires x == (1 as bv3)
  requires a == 3 && b == 0
{
  assert false;
}
