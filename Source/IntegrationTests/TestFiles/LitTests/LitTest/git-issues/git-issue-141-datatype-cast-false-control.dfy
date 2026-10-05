// RUN: %exits-with 4 %verify --type-system-refresh --general-traits=datatype --general-newtypes --cores=1 --resource-limit=16000000 --verification-time-limit=60 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
datatype Box<+T> = Box(value: T)
lemma InhabitedControl() {
  var source: Box<int> := Box(0);
  var valid := source as Box<nat>;
  assert valid.value == 0;
  assert false; // ERROR: the accepted cast has an inhabited target
}
