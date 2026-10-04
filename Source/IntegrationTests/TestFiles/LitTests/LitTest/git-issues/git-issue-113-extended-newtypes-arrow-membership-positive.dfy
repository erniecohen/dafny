// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Produce<+T> = () -> T witness *
newtype Consume<-T> = T -> int witness *

lemma CovariantMember(f: Produce<nat>) {
  var widened: Produce<int> := f;
  var result: int := widened();
  var base: () -> int := widened as () -> int;
}
lemma ContravariantMember(f: Consume<int>, x: nat) {
  var narrowed: Consume<nat> := f;
  var result: int := narrowed(x);
  var base: nat -> int := narrowed as nat -> int;
}
lemma ExactEtaDomain(f: Consume<int>, x: nat) {
  var narrowed := ((y: nat) => f(y)) as Consume<nat>;
  var base: nat -> int := narrowed as nat -> int;
  var result: int := narrowed(x);
  assert narrowed(x) == f(x);
}
