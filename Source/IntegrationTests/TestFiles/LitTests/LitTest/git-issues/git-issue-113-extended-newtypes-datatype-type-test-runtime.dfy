// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

datatype Box<T> = Box(value:T)
newtype Positive = b:Box<int> | b.value > 0 witness Box(1)
newtype PositiveLayer = Positive witness Box(1) as Positive

datatype Maybe<T> = None | Some(value:T)
newtype OnlySome<T> = m:Maybe<T> | m.Some? witness *
newtype DeepSome<T> = OnlySome<seq<T>> witness *

datatype Pair<A,B> = Pair(left:A,right:B)
newtype Flip<X,Y> = p:Pair<Y,X> | true witness *
newtype BoolFirst<T> = p:Pair<bool,T> | p.left witness *
newtype PairTuple<X,Y> = p:(Y,X) | true witness *

method Main() {
  var positive := Box(7);
  var zero := Box(0);
  print positive is Positive, " ", zero is Positive, " ";
  print positive is PositiveLayer, " ", zero is PositiveLayer, " ";
  var some := Some(42);
  var none:Maybe<int> := None;
  print some is OnlySome<int>, " ";
  var deep := Some([42]);
  print deep is DeepSome<int>, " ";
  var pair := Pair(true, [42]);
  print pair is Flip<seq<int>,bool>, " ";
  print Pair(false, 42) is BoolFirst<int>, "\n";
  var tuple := (true, [42]);
  expect tuple is PairTuple<seq<int>,bool>;
  expect !(none is OnlySome<int>);
  var wrapped := positive as Positive;
  expect wrapped is Box<int>;
  expect wrapped is PositiveLayer;
  var wrappedSome := some as OnlySome<int>;
  expect wrappedSome is Maybe<int>;
  var wrappedFlip := pair as Flip<seq<int>,bool>;
  expect wrappedFlip is Pair<bool,seq<int>>;
}
