// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
