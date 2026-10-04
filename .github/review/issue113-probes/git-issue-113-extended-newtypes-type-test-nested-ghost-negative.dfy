// Private compiled-type-test rejection draft. Its target's outer predicate is
// unconstrained; the required ghost predicate belongs to an inner newtype layer.
datatype Maybe<T> = None | Some(value: T)
ghost predicate GhostPredicate(m: Maybe<int>) { m.None? }
newtype HiddenByGhost = m: Maybe<int> | GhostPredicate(m) ghost witness None
newtype OuterGhost = HiddenByGhost witness *

method CannotRunInheritedGhostConstraint(m: Maybe<int>) returns (b: bool) {
  b := m is OuterGhost; // ERROR: runtime check must establish the inner predicate
}
