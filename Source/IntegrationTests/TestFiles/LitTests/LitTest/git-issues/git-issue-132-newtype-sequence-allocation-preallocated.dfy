// RUN: %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=false --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh=true --general-newtypes=true --general-traits=datatype --additional-axioms=true --cores=1 --resource-limit=16000000 --show-snippets=false --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.axioms-on.expect" "%t"

class C {
  var x: int
  var next: C?
  constructor () { x := 0; next := null; }
  function F(): C reads this { this }
  twostate function P(): C? reads this { old(next) }
}
newtype Wrapped = values: seq<() ~> int> | true witness []
method Preallocated() {
  var n := new C();
  label L:
  var f: () ~> int := () reads n => 0;
  var values := [f] as Wrapped;
  var produce := () => values;
  assert old@L(allocated(produce));
  assert old@L(allocated(produce()));
  assert old@L(n in ((produce() as seq<() ~> int>)[0]).reads());
  assert old@L(allocated(n));
}
