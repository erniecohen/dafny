// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases --general-traits=full "%s" > "%t"

trait V {
  function Tag(): int
}
datatype D extends V = D(value: int) {
  function Tag(): int { value }
}
newtype ND = D witness D(0)

method BaseMember(n: ND) {
  assert n.Tag() == n.value; // base operation is available if this combination is supported
}
method BadTraitInheritance(n: ND) {
  var v: V := n; // no nominal implementation of V was declared by ND
}
newtype InvalidOwnTrait extends V = D witness D(0) // unsupported newtype-owned trait combination
