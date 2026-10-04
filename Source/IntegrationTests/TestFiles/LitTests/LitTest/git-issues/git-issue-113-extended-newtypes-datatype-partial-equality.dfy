// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Maybe = ghost Ghost | Value(value: int)
newtype WrappedMaybe = Maybe ghost witness Ghost

method BaseEquality(a: Maybe, b: Maybe) returns (same: bool)
  requires a.Value? && b.Value?
{
  same := a == b;
}

method WrappedEquality(a: WrappedMaybe, b: WrappedMaybe) returns (same: bool)
  requires a.Value? && b.Value?
{
  same := a == b;
}
