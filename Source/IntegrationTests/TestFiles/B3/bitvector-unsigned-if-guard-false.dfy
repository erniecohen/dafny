// Satisfiable input; the retained producer guard must not make false provable.
method UnsignedGuardFalse(x: bv3)
  requires x == (1 as bv3)
{
  assert 0 <= (x as int) < 8;
  assert false;
}
