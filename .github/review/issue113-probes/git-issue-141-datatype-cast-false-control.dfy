// Private anti-vacuity control; valid narrowing must not prove false.
// Use --type-system-refresh=true. No issue113 option is needed.
datatype Box<+T> = Box(value: T)
lemma InhabitedControl() {
  var source: Box<int> := Box(0);
  var valid := source as Box<nat>;
  assert valid.value == 0;
  assert false; // ERROR: the accepted cast has an inhabited target
}
