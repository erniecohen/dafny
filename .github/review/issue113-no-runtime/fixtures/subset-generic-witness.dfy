
datatype Maybe<T(0)> = None | Some(value:T)
type Witnessed<T(0)> = x: Maybe<T> | true witness None
type Layer<T(0)> = x: Witnessed<T> | true witness None as Witnessed<T>
datatype Pair<A(0),B(0)> = Pair(left:A,right:B)
type BareFlip<X(0),Y(0)> = Pair<Y,X>

method Observe<T(0)>() returns (n:bool, l:bool) {
  var w:Witnessed<T>;
  var layer:Layer<T>;
  n := w.None?;
  l := layer.None?;
}
method Main() {
  var a,b := Observe<int>();
  var c,d := Observe<bool>();
  print a, " ", b, " ", c, " ", d, "\n";
  var pair:BareFlip<int,bool>;
  print pair.left, " ", pair.right, "\n";
}
