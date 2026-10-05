// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(head: T, tail: Stream<T>)
newtype UnknownHeadDefault<T> = Stream<T>

// This witness check must fail: T has no known compiled default (and may be empty).
// Contrast Bare<T(0)> in the positive/runtime fixture, which uses the real base default.
