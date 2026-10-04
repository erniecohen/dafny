// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

newtype Ord = ORDINAL
newtype OrdLayer = Ord

method Wrap(o: ORDINAL) returns (n: Ord)
  ensures (n as ORDINAL) == o
{
  n := o as Ord;
}

method Main() {
  var zero: Ord;
  print zero, " ", zero.IsNat, " ", zero.Offset, "\n";
  var x: Ord := 5;
  var y: Ord := 3;
  var n := Wrap(7);
  var layer := n as OrdLayer;
  print x, " ", y, " ", x + y, " ", x - y, "\n";
  print layer, " ", layer as ORDINAL, " ", layer as int, "\n";
}

// Program output should be:
// 0 true 0
// 5 3 8 2
// 7 7 7
