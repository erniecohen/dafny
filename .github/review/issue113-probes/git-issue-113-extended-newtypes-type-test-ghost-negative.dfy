// Private compiled-type-test rejection draft, isolated from other resolver failures.
datatype Maybe<T> = None | Some(value: T)
ghost predicate GhostPredicate(m: Maybe<int>) { m.None? }
newtype HiddenByGhost = m: Maybe<int> | GhostPredicate(m) ghost witness None

method CannotRunGhostConstraint(m: Maybe<int>) returns (b: bool) {
  b := m is HiddenByGhost; // ERROR: required destination predicate is ghost
}
