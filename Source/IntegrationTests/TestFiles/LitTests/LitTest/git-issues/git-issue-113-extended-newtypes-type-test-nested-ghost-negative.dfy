// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Maybe<T> = None | Some(value: T)
ghost predicate GhostPredicate(m: Maybe<int>) { m.None? }
newtype HiddenByGhost = m: Maybe<int> | GhostPredicate(m) ghost witness None
newtype OuterGhost = HiddenByGhost witness *

method CannotRunInheritedGhostConstraint(m: Maybe<int>) returns (b: bool) {
  b := m is OuterGhost; // ERROR: runtime check must establish the inner predicate
}
