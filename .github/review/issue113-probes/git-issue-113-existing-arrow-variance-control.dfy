// Direct-base control for issue-113 nominal variance proof claims.
// Run unchanged baseline and feature prototype with extended-newtype-bases off.
// Unexecuted: do not assert an expected verdict until the diagnostic completes.

lemma {:isolate_assertions} CovariantResult(f:() -> nat) {
  var widened:() -> int := f;
  assert widened() == f();
}
lemma {:isolate_assertions} ContravariantInput(f:int -> int, x:nat) {
  var narrowed:nat -> int := f;
  assert narrowed(x) == f(x);
}
lemma {:isolate_assertions} CovariantAsExpression(f:() -> nat) {
  assert (f as () -> int)() == f();
}
lemma {:isolate_assertions} ContravariantAsExpression(f:int -> int, x:nat) {
  assert (f as nat -> int)(x) == f(x);
}
lemma {:isolate_assertions} CovariantEta(f:() -> nat) {
  var widened:() -> int := (() => f());
  assert widened() == f();
}
lemma {:isolate_assertions} ContravariantEta(f:int -> int, x:nat) {
  var narrowed:nat -> int := ((y:nat) => f(y));
  assert narrowed(x) == f(x);
}
