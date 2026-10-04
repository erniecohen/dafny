// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
