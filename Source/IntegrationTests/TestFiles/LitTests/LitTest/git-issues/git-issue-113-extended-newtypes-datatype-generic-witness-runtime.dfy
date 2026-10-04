// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

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
