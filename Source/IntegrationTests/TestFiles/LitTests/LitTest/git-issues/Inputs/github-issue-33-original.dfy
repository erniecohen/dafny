lemma RoundTrip8(a: int) requires 0 <= a < 0x100 ensures (a as bv8) as int == a { }

lemma RoundTrip16(a: int) requires 0 <= a < 0x1_0000 ensures (a as bv16) as int == a { }

lemma RoundTrip32(a: int) requires 0 <= a < 0x1_0000_0000 ensures (a as bv32) as int == a { }

lemma RoundTrip64(a: int) requires 0 <= a < 0x1_0000_0000_0000_0000
  ensures (a as bv64) as int == a { }

// The other direction.
lemma Back32(w: bv32) ensures (w as int) as bv32 == w { }

// The range of the round trip.
lemma Range32(a: int) requires 0 <= a < 0x1_0000_0000
  ensures 0 <= (a as bv32) as int < 0x1_0000_0000 { }

// Asserting the range first closes it.
lemma RoundTrip32WithRange(a: int) requires 0 <= a < 0x1_0000_0000
  ensures (a as bv32) as int == a
{
  assert 0 <= (a as bv32) as int < 0x1_0000_0000;
}

// A consequence: no general lemma about a function written this way.
function ShiftRight(a: int, k: int): int
  requires 0 <= a < 0x1_0000_0000 && 0 <= k < 32
{
  ((a as bv32) >> (k as bv32)) as int
}

lemma ShiftRightByZero(a: int) requires 0 <= a < 0x1_0000_0000
  ensures ShiftRight(a, 0) == a { }
