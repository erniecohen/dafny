method Native(x:bv3) {
  assert 0 <= (x as int) < 8;
  assert ((x as int) as bv3) == x;
}
method False(x:bv67) requires x == (73786976294838206464 as bv67) {
  assert 0 <= (x as int);
  assert false;
}