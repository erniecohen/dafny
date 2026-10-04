// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
