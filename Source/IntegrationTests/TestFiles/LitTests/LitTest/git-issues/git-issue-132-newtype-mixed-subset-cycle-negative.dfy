// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// OMITTED-RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// OMITTED-RUN: %diff "%s.expect" "%t"

newtype N = S
type S = x: N | true witness *
