// Private unexecuted anti-vacuity control for an executable refinement.
// Only the final assert false may fail.
type Even = x: int | x % 2 == 0 witness 0
codatatype EvenStream = E(tail: EvenStream, head: Even)
function Evens(n: int): EvenStream { E(Evens(n + 1), (2 * n) as Even) }
lemma DoesNotProveFalse(n: int) {
  var s := Evens(n);
  assert s.head == 2 * n;
  assert s.tail.head == 2 * (n + 1);
  assert false;
}
