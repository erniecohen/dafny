// RUN: %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Unexecuted dev-only draft. Shipped 4.11 has no fp32/fp64 declarations.

codatatype FloatingStream = FCons(value: fp32, tail: FloatingStream)
newtype WrappedFloating = FloatingStream witness *
lemma GhostFloatingEquality(s: WrappedFloating)
  ensures s == s
{}
