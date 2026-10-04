// Corresponding nominal controls for existing-arrow-variance-control.
// Intended flags: --type-system-refresh=true --general-newtypes --extended-newtype-bases
// Unexecuted. Eta forms are separate semantic controls, not an implementation
// replacement for subtype assignment and its function-identity behavior.

newtype Produce<+T> = () -> T witness *
newtype Consume<-T> = T -> int witness *
lemma {:isolate_assertions} CovariantResult(f:Produce<nat>) {
  var widened:Produce<int> := f;
  assert widened() == f();
}
lemma {:isolate_assertions} ContravariantInput(f:Consume<int>, x:nat) {
  var narrowed:Consume<nat> := f;
  assert narrowed(x) == f(x);
}
lemma {:isolate_assertions} CovariantExplicitBase(f:Produce<nat>) {
  var base:() -> nat := f as () -> nat;
  var widened:() -> int := base;
  assert widened() == base();
}
lemma {:isolate_assertions} ContravariantExplicitBase(f:Consume<int>, x:nat) {
  var base:int -> int := f as int -> int;
  var narrowed:nat -> int := base;
  assert narrowed(x) == base(x);
}
lemma {:isolate_assertions} CovariantEta(f:Produce<nat>) {
  var widened := (() => f()) as Produce<int>;
  assert widened() == f();
}
lemma {:isolate_assertions} ContravariantEta(f:Consume<int>, x:nat) {
  var narrowed := ((y:nat) => f(y)) as Consume<nat>;
  assert narrowed(x) == f(x);
}
