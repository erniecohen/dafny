// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

datatype Box<T> = Box(value:T)
newtype Bare<T(0)> = Box<T>
datatype Maybe<T(0)> = None | Some(value:T)
newtype Witnessed<T(0)> = Maybe<T> witness None

datatype Pair<A,B> = Pair(left:A,right:B)
newtype PairN<X,Y> = Pair<Y,X> witness *
datatype Container<X,Y> = Container(pair:PairN<X,Y>, tag:bool)
newtype WrappedContainer<X,Y> = Container<X,Y> witness *

method DefaultOf<T(0)>() returns (r:T) {
  var x:T;
  r := x;
}
method Main() {
  var bi := DefaultOf<Bare<int>>();
  var bb := DefaultOf<Bare<bool>>();
  print bi.value, " ", bb.value, "\n";
  var wi := DefaultOf<Witnessed<int>>();
  var wb := DefaultOf<Witnessed<bool>>();
  print wi.None?, " ", wb.None?, "\n";
  var a := Container(Pair(false, [42]) as PairN<seq<int>,bool>, true);
  var b := Container(Pair(false, [42]) as PairN<seq<int>,bool>, true);
  expect a == b;
  expect a.pair == b.pair;
}
