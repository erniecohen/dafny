// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun independent controls: every marked conversion must fail normally.

newtype Ord = ORDINAL
newtype OrdLayer = Ord
newtype CharView = char

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
  var c := n as char;
  assert c == 'A';
  assert false; // ERROR: successful range conversions are consistent
}
