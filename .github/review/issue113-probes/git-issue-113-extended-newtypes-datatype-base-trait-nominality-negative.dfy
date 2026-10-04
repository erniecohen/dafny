// Private resolution-negative draft. Use refreshed/general/extended newtypes
// and --general-traits=datatype. This does not declare a newtype-owned trait.

trait Tagged { function Tag(): int }
datatype Record extends Tagged = Record(value: int) {
  function Tag(): int { value }
}
newtype Wrapped = Record witness Record(0)
newtype Sibling = Record witness Record(0)

method Consume(v: Tagged) {}
method NoNewNominalImplementation(n: Wrapped) {
  var traitValue: Tagged := n; // ERROR: Wrapped did not declare Tagged as a parent
  Consume(n); // ERROR: parameter passing does not erase Wrapped
  var sibling: Sibling := n; // ERROR: distinct nominal types
}
