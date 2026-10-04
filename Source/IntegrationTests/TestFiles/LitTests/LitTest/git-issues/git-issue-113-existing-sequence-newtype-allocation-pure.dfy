// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --general-traits=datatype --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --general-traits=datatype --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
newtype Wrapped = values: seq<() ~> int> | true witness []
method Pure() {
  label L:
  var f: () ~> int := () => 0;
  var values := [f] as Wrapped;
  var produce := () => values;
  assert old@L(allocated(produce));
  assert old@L(allocated(produce()));
  assert ((produce() as seq<() ~> int>)[0])() == 0;
}
