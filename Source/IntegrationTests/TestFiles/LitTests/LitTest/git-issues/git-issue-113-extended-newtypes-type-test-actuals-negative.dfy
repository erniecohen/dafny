// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Pair<A, B> = Pair(left: A, right: B)
newtype Permuted<X, Y> = Pair<Y, X> witness *

method DifferentActuals(p: Pair<bool, int>) returns (b: bool) {
  b := p is Permuted<bool, int>; // ERROR: base is Pair<int,bool>, not Pair<bool,int>
}
