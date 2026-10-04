// RUN: %baredafny verify --general-newtypes=true --type-system-refresh=true --unicode-char=false --show-snippets=false --allow-warnings "%s" > "%t"
// RUN: %diff "%s.expect" "%t"


newtype CharView = char
newtype WideView = bv22

lemma LegacyCodeUnits() {
  var surrogate := ((0xD800 as bv22) as WideView) as CharView;
  var largest := (0xFFFF as ORDINAL) as CharView;
  assert (surrogate as int) == 0xD800;
  assert (largest as int) == 0xFFFF;
}
