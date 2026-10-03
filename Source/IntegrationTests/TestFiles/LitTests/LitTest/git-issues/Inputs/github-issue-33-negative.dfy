lemma Lower(a: int)
  requires a == -1
{ var b := a as bv32; }

lemma Upper(a: int)
  requires a == 0x1_0000_0000
{ var b := a as bv32; }

lemma MissingUpper(a: int)
  requires 0 <= a
{ var b := a as bv32; }

lemma MissingLower(a: int)
  requires a < 0x1_0000_0000
{ var b := a as bv32; }

lemma ZeroWidth(a: int)
  requires a == 1
{ var b := a as bv0; }

lemma Narrow(a: bv64)
  requires a == 0x1_0000_0000
{ var b := a as bv32; }

lemma Fraction(a: real)
  requires a == 0.5
{ var b := a as bv32; }

lemma Wrapped()
{ assert ((0xffff_ffff as bv32) + 1) as int == 0x1_0000_0000; }

lemma Unsigned()
{ assert (0x8000_0000 as bv32) as int < 0; }

lemma RoundTripDoesNotProveFalse(a: int)
  requires 0 <= a < 0x1_0000_0000
{
  assert (a as bv32) as int == a;
  assert false;
}

lemma WrongValue(a: int)
  requires 0 <= a < 0x1_0000_0000
  ensures (a as bv32) as int == a + 1
{}
