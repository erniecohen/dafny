// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun independent controls: every marked conversion must fail normally.

newtype Ord = ORDINAL
newtype OrdLayer = Ord
newtype CharView = char
newtype ByteView = bv8

lemma BitvectorRange() {
  var n := (256 as ORDINAL) as Ord;
  var b := n as bv8; // ERROR: ordinal offset exceeds bv8 range
}

lemma BitvectorRangeThroughNewtype() {
  var n := (256 as ORDINAL) as OrdLayer;
  var b := n as ByteView; // ERROR: nominal target must retain the bv8 range check
}

lemma CharacterRange() {
  var n := (0x200000 as ORDINAL) as Ord;
  var c := n as char; // ERROR: above both Unicode and legacy character maxima
}

lemma CharacterRangeThroughNewtype() {
  var n := (0x200000 as ORDINAL) as OrdLayer;
  var c := n as CharView; // ERROR: nominal char target retains the range check
}

lemma RangesDoNotHideContradiction() {
  var n := (65 as ORDINAL) as Ord;
  var b := n as bv8;
  var c := n as char;
  assert b as int == 65;
  assert c == 'A';
  assert false; // ERROR: successful range conversions are consistent
}
