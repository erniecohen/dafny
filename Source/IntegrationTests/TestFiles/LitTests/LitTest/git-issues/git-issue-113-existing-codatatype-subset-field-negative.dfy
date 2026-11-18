// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

type Impossible = x: int | false witness *
codatatype Outer = Outer(tail: Outer, field: Impossible)
function Bad(): Outer {
  Outer(Bad(), 0 as Impossible)
}

lemma Exploit() {
  var s := Bad();
  assert false;
}
