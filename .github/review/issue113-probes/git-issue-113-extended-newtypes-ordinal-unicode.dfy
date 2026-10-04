// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --unicode-char=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun positive: ordinal character conversion follows Unicode scalar bounds.

newtype Ord = ORDINAL
newtype CharView = char

lemma SupplementaryScalar() {
  var o := (0x10000 as ORDINAL) as Ord;
  var c := o as char;
  assert c == '\U{10000}';
  var d := o as CharView;
  assert (d as int) == 0x10000;
}
