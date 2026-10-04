// RUN: %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// The wrapped version must have the same partial executable equality eligibility as the base.

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
