
datatype Optional<T> = Empty | Some(value:T)
type Fixed<T> = x: Optional<T> | true witness Empty
type FixedLayer<T> = x: Fixed<T> | true witness Empty as Fixed<T>

datatype Choices<A(0),B(0)> = EmptyChoices | Values(first:A,second:B)
type Swap<X(0),Y(0)> = x: Choices<Y,X> | true witness EmptyChoices
type SwapLayer<X(0),Y(0)> = x: Swap<X,Y> | true witness EmptyChoices as Swap<X,Y>

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
