// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=true --type-system-refresh=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --general-newtypes=false --type-system-refresh=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// This program is semantically valid and verified before the soundness repair.
// Its rejection records a conservative completeness loss.
// See docs/dev/codatatype-refinement-boundaries.md.
codatatype Stream = Cons(head: nat, tail: Stream)
function Values(n: nat): Stream { Cons(n, (match Values(n + 1) case Cons(h, t) => Cons(h, t))) }
lemma Head(n: nat) ensures Values(n).head == n {}
