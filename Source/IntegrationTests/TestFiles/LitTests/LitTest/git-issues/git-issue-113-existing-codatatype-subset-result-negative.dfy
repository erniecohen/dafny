// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream = Cons(head: int, tail: Stream)
type Impossible = s: Stream | false witness *

function Bad(): Impossible {
  Cons(0, Bad() as Stream) as Impossible
}

lemma Exploit() {
  var s := Bad();
  assert false;
}
