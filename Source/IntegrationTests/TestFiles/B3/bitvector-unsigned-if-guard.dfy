// Default unsigned conversion, with its actual compound-assertion If guard.
method UnsignedGuard67(x: bv67) {
  assert 0 <= (x as int) < 147573952589676412928;
  assert ((x as int) as bv67) == x;
}
