// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
method BadCarrier(s:Stream<int>) { var n := s as Stream<nat>; }
