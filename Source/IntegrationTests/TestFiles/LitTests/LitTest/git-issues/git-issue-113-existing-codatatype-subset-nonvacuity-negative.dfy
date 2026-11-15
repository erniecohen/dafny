// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream = Cons(head: int, tail: Stream)
type Trivial = s: Stream | true witness *
function Raw(): Stream { Cons(1, Raw()) }
function Wrapped(): Trivial { Cons(1, Wrapped() as Stream) as Trivial }
lemma NonVacuity() {
  assert Raw().head == 1;
  assert Wrapped().head == 1;
  assert false;
}
