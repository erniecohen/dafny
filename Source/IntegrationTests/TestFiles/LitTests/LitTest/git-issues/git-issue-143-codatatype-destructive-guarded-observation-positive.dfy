// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// This program is semantically valid and verified before the soundness repair.
// Its rejection records a conservative completeness loss.
// See docs/dev/codatatype-refinement-boundaries.md.
codatatype Stream = Cons(head: nat, tail: Stream)
function Values(n: nat): Stream {
  Cons(n, Cons(Values(n + 2).head, Values(n + 2)))
}
lemma FirstTwo(n: nat)
  ensures Values(n).head == n
  ensures Values(n).tail.head == n + 2
{}
method Main() { print Values(3).head, " ", Values(3).tail.head, "\n"; }
