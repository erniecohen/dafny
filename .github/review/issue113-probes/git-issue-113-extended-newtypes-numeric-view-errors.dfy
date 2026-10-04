// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --unicode-char=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun: neither nominal source nor target may suppress character validity.

newtype CharView = char
newtype WideView = bv22

lemma WideViewOverflow() {
  var b := (0x200000 as bv22) as WideView;
  var c := b as CharView; // ERROR: above both Unicode and legacy character maxima
}

lemma WideViewSurrogate() {
  var b := (0xD800 as bv22) as WideView;
  var c := b as CharView; // ERROR: surrogate is not a Unicode scalar
}
