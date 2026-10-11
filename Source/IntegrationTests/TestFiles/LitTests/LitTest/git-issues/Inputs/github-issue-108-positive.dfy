// Examples in the bit-vector operator documentation.
lemma UnsignedDivisionAndRemainder() {
  assert (255 as bv8) / 16 == 15;
  assert (255 as bv8) % 16 == 15;
  assert (9 as bv4) / 3 == 3;
  assert (9 as bv4) % 2 == 1;
  assert (0 as bv8) / 7 == 0;
  assert (0 as bv8) % 7 == 0;
}

// Multiplication, division and remainder bind equally tightly, left to right.
// They bind more tightly than addition, and less tightly than bitwise operators.
lemma PrecedenceAndAssociativity() {
  assert (30 as bv8) / 4 * 2 == 14;
  assert (30 as bv8) % 4 * 2 == 4;
  assert (60 as bv8) / 5 / 3 == 4;
  assert (30 as bv8) % 7 % 2 == 0;
  assert (3 as bv8) + 20 / 4 == 8;
  assert (3 as bv8) + 20 % 6 == 5;
  assert (30 as bv8) / 7 & 3 == 10;
  assert (30 as bv8) % 7 & 3 == 0;
}

lemma RemainderBound(x: bv8, y: bv8)
  requires y != 0
  ensures x / y <= x
  ensures x % y < y
{
}
