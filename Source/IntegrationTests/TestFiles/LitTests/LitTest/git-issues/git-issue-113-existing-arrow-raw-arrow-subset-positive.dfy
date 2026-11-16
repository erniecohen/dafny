// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

type PositiveAtZero = f: int -> int | f(0) > 0 witness (x: int) => 1

lemma IntroduceRawSubset(f: int -> int)
  requires f(0) > 0
{
  var value := f as PositiveAtZero;
  assert value(0) > 0;
}

lemma PreserveRawSubset(f: PositiveAtZero) {
  var value := f as PositiveAtZero;
  assert value(0) > 0;
}

lemma Inhabited() {
  var value := ((x: int) => 1) as PositiveAtZero;
  assert value(0) == 1;
}
