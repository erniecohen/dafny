// Private draft; not registered or executed. EXPECT_FAIL comments are obligations,
// not measured diagnostic lines or counts. Requires reviewed issue 132 integration.
// Intended flags: --type-system-refresh=true --general-newtypes=true --extended-newtype-bases

newtype Total = int -> int witness ((x: int) => x)
newtype Partial = int --> int witness ((x: int) => x)
newtype General = int ~> int witness ((x: int) => x)
newtype AtZero = f: int -> int | f(0) == 0 witness ((x: int) => x)
newtype Impossible = f: int -> int | false witness *

method PartialRequiresMustSurvive() {
  var wrapped := ((x: int) requires 0 <= x => x) as Partial;
  var bad := wrapped(-1); // EXPECT_FAIL: original precondition
}

class Cell {
  var value: int
  constructor() { value := 0; }
}

ghost function InsufficientReads(cell: Cell): int
  // EXPECT_FAIL: wrapping must not remove the captured cell's frame
{
  var f := (x: int) reads cell => cell.value + x;
  var wrapped := f as General;
  wrapped(0)
}

method PartialDoesNotBecomeTotal(f: Partial) {
  var bad := f as Total; // EXPECT_FAIL: no totality evidence
}

method ReadsDoNotDisappear(f: General) {
  var bad := f as Partial; // EXPECT_FAIL: no empty-frame evidence
}

method PredicateMustBeChecked() {
  var bad := ((x: int) => x + 1) as AtZero; // EXPECT_FAIL: result at zero is 1
}

method IdentityVacuityControl() {
  var concrete := ((x: int) => x) as Total;
  assert concrete(42) == 42;
  assert false; // EXPECT_FAIL: witness/application facts remain nonvacuous
}

method PredicateVacuityControl() {
  var concrete := ((x: int) => x) as AtZero;
  assert concrete(0) == 0;
  assert false; // EXPECT_FAIL: concrete refinement is inhabited
}

method EmptyTypeDoesNotFabricateValue() {
  var impossible: Impossible;
  var bad := impossible(0); // EXPECT_FAIL: uninitialized possibly-empty value
}
