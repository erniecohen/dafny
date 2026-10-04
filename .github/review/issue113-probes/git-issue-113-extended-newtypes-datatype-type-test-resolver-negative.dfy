// RUN: ! %resolve "%s" --type-system-refresh=true --general-newtypes --extended-newtype-bases
// Source audit draft: compiled tests must reject ghost constraints and phantom
// type arguments; no execution result or exact expected diagnostics yet.

datatype Maybe<T> = None | Some(value:T)
newtype Phantom<T> = Maybe<int> witness None
newtype HiddenByGhost = m:Maybe<int> | GhostPredicate(m) ghost witness None
newtype OuterGhost = HiddenByGhost witness *
ghost predicate GhostPredicate(m:Maybe<int>) { m.None? }

datatype Pair<A,B> = Pair(left:A,right:B)
newtype Permuted<X,Y> = Pair<Y,X> witness *

method PhantomCannotBeRecovered(m:Maybe<int>) returns (b:bool) {
  b := m is Phantom<bool>;
}
method GhostPredicateCannotBeRun(m:Maybe<int>) returns (b:bool) {
  b := m is HiddenByGhost;
}
method NestedGhostPredicateCannotBeRun(m:Maybe<int>) returns (b:bool) {
  b := m is OuterGhost;
}
method MismatchedActualArguments(p:Pair<bool,int>) returns (b:bool) {
  b := p is Permuted<bool,int>;
}
