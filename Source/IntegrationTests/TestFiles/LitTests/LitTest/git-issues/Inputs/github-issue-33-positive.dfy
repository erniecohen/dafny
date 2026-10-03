lemma Width1(a: int, b: bv1)
  requires 0 <= a < 2
  ensures (a as bv1) as int == a
  ensures (b as int) as bv1 == b
  ensures 0 <= b as int < 2
  ensures (0 as bv1) as int == 0
  ensures (1 as bv1) as int == 1
  ensures (1 as bv1) as int == 1
  ensures (1 as bv1) as int == 1
{}

lemma Width3(a: int, b: bv3)
  requires 0 <= a < 8
  ensures (a as bv3) as int == a
  ensures (b as int) as bv3 == b
  ensures 0 <= b as int < 8
  ensures (0 as bv3) as int == 0
  ensures (1 as bv3) as int == 1
  ensures (4 as bv3) as int == 4
  ensures (7 as bv3) as int == 7
{}

lemma Width7(a: int, b: bv7)
  requires 0 <= a < 128
  ensures (a as bv7) as int == a
  ensures (b as int) as bv7 == b
  ensures 0 <= b as int < 128
  ensures (0 as bv7) as int == 0
  ensures (1 as bv7) as int == 1
  ensures (64 as bv7) as int == 64
  ensures (127 as bv7) as int == 127
{}

lemma Width8(a: int, b: bv8)
  requires 0 <= a < 256
  ensures (a as bv8) as int == a
  ensures (b as int) as bv8 == b
  ensures 0 <= b as int < 256
  ensures (0 as bv8) as int == 0
  ensures (1 as bv8) as int == 1
  ensures (128 as bv8) as int == 128
  ensures (255 as bv8) as int == 255
{}

lemma Width16(a: int, b: bv16)
  requires 0 <= a < 65536
  ensures (a as bv16) as int == a
  ensures (b as int) as bv16 == b
  ensures 0 <= b as int < 65536
  ensures (0 as bv16) as int == 0
  ensures (1 as bv16) as int == 1
  ensures (32768 as bv16) as int == 32768
  ensures (65535 as bv16) as int == 65535
{}

lemma Width31(a: int, b: bv31)
  requires 0 <= a < 2147483648
  ensures (a as bv31) as int == a
  ensures (b as int) as bv31 == b
  ensures 0 <= b as int < 2147483648
  ensures (0 as bv31) as int == 0
  ensures (1 as bv31) as int == 1
  ensures (1073741824 as bv31) as int == 1073741824
  ensures (2147483647 as bv31) as int == 2147483647
{}

lemma Width32(a: int, b: bv32)
  requires 0 <= a < 4294967296
  ensures (a as bv32) as int == a
  ensures (b as int) as bv32 == b
  ensures 0 <= b as int < 4294967296
  ensures (0 as bv32) as int == 0
  ensures (1 as bv32) as int == 1
  ensures (2147483648 as bv32) as int == 2147483648
  ensures (4294967295 as bv32) as int == 4294967295
{}

lemma Width33(a: int, b: bv33)
  requires 0 <= a < 8589934592
  ensures (a as bv33) as int == a
  ensures (b as int) as bv33 == b
  ensures 0 <= b as int < 8589934592
  ensures (0 as bv33) as int == 0
  ensures (1 as bv33) as int == 1
  ensures (4294967296 as bv33) as int == 4294967296
  ensures (8589934591 as bv33) as int == 8589934591
{}

lemma Width63(a: int, b: bv63)
  requires 0 <= a < 9223372036854775808
  ensures (a as bv63) as int == a
  ensures (b as int) as bv63 == b
  ensures 0 <= b as int < 9223372036854775808
  ensures (0 as bv63) as int == 0
  ensures (1 as bv63) as int == 1
  ensures (4611686018427387904 as bv63) as int == 4611686018427387904
  ensures (9223372036854775807 as bv63) as int == 9223372036854775807
{}

lemma Width64(a: int, b: bv64)
  requires 0 <= a < 18446744073709551616
  ensures (a as bv64) as int == a
  ensures (b as int) as bv64 == b
  ensures 0 <= b as int < 18446744073709551616
  ensures (0 as bv64) as int == 0
  ensures (1 as bv64) as int == 1
  ensures (9223372036854775808 as bv64) as int == 9223372036854775808
  ensures (18446744073709551615 as bv64) as int == 18446744073709551615
{}

lemma Width65(a: int, b: bv65)
  requires 0 <= a < 36893488147419103232
  ensures (a as bv65) as int == a
  ensures (b as int) as bv65 == b
  ensures 0 <= b as int < 36893488147419103232
  ensures (0 as bv65) as int == 0
  ensures (1 as bv65) as int == 1
  ensures (18446744073709551616 as bv65) as int == 18446744073709551616
  ensures (36893488147419103231 as bv65) as int == 36893488147419103231
{}

