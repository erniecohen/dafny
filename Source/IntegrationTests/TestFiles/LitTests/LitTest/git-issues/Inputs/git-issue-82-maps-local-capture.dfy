// A chosen witness must preserve arithmetic captures and source subset guards.
type NoWitness = x: int | false witness *

lemma LocalCaptureAndEmptyControls(n: int, i: int) {
  var m := map j: int | j == n + i :: 0 := j;
  assert m.Keys == {0};
  assert m[0] == n + i;
  var impossible := map x: NoWitness | x == n + i :: 0 := x;
  assert impossible == map[];
  var selfReference := map x: int | 0 <= x < 2 && x == x + 1 :: 0 := x;
  assert selfReference == map[];
  assert false;
}
