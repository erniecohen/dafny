// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