lemma Width127(a: int, b: bv127)
  requires 0 <= a < 170141183460469231731687303715884105728
  ensures (a as bv127) as int == a
  ensures (b as int) as bv127 == b
  ensures 0 <= b as int < 170141183460469231731687303715884105728
  ensures (0 as bv127) as int == 0
  ensures (1 as bv127) as int == 1
  ensures (85070591730234615865843651857942052864 as bv127) as int == 85070591730234615865843651857942052864
  ensures (170141183460469231731687303715884105727 as bv127) as int == 170141183460469231731687303715884105727
{}

lemma Width128(a: int, b: bv128)
  requires 0 <= a < 340282366920938463463374607431768211456
  ensures (a as bv128) as int == a
  ensures (b as int) as bv128 == b
  ensures 0 <= b as int < 340282366920938463463374607431768211456
  ensures (0 as bv128) as int == 0
  ensures (1 as bv128) as int == 1
  ensures (170141183460469231731687303715884105728 as bv128) as int == 170141183460469231731687303715884105728
  ensures (340282366920938463463374607431768211455 as bv128) as int == 340282366920938463463374607431768211455
{}

lemma Width256(a: int, b: bv256)
  requires 0 <= a < 115792089237316195423570985008687907853269984665640564039457584007913129639936
  ensures (a as bv256) as int == a
  ensures (b as int) as bv256 == b
  ensures 0 <= b as int < 115792089237316195423570985008687907853269984665640564039457584007913129639936
  ensures (0 as bv256) as int == 0
  ensures (1 as bv256) as int == 1
  ensures (57896044618658097711785492504343953926634992332820282019728792003956564819968 as bv256) as int == 57896044618658097711785492504343953926634992332820282019728792003956564819968
  ensures (115792089237316195423570985008687907853269984665640564039457584007913129639935 as bv256) as int == 115792089237316195423570985008687907853269984665640564039457584007913129639935
{}

lemma Zero(a: int, b: bv0)
  requires a == 0
  ensures (a as bv0) as int == 0
  ensures (b as int) as bv0 == b
{}

type WordInt = a: int | 0 <= a < 0x1_0000_0000 witness 0
newtype WordNat = a: int | 0 <= a < 0x1_0000_0000 witness 0
datatype Holder = Holder(value: bv32)
function Id<T>(x: T): T { x }

lemma Generic(a: int)
  requires 0 <= a < 0x1_0000_0000
  ensures (Id(a as bv32)) as int == a
{}

lemma Sequence(a: int)
  requires 0 <= a < 0x1_0000_0000
  ensures ([a as bv32][0]) as int == a
{}

lemma Map(a: int)
  requires 0 <= a < 0x1_0000_0000
  ensures (map[0 := a as bv32][0]) as int == a
{}

lemma Datatype(a: int)
  requires 0 <= a < 0x1_0000_0000
  ensures Holder(a as bv32).value as int == a
{}

lemma Nested(a: int)
  requires 0 <= a < 0x1_0000_0000
  ensures (((a as bv32) as int) as bv32) as int == a
{}

// Width changes retain their existing bitvector fit and value obligations.
lemma Widen(b: bv32)
  ensures ((b as bv64) as bv32) == b
{}

lemma Narrow(b: bv64)
  requires b < 0x1_0000_0000
  ensures ((b as bv32) as bv64) == b
{}

lemma Temporary(a: int)
  requires 0 <= a < 0x1_0000_0000
{
  var b := a as bv32;
  assert b as int == a;
}

lemma Alias(a: int, b: bv32)
  requires 0 <= a < 0x1_0000_0000
  requires b == a as bv32
  ensures b as int == a
{}

lemma Subset(a: WordInt, b: WordNat)
  ensures (a as bv32) as int == a
  ensures ((b as int) as bv32) as int == b as int
{}

lemma Simplification(a: int)
  requires 0 <= a < 0x1_0000_0000
  ensures ((a as bv32) >> 0) as int == a
  ensures ((a as bv32) << 0) as int == a
  ensures ((a as bv32) | 0) as int == a
  ensures ((a as bv32) ^ 0) as int == a
  ensures ((a as bv32) & 0xffff_ffff) as int == a
  ensures ((a as bv32) + 0) as int == a
{}

function ShiftAtBoundary(a: int, k: int): int
  requires 0 <= a < 0x1_0000_0000 && 0 <= k <= 32
{
  // Expose the count's integer value before checking the optimized narrower cast.
  assert (k as bv32) as int == k;
  ((a as bv32) >> (k as bv32)) as int
}

lemma VariableShift(a: int, k: int)
  requires 0 <= a < 0x1_0000_0000 && 0 <= k <= 32
  ensures 0 <= ShiftAtBoundary(a, k) < 0x1_0000_0000
{}

lemma OtherConversions(a: int, c: char, o: ORDINAL)
  requires 0 <= a < 256
  requires o.IsNat && o.Offset < 256
  ensures ((a as real) as bv8) as int == a
  ensures ((o as int) as bv8) as int == o as int
  ensures 0 <= (c as bv32) as int
{}
