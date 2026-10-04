// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes --extended-newtype-bases
// Source audit draft. Generic compiled witnesses must receive T's descriptors
// in a callable scope; a static initializer cannot refer to an unbound _td_T.

datatype Maybe<T(0)> = None | Some(value:T)
newtype Witnessed<T(0)> = Maybe<T> witness None
newtype Layer<T(0)> = Witnessed<T> witness None as Witnessed<T>
datatype Pair<A(0),B(0)> = Pair(left:A,right:B)
newtype BareFlip<X(0),Y(0)> = Pair<Y,X>

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
