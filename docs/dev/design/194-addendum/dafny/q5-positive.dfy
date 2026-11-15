// PROPOSED Q5 syntax; expected outcomes are UNVERIFIED (no Dafny run).
// Flags after the bitwise milestone: --type-system-refresh --user-defined-operators
// All members except false_control should verify. false_control must report
// one assertion failure at its final `assert false` after a successful user call.
// No --integer-bitwise or --additional-axioms is required.

datatype Bits = Bits(raw: bv8) {
  function {:operator "&"} band(b: Bits): Bits { Bits(raw & b.raw) }
  function {:operator "|"} bor(b: Bits): Bits { Bits(raw | b.raw) }
  static function {:operator "^"} bxor(a: Bits, b: Bits): Bits {
    Bits(a.raw ^ b.raw)
  }
  function {:operator "!"} complement(): Bits { Bits(!raw) }
  function {:operator "<<"} left(k: int): Bits
    requires 0 <= k <= 8
  { Bits(raw << k) }
  function {:operator ">>"} right(k: int): Bits
    requires 0 <= k <= 8
  { Bits(raw >> k) }
}

datatype Bag = Bag(items: set<int>) {
  function {:operator "&"} intersection(b: Bag): set<int> { items * b.items }
}

method positive() {
  var a := Bits(5);
  var b := Bits(3);
  var c := Bits(6);
  assert (a & b) == a.band(b);
  assert (a | b) == a.bor(b);
  assert (a ^ b) == Bits.bxor(a, b);
  assert !a == a.complement();
  assert (a << 1) == a.left(1);
  assert (a >> 1) == a.right(1);
  assert (a << 8).raw == 0;
  assert (a & b & c) == a.band(b).band(c);
  assert (a & (b | c)) == a.band(b.bor(c));
  assert !false;                         // Existing Boolean intrinsic.
  assert ((5 as bv8) & (3 as bv8)) == 1; // Existing BV intrinsic.
  var x := Bag({1, 2});
  var y := Bag({2, 3});
  assert |(x & y)| == 1; // Parentheses re-enable bitwise parsing inside |...|.
}

method false_control() {
  var a := Bits(5);
  var r := a << 1;
  assert r == a.left(1);
  assert false; // EXPECTED verification error; this is the vacuity control.
}
