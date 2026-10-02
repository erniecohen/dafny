// RUN: %exits-with 2 %resolve --type-system-refresh --general-traits=datatype "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

trait V {}
datatype VP extends V = VP(p: V -> bool)
function box(p: VP): V { p as V }
predicate d(v: V) { if v is VP then !(v as VP).p(v) else false }
const z: V := box(VP(d))
method test() ensures false {
  if z is VP { assert d(z) == !(z as VP).p(z); }
}
