// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

datatype Optional<T> = Empty | Some(value:T)
newtype Fixed<T> = Optional<T> witness Empty
newtype FixedLayer<T> = Fixed<T> witness Empty as Fixed<T>

datatype Choices<A(0),B(0)> = EmptyChoices | Values(first:A,second:B)
newtype Swap<X(0),Y(0)> = Choices<Y,X> witness EmptyChoices
newtype SwapLayer<X(0),Y(0)> = Swap<X,Y> witness EmptyChoices as Swap<X,Y>

method DefaultOf<V(0)>() returns (r:V) {
  var x:V;
  r := x;
}

method WithoutTypeDefault<T>() returns (a:bool,b:bool) {
  var n := DefaultOf<Fixed<T>>();
  var layer := DefaultOf<FixedLayer<T>>();
  a := n.Empty?;
  b := layer.Empty?;
}

method WithPermutedDescriptors<X(0),Y(0)>() returns (a:bool,b:bool) {
  var n := DefaultOf<Swap<X,Y>>();
  var layer := DefaultOf<SwapLayer<X,Y>>();
  a := n.EmptyChoices?;
  b := layer.EmptyChoices?;
}

method Main() {
  var a,b := WithoutTypeDefault<int>();
  var c,d := WithoutTypeDefault<bool>();
  var e,f := WithPermutedDescriptors<seq<int>,bool>();
  var g,h := WithPermutedDescriptors<bool,int>();
  expect a && b && c && d && e && f && g && h;
  print a, " ", b, " ", c, " ", d, " ", e, " ", f, " ", g, " ", h, "\n";
}
