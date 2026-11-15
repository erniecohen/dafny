// PROPOSED Q5 syntax; expected outcomes are UNVERIFIED (no Dafny run).
// Flags: --type-system-refresh --user-defined-operators
// negative has a precondition failure at a << -1, exactly as a.left(-1).
// false_control has a separate assertion failure after a legal selected call.
// Count restrictions come from this ordinary function contract, not the symbol.

datatype Bits = Bits(raw: bv8) {
  function {:operator "<<"} left(k: int): Bits
    requires 0 <= k <= 8
  { Bits(raw << k) }
}

method negative(a: Bits) {
  var bad := a << -1; // EXPECTED: left's precondition cannot be proved.
}

method false_control() {
  var a := Bits(5);
  var r := a << 1;
  assert r == a.left(1);
  assert false; // EXPECTED verification error at this assertion.
}
