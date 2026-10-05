// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Ord = ORDINAL
newtype CharView = char

lemma SupplementaryScalar() {
  var o := (0x10000 as ORDINAL) as Ord;
  var c := o as char;
  assert c == '\U{10000}';
  var d := o as CharView;
  assert (d as int) == 0x10000;
}
