
newtype CharView = char
newtype WideView = bv22
newtype Ascii = c: char | (c as int) < 128 witness 'A'

lemma DirectWideSurrogate() {
  var c := (0xD800 as bv22) as char; // ERROR: surrogate
}

lemma DirectNarrowSurrogate() {
  var c := (0xDFFF as bv16) as char; // ERROR: surrogate even at width 16
}

lemma DirectOrdinalSurrogate() {
  var c := (0xD800 as ORDINAL) as char; // ERROR: surrogate
}

lemma DirectWideOverflow() {
  var c := (0x110000 as bv22) as char; // ERROR: beyond Unicode scalar maximum
}

lemma DirectOrdinalOverflow() {
  var c := (0x110000 as ORDINAL) as char; // ERROR: beyond Unicode scalar maximum
}

lemma NominalSurrogate() {
  var b := (0xD800 as bv22) as WideView;
  var c := b as CharView; // ERROR: nominal names cannot suppress scalar validity
}

lemma NominalOverflow() {
  var b := (0x200000 as bv22) as WideView;
  var c := b as CharView; // ERROR: nominal names cannot suppress range checking
}

lemma CharacterTargetConstraint() {
  var source := '\u00C8' as CharView;
  var c := source as Ascii; // ERROR: valid char, false destination predicate
}

lemma BitvectorTargetConstraint() {
  var source := (200 as bv22) as WideView;
  var c := source as Ascii; // ERROR: valid char, false destination predicate
}

lemma NonfiniteOrdinalStillNeedsFiniteness(o: ORDINAL) {
  var c := o as CharView; // ERROR: no IsNat or scalar-bound precondition
}

lemma InhabitedControlCannotProveFalse() {
  var source := (65 as bv22) as WideView;
  var c := source as CharView;
  assert (c as int) == 65;
  assert false; // ERROR: corrected conversion facts remain nonvacuous
}
