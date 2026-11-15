// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Consume<-T> = T -> int witness *
lemma InhabitedControl() {
  var f := ((x: int) => x) as Consume<int>;
  var narrowed: Consume<nat> := f;
  var zero: nat := 0;
  assert narrowed(zero) == 0;
  assert false; // ERROR: valid arrow membership cannot prove false
}
