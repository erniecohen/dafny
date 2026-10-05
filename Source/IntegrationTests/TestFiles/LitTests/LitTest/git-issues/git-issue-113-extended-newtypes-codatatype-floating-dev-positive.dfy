// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype FloatingStream = FCons(value: fp32, tail: FloatingStream)
newtype WrappedFloating = FloatingStream witness *
lemma GhostFloatingEquality(s: WrappedFloating)
  ensures s == s
{}
