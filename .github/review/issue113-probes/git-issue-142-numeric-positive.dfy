
newtype CharView = char
newtype WideView = bv22
newtype Ascii = c: char | (c as int) < 128 witness 'A'

lemma DirectAscii() {
  var fromBv := (65 as bv22) as char;
  var fromOrdinal := (65 as ORDINAL) as char;
  var fromChar := 'A' as bv8;
  assert (fromBv as int) == 65;
  assert (fromOrdinal as int) == 65;
  assert fromChar == 65;
}

lemma NominalAscii() {
  var wrappedChar := 'A' as CharView;
  var fromChar := wrappedChar as bv8;
  var wrappedBv := (65 as bv22) as WideView;
  var toChar := wrappedBv as CharView;
  var constrained := wrappedChar as Ascii;
  assert fromChar == 65;
  assert (toChar as int) == 65;
  assert (constrained as int) == 65;
}

lemma SupplementaryUnicode() {
  var fromBv := (0x10000 as bv22) as char;
  var fromOrdinal := (0x10000 as ORDINAL) as char;
  var fromNominal := ((0x10000 as bv22) as WideView) as CharView;
  assert (fromBv as int) == 0x10000;
  assert (fromOrdinal as int) == 0x10000;
  assert (fromNominal as int) == 0x10000;
}

lemma NominalCharacterIdentity(c: char) {
  var wrapped := c as CharView;
  assert (wrapped as char) == c;
  assert (wrapped as int) == (c as int);
}

lemma WidthZeroAndSmallWidth() {
  var zero := (0 as bv0) as CharView;
  var small := (65 as bv8) as CharView;
  assert (zero as int) == 0;
  assert (small as int) == 65;
}

lemma BitvectorScalarRoundTrip(b: bv22)
  requires (b as int) < 0xD800 || 0xE000 <= (b as int) < 0x110000
  ensures ((b as CharView) as int) == (b as int)
{}
lemma OrdinalScalarRoundTrip(o: ORDINAL)
  requires o.IsNat
  requires o.Offset < 0xD800 || 0xE000 <= o.Offset < 0x110000
  ensures ((o as CharView) as int) == o.Offset
{}
