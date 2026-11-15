// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

type PositiveAtZero = f: int -> int | f(0) > 0 witness (x: int) => 1

lemma InhabitedAndConsistent() {
  var value := ((x: int) => 1) as PositiveAtZero;
  assert value(0) == 1;
  assert false; // Only this assertion is intended to fail.
}
