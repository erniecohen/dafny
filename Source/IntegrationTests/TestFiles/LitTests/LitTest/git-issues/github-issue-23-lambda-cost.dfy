// RUN: %verify --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// This fork keeps source lambda permissions under the formal type guard, instantiated
// at an actual application. Pos need not have a declared witness: 5 inhabits it.
type Pos = x: int | 0 < x witness *
function F(): int { 7 }
method Cost() {
  var f := (x: Pos) => F();
  assert f(5) == 7;
}
