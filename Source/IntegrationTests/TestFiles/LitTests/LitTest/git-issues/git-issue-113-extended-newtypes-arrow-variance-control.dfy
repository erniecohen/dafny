// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
