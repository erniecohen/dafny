// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --unicode-char=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun: preserve ordinary numeric conversions through nominal views.

newtype CharView = char
newtype WideView = bv22

lemma CharacterViewToBitvector() {
  var c := 'A' as CharView;
  var b := c as bv8;
  assert b == 65;
}

lemma BitvectorViewToCharacter() {
  var b := (65 as bv22) as WideView;
  var c := b as CharView;
  assert (c as int) == 65;
}
