// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --unicode-char=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun negative: every invalid Unicode scalar conversion must fail.

newtype Ord = ORDINAL
newtype CharView = char

lemma Surrogate() {
  var o := (0xD800 as ORDINAL) as Ord;
  var c := o as char; // ERROR: surrogate is not a Unicode scalar
}

lemma SurrogateNominalTarget() {
  var o := (0xDFFF as ORDINAL) as Ord;
  var c := o as CharView; // ERROR: nominal target retains scalar validity
}

lemma ScalarDoesNotHideContradiction() {
  var o := (0x10000 as ORDINAL) as Ord;
  var c := o as char;
  assert (c as int) == 0x10000;
  assert false; // ERROR: successful supplementary conversion is consistent
}
