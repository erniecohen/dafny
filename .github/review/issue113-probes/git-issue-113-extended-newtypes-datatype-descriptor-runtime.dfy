// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes --extended-newtype-bases --relax-definite-assignment
// Also run with --optimize-erasable-datatype-wrapper=false.
// Source audit draft: this exercises N's descriptor through a generic T(0)
// caller rather than only constructing N's base default directly at a use site.

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
