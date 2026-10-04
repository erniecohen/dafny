// Private focused membership/operation-signature draft; no execution yet.
// Use refresh/general-newtypes/extended-newtype-bases.
// Equality across genuine signature variance is isolated in the existing controls.
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
