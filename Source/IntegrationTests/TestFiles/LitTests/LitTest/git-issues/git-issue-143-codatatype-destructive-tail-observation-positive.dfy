// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// This program is semantically valid and verified before the soundness repair.
// Its rejection records a conservative completeness loss.
// See docs/dev/codatatype-refinement-boundaries.md.
codatatype Stream = Cons(head: int, tail: Stream)
function Values(n: int): Stream {
  Cons(n, Cons(n + 1, Values(n + 2).tail))
}
lemma FirstTwo(n: int)
  ensures Values(n).head == n
  ensures Values(n).tail.head == n + 1
{}
method Main() { print Values(3).head, " ", Values(3).tail.head, "\n"; }
