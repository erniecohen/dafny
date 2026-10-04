// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype W = ghost Stop | Wrap(payload: NW)
newtype NW = W ghost witness Stop
datatype G<T> = ghost Ground | G(payload: NG<T>)
newtype NG<T> = G<T> ghost witness Ground

method RepresentationCycleRecord() {
  var w: W := *;
  var g: G<int> := *;
}
