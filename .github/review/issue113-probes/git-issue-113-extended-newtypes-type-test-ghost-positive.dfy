// Private ghost-context control; no execution or expected-output file yet.
// Use --type-system-refresh --general-newtypes --extended-newtype-bases.
datatype Maybe<T> = None | Some(value: T)
ghost predicate GhostPredicate(m: Maybe<int>) { m.None? }
newtype HiddenByGhost = m: Maybe<int> | GhostPredicate(m) ghost witness None
newtype OuterGhost = HiddenByGhost witness *
newtype Phantom<T> = Maybe<int> witness None

lemma GhostTypeTests(m: Maybe<int>) {
  var canUseGhostConstraint := m is OuterGhost;
  var canUsePhantomArgument := m is Phantom<bool>;
  var none: Maybe<int> := None;
  assert none is OuterGhost;
  assert canUseGhostConstraint == m.None?;
  assert canUsePhantomArgument;
}
