lemma FloorBounds(x: real) {
  assert (x.Floor as real) <= x;
  assert x < ((x.Floor + 1) as real);
  assert (-1.3).Floor == -2;
  assert (1.3).Floor == 1;
}
