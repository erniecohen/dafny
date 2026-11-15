// A finite definition must not escape the impossible outer source guard.
type Empty = x: int | false witness *

lemma EmptyOuter() {
  assert forall z: Empty :: (set i: int | i == z) == {z};
  var active := set i: int | i == 0;
  assert active == {0};
  assert false; // intended sole failing obligation
}
